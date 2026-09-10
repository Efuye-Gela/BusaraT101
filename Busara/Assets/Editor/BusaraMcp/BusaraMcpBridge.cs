#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Busara.EditorMcp
{
    [Serializable]
    public sealed class McpRequest
    {
        public string op, action, query, path, name, primitive, type, property, value, shader, content, confirm, view, mode, target, jobId;
        public int id, parentId, limit = 100;
        public int[] ids;
        public string[] tests;
    }

    [InitializeOnLoad]
    public static class BusaraMcpBridge
    {
        [Serializable] sealed class Discovery { public int protocolVersion = 1; public string projectPath, token; public int port, processId; }
        sealed class Work
        {
            public McpRequest Request;
            public int State;
            public readonly TaskCompletionSource<string> Result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        static readonly ConcurrentQueue<Work> Queue = new ConcurrentQueue<Work>();
        static readonly SemaphoreSlim Connections = new SemaphoreSlim(16);
        static HttpListener listener;
        static string token;
        static string publishedDiscoveryPath;
        static int port;
        static int queued;
        static volatile bool stopping;
        static bool startupPending = true;
        static bool pumpReady;
        static long lastUpdateTicks;
        public static readonly string ProjectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static readonly string StatePath = Path.Combine(ProjectPath, "Library", "BusaraMcp");
        const int MaxBody = 1024 * 1024;

        static BusaraMcpBridge()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
                RegisterCallbacks();
        }

        static void RegisterCallbacks()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting -= Stop;
            EditorApplication.quitting += Stop;
        }

        [InitializeOnLoadMethod]
        static void InitializeBridge()
        {
            // Listener setup needs no imported assets or loaded scenes.
            Start();
        }

        [MenuItem("Tools/Busara MCP/Start bridge")]
        public static void Start()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                startupPending = false;
                return;
            }
            RegisterCallbacks();
            startupPending = false;
            if (listener != null && listener.IsListening) return;
            try
            {
                McpPaths.CheckNoLinks(ProjectPath, StatePath);
                Directory.CreateDirectory(StatePath);
                stopping = false;
                using (var random = RandomNumberGenerator.Create())
                {
                    var bytes = new byte[32];
                    random.GetBytes(bytes);
                    token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                }
                // Bind a random loopback port; retry the small reservation/listener race.
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var reservation = new TcpListener(IPAddress.Loopback, 0);
                    reservation.Start();
                    port = ((IPEndPoint)reservation.LocalEndpoint).Port;
                    reservation.Stop();
                    listener = new HttpListener();
                    listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                    try { listener.Start(); break; }
                    catch { listener.Close(); listener = null; if (attempt == 9) throw; }
                }
                string discoveryPath = Path.Combine(StatePath, "discovery.json");
                McpPaths.CheckNoLinks(ProjectPath, discoveryPath);
                File.WriteAllText(discoveryPath,
                    JsonUtility.ToJson(new Discovery { projectPath = ProjectPath, token = token, port = port,
                        processId = System.Diagnostics.Process.GetCurrentProcess().Id }), new UTF8Encoding(false));
                publishedDiscoveryPath = discoveryPath;
                var current = listener;
                Task.Run(() => Accept(current));
                McpJobs.Initialize();
                Debug.Log("Busara MCP listening on loopback port " + port + ". Credentials remain in Library/BusaraMcp.");
            }
            catch (Exception exception)
            {
                Stop();
                Debug.LogError("Busara MCP could not start: " + exception.Message);
            }
        }

        [MenuItem("Tools/Busara MCP/Stop bridge")]
        public static void Stop()
        {
            startupPending = false;
            stopping = true;
            listener?.Close();
            listener = null;
            while (Queue.TryDequeue(out var work))
            {
                Interlocked.Decrement(ref queued);
                RawBodies.TryRemove(work, out _);
                work.Result.TrySetResult(Error("Bridge stopped or scripts reloaded. Inspect state before retrying a mutation."));
            }
            string discoveryPath = publishedDiscoveryPath;
            publishedDiscoveryPath = null;
            if (discoveryPath == null) return;
            try
            {
                McpPaths.CheckNoLinks(ProjectPath, discoveryPath);
                if (File.Exists(discoveryPath))
                {
                    if (new FileInfo(discoveryPath).Length > 16384)
                        throw new IOException("Discovery file is unexpectedly large.");
                    var discovery = JsonUtility.FromJson<Discovery>(File.ReadAllText(discoveryPath));
                    if (discovery != null && discovery.processId == System.Diagnostics.Process.GetCurrentProcess().Id &&
                        ConstantEquals(discovery.token, token))
                        File.Delete(discoveryPath);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogWarning("Busara MCP could not safely remove its discovery file: " + exception.Message);
            }
        }

        static async Task Accept(HttpListener current)
        {
            while (!stopping && current.IsListening)
            {
                HttpListenerContext context;
                try { context = await current.GetContextAsync(); }
                catch (Exception) { break; }
                if (!Connections.Wait(0)) { context.Response.StatusCode = 429; context.Response.Close(); continue; }
                _ = Handle(context);
            }
        }

        static async Task Handle(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                if (!IPAddress.IsLoopback(request.RemoteEndPoint.Address) ||
                    request.Headers["Host"] != "127.0.0.1:" + port ||
                    !string.IsNullOrEmpty(request.Headers["Origin"]) ||
                    !string.IsNullOrEmpty(request.Headers["Sec-Fetch-Site"]))
                { await Reply(context, 403, Error("Only direct loopback clients without browser Origin are allowed.")); return; }
                if (!ConstantEquals(request.Headers["Authorization"], "Bearer " + token))
                { await Reply(context, 401, Error("Invalid bridge credentials.")); return; }
                if (request.HttpMethod != "POST" || request.RawUrl != "/command")
                { await Reply(context, 404, Error("Only POST /command is supported.")); return; }
                if (request.ContentType == null || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                { await Reply(context, 415, Error("application/json required.")); return; }
                if (request.ContentLength64 > MaxBody)
                { await Reply(context, 413, Error("Body exceeds 1 MiB.")); return; }
                string body;
                using (var stream = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    var deadline = Task.Delay(5000);
                    while (true)
                    {
                        var read = request.InputStream.ReadAsync(buffer, 0, buffer.Length);
                        if (await Task.WhenAny(read, deadline) != read) { context.Response.Abort(); return; }
                        int count = await read;
                        if (count == 0) break;
                        if (stream.Length + count > MaxBody) { await Reply(context, 413, Error("Body exceeds 1 MiB.")); return; }
                        stream.Write(buffer, 0, count);
                    }
                    body = new UTF8Encoding(false, true).GetString(stream.ToArray());
                }
                // JsonUtility is deliberately called on the main thread along with every Unity API.
                if (stopping) { await Reply(context, 503, Error("Bridge is stopping.")); return; }
                if (Interlocked.Increment(ref queued) > 32)
                {
                    Interlocked.Decrement(ref queued);
                    await Reply(context, 429, Error("Editor command queue is full."));
                    return;
                }
                var pending = new Work { Request = null };
                RawBodies[pending] = body;
                Queue.Enqueue(pending);
                if (await Task.WhenAny(pending.Result.Task, Task.Delay(10000)) != pending.Result.Task)
                {
                    int state = Interlocked.CompareExchange(ref pending.State, 3, 0);
                    long ticks = Interlocked.Read(ref lastUpdateTicks);
                    string updated = ticks == 0 ? "never" : new DateTime(ticks, DateTimeKind.Utc).ToString("O");
                    await Reply(context, 504, Error("Editor timed out (dispatch state " + state +
                        ", last main-thread update " + updated + "). Pending work was cancelled where possible; " +
                        "an executing mutation may have completed. Never retry blindly."));
                    return;
                }
                await Reply(context, 200, await pending.Result.Task);
            }
            catch (Exception exception)
            {
                try { await Reply(context, 400, Error(exception.Message)); } catch (Exception) { }
            }
            finally { try { context.Response.Close(); } finally { Connections.Release(); } }
        }

        static readonly ConcurrentDictionary<Work, string> RawBodies = new ConcurrentDictionary<Work, string>();

        static void Update()
        {
            Interlocked.Exchange(ref lastUpdateTicks, DateTime.UtcNow.Ticks);
            if (!pumpReady)
            {
                pumpReady = true;
                Debug.Log("Busara MCP main-thread pump ready.");
            }
            if (startupPending && !stopping && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                Start();
            for (int count = 0; count < 8 && Queue.TryDequeue(out var work); count++)
            {
                Interlocked.Decrement(ref queued);
                RawBodies.TryRemove(work, out var body);
                if (stopping)
                {
                    work.Result.TrySetResult(Error("Bridge stopped before command execution."));
                    continue;
                }
                if (Interlocked.CompareExchange(ref work.State, 1, 0) != 0) continue;
                try
                {
                    work.Request = JsonUtility.FromJson<McpRequest>(body);
                    if (work.Request == null || string.IsNullOrEmpty(work.Request.op)) throw new ArgumentException("op is required.");
                    string result = McpCommands.Execute(work.Request);
                    work.Result.TrySetResult("{\"ok\":true,\"data\":" + result + "}");
                }
                catch (Exception exception) { work.Result.TrySetResult(Error(exception.Message)); }
                finally { Interlocked.Exchange(ref work.State, 2); }
            }
        }

        static async Task Reply(HttpListenerContext context, int status, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > 4 * 1024 * 1024) { status = 413; bytes = Encoding.UTF8.GetBytes(Error("Result exceeds 4 MiB; narrow your query.")); }
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        public static bool ConstantEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            int difference = a.Length ^ b.Length;
            for (int i = 0; i < b.Length; i++) difference |= b[i] ^ (i < a.Length ? a[i] : 0);
            return difference == 0;
        }
        static string Error(string message)
        {
            var escaped = new StringBuilder();
            foreach (char character in message ?? "Unknown error")
            {
                if (character == '"' || character == '\\') escaped.Append('\\').Append(character);
                else if (character < 32) escaped.Append("\\u").Append(((int)character).ToString("x4"));
                else escaped.Append(character);
            }
            return "{\"ok\":false,\"error\":\"" + escaped + "\"}";
        }
    }

    public static class McpPaths
    {
        public static string Asset(string relative, string extension = null, bool existing = false)
        {
            if (string.IsNullOrEmpty(relative) || !relative.StartsWith("Assets/", StringComparison.Ordinal) ||
                relative.Contains("\\") || relative.Contains(":") || Path.IsPathRooted(relative))
                throw new ArgumentException("Path must be relative to Assets/, using forward slashes.");
            foreach (string part in relative.Split('/'))
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".") || part.EndsWith(" ") ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ArgumentException("Unsafe path segment.");
            if (extension != null && !relative.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected " + extension + " path.");
            string full = Path.GetFullPath(Path.Combine(BusaraMcpBridge.ProjectPath, relative));
            CheckNoLinks(BusaraMcpBridge.ProjectPath, full);
            if (existing && !File.Exists(full) && !Directory.Exists(full)) throw new FileNotFoundException("Asset does not exist: " + relative);
            return full;
        }

        public static void CheckNoLinks(string root, string full)
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            full = Path.GetFullPath(full);
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path escapes project root.");
            for (string current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new ArgumentException("Symbolic links and reparse points are not permitted.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        public static void NewAsset(string path, string extension)
        {
            string full = Asset(path, extension);
            if (File.Exists(full) || Directory.Exists(full) || File.Exists(full + ".meta")) throw new IOException("Destination already exists.");
            if (!Directory.Exists(Path.GetDirectoryName(full))) throw new DirectoryNotFoundException("Create the asset's parent directory first.");
        }
    }
}
#endif
