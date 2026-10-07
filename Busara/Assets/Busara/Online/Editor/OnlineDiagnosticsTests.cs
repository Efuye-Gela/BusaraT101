using System;
using System.Collections.Generic;
using System.Reflection;
using Busara.Online;
using Busara.Online.Client;
using NUnit.Framework;
using UnityEngine;

public sealed class OnlineDiagnosticsTests
{
    private sealed class Observations : IOnlineDiagnostics
    {
        public readonly List<string> Events = new List<string>();
        public int Begin(string origin) { Events.Add("begin:" + origin); return Events.Count; }
        public void Receipt(int sample, string version, bool accepted) { Events.Add("receipt"); }
        public void Failed(int sample, string outcome) { Events.Add("failed:" + outcome); }
        public void ViewApplied(string version) { Events.Add("view:" + version); }
        public int PrepareFrame(string version) { Events.Add("rebuild:" + version); return 0; }
        public void FrameBoundary(int ticket) { Events.Add("frame"); }
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static string Apply(OnlineSession session, ClientView view) =>
        typeof(OnlineSession).GetMethod("ApplyFreshView", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session, new object[] { view }).ToString();

    [Test]
    public void BrowserObserverIsOptionalOutsideWebPlayer()
    {
        var diagnostics = new BrowserOnlineDiagnostics();
        Assert.AreEqual(0, diagnostics.Begin("submit"));
        Assert.DoesNotThrow(() => {
            diagnostics.Receipt(0, "2", true);
            diagnostics.Failed(0, "delivery_uncertain");
            diagnostics.ViewApplied("2");
            diagnostics.FrameBoundary(0);
        });
        Assert.AreEqual(0, diagnostics.PrepareFrame("2"));
    }

    [TestCase("busara-online-mvp-v1")]
    [TestCase("busara-online-mvp-v2")]
    [TestCase("busara-online-v3")]
    public void OnlyAuthorizedNonStaleViewsReachDiagnostics(string ruleset)
    {
        var host = new GameObject("Timing projection test");
        try
        {
            var session = host.AddComponent<OnlineSession>();
            var observations = new Observations();
            Set(session, "diagnostics", observations);
            Set(session, "matchId", "room");
            Set(session, "confirmedVersion", "10");
            Assert.AreEqual("Invalid", Apply(session, new ClientView {
                matchId = "other-room", ruleset = ruleset, version = "11" }));
            Assert.AreEqual("Stale", Apply(session, new ClientView {
                matchId = "room", ruleset = ruleset, version = "9" }));
            Assert.AreEqual(0, observations.Events.Count);
            Assert.AreEqual("Applied", Apply(session, new ClientView {
                matchId = "room", ruleset = ruleset, version = "11" }));
            session.NotifyViewRendered("10");
            CollectionAssert.AreEqual(new[] { "view:11" }, observations.Events);
            session.NotifyViewRendered("11");
            CollectionAssert.AreEqual(new[] { "view:11", "rebuild:11" }, observations.Events);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void RecoveryAndRetryKeepTheSamePendingBodyAndCommand()
    {
        var host = new GameObject("Timing retry test");
        try
        {
            var session = host.AddComponent<OnlineSession>();
            var transport = host.AddComponent<OnlineBrowserTransport>();
            var observations = new Observations();
            var command = new OnlineCommand { commandId = Guid.NewGuid().ToString(), expectedVersion = "10",
                kind = "use", slots = new[] { 1, 4, 8 }, paymentIds = new[] { "owned" } };
            string body = JsonUtility.ToJson(command);
            Set(session, "transport", transport);
            Set(session, "diagnostics", observations);
            Set(session, "<Guest>k__BackingField", new GuestView { guestId = "guest", csrfToken = "private" });
            Set(session, "matchId", "room");
            Set(session, "pending", command);
            Set(session, "pendingBody", body);
            Set(session, "recoveringPending", true);
            session.RetryPending();
            session.RetryPending();
            CollectionAssert.AreEqual(new[] { "begin:recovery", "failed:delivery_uncertain",
                "begin:retry", "failed:delivery_uncertain" }, observations.Events);
            Assert.IsTrue(session.Pending);
            Assert.AreSame(command, typeof(OnlineSession).GetField("pending",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session));
            Assert.AreEqual(body, typeof(OnlineSession).GetField("pendingBody",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session));
            CollectionAssert.AreEqual(command.slots, JsonUtility.FromJson<OnlineCommand>(body).slots);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
