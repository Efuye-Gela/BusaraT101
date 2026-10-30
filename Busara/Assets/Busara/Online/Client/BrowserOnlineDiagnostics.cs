using System;
using System.Runtime.InteropServices;

namespace Busara.Online.Client
{
    public sealed class BrowserOnlineDiagnostics : IOnlineDiagnostics
    {
        public int Begin(string origin) => Observe("begin", 0, origin);
        public void Receipt(int sample, string version, bool accepted) =>
            Observe(accepted ? "accepted" : "rejected", sample, version);
        public void Failed(int sample, string outcome) => Observe("failed", sample, outcome);
        public void ViewApplied(string version) => Observe("view", 0, version);
        public int PrepareFrame(string version) => Observe("prepareFrame", 0, version);
        public void FrameBoundary(int ticket) => Observe("frame", ticket, "");

        private static int Observe(string stage, int sample, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return Busara_Latency(stage, sample, value); }
            catch (Exception) { return 0; }
#else
            return 0;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int Busara_Latency(string stage, int sample, string value);
#endif
    }
}
