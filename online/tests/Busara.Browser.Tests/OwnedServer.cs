using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using NUnit.Framework;

namespace Busara.Browser.Tests;

internal sealed class OwnedServer : IAsyncDisposable
{
    private readonly string dotnet;
    private readonly string server;
    private readonly string webRoot;
    private readonly string connection;
    private readonly string baseConnection;
    private readonly string schema = "busara_browser_" + Guid.NewGuid().ToString("N");
    private readonly string secret;
    private readonly string certificate;
    private readonly string certificatePassword;
    private readonly HttpClient health;
    private readonly ConcurrentQueue<string> processEvents = [];
    private Process? child;
    private bool schemaCreated;
    public Uri Origin { get; }
    public string Artifacts { get; }

    public OwnedServer()
    {
        dotnet = RequiredFile("BUSARA_BROWSER_DOTNET");
        server = RequiredFile("BUSARA_BROWSER_SERVER_DLL");
        webRoot = RequiredDirectory("BUSARA_BROWSER_WEB_ROOT");
        Artifacts = RequiredDirectory("BUSARA_BROWSER_ARTIFACTS");
        Require(File.Exists(Path.Combine(webRoot, "index.html")) &&
            Directory.Exists(Path.Combine(webRoot, "Build")), "Build the real development Unity Web client first.");
        Require(Directory.EnumerateFiles(Path.Combine(webRoot, "Build"), "*.wasm*").Any(),
            "The Web root must contain a real Unity WebAssembly build; an HTML substitute is not accepted.");
        var configuredOrigin = Environment.GetEnvironmentVariable("BUSARA_BROWSER_ORIGIN");
        Require(Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin) && origin.Scheme == "https" &&
            origin.Host == "127.0.0.1" && origin.Port > 1024 && origin.AbsolutePath == "/" &&
            origin.Query == "" && origin.Fragment == "" && origin.UserInfo == "",
            "BUSARA_BROWSER_ORIGIN must be one explicit https://127.0.0.1:<unprivileged-port> origin.");
        Origin = origin!;
        using var credentials = JsonDocument.Parse(File.ReadAllText(RequiredFile("BUSARA_BROWSER_CONNECTION_FILE")));
        var db = credentials.RootElement;
        Require(db.GetProperty("host").GetString() == "127.0.0.1", "Browser tests require loopback PostgreSQL.");
        var database = db.GetProperty("testDatabase").GetString();
        Require(database == "busara_test", "Browser fixture refuses any database other than busara_test.");
        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = "127.0.0.1",
            ["Port"] = db.GetProperty("port").ToString(),
            ["Username"] = db.GetProperty("username").GetString()!,
            ["Password"] = db.GetProperty("password").GetString()!,
            ["Database"] = database!,
            ["Include Error Detail"] = false
        };
        baseConnection = builder.ConnectionString;
        builder["Search Path"] = schema;
        connection = builder.ConnectionString;
        secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var cert = JsonDocument.Parse(File.ReadAllText(RequiredFile("BUSARA_BROWSER_CERTIFICATE_FILE")));
        certificate = cert.RootElement.GetProperty("path").GetString()!;
        certificatePassword = cert.RootElement.GetProperty("password").GetString()!;
        Require(File.Exists(certificate), "Generate the file-only loopback HTTPS certificate first.");
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request.RequestUri?.GetLeftPart(UriPartial.Authority) == Origin.GetLeftPart(UriPartial.Authority)
        };
        health = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
    }

    public async Task StartAsync(bool migrate = false)
    {
        Require(child == null, "This fixture already owns a backend process.");
        bool portFree;
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, Origin.Port);
        try { listener.Start(); portFree = true; }
        catch (System.Net.Sockets.SocketException) { portFree = false; }
        finally { listener.Stop(); }
        Require(portFree, "The chosen HTTPS port is occupied. Refusing to attach to or stop an unowned process.");
        if (migrate)
        {
            Require(!schemaCreated, "The fixture's isolated test schema was already initialized.");
            try
            {
                await using var database = NpgsqlDataSource.Create(baseConnection);
                await using var create = database.CreateCommand("CREATE SCHEMA " + schema);
                await create.ExecuteNonQueryAsync();
                schemaCreated = true;
            }
            catch { throw new AssertionException("Could not create an isolated browser fixture schema in busara_test."); }
            using var migration = Launch(true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await migration.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!migration.HasExited) migration.Kill(entireProcessTree: true);
                throw new AssertionException("Owned backend migration timed out; no credentials were logged.");
            }
            Require(migration.ExitCode == 0, "Backend migration failed. Verify private test database/key configuration.");
        }
        child = Launch(false);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            Require(!child.HasExited, "Owned backend exited before readiness; see sanitized backend process events.");
            try
            {
                using var response = await health.GetAsync(new Uri(Origin, "/api/health/ready"));
                if (response.IsSuccessStatusCode)
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (json.RootElement.GetProperty("status").GetString() == "ready")
                    {
                        processEvents.Enqueue("owned-backend-ready");
                        return;
                    }
                }
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(200);
        }
        throw new AssertionException("Owned backend did not become ready within 60 seconds.");
    }

    private Process Launch(bool migrate)
    {
        var start = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = Path.GetDirectoryName(server)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(server);
        if (migrate) start.ArgumentList.Add("--migrate");
        start.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(dotnet)!;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["BUSARA_DATABASE"] = connection;
        start.Environment["ConnectionStrings__Busara"] = connection;
        start.Environment["BUSARA_ORIGIN"] = Origin.GetLeftPart(UriPartial.Authority);
        start.Environment["ASPNETCORE_URLS"] = Origin.GetLeftPart(UriPartial.Authority);
        start.Environment["BUSARA_WEB_ROOT"] = webRoot;
        start.Environment["BUSARA_SECRET_KEY"] = secret;
        start.Environment["BUSARA_TEST_RANDOM"] = "fixed-zero";
        start.Environment["ASPNETCORE_Kestrel__Certificates__Default__Path"] = certificate;
        start.Environment["ASPNETCORE_Kestrel__Certificates__Default__Password"] = certificatePassword;
        var process = new Process { StartInfo = start };
        // Child output is deliberately reduced to categories: exceptions may include connection details.
        process.OutputDataReceived += (_, e) => RecordOutput(e.Data, "stdout");
        process.ErrorDataReceived += (_, e) => RecordOutput(e.Data, "stderr");
        Require(process.Start(), "Could not start the owned backend process.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        processEvents.Enqueue(migrate ? "owned-migration-started" : "owned-backend-started");
        return process;
    }

    private void RecordOutput(string? line, string stream)
    {
        if (line == null) return;
        processEvents.Enqueue("backend-" + stream + "-line-redacted");
    }

    public async Task StopAsync()
    {
        if (child == null) return;
        var owned = child;
        child = null;
        try
        {
            if (!owned.HasExited) owned.Kill(entireProcessTree: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await owned.WaitForExitAsync(timeout.Token);
            processEvents.Enqueue("owned-backend-stopped");
        }
        finally { owned.Dispose(); }
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
        processEvents.Enqueue("same-database-backend-restart-complete");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        health.Dispose();
        if (schemaCreated)
        {
            try
            {
                await using var database = NpgsqlDataSource.Create(baseConnection);
                await using var drop = database.CreateCommand("DROP SCHEMA " + schema + " CASCADE");
                await drop.ExecuteNonQueryAsync();
                schemaCreated = false;
                processEvents.Enqueue("owned-browser-test-schema-removed");
            }
            catch { processEvents.Enqueue("owned-browser-test-schema-cleanup-failed"); }
        }
        await File.WriteAllLinesAsync(Path.Combine(Artifacts, "backend-process-events.txt"), processEvents.ToArray());
    }

    private static string RequiredFile(string name)
    {
        var path = Environment.GetEnvironmentVariable(name);
        Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && File.Exists(path),
            name + " must point to an existing absolute file; run the vetted browser prerequisite runner.");
        return path!;
    }

    private static string RequiredDirectory(string name)
    {
        var path = Environment.GetEnvironmentVariable(name);
        Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && Directory.Exists(path),
            name + " must point to an existing absolute directory; run the vetted browser prerequisite runner.");
        return path!;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new AssertionException(message);
    }
}
