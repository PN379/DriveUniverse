using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DriveUniverse
{
    /// <summary>
    /// Pure Win32 interop: Restart Manager (who has the volume open),
    /// and the Configuration Manager (device tree: locate, walk parents,
    /// read properties/status, eject, disable, enable).
    ///
    /// These are the exact APIs Windows "Safely Remove Hardware" and Device
    /// Manager use, so the behaviour matches what you see in the GUI.
    /// </summary>
    internal static class Win32
    {
        // ---- ConfigRet return codes -------------------------------------------------
        public const uint CR_SUCCESS          = 0x00000000;
        public const uint CR_NEED_RESTART     = 0x0000000E;  // 14
        public const uint CR_NOT_DISABLEABLE  = 0x0000003A;  // 58
        public const uint CR_ACCESS_DENIED    = 0x00000033;  // 51  (not running as admin)

        // ---- Flags for CM_Disable_DevNode (from cfgmgr32.h) ------------------------
        public const uint CM_DISABLE_POLITE    = 0x00000000; // Ask the driver (Device Manager default; can prompt / request restart)
        public const uint CM_DISABLE_ABSOLUTE  = 0x00000001; // Force-disable, do NOT ask the driver (bypasses "need to restart")
        public const uint CM_DISABLE_HARDWARE  = 0x00000002; // Force and not restartable (avoid)
        public const uint CM_DISABLE_UI_NOT_OK = 0x00000004; // Suppress any veto / prompt UI
        public const uint CM_DISABLE_PERSIST   = 0x00000008; // Persist across reboot

        // Combined "force disable, no prompts" flag used by the power-cycle routine.
        public const uint DISABLE_FORCE = CM_DISABLE_ABSOLUTE | CM_DISABLE_UI_NOT_OK;

        // ---- Device registry properties (CM_DRP_*) ---------------------------------
        public const uint CM_DRP_DEVICEDESC    = 0x00000000;
        public const uint CM_DRP_CLASSGUID     = 0x00000008;
        public const uint CM_DRP_CLASS         = 0x00000009;
        public const uint CM_DRP_FRIENDLYNAME  = 0x0000000C;
        public const uint CM_DRP_CAPABILITIES  = 0x0000000F;
        public const uint CM_DRP_REMOVAL_POLICY = 0x00000020;

        // ---- Removal policy values -------------------------------------------------
        public const int CM_REMOVAL_POLICY_EXPECT_NO_REMOVAL       = 1;
        public const int CM_REMOVAL_POLICY_EXPECT_ORDERLY_REMOVAL  = 2;
        public const int CM_REMOVAL_POLICY_EXPECT_SURPRISE_REMOVAL = 3;

        // ---- DevNode status flags --------------------------------------------------
        public const uint DN_HAS_PROBLEM      = 0x00000400;
        public const uint DN_PRIVATE_PROBLEM   = 0x00000800;

        // ---- Restart Manager structs ----------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        public struct RM_UNIQUE_PROCESS
        {
            public uint dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string strServiceShortName;
            public uint ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
        }

        // ---- Restart Manager -------------------------------------------------------
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        public static extern int RmStartSession(out uint pSessionHandle, int sessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        public static extern int RmRegisterResources(uint dwSessionHandle,
            uint nFiles, string[] rgsFilenames,
            uint nApplications, [In] RM_UNIQUE_PROCESS[] rgApplications,
            uint nServices, [In] string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        public static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded, out uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        public static extern int RmEndSession(uint pSessionHandle);

        // ---- Configuration Manager -------------------------------------------------
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_IDW(uint dnDevInst, StringBuilder szBuffer, uint uBufferLen, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_ID_List_SizeW(out uint pulLen, string pszFilter, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_ID_ListW(string pszFilter, IntPtr buffer, uint bufferLen, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_DevNode_Registry_PropertyW(uint dnDevInst, uint ulProperty,
            out uint pulRegDataType, IntPtr buffer, ref uint pulLength, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Get_DevNode_Status(out uint pulStatus, out uint pulProblem, uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Request_Device_EjectW(uint dnDevInst, out uint pVetoType,
            StringBuilder pszVetoName, uint ulNameLength, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Disable_DevNode(uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Enable_DevNode(uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Query_And_Remove_SubTreeW(uint dnDevInst, out uint pVetoType,
            StringBuilder pszVetoName, uint ulNameLength, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Setup_DevNode(uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Reenumerate_DevNode(uint dnDevInst, uint ulFlags);

        // Flags for CM_Query_And_Remove_SubTree
        public const uint CM_REMOVE_UI_OK       = 0x00000000;
        public const uint CM_REMOVE_UI_NOT_OK    = 0x00000001;
        public const uint CM_REMOVE_NO_RESTART   = 0x00000002;

        // Flags for CM_Setup_DevNode
        public const uint CM_SETUP_DEVNODE_READY = 0x00000000;

        // Flags for CM_Reenumerate_DevNode
        public const uint CM_REENUMERATE_NORMAL = 0x00000000;

        // ---- Small managed helpers -------------------------------------------------

        public static string GetDevString(uint dn, uint prop)
        {
            uint dt;
            uint len = 0;
            CM_Get_DevNode_Registry_PropertyW(dn, prop, out dt, IntPtr.Zero, ref len, 0);
            if (len == 0) return null;
            IntPtr buf = Marshal.AllocHGlobal((int)len);
            try
            {
                if (CM_Get_DevNode_Registry_PropertyW(dn, prop, out dt, buf, ref len, 0) == CR_SUCCESS)
                    return Marshal.PtrToStringUni(buf);
                return null;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static int GetDevInt(uint dn, uint prop)
        {
            uint dt;
            uint len = 8;
            IntPtr buf = Marshal.AllocHGlobal(8);
            try
            {
                if (CM_Get_DevNode_Registry_PropertyW(dn, prop, out dt, buf, ref len, 0) == CR_SUCCESS)
                    return Marshal.ReadInt32(buf);
                return -1;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static string GetInstanceID(uint dn)
        {
            StringBuilder sb = new StringBuilder(1024);
            if (CM_Get_Device_IDW(dn, sb, 1024, 0) == CR_SUCCESS) return sb.ToString();
            return null;
        }
    }
}
