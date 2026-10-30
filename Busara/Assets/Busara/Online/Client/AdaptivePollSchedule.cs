using System;

namespace Busara.Online.Client
{
    public enum PollActivity { Waiting, LocalTurn, Finished }

    public sealed class AdaptivePollSchedule
    {
        private readonly double localSeconds, waitingSeconds, quietSeconds;
        private readonly bool backoff;
        private double startedAt;
        private bool background, refreshOnResume;
        private int failures;
        private PollActivity activity = PollActivity.Waiting;
        public double NextPollAt { get; private set; }
        public bool InFlight { get; private set; }
        public double Interval => background || activity == PollActivity.Finished ? quietSeconds :
            activity == PollActivity.Waiting ? waitingSeconds : localSeconds;

        public AdaptivePollSchedule(double localSeconds, double waitingSeconds, double quietSeconds, bool backoff = true)
        {
            foreach (double seconds in new[] { localSeconds, waitingSeconds, quietSeconds })
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(localSeconds), "Polling intervals must be finite and positive.");
            this.localSeconds = localSeconds;
            this.waitingSeconds = waitingSeconds;
            this.quietSeconds = quietSeconds;
            this.backoff = backoff;
        }

        public bool IsDue(double now) => !InFlight && now >= NextPollAt;

        public void Started(double now)
        {
            if (InFlight) throw new InvalidOperationException("A projection request is already in flight.");
            InFlight = true;
            refreshOnResume = false;
            startedAt = now;
            NextPollAt = now + Interval;
        }

        public void Completed(double now, bool succeeded, PollActivity currentActivity)
        {
            InFlight = false;
            activity = currentActivity;
            failures = succeeded ? 0 : Math.Min(6, failures + 1);
            if (!succeeded && backoff)
                NextPollAt = now + Math.Min(60, Math.Max(Interval, 5 * Math.Pow(2, failures)));
            else
                NextPollAt = refreshOnResume ? now : Math.Max(now, startedAt + Interval);
        }

        public void SetBackground(bool hidden, double now)
        {
            if (background == hidden) return;
            background = hidden;
            if (failures > 0) return;
            if (!hidden)
            {
                refreshOnResume = InFlight;
                NextPollAt = now;
            }
            else
            {
                refreshOnResume = false;
                NextPollAt = Math.Max(now, startedAt + Interval);
            }
        }

        public void SetActivity(PollActivity currentActivity, double now)
        {
            if (activity == currentActivity) return;
            activity = currentActivity;
            // An embedded command projection is not completion of an outstanding GET.
            if (!InFlight && failures == 0)
                NextPollAt = Math.Max(now, startedAt + Interval);
        }

        public void RetryStaleProjection(double now)
        {
            NextPollAt = Math.Min(NextPollAt, now + .5);
        }
    }
}
