using System;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Threading;
using DriveUniverse.Services;

namespace DriveUniverse.Voice
{
    public enum VoiceIntent { Eject, Connect, Disconnect, OpenApp, Help, Sleep, Wake, Status, Confirm, Decline, Unknown }

    public class VoiceCommand
    {
        public VoiceIntent Intent;
        public string Argument;
        public string RawText;
    }

    /// <summary>
    /// Offline voice framework using System.Speech (SAPI5).
    ///
    /// Wake word design: a SINGLE grammar containing both the wake word AND all
    /// commands is always loaded. The _wakeMode flag controls which results are
    /// acted upon. This avoids fragile grammar hot-swapping during recognition.
    ///
    /// State flow:
    ///   wake mode ON:  only "orbit"/"hey orbit" is acted on → enters command mode
    ///   command mode:  all commands active for 8 seconds → reverts to wake mode
    ///   wake mode OFF: all commands always active
    /// </summary>
    public sealed class VoiceCommandService : IDisposable
    {
        private SpeechRecognitionEngine _engine;
        private SpeechSynthesizer _synth;
        private bool _listening;
        private bool _wakeMode = true;
        private DispatcherTimer _commandWindowTimer;
        private const float MinConfidence = 0.80f;

        public bool IsAvailable { get; private set; }
        public string UnavailableReason { get; private set; } = "";
        public string DiagnosticsLog { get; private set; } = "";
        public bool AlwaysListening { get; set; }
        public string SelectedCulture { get; set; } = "";
        public bool InCommandMode => !_wakeMode;

        public event Action<VoiceCommand> CommandRecognized;
        public event Action<bool> ListeningChanged;
        public event Action WakeWordHeard;
        public event Action CommandWindowExpired;

        public VoiceCommandService()
        {
            IsAvailable = false;
            // Sync wake mode with preference at construction
            _wakeMode = LauncherService.Instance.WakeWordEnabled;
        }

        public System.Collections.Generic.List<(string name, string culture)> GetAvailableCultures()
        {
            var result = new System.Collections.Generic.List<(string, string)>();
            try
            {
                foreach (var r in SpeechRecognitionEngine.InstalledRecognizers())
                {
                    string cult = r.Culture?.Name ?? "unknown";
                    string display = r.Culture?.EnglishName ?? r.Name;
                    result.Add((display, cult));
                }
            }
            catch { }
            return result;
        }

        public void Initialize()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== Voice Diagnostics ===");

            try
            {
                log.AppendLine("Step 1: Checking recognizers...");
                var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
                log.AppendLine("  Found " + recognizers.Count + " recognizer(s).");
                if (recognizers.Count > 0)
                    foreach (var r in recognizers)
                        log.AppendLine("    - " + r.Name + " [" + (r.Culture?.Name ?? "?") + "]");

                if (recognizers == null || recognizers.Count == 0)
                {
                    log.AppendLine("  FAILED: No recognizers.");
                    IsAvailable = false;
                    UnavailableReason = "No speech recognizer found.";
                    DiagnosticsLog = log.ToString();
                    return;
                }

                log.AppendLine("Step 2: Selecting recognizer...");
                RecognizerInfo ri;
                if (!string.IsNullOrEmpty(SelectedCulture))
                {
                    ri = recognizers.FirstOrDefault(r => r.Culture?.Name == SelectedCulture)
                         ?? recognizers.FirstOrDefault(r => r.Culture?.Name.StartsWith(SelectedCulture) == true)
                         ?? recognizers[0];
                    log.AppendLine("  Culture: " + SelectedCulture);
                }
                else
                {
                    var culture = System.Globalization.CultureInfo.CurrentUICulture;
                    ri = recognizers.FirstOrDefault(r => r.Culture != null &&
                        r.Culture.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
                        ?? recognizers[0];
                }
                log.AppendLine("  Selected: " + ri.Name + " [" + (ri.Culture?.Name ?? "?") + "]");

                try { _engine?.Dispose(); } catch { }

                log.AppendLine("Step 3: Creating engine...");
                _engine = new SpeechRecognitionEngine(ri);
                _engine.SetInputToDefaultAudioDevice();
                _engine.SpeechRecognized += OnRecognized;

                log.AppendLine("Step 4: Loading grammar...");
                LoadGrammar();
                try { _engine.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", 20); } catch { }

                log.AppendLine("Step 5: Creating synthesizer...");
                _synth = new SpeechSynthesizer();
                string selectedVoiceName = "";
                try
                {
                    var voices = _synth.GetInstalledVoices(ri.Culture);
                    if (voices != null && voices.Count > 0)
                    {
                        _synth.SelectVoice(voices[0].VoiceInfo.Name);
                        selectedVoiceName = voices[0].VoiceInfo.Name;
                    }
                    else
                    {
                        string langPrefix = ri.Culture?.TwoLetterISOLanguageName ?? "";
                        var allVoices = _synth.GetInstalledVoices();
                        var match = allVoices.FirstOrDefault(v =>
                            v.VoiceInfo.Culture != null &&
                            v.VoiceInfo.Culture.TwoLetterISOLanguageName == langPrefix);
                        if (match != null)
                        {
                            _synth.SelectVoice(match.VoiceInfo.Name);
                            selectedVoiceName = match.VoiceInfo.Name;
                        }
                    }

                    if (string.IsNullOrEmpty(selectedVoiceName))
                    {
                        log.AppendLine("  Available TTS voices:");
                        var allVoices = _synth.GetInstalledVoices();
                        foreach (var v in allVoices)
                            log.AppendLine("    - " + v.VoiceInfo.Name + " [" + (v.VoiceInfo.Culture?.Name ?? "?") + "]");
                        log.AppendLine("  No voice for " + (ri.Culture?.Name ?? "?") + " - using default");
                    }
                    else
                    {
                        log.AppendLine("  TTS voice: " + selectedVoiceName);
                    }
                }
                catch { log.AppendLine("  TTS: using system default"); }

                // Sync wake mode with current preference
                _wakeMode = LauncherService.Instance.WakeWordEnabled;

                log.AppendLine("=== ALL CHECKS PASSED ===");
                log.AppendLine("Wake word mode: " + (_wakeMode ? "ON (say 'Orbit' first)" : "OFF (always active)"));
                log.AppendLine("Confidence threshold: " + MinConfidence);
                IsAvailable = true;
                UnavailableReason = "";
                DiagnosticsLog = log.ToString();
            }
            catch (Exception ex)
            {
                log.AppendLine("FAILED: " + ex.GetType().Name + " - " + ex.Message);
                IsAvailable = false;
                UnavailableReason = ex.Message;
                DiagnosticsLog = log.ToString();
            }
        }

        public void RegisterApp(string name) { /* apps matched by name in Parse */ }

        /// <summary>
        /// Load a SINGLE grammar containing ALL phrases (wake word + commands).
        /// No grammar hot-swapping — the _wakeMode flag decides what to act on.
        /// This is stable and reliable.
        /// </summary>
        private void LoadGrammar()
        {
            var cult = _engine.RecognizerInfo?.Culture
                       ?? System.Globalization.CultureInfo.InvariantCulture;

            var choicesList = new System.Collections.Generic.List<string>
            {
                "orbit", "hey orbit", "wake up orbit",
                "eject", "eject drive", "safely remove", "safely remove drive", "turn off drive",
                "connect", "connect drive", "mount", "mount drive", "turn on", "turn on drive", "spin up", "spin up drive",
                "disconnect", "disconnect drive", "power down", "power down drive",
                "what can you do", "show commands", "help",
                "go to sleep", "go standby",
                "drive status", "what is the status",
                "yes", "confirm", "affirmative", "go ahead", "do it",
                "no", "cancel", "stop", "never mind", "abort"
            };

            // Add "open [appname]" for each linked app (more reliable than wildcard)
            foreach (var app in LauncherService.Instance.Apps)
            {
                if (!string.IsNullOrEmpty(app.Name))
                    choicesList.Add("open " + app.Name.ToLowerInvariant());
            }

            var choices = new Choices(choicesList.ToArray());
            var gb = new GrammarBuilder(choices) { Culture = cult };
            var g = new Grammar(gb) { Name = "all" };

            _engine.UnloadAllGrammars();
            _engine.LoadGrammar(g);
        }

        /// <summary>Reload grammar (call when apps are added/removed).</summary>
        public void ReloadGrammar()
        {
            if (_engine == null) return;
            try
            {
                bool wasListening = AlwaysListening;
                if (wasListening) _engine.RecognizeAsyncStop();
                LoadGrammar();
                if (wasListening) _engine.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch { }
        }

        // ---- listening control ----

        public bool StartAlwaysListening()
        {
            if (!IsAvailable || _engine == null) return false;
            try
            {
                AlwaysListening = true;
                _wakeMode = LauncherService.Instance.WakeWordEnabled;
                _engine.RecognizeAsync(RecognizeMode.Multiple);
                SetListening(true);
                return true;
            }
            catch { return false; }
        }

        public void StopAlwaysListening()
        {
            AlwaysListening = false;
            if (!IsAvailable || _engine == null) return;
            try { _engine.RecognizeAsyncStop(); } catch { }
            if (!_ptt) SetListening(false);
        }

        private bool _ptt;
        public void BeginPushToTalk()
        {
            if (!IsAvailable || _engine == null) return;
            _ptt = true;
            // PTT temporarily enters command mode (bypasses wake word)
            _wakeMode = false;
            try
            {
                _engine.RecognizeAsyncStop();
                _engine.RecognizeAsync(RecognizeMode.Single);
                SetListening(true);
            }
            catch { }
        }

        public void EndPushToTalk()
        {
            _ptt = false;
            // Restore wake mode after PTT
            _wakeMode = LauncherService.Instance.WakeWordEnabled;
            if (!AlwaysListening) SetListening(false);
        }

        private void SetListening(bool v)
        {
            if (_listening == v) return;
            _listening = v;
            ListeningChanged?.Invoke(v);
        }

        /// <summary>Update wake mode from settings (called when preference changes).</summary>
        public void UpdateWakeMode()
        {
            _wakeMode = LauncherService.Instance.WakeWordEnabled;
            _commandWindowTimer?.Stop();
            LogService.Log("Voice: wake mode updated to " + (_wakeMode ? "ON" : "OFF"));
        }

        // ---- recognition event ----

        private void OnRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Strict confidence check — higher bar for single words (they false-trigger more)
            string text = e.Result.Text.ToLowerInvariant();
            int wordCount = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            float requiredConfidence = wordCount <= 1 ? 0.93f : MinConfidence;

            if (e.Result.Confidence < requiredConfidence)
            {
                LogService.Log("Voice: rejected (" + e.Result.Confidence.ToString("F2") + " < " + requiredConfidence + "): \"" + e.Result.Text + "\"");
                return;
            }

            // Check if this is a wake word
            bool isWakeWord = text == "orbit" || text == "hey orbit" || text == "wake up orbit";

            if (_wakeMode)
            {
                // In wake mode — only respond to the wake word
                if (isWakeWord)
                {
                    LogService.Log("Voice: WAKE WORD heard: \"" + e.Result.Text + "\"");
                    _wakeMode = false; // enter command mode
                    WakeWordHeard?.Invoke();

                    // Start 8-second command window
                    _commandWindowTimer?.Stop();
                    _commandWindowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                    _commandWindowTimer.Tick += (s, ev) =>
                    {
                        _wakeMode = LauncherService.Instance.WakeWordEnabled;
                        _commandWindowTimer.Stop();
                        CommandWindowExpired?.Invoke();
                        LogService.Log("Voice: command window expired, back to wake mode");
                    };
                    _commandWindowTimer.Start();
                    return;
                }
                else
                {
                    // In wake mode, ignore everything except the wake word
                    return;
                }
            }

            // In command mode — process the command
            var cmd = Parse(e.Result);
            if (cmd.Intent == VoiceIntent.Unknown) return;

            LogService.Log("Voice: command: \"" + e.Result.Text + "\" -> " + cmd.Intent);
            CommandRecognized?.Invoke(cmd);
            Speak(FriendlyEcho(cmd));

            // After processing a command, restart the command window
            // (gives time for follow-up commands like "yes"/"no")
            _commandWindowTimer?.Stop();
            _commandWindowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _commandWindowTimer.Tick += (s, ev) =>
            {
                _wakeMode = LauncherService.Instance.WakeWordEnabled;
                _commandWindowTimer.Stop();
                CommandWindowExpired?.Invoke();
                LogService.Log("Voice: command window expired, back to wake mode");
            };
            _commandWindowTimer.Start();
        }

        private VoiceCommand Parse(RecognitionResult r)
        {
            var c = new VoiceCommand { RawText = r.Text };
            var t = r.Text.ToLowerInvariant();
            if (t.StartsWith("open "))
            {
                c.Intent = VoiceIntent.OpenApp;
                c.Argument = r.Text.Substring(5).Trim();
                return c;
            }
            if (t == "orbit" || t == "hey orbit" || t == "wake up orbit") { c.Intent = VoiceIntent.Wake; return c; }
            if (t.Contains("eject") || t.Contains("safely remove") || t.Contains("turn off")) c.Intent = VoiceIntent.Eject;
            else if (t.Contains("connect") || t.Contains("mount") || t.Contains("turn on") || t.Contains("spin up")) c.Intent = VoiceIntent.Connect;
            else if (t.Contains("disconnect") || t.Contains("power down")) c.Intent = VoiceIntent.Disconnect;
            else if (t.Contains("what can") || t.Contains("show command") || t == "help") c.Intent = VoiceIntent.Help;
            else if (t.Contains("go to sleep") || t.Contains("standby")) c.Intent = VoiceIntent.Sleep;
            else if (t.Contains("status")) c.Intent = VoiceIntent.Status;
            else if (t == "yes" || t == "confirm" || t == "affirmative" || t == "go ahead" || t == "do it" || t == "ok") c.Intent = VoiceIntent.Confirm;
            else if (t == "no" || t == "cancel" || t == "stop" || t == "never mind" || t == "abort") c.Intent = VoiceIntent.Decline;
            return c;
        }

        private string FriendlyEcho(VoiceCommand c)
        {
            switch (c.Intent)
            {
                case VoiceIntent.Eject: return "Ejecting drive.";
                case VoiceIntent.Connect: return "Connecting drive.";
                case VoiceIntent.Disconnect: return "Disconnecting.";
                case VoiceIntent.OpenApp: return "Opening " + (c.Argument ?? "that") + ".";
                case VoiceIntent.Help: return "Say: eject drive, connect drive, or open an app.";
                case VoiceIntent.Sleep: return "Going to sleep.";
                case VoiceIntent.Wake: return "Yes?";
                case VoiceIntent.Status: return "Checking status.";
                case VoiceIntent.Confirm: return "Confirmed.";
                case VoiceIntent.Decline: return "Cancelled.";
                default: return "";
            }
        }

        public void Speak(string text)
        {
            if (!IsAvailable || _synth == null) return;
            try { _synth.SpeakAsync(text); } catch { }
        }

        public void Dispose()
        {
            try { _engine?.Dispose(); _synth?.Dispose(); } catch { }
        }
    }
}
