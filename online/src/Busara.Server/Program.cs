using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Busara.Online;
using Busara.Server;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
// Request URLs, cookies, bodies, database errors and private game state are never logged.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var settings = new ServerSettings(builder.Configuration);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 16 * 1024;
    options.Listen(IPAddress.Loopback, settings.Origin.Port, listen => listen.UseHttps());
});
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<Authentication>();
builder.Services.AddSingleton<Rooms>();
var testRandom = builder.Configuration["BUSARA_TEST_RANDOM"];
if (testRandom is not null)
{
    if (!builder.Environment.IsEnvironment("Testing") || testRandom != "fixed-zero")
        throw new InvalidOperationException("BUSARA_TEST_RANDOM=fixed-zero is supported only in the explicit Testing environment.");
    builder.Services.AddSingleton<IGameRandom, FixedZeroGameRandom>();
}
else
{
    builder.Services.AddSingleton<IGameRandom, SecureGameRandom>();
}
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.IncludeFields = true;
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
    options.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.MaxDepth = 32;
});
var app = builder.Build();
try
{
    var db = app.Services.GetRequiredService<Database>();
    if (args.Contains("--migrate"))
    {
        await db.MigrateAsync();
        Console.WriteLine("Database migration complete; schema and key verified.");
        return;
    }
    await db.CheckAsync();
}
catch (Exception error)
{
    Console.Error.WriteLine($"Startup failed ({error.GetType().Name}): verify database availability, explicit migrations, and persistent secret configuration.");
    Environment.ExitCode = 1;
    return;
}
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    try
    {
        if (!context.Request.IsHttps ||
            !string.Equals(context.Request.Host.Value, settings.Origin.Authority, StringComparison.OrdinalIgnoreCase))
            throw new ApiException(400, "origin_rejected", "Use the configured HTTPS origin.");
        await next(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        // The caller disconnected; do not write a response to an aborted request.
    }
    catch (Exception error) when (!context.Response.HasStarted)
    {
        var status = error switch
        {
            ApiException apiError => apiError.Status,
            BadHttpRequestException bad => bad.StatusCode,
            JsonException => 400,
            RuleException => 409,
            _ => 503
        };
        if (error is not (ApiException or BadHttpRequestException or JsonException))
            app.Logger.LogError("Request failed ({ExceptionType}); trace {TraceIdentifier}.",
                error.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            code = error is ApiException api ? api.Code : status == 400 ? "invalid_request" : "request_unavailable",
            message = error is ApiException known ? known.Message : "Request unavailable. Retry the identical command if delivery is uncertain."
        }, context.RequestAborted);
    }
});
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.MapGet("/api/health/ready", async (Database db, CancellationToken ct) =>
{
    await db.CheckAsync(ct);
    return Results.Ok(new { status = "ready" });
});
app.MapPost("/api/guest", (HttpContext context, Authentication auth) => auth.CreateAsync(context));
app.MapGet("/api/guest", async (HttpContext context, Authentication auth) => auth.View(await auth.RequireAsync(context)));
app.MapPost("/api/rooms", async (HttpContext context, Authentication auth, Rooms rooms, CreateRoomRequest request) =>
    await rooms.CreateAsync(await auth.RequireAsync(context, true), request, context.RequestAborted));
app.MapPost("/api/rooms/join", async (HttpContext context, Authentication auth, Rooms rooms, JoinRoomRequest request) =>
    await rooms.JoinAsync(await auth.RequireAsync(context, true), request, context.RequestAborted));
app.MapGet("/api/rooms/{matchId:guid}", async (Guid matchId, HttpContext context, Authentication auth, Rooms rooms) =>
    await rooms.ViewAsync(await auth.RequireAsync(context), matchId, context.RequestAborted));
app.MapPost("/api/rooms/{matchId:guid}/commands", async (Guid matchId, HttpContext context, Authentication auth, Rooms rooms, OnlineCommand request) =>
{
    var result = await rooms.ApplyAsync(await auth.RequireAsync(context, true), matchId, request, context.RequestAborted);
    return Results.Json(result.Receipt, Wire.Json, statusCode: result.HttpStatus);
});
// HTTP/2 browsers establish WebSockets with CONNECT rather than GET.
app.MapMethods("/api/rooms/{matchId:guid}/events", ["GET", "CONNECT"], async (Guid matchId, HttpContext context, Authentication auth, Rooms rooms) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
        throw new ApiException(400, "websocket_required", "Use a secure WebSocket connection.");
    // Browsers cannot set WebSocket headers: carry CSRF as an offered subprotocol, never in the URL.
    auth.CheckOrigin(context);
    var guest = await auth.RequireAsync(context);
    var protocols = context.WebSockets.WebSocketRequestedProtocols;
    if (!protocols.Contains("busara.v1") || !protocols.Contains("csrf." + auth.Csrf(guest)))
        throw new ApiException(403, "csrf_rejected", "A valid CSRF WebSocket subprotocol is required.");
    var version = await rooms.VersionAsync(guest, matchId, context.RequestAborted);
    using var socket = await context.WebSockets.AcceptWebSocketAsync("busara.v1");
    using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    var ct = lifetime.Token;
    var receive = Task.Run(async () =>
    {
        var buffer = new byte[256];
        while (socket.State == WebSocketState.Open)
        {
            var received = await socket.ReceiveAsync(buffer.AsMemory(), ct);
            if (received.MessageType == WebSocketMessageType.Close) break;
            // This is a server-to-client invalidation channel, not a command transport.
            if (received.Count > 0) break;
        }
        lifetime.Cancel();
    }, ct);
    try
    {
        string? sent = null;
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            if (guest.ExpiresAt <= DateTimeOffset.UtcNow) break;
            version = await rooms.VersionAsync(guest, matchId, ct);
            if (sent != version)
            {
                var payload = Encoding.UTF8.GetBytes(Wire.Encode(new { matchId = matchId.ToString("D"), version }));
                await socket.SendAsync(payload.AsMemory(), WebSocketMessageType.Text, true, ct);
                sent = version;
            }
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
    catch (Exception error) when (error is OperationCanceledException or WebSocketException or ApiException or Npgsql.NpgsqlException)
    {
        if (error is Npgsql.NpgsqlException)
            app.Logger.LogError("Invalidation channel failed ({ExceptionType}); trace {TraceIdentifier}.",
                error.GetType().Name, context.TraceIdentifier);
    }
    finally
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "Reconnect and authenticate.", closeTimeout.Token); }
            catch (Exception error) when (error is OperationCanceledException or WebSocketException) { }
        }
        lifetime.Cancel();
        try { await receive; } catch (Exception error) when (error is OperationCanceledException or WebSocketException) { }
    }
});
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".wasm"] = "application/wasm";
contentTypes.Mappings[".data"] = "application/octet-stream";
var webRoot = builder.Configuration["BUSARA_WEB_ROOT"] ??
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "web"));
if (Directory.Exists(webRoot))
{
    var files = new PhysicalFileProvider(webRoot);
    app.Lifetime.ApplicationStopped.Register(files.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ContentTypeProvider = contentTypes });
}
else
{
    app.MapGet("/", () => Results.Problem(
        "The Unity Web build is unavailable. Build online/web or configure BUSARA_WEB_ROOT.", statusCode: 503));
}
await app.RunAsync();

public partial class Program { }
