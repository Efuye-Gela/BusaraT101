using System;
using System.IO;
using System.Linq;
using Busara.Online.Client;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BusaraOnlineBuild
{
    [Serializable] private sealed class WebConfiguration
    {
        public string backend = "ugs";
        public string projectId = "";
        public string environmentName = "development";
        public string moduleName = "BusaraUgs";
        public int pollSeconds = 10;
    }
    public const string ScenePath = "Assets/Busara/Online/Scenes/OnlineMVP.unity";

    [MenuItem("Busara/Online MVP/Generate presentation scene")]
    public static void GenerateScene()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Leave Play mode before generating the online scene.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard open scene edits explicitly before generating OnlineMVP.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BusaraOnlineSceneBuilder.Populate();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new BuildFailedException("Could not persist OnlineMVP scene.");
            AssetDatabase.SaveAssets();
            // Append only: existing Offline Play scene indices and default startup remain intact.
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        }
        finally { RestorePreviousSceneSetup(setup); }
    }

    internal static void RestorePreviousSceneSetup(SceneSetup[] setup)
    {
        // Batch startup can have no scene setup; keep the newly saved scene loaded in that case.
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    [MenuItem("Busara/Online MVP/Build Web player")]
    public static void BuildWeb()
    {
        if (!Application.unityVersion.StartsWith("6000.3.6f1", StringComparison.Ordinal))
            throw new BuildFailedException("This worktree's approved online build requires Unity 6000.3.6f1.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new BuildFailedException("Install the matching Unity 6000.3.6f1 WebGL module first.");
        var projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
        string output = Path.Combine(projectRoot, "online", "web");
        var webConfig = new WebConfiguration
        {
            backend = Environment.GetEnvironmentVariable("BUSARA_ONLINE_BACKEND") ?? "ugs",
            projectId = Environment.GetEnvironmentVariable("BUSARA_UGS_PROJECT_ID") ?? "",
            environmentName = Environment.GetEnvironmentVariable("BUSARA_UGS_ENVIRONMENT") ?? "development"
        };
        if (webConfig.backend != "ugs" && webConfig.backend != "legacy")
            throw new BuildFailedException("BUSARA_ONLINE_BACKEND must be ugs or legacy.");
        if (webConfig.backend == "ugs" &&
            (!Guid.TryParse(webConfig.projectId, out _) || string.IsNullOrWhiteSpace(webConfig.environmentName)))
            throw new BuildFailedException("Set BUSARA_UGS_PROJECT_ID and BUSARA_UGS_ENVIRONMENT before building UGS. Use the PowerShell build script parameters; legacy must be chosen explicitly.");
        GenerateScene();
        string template = PlayerSettings.WebGL.template;
        var compression = PlayerSettings.WebGL.compressionFormat;
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        try
        {
            PlayerSettings.WebGL.template = "PROJECT:BusaraOnline";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            bool development = Environment.GetEnvironmentVariable("BUSARA_ONLINE_DEVELOPMENT") == "1";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Unity OnlineMVP Web build failed: " + report.summary.result);
            File.WriteAllText(Path.Combine(output, "busara-config.js"),
                "window.busaraConfig = " + JsonUtility.ToJson(webConfig, true) + ";\n");
            Debug.Log("OnlineMVP Unity Web build succeeded: " + output);
        }
        finally
        {
            PlayerSettings.WebGL.template = template;
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            AssetDatabase.SaveAssets();
        }
    }
}
