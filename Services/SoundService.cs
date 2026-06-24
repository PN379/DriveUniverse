using System;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Plays embedded UI sounds. All clips ship inside the exe as resources, so
    /// there are no external files to manage. Sounds are user-toggleable.
    /// </summary>
    public sealed class SoundService
    {
        public enum Clip { Click, Confirm, Connect, Disconnect, Error, Listen }

        private static SoundService _instance;
        public static SoundService Instance => _instance ?? (_instance = new SoundService());

        public bool Enabled { get; set; } = true;

        private readonly System.Collections.Generic.Dictionary<Clip, SoundPlayer> _players =
            new System.Collections.Generic.Dictionary<Clip, SoundPlayer>();

        private SoundService()
        {
            foreach (Clip c in Enum.GetValues(typeof(Clip)))
            {
                var uri = new Uri("pack://application:,,,/Assets/" + c.ToString().ToLower() + ".wav");
                var ri = Application.GetResourceStream(uri);
                if (ri == null) continue;
                // SoundPlayer needs a seekable stream; copy to a memory stream.
                var ms = new MemoryStream();
                ri.Stream.CopyTo(ms);
                ms.Position = 0;
                var p = new SoundPlayer(ms);
                try { p.Load(); } catch { }
                _players[c] = p;
            }
        }

        public void Play(Clip c)
        {
            if (!Enabled) return;
            if (_players.TryGetValue(c, out var p))
            {
                try { p.Play(); } catch { }   // fire-and-forget, never crash the UI
            }
        }
    }
}
