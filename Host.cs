using DriveUniverse.Services;
using DriveUniverse.Voice;

namespace DriveUniverse
{
    /// <summary>
    /// Single place that owns the long-lived service instances so every view can
    /// reach them without re-creating them. The window creates/starts these.
    /// </summary>
    public static class Host
    {
        public static ProcessMonitor Monitor { get; private set; }
        public static RuleEngine Rules { get; private set; }
        public static VoiceCommandService Voice { get; private set; }
        public static bool VoiceReady { get; private set; }

        public static void Init(bool safe = false)
        {
            LogService.Log("Host.Init: creating services (safe=" + safe + ")");

            Monitor = new ProcessMonitor();
            Rules = new RuleEngine(Monitor);

            // Voice is the most failure-prone subsystem (no mic, no recogniser).
            // In safe mode skip it entirely; otherwise wrap it so it can't kill startup.
            if (!safe)
            {
                try
                {
                    Voice = new VoiceCommandService();
                    Voice.Initialize();
                    VoiceReady = Voice.IsAvailable;
                    LogService.Log("Host.Init: voice ready=" + VoiceReady);
                    foreach (var a in LauncherService.Instance.Apps) Voice.RegisterApp(a.Name);
                }
                catch (System.Exception ex)
                {
                    LogService.Log("Host.Init: voice init failed (non-fatal): " + ex.Message);
                    VoiceReady = false;
                }
            }
            else
            {
                LogService.Log("Host.Init: skipping voice (safe mode).");
                VoiceReady = false;
            }

            // wire rule events -> sounds
            Rules.ActionFired += name => SoundService.Instance.Play(SoundService.Clip.Confirm);

            LogService.Log("Host.Init: done.");
        }

        public static void Shutdown()
        {
            try { Monitor?.Stop(); Rules?.Stop(); Voice?.Dispose(); } catch { }
        }
    }
}
