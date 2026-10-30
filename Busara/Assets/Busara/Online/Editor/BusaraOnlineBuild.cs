using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
    public const string MenuScenePath = "Assets/Scenes/mainMenu.unity";
    public const string GameScenePath = "Assets/Scenes/GameScene.unity";

    // The Web game starts at the main menu: Play leads to bots and kingdom selection offline, Play with friends to the
    // online scene. BUSARA_ONLINE_ONLY=1 builds just the online scene, as earlier Web builds did.
    public static string[] WebScenes(bool onlineOnly) =>
        onlineOnly ? new[] { ScenePath } : new[] { MenuScenePath, GameScenePath, ScenePath };

    internal static void RestorePreviousSceneSetup(UnityEditor.SceneManagement.SceneSetup[] setup)
    {
        // Batch startup can have no scene setup; keep the newly saved scene loaded in that case.
        if (setup.Length > 0) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
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
        // OnlineMVP is an authored scene: build it as saved, never regenerate it.
        bool onlineOnly = Environment.GetEnvironmentVariable("BUSARA_ONLINE_ONLY") == "1";
        foreach (string scene in WebScenes(onlineOnly))
            if (!File.Exists(Path.Combine(projectRoot, "Busara", scene)))
                throw new BuildFailedException("Missing authored scene " + scene + ".");
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
                scenes = WebScenes(onlineOnly),
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
