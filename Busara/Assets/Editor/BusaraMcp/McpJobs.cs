#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Busara.EditorMcp
{
    public static class McpJobs
    {
        [Serializable] public sealed class Job
        {
            public string jobId, kind, status, message, output, startedUtc, finishedUtc;
            public int processId, passed, failed, skipped, inconclusive;
            public bool compilationObserved;
            public List<string> errors = new List<string>();
        }
        static readonly Dictionary<string, Job> Jobs = new Dictionary<string, Job>();
        static readonly Queue<Action> PendingWork = new Queue<Action>();
        static TestRunnerApi testRunner;
        static bool initialized;
        static readonly int ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
        static string JobDirectory => Path.Combine(BusaraMcpBridge.StatePath, "Jobs");

        [InitializeOnLoadMethod]
        static void RegisterAfterReload()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;
            Initialize();
            RegisterEditorCallbacks();
        }

        public static void Initialize()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                throw new InvalidOperationException("MCP jobs run only in the main Editor, not asset import workers.");
            if (initialized) return;
            initialized = true;
            McpPaths.CheckNoLinks(BusaraMcpBridge.ProjectPath, JobDirectory);
            Directory.CreateDirectory(JobDirectory);
            foreach (string file in Directory.GetFiles(JobDirectory, "*.json").Take(1000))
            {
                try
                {
                    McpPaths.CheckNoLinks(BusaraMcpBridge.ProjectPath, file);
                    if (new FileInfo(file).Length > 1024 * 1024) continue;
                    var job = JsonUtility.FromJson<Job>(File.ReadAllText(file));
                    if (job == null || !Guid.TryParseExact(job.jobId, "N", out _)) continue;
                    Jobs[job.jobId] = job;
                    if (ShouldInterruptOnReload(job, ProcessId))
                        Finish(job, "interrupted", "Editor restarted or reload interrupted queued work. Inspect state; never retry automatically.");
                }
                catch (Exception exception) { Debug.LogWarning("MCP skipped invalid job record: " + exception.Message); }
            }
            RegisterEditorCallbacks();
            testRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            testRunner.RegisterCallbacks(new TestCallbacks());
        }
        static void RegisterEditorCallbacks()
        {
            CompilationPipeline.compilationStarted -= CompilationStarted;
            CompilationPipeline.compilationStarted += CompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= AssemblyFinished;
            CompilationPipeline.assemblyCompilationFinished += AssemblyFinished;
            CompilationPipeline.compilationFinished -= CompilationFinished;
            CompilationPipeline.compilationFinished += CompilationFinished;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }
        static bool Active(Job job) => job.status == "queued" || job.status == "running";
        internal static bool ShouldInterruptOnReload(Job job, int processId)
        {
            return Active(job) && (job.processId != processId || job.kind == "build" || job.status == "queued");
        }
        static void Save(Job job)
        {
            string path = Path.Combine(JobDirectory, job.jobId + ".json");
            McpPaths.CheckNoLinks(BusaraMcpBridge.ProjectPath, path);
            string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
            McpPaths.CheckNoLinks(BusaraMcpBridge.ProjectPath, pending);
            byte[] content = new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(job));
            if (content.Length > 1024 * 1024) throw new IOException("Job record exceeds 1 MiB.");
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(content, 0, content.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(pending, path, null);
                else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
        static Job New(string kind)
        {
            Initialize();
            if (Jobs.Values.Any(Active)) throw new InvalidOperationException("Another MCP async job is active. Poll it before starting another operation.");
            var job = new Job { jobId = Guid.NewGuid().ToString("N"), kind = kind, status = "queued", processId = ProcessId,
                startedUtc = DateTime.UtcNow.ToString("O"), message = "Queued; use unity_job to poll." };
            Jobs[job.jobId] = job;
            Save(job);
            return job;
        }
        static void Finish(Job job, string status, string message)
        {
            job.status = status;
            job.message = message;
            job.finishedUtc = DateTime.UtcNow.ToString("O");
            Save(job);
        }
        public static string Get(string id)
        {
            Initialize();
            if (!Guid.TryParseExact(id, "N", out _) || !Jobs.TryGetValue(id, out var job)) throw new ArgumentException("Unknown job ID.");
            return JsonUtility.ToJson(job);
        }
        public static string Schedule(string kind, McpRequest request, Action action)
        {
            var job = New(kind);
            Enqueue(job, () =>
            {
                try
                {
                    job.status = "running";
                    Save(job);
                    action();
                    if (kind == "refresh" && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                        Finish(job, "succeeded", "Asset refresh/import returned successfully.");
                }
                catch (Exception exception) { Finish(job, "failed", exception.Message); }
            });
            return JsonUtility.ToJson(job);
        }
        static void CompilationStarted(object context)
        {
            foreach (var job in Jobs.Values.Where(job => job.status == "running" && (job.kind == "compile" || job.kind == "refresh")))
            { job.compilationObserved = true; job.status = "running"; Save(job); }
        }
        static void AssemblyFinished(string assembly, CompilerMessage[] messages)
        {
            foreach (var job in Jobs.Values.Where(job => job.status == "running" && job.compilationObserved &&
                (job.kind == "compile" || job.kind == "refresh")))
            {
                foreach (var message in messages.Where(message => message.type == CompilerMessageType.Error))
                    if (job.errors.Count < 100) job.errors.Add(message.file + ":" + message.line + " " + message.message);
                Save(job);
            }
        }
        static void CompilationFinished(object context)
        {
            foreach (var job in Jobs.Values.Where(job => Active(job) && job.compilationObserved))
                Finish(job, job.errors.Count == 0 ? "succeeded" : "failed", "Compilation finished; poll unity_status before the next mutation because domain reload may still be pending.");
        }
        static void Update()
        {
            foreach (var job in Jobs.Values.Where(Active).ToArray())
            {
                if (DateTime.TryParse(job.startedUtc, out var started) && DateTime.UtcNow - started.ToUniversalTime() > TimeSpan.FromHours(1))
                    Finish(job, "interrupted", "Job exceeded one hour; execution may still be running. Inspect Unity before starting more work.");
                else if (job.kind == "refresh" && job.status == "running" && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    Finish(job, "succeeded", "Asset refresh/import finished.");
            }
            RunPendingWork();
        }

        internal static void RunPendingWork()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && PendingWork.Count > 0)
                PendingWork.Dequeue()();
        }

        internal static void Enqueue(Job job, Action action)
        {
            // Inspector delay callbacks can stall in an unfocused Editor; update also services the bridge.
            PendingWork.Enqueue(() =>
            {
                if (job.status == "queued")
                    action();
            });
        }

        public static string Tests(McpRequest request)
        {
            TestMode mode;
            if (request.mode == "EditMode") mode = TestMode.EditMode;
            else if (request.mode == "PlayMode") mode = TestMode.PlayMode;
            else throw new ArgumentException("mode must be EditMode or PlayMode.");
            RequireCleanTestScenes();
            var job = New("tests");
            Enqueue(job, () =>
            {
                try
                {
                    RequireCleanTestScenes();
                    job.status = "running"; Save(job);
                    testRunner.Execute(new ExecutionSettings(new Filter { testMode = mode, testNames = request.tests == null || request.tests.Length == 0 ? null : request.tests }));
                }
                catch (Exception exception) { Finish(job, "failed", exception.Message); }
            });
            return JsonUtility.ToJson(job);
        }

        static void RequireCleanTestScenes()
        {
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                    throw new InvalidOperationException("Save dirty open scenes before running tests; no scene changes were discarded. Dirty scene: " + scene.name);
            }
        }

        internal static bool IsSuccessfulTestRun(TestStatus status, string resultState, int passed, int failed, int inconclusive)
        {
            bool completed = status == TestStatus.Passed && resultState == "Passed" ||
                status == TestStatus.Skipped && (resultState == "Skipped" ||
                    resultState == "Skipped:Ignored" || resultState == "Skipped:Explicit");
            return completed && passed > 0 && failed == 0 && inconclusive == 0;
        }

        sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                var job = Jobs.Values.FirstOrDefault(item => item.kind == "tests" && Active(item));
                if (job == null || result.Test.IsSuite || (result.TestStatus != TestStatus.Failed && result.TestStatus != TestStatus.Inconclusive)) return;
                if (job.errors.Count < 100) job.errors.Add(result.Test.FullName + ": " + result.Message);
                Save(job);
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                var job = Jobs.Values.FirstOrDefault(item => item.kind == "tests" && Active(item));
                if (job == null) return;
                job.passed = result.PassCount; job.failed = result.FailCount; job.skipped = result.SkipCount; job.inconclusive = result.InconclusiveCount;
                bool succeeded = IsSuccessfulTestRun(result.TestStatus, result.ResultState, job.passed, job.failed, job.inconclusive);
                string message = job.passed + job.failed + job.inconclusive == 0
                    ? "No tests executed; check the requested test filter. Result: " + result.ResultState
                    : "Test run finished: " + result.ResultState + "; passed: " + job.passed + ", failed: " + job.failed + ", inconclusive: " + job.inconclusive + ", skipped: " + job.skipped;
                if (!succeeded && job.errors.Count < 100) job.errors.Add(message);
                Finish(job, succeeded ? "succeeded" : "failed", message);
            }
        }

        public static string Build(McpRequest request)
        {
            if (!Enum.TryParse(request.target, out BuildTarget target)) throw new ArgumentException("Unknown build target.");
            string filename;
            switch (target)
            {
                case BuildTarget.StandaloneWindows64: filename = "Busara.exe"; break;
                case BuildTarget.StandaloneLinux64: filename = "Busara"; break;
                case BuildTarget.StandaloneOSX: filename = "Busara.app"; break;
                case BuildTarget.Android: filename = "Busara.apk"; break;
                case BuildTarget.WebGL: filename = "WebGL"; break;
                default: throw new NotSupportedException("This build target is not exposed by the bridge.");
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new NotSupportedException("Install Unity build support for " + target + " first.");
            if (target != EditorUserBuildSettings.activeBuildTarget)
                throw new NotSupportedException("Switch the active build target manually in Unity first; automatic platform switches/domain reload are not supported.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new ArgumentException("No enabled scenes in Build Settings.");
            foreach (string scene in scenes) McpPaths.Asset(scene, ".unity", true);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before building.");
            var job = New("build");
            string directory = Path.Combine(BusaraMcpBridge.StatePath, "Builds", job.jobId);
            McpPaths.CheckNoLinks(BusaraMcpBridge.ProjectPath, directory);
            Directory.CreateDirectory(directory);
            job.output = Path.Combine(directory, filename);
            Save(job);
            Enqueue(job, () =>
            {
                try
                {
                    job.status = "running"; Save(job);
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, target = target, locationPathName = job.output, options = BuildOptions.None });
                    job.failed = (int)report.summary.totalErrors;
                    Finish(job, report.summary.result == BuildResult.Succeeded ? "succeeded" : "failed", report.summary.result + ", errors: " + report.summary.totalErrors);
                }
                catch (Exception exception) { Finish(job, "failed", exception.Message); }
            });
            return JsonUtility.ToJson(job);
        }
    }
}
#endif
