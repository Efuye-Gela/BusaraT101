using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Busara.Online;
using Busara.Server;
using Npgsql;
using Xunit;

namespace Busara.Server.Tests;

[CollectionDefinition("PostgreSQL", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string schema = "busara_test_" + Guid.NewGuid().ToString("N");
    private readonly string runtimeDirectory = Path.Combine(AppContext.BaseDirectory, ".runtime", Guid.NewGuid().ToString("N"));
    private readonly string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string certificatePassword = ServerSettings.NewToken();
    private string baseConnection = "";
    private string certificate = "";
    private string serverDll = "";
    private readonly List<Worker> workers = [];
    public string ConnectionString { get; private set; } = "";
    public Worker First { get; private set; } = null!;
    public Worker Second { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        baseConnection = Environment.GetEnvironmentVariable("BUSARA_TEST_DATABASE") ??
            throw new InvalidOperationException("Actual PostgreSQL is mandatory: set BUSARA_TEST_DATABASE to a disposable local test database.");
        serverDll = Environment.GetEnvironmentVariable("BUSARA_TEST_SERVER_DLL") ??
            throw new InvalidOperationException("Set BUSARA_TEST_SERVER_DLL to the built net10.0 Busara.Server.dll (use test-server.ps1).");
        if (!File.Exists(serverDll)) throw new InvalidOperationException("Build Busara.Server before integration tests.");
        var parsed = new NpgsqlConnectionStringBuilder(baseConnection);
        if (parsed.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Integration tests require an explicitly configured loopback PostgreSQL host.");
        await using (var source = NpgsqlDataSource.Create(baseConnection))
        await using (var command = source.CreateCommand($"CREATE SCHEMA {schema}"))
            await command.ExecuteNonQueryAsync();
        parsed.SearchPath = schema;
        ConnectionString = parsed.ConnectionString;
        Directory.CreateDirectory(runtimeDirectory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        certificate = Path.Combine(runtimeDirectory, "localhost.pfx");
        await File.WriteAllBytesAsync(certificate, cert.Export(X509ContentType.Pfx, certificatePassword));
        var migration = StartProcess(FreePort(), true);
        await migration.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, migration.ExitCode);
        migration.Dispose();
        First = await StartWorkerAsync();
        Second = await StartWorkerAsync();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private Process StartProcess(int port, bool migrate)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(serverDll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(serverDll);
        if (migrate) start.ArgumentList.Add("--migrate");
        start.Environment["BUSARA_DATABASE"] = ConnectionString;
        start.Environment["ConnectionStrings__Busara"] = ConnectionString;
        start.Environment["BUSARA_SECRET_KEY"] = secret;
        start.Environment["BUSARA_ORIGIN"] = $"https://127.0.0.1:{port}";
        start.Environment["ASPNETCORE_URLS"] = $"https://127.0.0.1:{port}";
        start.Environment["ASPNETCORE_Kestrel__Certificates__Default__Path"] = certificate;
        start.Environment["ASPNETCORE_Kestrel__Certificates__Default__Password"] = certificatePassword;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["BUSARA_TEST_RANDOM"] = "fixed-zero";
        var process = Process.Start(start) ?? throw new InvalidOperationException("Server process failed to start.");
        // Drain logs without printing configuration, cookies, or request payloads.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    public async Task<Worker> StartWorkerAsync()
    {
        var port = FreePort();
        var worker = new Worker(StartProcess(port, false), new Uri($"https://127.0.0.1:{port}"));
        workers.Add(worker);
        using var http = NewHttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && !worker.Process.HasExited)
        {
            try
            {
                if ((await http.GetAsync(new Uri(worker.Origin, "/api/health/ready"))).IsSuccessStatusCode)
                    return worker;
            }
            catch (HttpRequestException) { }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Real server worker failed readiness. Check explicit migration and HTTPS test configuration.");
    }

    internal static bool IsLocalTlsTarget(Uri? uri) =>
        uri is { IsAbsoluteUri: true } && uri.IsLoopback &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeWss);

    internal static HttpClientHandler NewHttpHandler() => new()
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (request, _, _, _) => IsLocalTlsTarget(request.RequestUri)
    };

    public static HttpClient NewHttpClient() => new(NewHttpHandler()) { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<Browser> GuestAsync(Worker? worker = null)
    {
        worker ??= First;
        var browser = new Browser(worker, NewHttpClient());
        using var response = await browser.SendAsync(HttpMethod.Post, "/api/guest", new { }, csrf: false);
        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<GuestView>(Wire.Json);
        browser.Cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        browser.View = view!;
        return browser;
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        await using var command = source.CreateCommand(sql);
        foreach (var item in parameters) command.Parameters.AddWithValue(item.Name, item.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        await using var command = source.CreateCommand(sql);
        foreach (var item in parameters) command.Parameters.AddWithValue(item.Name, item.Value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async Task DisposeAsync()
    {
        foreach (var worker in workers) worker.Dispose();
        NpgsqlConnection.ClearAllPools();
        if (baseConnection.Length > 0)
        {
            await using var source = NpgsqlDataSource.Create(baseConnection);
            await using var command = source.CreateCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE");
            await command.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(runtimeDirectory)) Directory.Delete(runtimeDirectory, true);
    }
}

public sealed class Worker(Process process, Uri origin) : IDisposable
{
    private bool disposed;
    public Process Process { get; } = process;
    public Uri Origin { get; } = origin;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (!Process.HasExited) { Process.Kill(entireProcessTree: true); Process.WaitForExit(5000); }
        }
        finally { Process.Dispose(); }
    }
}

public sealed class Browser(Worker worker, HttpClient client) : IDisposable
{
    public Worker Worker { get; set; } = worker;
    public string? Cookie { get; set; }
    public GuestView View { get; set; } = null!;

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null,
        bool csrf = true, string? origin = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(Worker.Origin, path));
        if (body is not null) request.Content = new StringContent(Wire.Encode(body), System.Text.Encoding.UTF8, "application/json");
        if (Cookie is not null) request.Headers.TryAddWithoutValidation("Cookie", Cookie);
        request.Headers.Add("Origin", origin ?? Worker.Origin.GetLeftPart(UriPartial.Authority));
        if (csrf && View is not null) request.Headers.Add("X-CSRF-Token", View.csrfToken);
        return await client.SendAsync(request);
    }

    public async Task<T> GetAsync<T>(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Wire.Json))!;
    }

    public async Task<RoomResult> CreateAsync(string? commandId = null)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/rooms", new { commandId = commandId ?? Guid.NewGuid().ToString("D") });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResult>(Wire.Json))!;
    }

    public async Task<RoomResult> JoinAsync(RoomResult room, string? commandId = null)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/rooms/join", new
        {
            commandId = commandId ?? Guid.NewGuid().ToString("D"), inviteToken = Token(room)
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResult>(Wire.Json))!;
    }

    public static string Token(RoomResult room) => new Uri(room.inviteUrl).Fragment["#invite=".Length..];
    public void Dispose() => client.Dispose();
}
