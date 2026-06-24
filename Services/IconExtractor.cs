using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Extracts the highest-quality icon from an .exe/.msc.
    /// Tries jumbo (256px) -> extra-large (48px) -> large (32px) -> associated.
    /// </summary>
    public static class IconExtractor
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SYSICONINDEX = 0x000004000;

        // Image list sizes
        private const int SHIL_LARGE = 0;     // 32x32
        private const int SHIL_SMALL = 1;     // 16x16
        private const int SHIL_EXTRALARGE = 2; // 48x48
        private const int SHIL_JUMBO = 4;      // 256x256 (Vista+)

        [DllImport("shell32.dll", EntryPoint = "SHGetImageList")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, ref IntPtr ppv);

        private static readonly Guid IImageListGuid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA54B6");

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA54B6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        }

        private const int ILD_TRANSPARENT = 1;

        public static ImageSource Extract(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            string actualPath = filePath;
            if (!File.Exists(actualPath))
            {
                var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), actualPath);
                if (File.Exists(sys)) actualPath = sys;
                else return null;
            }

            // Get the system icon index first
            int iconIndex = -1;
            try
            {
                var shfi = new SHFILEINFO();
                SHGetFileInfo(actualPath, 0, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                    SHGFI_SYSICONINDEX);
                iconIndex = shfi.iIcon;
            }
            catch { }

            // Try jumbo (256x256) first — best quality
            if (iconIndex >= 0)
            {
                var jumbo = GetIconFromImageList(SHIL_JUMBO, iconIndex);
                if (jumbo != null) return jumbo;
            }

            // Try extra-large (48x48)
            if (iconIndex >= 0)
            {
                var extra = GetIconFromImageList(SHIL_EXTRALARGE, iconIndex);
                if (extra != null) return extra;
            }

            // Fallback: SHGetFileInfo large icon (32x32)
            try
            {
                var shfi = new SHFILEINFO();
                SHGetFileInfo(actualPath, 0, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                    SHGFI_ICON | SHGFI_LARGEICON);
                if (shfi.hIcon != IntPtr.Zero)
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    DestroyIcon(shfi.hIcon);
                    return src;
                }
            }
            catch { }

            // Last resort: ExtractAssociatedIcon
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(actualPath))
                {
                    if (icon != null)
                    {
                        var bmp = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch { }

            return null;
        }

        private static ImageSource GetIconFromImageList(int listType, int iconIndex)
        {
            try
            {
                IntPtr pImageList = IntPtr.Zero;
                Guid iid = IImageListGuid;
                int hr = SHGetImageList(listType, ref iid, ref pImageList);
                if (hr != 0 || pImageList == IntPtr.Zero) return null;

                var imageList = (IImageList)Marshal.GetObjectForIUnknown(pImageList);
                IntPtr hIcon = IntPtr.Zero;
                hr = imageList.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
                Marshal.Release(pImageList);

                if (hr != 0 || hIcon == IntPtr.Zero) return null;

                var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                DestroyIcon(hIcon);
                return src;
            }
            catch { return null; }
        }
    }
}
