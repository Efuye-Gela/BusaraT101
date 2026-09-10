#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Busara.EditorMcp;

public class BusaraMcpTests
{
    GameObject root;
    Scene testScene;

    [SetUp] public void SetUp()
    {
        // Test Framework restores the user's scene setup after the run.
        testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        root = new GameObject("MCP test root");
    }
    [TearDown] public void TearDown()
    {
        try { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }
    }

    [TestCase(TestStatus.Passed, "Passed", 1, 0, 0, true)]
    [TestCase(TestStatus.Passed, "Passed", 0, 0, 0, false)]
    [TestCase(TestStatus.Inconclusive, "Inconclusive", 0, 0, 1, false)]
    [TestCase(TestStatus.Skipped, "Skipped", 0, 0, 0, false)]
    [TestCase(TestStatus.Skipped, "Skipped:Ignored", 1, 0, 0, true)]
    [TestCase(TestStatus.Skipped, "Skipped", 1, 0, 0, true)]
    [TestCase(TestStatus.Skipped, "Skipped:Ignored", 0, 0, 0, false)]
    [TestCase(TestStatus.Skipped, "Skipped:Cancelled", 1, 0, 0, false)]
    [TestCase(TestStatus.Failed, "Failed:Cancelled", 1, 0, 0, false)]
    [TestCase(TestStatus.Passed, "Inconclusive", 1, 0, 0, false)]
    [TestCase(TestStatus.Passed, "Passed", 1, 1, 0, false)]
    [TestCase(TestStatus.Passed, "Passed", 1, 0, 1, false)]
    public void TestRunRequiresRealPassedOutcomes(TestStatus status, string state, int passed, int failed, int inconclusive, bool expected)
    {
        Assert.AreEqual(expected, McpJobs.IsSuccessfulTestRun(status, state, passed, failed, inconclusive));
    }

    [Test] public void TestStartRefusesDirtyScenesWithoutChangingThem()
    {
        EditorSceneManager.MarkSceneDirty(testScene);
        int sceneCount = SceneManager.sceneCount;
        var exception = Assert.Throws<InvalidOperationException>(() => McpJobs.Tests(new McpRequest { mode = "EditMode" }));
        StringAssert.Contains("Save dirty open scenes", exception.Message);
        Assert.AreEqual(sceneCount, SceneManager.sceneCount);
        Assert.AreEqual(testScene, SceneManager.GetActiveScene());
        Assert.IsTrue(testScene.isDirty);
        Assert.IsTrue(root != null);
    }

    [Serializable] private class DiscoveryOwner { public int processId; }

    [TestCase("tests", "running", 7, false)]
    [TestCase("compile", "running", 7, false)]
    [TestCase("tests", "queued", 7, true)]
    [TestCase("tests", "running", 8, true)]
    [TestCase("build", "running", 7, true)]
    [TestCase("tests", "succeeded", 8, false)]
    public void ReloadPreservesRunningTestsInTheSameMainEditor(string kind, string status, int processId, bool interrupted)
    {
        var job = new McpJobs.Job { kind = kind, status = status, processId = processId };
        Assert.AreEqual(interrupted, McpJobs.ShouldInterruptOnReload(job, 7));
    }

    [Test] public void DiscoveryAndStatusIdentifyTheMainEditorProcess()
    {
        Assert.IsFalse(AssetDatabase.IsAssetImportWorkerProcess());
        BusaraMcpBridge.Start();
        var discovery = JsonUtility.FromJson<DiscoveryOwner>(
            File.ReadAllText(Path.Combine(BusaraMcpBridge.StatePath, "discovery.json")));
        var status = JsonUtility.FromJson<DiscoveryOwner>(
            McpCommands.Execute(new McpRequest { op = "status" }));
        int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        Assert.AreEqual(processId, discovery.processId);
        Assert.AreEqual(processId, status.processId);
    }

    [TestCase("../Assets/file.cs")]
    [TestCase("Assets/../ProjectSettings/file.cs")]
    [TestCase("Assets/./file.cs")]
    [TestCase("Assets//file.cs")]
    [TestCase("Assets\\file.cs")]
    [TestCase("C:/Assets/file.cs")]
    [TestCase("Assets/file.cs:stream")]
    [TestCase("Assets/folder./file.cs")]
    public void RejectsUnconfinedPaths(string path) { Assert.Throws<ArgumentException>(() => McpPaths.Asset(path)); }

    [Test] public void AcceptsAssetScriptPath()
    {
        StringAssert.EndsWith("McpCommands.cs", McpPaths.Asset("Assets/Editor/BusaraMcp/McpCommands.cs", ".cs", true));
    }

#if UNITY_EDITOR_WIN
    [DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    static extern bool CreateTestSymbolicLink(string link, string target, int flags);
#endif

    [TestCase(false)]
    [TestCase(true)]
    public void RejectsDiscoveryFileLinksIncludingDanglingTargets(bool targetExists)
    {
#if UNITY_EDITOR_WIN
        string directory = Path.Combine(BusaraMcpBridge.StatePath, "PathTests-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(directory, "target.json");
        string link = Path.Combine(directory, "discovery.json");
        Directory.CreateDirectory(directory);
        try
        {
            if (targetExists) File.WriteAllText(target, "{}");
            if (!CreateTestSymbolicLink(link, target, 2))
                Assert.Ignore("Windows cannot create an unprivileged test symlink; error " + Marshal.GetLastWin32Error() + ".");
            Assert.Throws<ArgumentException>(() => McpPaths.CheckNoLinks(BusaraMcpBridge.ProjectPath, link));
        }
        finally
        {
            File.Delete(link);
            Directory.Delete(directory, true);
        }
#else
        Assert.Ignore("This file-symlink regression fixture currently requires Windows.");
#endif
    }

    [Test] public void AuthenticationRequiresExactSecret()
    {
        Assert.IsTrue(BusaraMcpBridge.ConstantEquals("Bearer secret", "Bearer secret"));
        Assert.IsFalse(BusaraMcpBridge.ConstantEquals("Bearer secre", "Bearer secret"));
        Assert.IsFalse(BusaraMcpBridge.ConstantEquals("Bearer secret!", "Bearer secret"));
        Assert.IsFalse(BusaraMcpBridge.ConstantEquals(null, "Bearer secret"));
    }
    [Test] public void UnsupportedOperationIsAnError()
    {
        Assert.Throws<NotSupportedException>(() => McpCommands.Execute(new McpRequest { op = "shell" }));
    }
    [Test] public void QueuedWorkUsesUpdateAndNeverRetriesOrRunsInterruptedWork()
    {
        int calls = 0;
        var job = new McpJobs.Job { status = "queued" };
        McpJobs.Enqueue(job, () => calls++);
        Assert.AreEqual(0, calls);
        McpJobs.RunPendingWork();
        McpJobs.RunPendingWork();
        Assert.AreEqual(1, calls);
        McpJobs.Enqueue(job, () => calls++);
        job.status = "interrupted";
        McpJobs.RunPendingWork();
        Assert.AreEqual(1, calls);
    }
    [Test] public void RootDeletionIsProtectedEvenWithConfirmation()
    {
        Assert.Throws<InvalidOperationException>(() => McpCommands.Execute(new McpRequest
        { op = "object.delete", id = root.GetInstanceID(), confirm = "CONFIRM_DESTRUCTIVE" }));
        Assert.IsTrue(root != null);
    }
    [Test] public void DestructiveActionRequiresConfirmation()
    {
        var child = new GameObject("child");
        child.transform.SetParent(root.transform);
        Assert.Throws<ArgumentException>(() => McpCommands.Execute(new McpRequest { op = "object.delete", id = child.GetInstanceID() }));
        Assert.IsTrue(child != null);
    }
    [Test] public void CreateInspectEditAndDeleteUseLiveUnityObjects()
    {
        string json = McpCommands.Execute(new McpRequest { op = "object.create", name = "original", parentId = root.GetInstanceID(), primitive = "empty" });
        var item = JsonUtility.FromJson<McpCommands.Item>(json);
        var child = (GameObject)McpCommands.Find(item.id);
        Assert.AreEqual(root.transform, child.transform.parent);
        McpCommands.Execute(new McpRequest { op = "property.set", id = item.id, property = "m_Name", value = "updated" });
        Assert.AreEqual("updated", child.name);
        StringAssert.Contains("updated", McpCommands.Execute(new McpRequest { op = "inspect", id = item.id, limit = 50 }));
        McpCommands.Execute(new McpRequest { op = "object.delete", id = item.id, confirm = "CONFIRM_DESTRUCTIVE" });
        Assert.IsTrue(child == null);
        Undo.PerformUndo();
        Assert.IsNotNull(root.transform.Find("updated"), "Deletion must be undoable.");
    }
    [Test] public void ParentingCyclesAreRejected()
    {
        var child = new GameObject("child");
        child.transform.SetParent(root.transform);
        Assert.Throws<ArgumentException>(() => McpCommands.Execute(new McpRequest { op = "object.reparent", id = root.GetInstanceID(), parentId = child.GetInstanceID() }));
    }
    [Test] public void ForwardedBuiltInComponentTypesCanBeAddedEditedRemovedAndUndone()
    {
        var added = JsonUtility.FromJson<McpCommands.Item>(McpCommands.Execute(new McpRequest
        {
            op = "component.add", id = root.GetInstanceID(), type = "UnityEngine.SphereCollider"
        }));
        Assert.AreSame(root.GetComponent<SphereCollider>(), McpCommands.Find(added.id));
        McpCommands.Execute(new McpRequest { op = "property.set", id = added.id, property = "m_Radius", value = "2.5" });
        Assert.AreEqual(2.5f, root.GetComponent<SphereCollider>().radius);
        McpCommands.Execute(new McpRequest { op = "component.remove", id = added.id, confirm = "CONFIRM_DESTRUCTIVE" });
        Assert.IsNull(root.GetComponent<SphereCollider>());
        Undo.PerformUndo();
        Assert.IsNotNull(root.GetComponent<SphereCollider>());
    }
    [Test] public void TransformRemovalIsRejected()
    {
        Assert.Throws<ArgumentException>(() => McpCommands.Execute(new McpRequest { op = "component.remove", id = root.transform.GetInstanceID(), confirm = "CONFIRM_DESTRUCTIVE" }));
    }
}
#endif
