using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DriveUniverse
{
    /// <summary>
    /// SetupAPI interop — the EXACT API Device Manager uses when you right-click
    /// a device and choose "Disable" or "Enable".
    ///
    /// This is fundamentally different from CM_Disable_DevNode:
    ///   - CM_Disable_DevNode calls the driver's Remove dispatch (fails on Code 47)
    ///   - SetupDiCallClassInstaller(DIF_PROPERTYCHANGE) goes through the class
    ///     installer / co-installer path, which is what Device Manager does,
    ///     and properly handles the "need restart" -> disabled -> enabled flow.
    ///
    /// This is the missing piece that makes the drive wake work.
    /// </summary>
    internal static class SetupApi
    {
        // --- Constants ---
        public const uint DIGCF_PRESENT       = 0x00000002;
        public const uint DIGCF_ALLCLASSES    = 0x00000004;
        public const uint DIGCF_PROFILE       = 0x00000008;
        public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

        public const uint DIF_PROPERTYCHANGE  = 0x00000012;

        public const uint DICS_ENABLE         = 0x00000001;
        public const uint DICS_DISABLE        = 0x00000002;
        public const uint DICS_PROPCHANGE     = 0x00000003;
        public const uint DICS_FLAG_GLOBAL    = 0x00000001;
        public const uint DICS_FLAG_CONFIGSPECIFIC = 0x00000002;

        public const uint ERROR_NO_MORE_ITEMS = 259;
        public const uint ERROR_INVALID_DATA  = 13;

        // --- Structs ---
        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SP_CLASSINSTALL_HEADER
        {
            public uint cbSize;
            public uint InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SP_PROPCHANGE_PARAMS
        {
            public SP_CLASSINSTALL_HEADER ClassInstallHeader;
            public uint StateChange;
            public uint Scope;
            public uint HwProfile;
        }

        // --- Imports ---
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr SetupDiGetClassDevs(
            IntPtr ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDeviceInstanceIdW(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA did,
            [Out] StringBuilder DeviceInstanceId, uint DeviceInstanceIdSize,
            out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiSetClassInstallParamsW(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            ref SP_PROPCHANGE_PARAMS ClassInstallParams, uint ClassInstallParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiCallClassInstaller(
            uint InstallFunction, IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);

        // --- High-level helpers ---

        /// <summary>
        /// Disable or enable a device using the EXACT same method as Device Manager's
        /// right-click menu. This goes through the class installer path.
        ///
        /// stateChange = DICS_DISABLE or DICS_ENABLE
        /// </summary>
        public static bool ChangeDeviceState(string deviceInstanceId, uint stateChange)
        {
            IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero,
                DIGCF_ALLCLASSES);
            if (hDevInfo == new IntPtr(-1)) return false;

            try
            {
                var did = new SP_DEVINFO_DATA();
                did.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));

                // Find the device by instance ID
                for (uint i = 0; ; i++)
                {
                    if (!SetupDiEnumDeviceInfo(hDevInfo, i, ref did))
                    {
                        if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_ITEMS) break;
                        continue;
                    }

                    var sb = new StringBuilder(1024);
                    uint req;
                    if (SetupDiGetDeviceInstanceIdW(hDevInfo, ref did, sb, 1024, out req))
                    {
                        if (string.Equals(sb.ToString(), deviceInstanceId,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            // Found it — change state
                            return DoPropertyChange(hDevInfo, ref did, stateChange);
                        }
                    }
                }
                return false; // not found
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }
        }

        private static bool DoPropertyChange(IntPtr hDevInfo, ref SP_DEVINFO_DATA did, uint stateChange)
        {
            var pars = new SP_PROPCHANGE_PARAMS();
            pars.ClassInstallHeader.cbSize = (uint)Marshal.SizeOf(typeof(SP_CLASSINSTALL_HEADER));
            pars.ClassInstallHeader.InstallFunction = DIF_PROPERTYCHANGE;
            pars.StateChange = stateChange;
            pars.Scope = DICS_FLAG_GLOBAL;
            pars.HwProfile = 0;

            bool ok = SetupDiSetClassInstallParamsW(hDevInfo, ref did,
                ref pars, (uint)Marshal.SizeOf(typeof(SP_PROPCHANGE_PARAMS)));
            if (!ok) return false;

            return SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, hDevInfo, ref did);
        }
    }
}
