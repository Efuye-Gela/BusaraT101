using System;
using System.Linq;
using System.Text.RegularExpressions;
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
    public void UgsRouteAndPollingDoNotClaimLegacyLiveSocket()
    {
        var route = JsonUtility.FromJson<OnlineBrowserTransport.Route>(
            "{\"backend\":\"ugs\",\"pollSeconds\":10,\"hasInvite\":false}");
        Assert.AreEqual("ugs", route.backend);
        Assert.AreEqual(10, route.pollSeconds);
        var host = new GameObject("UGS protocol test");
        try
        {
            var transport = host.AddComponent<OnlineBrowserTransport>();
            var session = host.AddComponent<OnlineSession>();
            session.Initialize(transport);
            transport.OnBrowserEvent("{\"kind\":\"polling\"}");
            Assert.AreEqual("UGS - HTTPS polling", session.Connection);
            Assert.IsNull(session.View);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

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
            Assert.AreEqual(4, screen.resourceIcons.Length);
            Assert.IsTrue(screen.resourceIcons.All(sprite => sprite != null));
            Assert.IsTrue(components.All(component => IsOnlinePresentation(component.GetType().Namespace)),
                string.Join(", ", components.Select(c => c.GetType().FullName).Distinct()));
            CollectionAssert.AreEquivalent(new[] { "Entry", "Lobby", "Match" },
                components.OfType<OnlinePage>().Select(page => page.Id).ToArray());
            Assert.AreEqual(1, components.OfType<Eg.UI.UIRoot>().Count());
            Assert.AreEqual("BusaraOnline", screen.gameObject.name, "The browser bridge sends events to this object name.");
            Assert.IsNotNull(screen.GetComponent<OnlineSession>());
            Assert.IsNotNull(screen.GetComponent<OnlineBrowserTransport>());
            // Every control the controller shows is authored on its page; nothing is created at runtime.
            var pages = components.OfType<OnlinePage>().ToDictionary(page => page.Id);
            var pending = new[] { "retry-pending", "resolve-conflict" };
            AssertAuthored(pages["Entry"], pending.Concat(new[] { "Entry heading", "Scope", "Entry actions", "Offline distinction",
                "retry-session", "retry-room", "discard-room", "create-room", "join-room" }));
            AssertAuthored(pages["Lobby"], pending.Concat(new[] { "Lobby heading", "Scope", "Seat card 0", "Seat card 1", "Seat 0",
                "Seat 1", "player-name", "configure", "start", "copy-invite", "Room revision" }));
            AssertAuthored(pages["Match"], pending.Concat(new[] { "Turn", "Revision", "Power status", "Board owner 0", "Board owner 1",
                "Player details 0", "Player details 1", "Legal actions", "choice-draw--1--1--1", "mode-forge:-1", "mode-trade:-1",
                "choice-weapon--1--1--1", "use-power", "Setup heading", "Setup hint" })
                .Concat(Enumerable.Range(0, 4).Select(i => "mode-setupPlace:" + i))
                .Concat(Enumerable.Range(0, 32).Select(i => "slot-" + i)));
            AssertAuthored(pages["Match"], Enumerable.Range(0, 32).Select(i => "slot-" + i + "/Legal"));
            AssertAuthored(pages["Match"], new[] { "view-seat-0", "view-seat-1", "info-tab-kingdom", "info-tab-virtues",
                "info-tab-combos", "Info title", "Kingdom name", "Kingdom story", "Power name", "Power description",
                "Goal heading", "Goal note", "Forged heading", "Forged note", "Combos heading" }
                .Concat(Enumerable.Range(0, 3).Select(i => "Goal row " + i))
                .Concat(Enumerable.Range(0, 6).Select(i => "Forged " + i))
                .Concat(Enumerable.Range(0, 6).Select(i => "Combo " + i)));
            Assert.AreEqual(6, screen.virtueIcons.Length);
            Assert.IsTrue(screen.virtueIcons.All(sprite => sprite != null));
            var list = pages["Match"].Content.Find("Legal actions").GetComponent<UnityEngine.UI.ScrollRect>().content;
            foreach (string template in new[] { "List Label Template", "List Button Template", "List Primary Template" })
                Assert.IsFalse(list.Find(template).gameObject.activeSelf, template + " must stay an inactive template.");
            var audit = Eg.Testing.Editor.SceneAudit.Audit(scene);
            Assert.IsEmpty(audit, string.Join("\n", audit));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void KingdomTextsMatchTheOfflineKingdomAssets()
    {
        Scene scene = EditorSceneManager.OpenScene(BusaraOnlineBuild.ScenePath, OpenSceneMode.Additive);
        try
        {
            var screen = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<OnlineMvpScreen>(true)).Single();
            CollectionAssert.AreEquivalent(KingdomCatalog.All.Select(k => k.Id), screen.kingdomTexts.Select(k => k.id));
            var offline = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Busara/ScriptableObjects/Kingdoms" })
                .Select(guid => new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid))))
                .Where(asset => asset.FindProperty("kingdomStory") != null)
                .ToDictionary(asset => asset.FindProperty("kingdomName").stringValue);
            foreach (var text in screen.kingdomTexts)
            {
                Assert.IsTrue(offline.TryGetValue(text.name, out var asset), "No offline kingdom named " + text.name);
                Assert.AreEqual(Regex.Replace(asset.FindProperty("kingdomStory").stringValue, @"\s+", " ").Trim(), text.story,
                    text.name + " story");
                var power = new SerializedObject(asset.FindProperty("power").objectReferenceValue);
                string description = Regex.Replace(power.FindProperty("powerDescription").stringValue.Replace("\r\n", "\n"),
                    @"^COST:\n- [^\n]*\n", "");
                Assert.AreEqual(Regex.Replace(description, @"\s*\n\s*", " ").Trim(), text.power, text.name + " power");
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    private static void AssertAuthored(OnlinePage page, IEnumerable<string> names)
    {
        foreach (string name in names)
            Assert.IsNotNull(page.Content.Find(name), page.Id + " page is missing authored control " + name);
    }

    // No offline managers, bots or gameplay MonoBehaviours may enter the online scene.
    private static bool IsOnlinePresentation(string ns) => ns != null &&
        (ns.StartsWith("Busara.Online") || ns.StartsWith("Eg.") || ns.StartsWith("UnityEngine") || ns == "TMPro");
}
