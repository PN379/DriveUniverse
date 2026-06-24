using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Manages tipping/donation links. Reads from a config file so the URL can
    /// be changed WITHOUT recompiling — just edit the file.
    ///
    /// The file is at: %AppData%\DriveUniverse\tip.txt
    /// If it exists, the "Support" button shows. If not, no button.
    ///
    /// Default URL is set to the GitHub profile for now. Later you can point it
    /// to Ko-fi, PayPal, Buy Me a Coffee, GitHub Sponsors, etc.
    /// </summary>
    public static class TippingService
    {
        private static readonly string TipDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DriveUniverse");
        private static readonly string TipFile = Path.Combine(TipDir, "tip.txt");

        // Default URL (used if no config file exists). Change this to your
        // preferred tipping platform later:
        //   Ko-fi:          https://ko-fi.com/pn379
        //   Buy Me Coffee:  https://buymeacoffee.com/pn379
        //   PayPal:         https://paypal.me/pn379
        //   GitHub Sponsors: https://github.com/sponsors/PN379
        private const string DefaultUrl = "https://github.com/PN379";

        /// <summary>The tipping URL. Reads from config file if it exists, else default.</summary>
        public static string GetTipUrl()
        {
            try
            {
                if (File.Exists(TipFile))
                {
                    string url = File.ReadAllText(TipFile).Trim();
                    if (!string.IsNullOrEmpty(url) && url.StartsWith("http"))
                        return url;
                }
            }
            catch { }
            return DefaultUrl;
        }

        /// <summary>Open the tipping page in the default browser.</summary>
        public static void OpenTipPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GetTipUrl(),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        /// <summary>Save a new tipping URL to the config file.</summary>
        public static void SetTipUrl(string url)
        {
            try
            {
                Directory.CreateDirectory(TipDir);
                File.WriteAllText(TipFile, url ?? "");
            }
            catch { }
        }
    }
}
