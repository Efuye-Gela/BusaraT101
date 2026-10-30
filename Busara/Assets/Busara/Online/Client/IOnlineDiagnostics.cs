namespace Busara.Online.Client
{
    // Optional observations only: implementations must not throw or change session behavior.
    public interface IOnlineDiagnostics
    {
        int Begin(string origin);
        void Receipt(int sample, string version, bool accepted);
        void Failed(int sample, string outcome);
        void ViewApplied(string version);
        int PrepareFrame(string version);
        void FrameBoundary(int ticket);
    }
}
