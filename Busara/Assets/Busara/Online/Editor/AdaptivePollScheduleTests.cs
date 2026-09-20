using System;
using System.Reflection;
using Busara.Online;
using Busara.Online.Client;
using NUnit.Framework;
using UnityEngine;

public sealed class AdaptivePollScheduleTests
{
    [TestCase(PollActivity.Waiting, 2)]
    [TestCase(PollActivity.LocalTurn, 10)]
    [TestCase(PollActivity.Finished, 30)]
    public void CadenceIsMeasuredFromRequestStart(PollActivity activity, double interval)
    {
        var polling = new AdaptivePollSchedule(10, 2, 30);
        polling.Started(0);
        Assert.IsFalse(polling.IsDue(100), "Requests never overlap.");
        Assert.Throws<InvalidOperationException>(() => polling.Started(1));
        polling.Completed(.75, true, activity);
        Assert.AreEqual(interval, polling.NextPollAt);
        Assert.IsFalse(polling.IsDue(interval - .01));
        Assert.IsTrue(polling.IsDue(interval));
    }

    [Test]
    public void SlowResponsesDoNotOverlapOrAddAnotherWaitingInterval()
    {
        var polling = new AdaptivePollSchedule(10, 2, 30);
        polling.Started(0);
        polling.Completed(3, true, PollActivity.Waiting);
        Assert.IsTrue(polling.IsDue(3));
    }

    [Test]
    public void HiddenTabsSlowDownAndResumeRefreshesImmediately()
    {
        var polling = new AdaptivePollSchedule(10, 2, 30);
        polling.Started(0);
        polling.Completed(.2, true, PollActivity.Waiting);
        polling.SetBackground(true, 1);
        Assert.AreEqual(30, polling.NextPollAt);
        polling.SetBackground(false, 5);
        Assert.IsTrue(polling.IsDue(5));
        polling.Started(5);
        polling.SetBackground(true, 5.1);
        polling.SetBackground(false, 5.2);
        Assert.IsFalse(polling.IsDue(5.2));
        polling.Completed(6, true, PollActivity.Waiting);
        Assert.IsTrue(polling.IsDue(6), "Focus during a request queues one refresh.");
    }

    [Test]
    public void FailuresBackOffAndFocusCannotBypassTheDelay()
    {
        var polling = new AdaptivePollSchedule(10, 2, 30);
        double now = 0;
        foreach (double delay in new[] { 10d, 20, 40, 60, 60 })
        {
            polling.Started(now);
            polling.Completed(now + 1, false, PollActivity.Waiting);
            Assert.AreEqual(now + 1 + delay, polling.NextPollAt);
            polling.SetBackground(true, now + 2);
            polling.SetBackground(false, now + 3);
            Assert.AreEqual(now + 1 + delay, polling.NextPollAt);
            now = polling.NextPollAt;
        }
        polling.Started(now);
        polling.Completed(now + .1, true, PollActivity.Waiting);
        Assert.AreEqual(now + 2, polling.NextPollAt);
    }

    [Test]
    public void HidingAgainCancelsAnInFlightResumeRefresh()
    {
        var polling = new AdaptivePollSchedule(10, 2, 30);
        polling.Started(0);
        polling.SetBackground(true, .1);
        polling.SetBackground(false, .2);
        polling.SetBackground(true, .3);
        polling.Completed(.5, true, PollActivity.Waiting);
        Assert.AreEqual(30, polling.NextPollAt);
    }

    [Test]
    public void FixedCadenceAndReceiptCatchupRemainAvailable()
    {
        var polling = new AdaptivePollSchedule(4, 4, 4, false);
        polling.Started(0);
        polling.Completed(1, false, PollActivity.Waiting);
        Assert.AreEqual(4, polling.NextPollAt);
        polling.Started(4);
        polling.Completed(4.1, true, PollActivity.LocalTurn);
        polling.RetryStaleProjection(4.1);
        Assert.AreEqual(4.6, polling.NextPollAt, .001);
    }

    [Test]
    public void OneMinuteWaitingBudgetChangesFromSixToThirtyRequests()
    {
        foreach (int interval in new[] { 10, 2 })
        {
            var polling = new AdaptivePollSchedule(10, interval, 30);
            int requests = 0;
            for (int second = 0; second < 60; second++)
                if (polling.IsDue(second))
                {
                    polling.Started(second);
                    polling.Completed(second + .25, true, PollActivity.Waiting);
                    requests++;
                }
            Assert.AreEqual(interval == 10 ? 6 : 30, requests);
        }
    }

    [Test]
    public void EmbeddedViewImmediatelySwitchesOwnTurnToWaiting()
    {
        var polling = new AdaptivePollSchedule(10, 2, 30);
        polling.Started(0);
        polling.Completed(.2, true, PollActivity.LocalTurn);
        Assert.AreEqual(10, polling.NextPollAt);
        polling.SetActivity(PollActivity.Waiting, 1);
        Assert.AreEqual(2, polling.NextPollAt);
        polling.SetActivity(PollActivity.Finished, 1.5);
        Assert.AreEqual(30, polling.NextPollAt);
    }

    [Test]
    public void EmbeddedViewDoesNotFinishAnInFlightGetOrBypassBackoff()
    {
        var polling = new AdaptivePollSchedule(10, 2, 30);
        polling.Started(0);
        polling.SetActivity(PollActivity.LocalTurn, .1);
        Assert.IsTrue(polling.InFlight);
        Assert.IsFalse(polling.IsDue(100));
        polling.Completed(.2, false, PollActivity.LocalTurn);
        double retry = polling.NextPollAt;
        polling.SetActivity(PollActivity.Waiting, 1);
        Assert.AreEqual(retry, polling.NextPollAt);
    }

    [Test]
    public void SessionEmbeddedProjectionUpdatesScheduler()
    {
        var host = new GameObject("Embedded projection scheduling test");
        try
        {
            var session = host.AddComponent<OnlineSession>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(OnlineSession).GetField("matchId", flags).SetValue(session, "match");
            var polling = new AdaptivePollSchedule(10, 2, 30);
            polling.Started(Time.unscaledTime);
            polling.Completed(Time.unscaledTime, true, PollActivity.LocalTurn);
            typeof(OnlineSession).GetField("polling", flags).SetValue(session, polling);
            typeof(OnlineSession).GetMethod("ApplyFreshView", flags).Invoke(session, new object[] {
                new ClientView { matchId = "match", ruleset = "busara-online-mvp-v1",
                    version = "2", phase = "Action", awaitingOther = true }
            });
            Assert.AreEqual(2, polling.Interval);
            Assert.LessOrEqual(polling.NextPollAt, Time.unscaledTime + 2);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [TestCase("Lobby", false, PollActivity.Waiting)]
    [TestCase("Setup", true, PollActivity.Waiting)]
    [TestCase("Action", true, PollActivity.Waiting)]
    [TestCase("Waiting", true, PollActivity.Waiting)]
    [TestCase("AfterActionDecision", false, PollActivity.LocalTurn)]
    [TestCase("Action", false, PollActivity.LocalTurn)]
    [TestCase("Finished", false, PollActivity.Finished)]
    public void SessionUsesAuthorizedWaitingState(string phase, bool waiting, PollActivity expected)
    {
        var host = new GameObject("Polling policy test");
        try
        {
            var session = host.AddComponent<OnlineSession>();
            typeof(OnlineSession).GetProperty("View").SetValue(session,
                new ClientView { phase = phase, awaitingOther = waiting, seat = 1, activeSeat = 0 });
            var activity = typeof(OnlineSession).GetProperty("CurrentPollActivity", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.AreEqual(expected, activity.GetValue(session));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
