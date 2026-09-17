using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Busara.Online;
using Busara.Online.Client;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OnlineClientTests
{
    [Test]
    public void EmptyBatchSceneSetupKeepsCurrentSceneLoaded()
    {
        Scene current = SceneManager.GetActiveScene();
        Assert.IsTrue(current.IsValid() && current.isLoaded);
        Assert.DoesNotThrow(() => BusaraOnlineBuild.RestorePreviousSceneSetup(Array.Empty<SceneSetup>()));
        Assert.IsTrue(current.isLoaded);
        Assert.AreEqual(current, SceneManager.GetActiveScene());
    }

    [TestCase("{invalid")]
    [TestCase("{}")]
    public void MalformedBrowserEnvelopeReleasesWaitersWithSafeDiagnostic(string json)
    {
        var host = new GameObject("Protocol test");
        try
        {
            var transport = host.AddComponent<OnlineBrowserTransport>();
            var requests = (Dictionary<string, Action<OnlineBrowserTransport.Envelope>>)
                typeof(OnlineBrowserTransport).GetField("requests", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transport);
            OnlineBrowserTransport.Envelope response = null;
            OnlineBrowserTransport.Envelope diagnostic = null;
            requests.Add("pending-request", value => response = value);
            transport.Event += value => diagnostic = value;
            transport.OnBrowserEvent(json);
            Assert.AreEqual("protocolError", response.kind);
            Assert.AreEqual("protocolError", diagnostic.kind);
            Assert.AreEqual(0, requests.Count);
            StringAssert.DoesNotContain(json, diagnostic.body);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [TestCase("9007199254740993", "9007199254740992", 1)]
    [TestCase("100000000000000000000000000", "99999999999999999999999999", 1)]
    [TestCase("0002", "2", 0)]
    [TestCase("5", "9", -1)]
    public void RevisionsNeverUseJavascriptOrFloatingPointNumbers(string left, string right, int expected)
    {
        Assert.AreEqual(expected, Math.Sign(OnlineSession.CompareVersions(left, right)));
    }

    [TestCase("")]
    [TestCase("1.2")]
    [TestCase("-1")]
    public void MalformedRevisionsFailClosed(string value)
    {
        Assert.Throws<ArgumentException>(() => OnlineSession.CompareVersions(value, "1"));
    }

    [Test]
    public void PendingCommandRoundTripPreservesIdentityAndExactPayment()
    {
        var command = new OnlineCommand
        {
            commandId = "retained-command", expectedVersion = "9007199254740993",
            decisionId = "same-decision", kind = "use", paymentIds = new[] { "owned-art", "owned-security" }
        };
        string json = JsonUtility.ToJson(command);
        var restored = JsonUtility.FromJson<OnlineCommand>(json);
        Assert.AreEqual(command.commandId, restored.commandId);
        Assert.AreEqual(command.expectedVersion, restored.expectedVersion);
        Assert.AreEqual(command.decisionId, restored.decisionId);
        CollectionAssert.AreEqual(command.paymentIds, restored.paymentIds);
        StringAssert.Contains("\"paymentIds\"", json);
    }

    [Test]
    public void JsonUtilityReadsNumericEnumsAndOwnerDecision()
    {
        var view = JsonUtility.FromJson<ClientView>(
            "{\"matchId\":\"m\",\"version\":\"12\",\"phase\":\"AfterActionDecision\",\"seat\":1," +
            "\"decision\":{\"id\":\"d\",\"kind\":\"Retraction\",\"owner\":1,\"prompt\":\"Choose exact payment\"," +
            "\"paymentCost\":2,\"paymentOptions\":[{\"id\":\"v1\",\"type\":0},{\"id\":\"v2\",\"type\":1}]}}");
        Assert.AreEqual(1, view.decision.owner);
        Assert.AreEqual(VirtueType.Art, view.decision.paymentOptions[0].type);
        Assert.AreEqual(VirtueType.Security, view.decision.paymentOptions[1].type);
    }

    [Test]
    public void PersistedOnlineSceneHasAuthoredPresentationAndNoOfflineManagers()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(BusaraOnlineBuild.ScenePath));
        Scene scene = EditorSceneManager.OpenScene(BusaraOnlineBuild.ScenePath, OpenSceneMode.Additive);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var screen = components.OfType<OnlineMvpScreen>().Single();
            Assert.IsNotNull(screen.font);
            Assert.IsNotNull(screen.boardArt);
            Assert.AreEqual(4, screen.resourceIcons.Length);
            Assert.IsTrue(screen.resourceIcons.All(sprite => sprite != null));
            Assert.IsTrue(components.All(component => component is OnlineMvpScreen));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
