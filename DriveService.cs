using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DriveUniverse
{
    // ============================================================================
    //  Data transfer objects
    // ============================================================================

    public class DriveEntry
    {
        public char Letter;            // '\0' when this is an ejected / no-letter device
        public string VolumeLabel;
        public string DiskPnpId;       // for off entries this holds the target device instance
        public string DiskFriendly;
        public int DriveType;            // Win32_LogicalDisk DriveType (2=removable,3=fixed,...)
        public bool IsOffOnly;           // true = ejected (code 47), no drive letter
        public bool IsSystem;            // true = contains Windows (NEVER eject this)
        public string LastPort;          // last known USB port (for tracking after eject)

        public bool IsUsb =>
            !string.IsNullOrEmpty(DiskPnpId) &&
            (DiskPnpId.StartsWith("USBSTOR", StringComparison.OrdinalIgnoreCase) ||
             DiskPnpId.StartsWith("USB", StringComparison.OrdinalIgnoreCase));

        public override string ToString()
        {
            if (IsOffOnly)
            {
                string offModel = string.IsNullOrEmpty(DiskFriendly) ? DiskPnpId : DiskFriendly;
                string port = string.IsNullOrEmpty(LastPort) ? "" : "  [" + LastPort + "]";
                return "\u26A0  " + offModel + port + "   (OFF)";
            }
            string lbl = string.IsNullOrEmpty(VolumeLabel) ? "" : " [" + VolumeLabel + "]";
            string model = string.IsNullOrEmpty(DiskFriendly) ? "" : "  " + DiskFriendly;
            string sys = IsSystem ? "   <SYSTEM>" : (IsUsb ? "   <USB>" : "");
            return Letter + ":" + lbl + model + sys;
        }
    }

    /// <summary>
    /// The device nodes resolved for a chosen drive: which node to eject
    /// (the topmost "removable" ancestor) and which "USB Mass Storage" node to
    /// toggle off/on (the one that goes yellow-triangle when ejected).
    /// </summary>
    public class ResolvedDevice
    {
        public char Letter;
        public string DiskInstance;
        public uint DiskDevNode;

        public string EjectInstance;
        public uint EjectDevNode;
        public string EjectDescription;

        public string UsbMsInstance;     // the device we disable/enable
        public uint UsbMsDevNode;
        public string UsbMsDescription;

        public string Error;
        public bool IsValid => string.IsNullOrEmpty(Error);
    }

    public class LockerInfo
    {
        public uint Pid;
        public string AppName;
        public string Service;
        public uint AppType;
        public string TypeName;
        public bool Restartable;
        public DateTime StartTime;
        public string ExecutablePath;

        public override string ToString()
        {
            string svc = string.IsNullOrEmpty(Service) ? "" : $" ({Service})";
            return $"PID {Pid}: {AppName}{svc}  [{TypeName}]";
        }
    }

    public class EjectResult
    {
        public bool Success;
        public bool Vetoed;
        public uint VetoType;
        public string VetoName;
        public string Message;
    }

    public class StatusInfo
    {
        public bool Present;
        public bool HasProblem;     // "yellow triangle"
        public uint ProblemCode;
        public string ProblemText;
        public string Class;
        public string Description;
        public string InstanceId;
        public bool IsOff => HasProblem && (ProblemCode == 22 || ProblemCode == 45 || ProblemCode == 47);
    }

    public class UsbDevView
    {
        public string InstanceId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public int ErrorCode { get; set; }
        public string Problem { get; set; }
    }

    // ============================================================================
    //  Drive service
    // ============================================================================

    public static class DriveService
    {
        // Session memory of every USB storage device node we've seen, so ejected
        // (no-drive-letter) devices keep showing in the device grid with a live status.
        private static readonly HashSet<string> _known =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Maps instance ID -> last known USB port label, for tracking after ejection.
        private static readonly Dictionary<string, string> _portMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>True if this drive letter contains the Windows directory.</summary>
        public static bool IsSystemDrive(char letter)
        {
            try
            {
                string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (sysDir.Length >= 2 && sysDir[1] == ':')
                    return char.ToUpperInvariant(sysDir[0]) == char.ToUpperInvariant(letter);
            }
            catch { }
            return false;
        }

        /// <summary>Extract a friendly port label from a USB instance ID for tracking.</summary>
        public static string ExtractPortLabel(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return "";
            try
            {
                // Instance IDs look like: USB\VID_1058&PID_259F\1234567890ABCD
                var parts = instanceId.Split('\\');
                if (parts.Length >= 2)
                    return parts[0] + " port";
            }
            catch { }
            return "";
        }

        private static void Remember(string instance)
        {
            if (!string.IsNullOrEmpty(instance)) _known.Add(instance);
        }

        // ---------------------------------------------------------------- drive list

        public static List<DriveEntry> GetDrives()
        {
            var result = new List<DriveEntry>();
            var seen = new HashSet<char>();
            try
            {
                using (var disks = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject disk in disks.Get())
                    {
                        string pnpId = disk["PNPDeviceID"] as string;
                        string model = disk["Model"] as string;

                        foreach (ManagementObject part in disk.GetRelated("Win32_DiskPartition"))
                            foreach (ManagementObject logical in part.GetRelated("Win32_LogicalDisk"))
                            {
                                object devId = logical["DeviceID"];          // e.g. "D:"
                                if (devId == null) continue;
                                string did = devId.ToString().Trim();
                                if (did.Length < 2 || did[1] != ':') continue;
                                char letter = char.ToUpperInvariant(did[0]);
                                if (!seen.Add(letter)) continue;

                                result.Add(new DriveEntry
                                {
                                    Letter = letter,
                                    VolumeLabel = logical["VolumeName"] as string ?? "",
                                    DiskPnpId = pnpId,
                                    DiskFriendly = model,
                                    DriveType = Convert.ToInt32(logical["DriveType"] ?? 0),
                                    IsSystem = IsSystemDrive(letter)
                                });
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                // WMI may be unavailable; the caller can still type a letter.
                System.Diagnostics.Debug.WriteLine("WMI enumeration failed: " + ex.Message);
            }
            return result.OrderBy(x => x.Letter).ToList();
        }

        public static string DiskPnpIdForLetter(char letter)
        {
            char L = char.ToUpperInvariant(letter);
            using (var disks = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
            {
                foreach (ManagementObject disk in disks.Get())
                {
                    string pnpId = disk["PNPDeviceID"] as string;
                    foreach (ManagementObject part in disk.GetRelated("Win32_DiskPartition"))
                        foreach (ManagementObject logical in part.GetRelated("Win32_LogicalDisk"))
                        {
                            object devId = logical["DeviceID"];
                            if (devId == null) continue;
                            string did = devId.ToString().Trim();
                            if (did.Length >= 2 && did[1] == ':' && char.ToUpperInvariant(did[0]) == L)
                                return pnpId;
                        }
                }
            }
            return null;
        }

        // ------------------------------------------------------------- device tree

        public static ResolvedDevice Resolve(char letter)
        {
            var r = new ResolvedDevice { Letter = char.ToUpperInvariant(letter) };
            try
            {
                string pnp = DiskPnpIdForLetter(r.Letter);
                if (string.IsNullOrEmpty(pnp))
                {
                    r.Error = "No physical disk found for drive " + r.Letter + ":. " +
                              "(Is the drive currently ejected / not present? Pick a present drive, " +
                              "or run 'Power-cycle' on a device selected in the list below.)";
                    return r;
                }
                r.DiskInstance = pnp;

                uint cr = Win32.CM_Locate_DevNodeW(out uint dn, pnp, 0);
                if (cr != Win32.CR_SUCCESS)
                {
                    r.Error = $"Could not locate device node (0x{cr:X8}) for {pnp}";
                    return r;
                }
                r.DiskDevNode = dn;

                // Walk up the device tree (volume -> disk -> USBSTOR -> USB enclosure -> hub ...).
                var chain = new List<(uint dn, string inst, string cls, string desc, string fr, int rp)>();
                uint cur = dn;
                for (int i = 0; i < 16; i++)
                {
                    string inst = Win32.GetInstanceID(cur);
                    string cls = Win32.GetDevString(cur, Win32.CM_DRP_CLASS) ?? "";
                    string desc = Win32.GetDevString(cur, Win32.CM_DRP_DEVICEDESC);
                    string fr = Win32.GetDevString(cur, Win32.CM_DRP_FRIENDLYNAME);
                    int rp = Win32.GetDevInt(cur, Win32.CM_DRP_REMOVAL_POLICY);
                    chain.Add((cur, inst, cls, desc ?? fr, fr, rp));

                    uint parent;
                    uint crc = Win32.CM_Get_Parent(out parent, cur, 0);
                    if (crc != Win32.CR_SUCCESS || parent == 0) break;
                    cur = parent;
                }

                // Eject target = topmost "removable" ancestor (what Safely Remove ejects).
                var removable = chain.Where(c => c.rp == Win32.CM_REMOVAL_POLICY_EXPECT_ORDERLY_REMOVAL
                                              || c.rp == Win32.CM_REMOVAL_POLICY_EXPECT_SURPRISE_REMOVAL).ToList();
                if (removable.Count > 0)
                {
                    var top = removable[removable.Count - 1];
                    r.EjectDevNode = top.dn; r.EjectInstance = top.inst;
                    r.EjectDescription = top.desc ?? top.fr ?? top.inst;
                }
                else
                {
                    var c0 = chain[0];
                    r.EjectDevNode = c0.dn; r.EjectInstance = c0.inst;
                    r.EjectDescription = c0.desc ?? c0.fr ?? c0.inst;
                }

                // "USB Mass Storage" node to toggle = the USB-class ancestor named
                // "*Mass Storage*", else the USBSTOR disk, else the eject target.
                var usb = chain.FirstOrDefault(c =>
                    c.cls.Equals("USB", StringComparison.OrdinalIgnoreCase) &&
                    (((c.desc ?? "") + " " + (c.fr ?? "")).Contains("Mass Storage")));
                if (!string.IsNullOrEmpty(usb.inst))
                {
                    r.UsbMsDevNode = usb.dn; r.UsbMsInstance = usb.inst;
                    r.UsbMsDescription = usb.desc ?? usb.fr ?? usb.inst;
                }
                else
                {
                    var stor = chain.FirstOrDefault(c =>
                        c.cls.Equals("USBSTOR", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(stor.inst))
                    {
                        r.UsbMsDevNode = stor.dn; r.UsbMsInstance = stor.inst;
                        r.UsbMsDescription = stor.desc ?? stor.fr ?? stor.inst;
                    }
                    else
                    {
                        r.UsbMsDevNode = r.EjectDevNode; r.UsbMsInstance = r.EjectInstance;
                        r.UsbMsDescription = r.EjectDescription;
                    }
                }
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
            }
            return r;
        }

        // ------------------------------------------------------- who is locking it

        public static List<LockerInfo> FindLockers(char letter)
        {
            var list = new List<LockerInfo>();
            uint session;
            StringBuilder key = new StringBuilder(256);
            if (Win32.RmStartSession(out session, 0, key) != 0)
                throw new Exception("Restart Manager: RmStartSession failed");
            try
            {
                // Restart Manager wants the volume path "X:\".
                string[] files = new string[] { char.ToUpperInvariant(letter) + @":\" };
                if (Win32.RmRegisterResources(session, (uint)files.Length, files, 0, null, 0, null) != 0)
                    throw new Exception("Restart Manager: RmRegisterResources failed");

                uint needed, count; uint reasons = 0;
                Win32.RmGetList(session, out needed, out count, null, ref reasons);
                if (needed == 0) return list;   // nothing is holding the volume open

                var arr = new Win32.RM_PROCESS_INFO[(int)needed];
                if (Win32.RmGetList(session, out needed, out count, arr, ref reasons) != 0) return list;

                for (int i = 0; i < (int)count; i++)
                {
                    var p = arr[i];
                    ulong low = (uint)p.Process.ProcessStartTime.dwLowDateTime;
                    ulong high = (uint)p.Process.ProcessStartTime.dwHighDateTime;
                    long ft = (long)((high << 32) | low);

                    var li = new LockerInfo
                    {
                        Pid = p.Process.dwProcessId,
                        AppName = p.strAppName,
                        Service = p.strServiceShortName,
                        AppType = p.ApplicationType,
                        TypeName = AppTypeName(p.ApplicationType),
                        Restartable = p.bRestartable,
                        StartTime = DateTime.FromFileTime(ft)
                    };
                    try
                    {
                        using (var proc = Process.GetProcessById((int)p.Process.dwProcessId))
                            li.ExecutablePath = proc.MainModule?.FileName;
                    }
                    catch { /* elevated/unowned processes can't be inspected */ }
                    list.Add(li);
                }
            }
            finally { Win32.RmEndSession(session); }
            return list;
        }

        // ------------------------------------------------------------------- eject

        public static EjectResult Eject(uint devNode)
        {
            var res = new EjectResult();
            StringBuilder veto = new StringBuilder(1024);
            uint vt;
            // CM_Request_Device_Eject returns CR_SUCCESS even when vetoed;
            // a non-zero pVetoType means the eject was refused.
            uint cr = Win32.CM_Request_Device_EjectW(devNode, out vt, veto, 1024, 0);
            res.VetoType = vt;
            res.VetoName = veto.ToString();

            if (vt == 0 && cr == Win32.CR_SUCCESS)
            {
                res.Success = true;
                res.Vetoed = false;
                res.Message = "Drive ejected (Safely Removed).";
            }
            else
            {
                res.Success = false;
                res.Vetoed = true;
                res.Message = "Could not eject: " + VetoDescription(vt) +
                              (string.IsNullOrEmpty(res.VetoName) ? "" : " — " + res.VetoName);
            }
            return res;
        }

        // ---------------------------------------------------------- disable/enable

        public static uint Disable(uint devNode) => Win32.CM_Disable_DevNode(devNode, Win32.DISABLE_FORCE);
        public static uint Enable(uint devNode) => Win32.CM_Enable_DevNode(devNode, 0);

        /// <summary>
        /// Power-cycle the USB Mass Storage node `count` times to make the port
        /// re-send its signal and spin the drive back up.
        ///
        /// This replicates the proven manual Device-Manager sequence:
        ///     start: code 47 (yellow triangle)
        ///     Disable -> "will stop functioning?" Yes   (still yellow)
        ///     Disable -> "need to restart PC?" No        (icon changes -> truly disabled)
        ///     Enable  -> spins up
        ///
        /// In code we use CM_DISABLE_ABSOLUTE | CM_DISABLE_UI_NOT_OK (= FORCE) which
        /// skips BOTH prompts and forces the device fully disabled in one go (the state
        /// you reach after declining the restart), then disable twice for robustness
        /// against drivers that need the second pass, then Enable.
        /// </summary>
        public static void PowerCycle(ResolvedDevice dev, string targetInstance,
            int cycles, int delayMs, Action<string> log, out bool cameBack)
        {
            cameBack = false;

            string instance = !string.IsNullOrEmpty(targetInstance) ? targetInstance : dev?.UsbMsInstance;
            if (string.IsNullOrEmpty(instance)) { log("No device selected."); return; }

            var stBefore = GetStatus(instance);
            string desc = stBefore.Description ?? instance;
            log("Target: '" + desc + "'  state: " + stBefore.ProblemText);

            // THE FIX: Use SetupAPI (SetupDiCallClassInstaller with DIF_PROPERTYCHANGE)
            // which is the EXACT API Device Manager uses when you right-click -> Disable.
            // This is fundamentally different from CM_Disable_DevNode and properly
            // handles the Code 47 "prepared for safe removal" state.
            for (int i = 1; i <= cycles && !cameBack; i++)
            {
                log("--- Cycle " + i + "/" + cycles + " (SetupAPI / Device Manager method) ---");

                log("  SetupAPI: Disable (DICS_DISABLE, GLOBAL)...");
                bool ok = SetupApi.ChangeDeviceState(instance, SetupApi.DICS_DISABLE);
                log("    -> " + (ok ? "OK" : "FAILED (Win32 err " + Marshal.GetLastWin32Error() + ")"));
                Thread.Sleep(delayMs);

                log("  SetupAPI: Disable #2...");
                ok = SetupApi.ChangeDeviceState(instance, SetupApi.DICS_DISABLE);
                log("    -> " + (ok ? "OK" : "FAILED (Win32 err " + Marshal.GetLastWin32Error() + ")"));
                Thread.Sleep(delayMs);

                var stMid = GetStatus(instance);
                log("  state now: " + stMid.ProblemText);

                log("  SetupAPI: Enable (DICS_ENABLE, GLOBAL)...");
                ok = SetupApi.ChangeDeviceState(instance, SetupApi.DICS_ENABLE);
                log("    -> " + (ok ? "OK" : "FAILED (Win32 err " + Marshal.GetLastWin32Error() + ")"));
                Thread.Sleep(delayMs);

                if (CheckBack(instance)) { cameBack = true; break; }

                uint cr = Win32.CM_Locate_DevNodeW(out uint dn, instance, 0);
                if (cr == Win32.CR_SUCCESS)
                {
                    cr = Win32.CM_Setup_DevNode(dn, Win32.CM_SETUP_DEVNODE_READY);
                    log("  CM_Setup_DevNode -> " + CrText(cr));
                    Thread.Sleep(delayMs);
                }

                cr = Win32.CM_Locate_DevNodeW(out dn, instance, 0);
                if (cr == Win32.CR_SUCCESS)
                {
                    uint parent;
                    if (Win32.CM_Get_Parent(out parent, dn, 0) == Win32.CR_SUCCESS && parent != 0)
                    {
                        cr = Win32.CM_Reenumerate_DevNode(parent, Win32.CM_REENUMERATE_NORMAL);
                        log("  reenumerate parent -> " + CrText(cr));
                    }
                }
                Thread.Sleep(delayMs);

                if (CheckBack(instance)) { cameBack = true; break; }
            }

            log("Waiting for spin-up...");
            for (int w = 0; w < 24; w++)
            {
                Thread.Sleep(750);
                if (CheckBack(instance)) { cameBack = true; break; }
            }
            var finalSt = GetStatus(instance);
            log(cameBack ? "ONLINE: " + finalSt.ProblemText : "Still: " + finalSt.ProblemText);
        }

        private static string GetParentInstance(string childInstance)
        {
            try
            {
                if (Win32.CM_Locate_DevNodeW(out uint dn, childInstance, 0) != Win32.CR_SUCCESS) return null;
                uint parent;
                if (Win32.CM_Get_Parent(out parent, dn, 0) != Win32.CR_SUCCESS || parent == 0) return null;
                return Win32.GetInstanceID(parent);
            }
            catch { return null; }
        }

        private static bool CycleNode(string instance, int delayMs, Action<string> log, string label)
        {
            log("  [" + label + "] disable -> setup -> enable...");

            uint cr = Win32.CM_Locate_DevNodeW(out uint dn, instance, 0);
            if (cr == Win32.CR_SUCCESS) { cr = Win32.CM_Disable_DevNode(dn, Win32.DISABLE_FORCE); log("    disable -> " + CrText(cr)); }
            Thread.Sleep(delayMs);

            cr = Win32.CM_Locate_DevNodeW(out dn, instance, 0);
            if (cr == Win32.CR_SUCCESS) { cr = Win32.CM_Setup_DevNode(dn, Win32.CM_SETUP_DEVNODE_READY); log("    setup -> " + CrText(cr)); }
            Thread.Sleep(delayMs);

            cr = Win32.CM_Locate_DevNodeW(out dn, instance, 0);
            if (cr == Win32.CR_SUCCESS) { cr = Win32.CM_Enable_DevNode(dn, 0); log("    enable -> " + CrText(cr)); }
            Thread.Sleep(delayMs);

            cr = Win32.CM_Locate_DevNodeW(out dn, instance, 0);
            if (cr == Win32.CR_SUCCESS)
            {
                uint parent;
                if (Win32.CM_Get_Parent(out parent, dn, 0) == Win32.CR_SUCCESS && parent != 0)
                { cr = Win32.CM_Reenumerate_DevNode(parent, Win32.CM_REENUMERATE_NORMAL); log("    reenum parent -> " + CrText(cr)); }
            }
            Thread.Sleep(delayMs);

            return CheckBack(instance);
        }

        private static bool CheckBack(string instance)
        {
            var st = GetStatus(instance);
            return st.Present && !st.HasProblem;
        }

        /// <summary>Human-readable name for a Configuration Manager return code.</summary>
        public static string CrText(uint cr)
        {
            if (cr == Win32.CR_SUCCESS) return "CR_SUCCESS (0x0) — OK";
            if (cr == Win32.CR_ACCESS_DENIED) return "CR_ACCESS_DENIED (0x33) — need Administrator";
            if (cr == Win32.CR_NEED_RESTART) return "CR_NEED_RESTART (0xE) — a restart is required";
            if (cr == Win32.CR_NOT_DISABLEABLE) return "CR_NOT_DISABLEABLE (0x3A) — device cannot be disabled";
            return string.Format("0x{0:X8}", cr);
        }

        // --------------------------------------------------------------- status

        public static StatusInfo GetStatus(string instanceId)
        {
            var s = new StatusInfo { InstanceId = instanceId };
            if (string.IsNullOrEmpty(instanceId)) return s;

            uint cr = Win32.CM_Locate_DevNodeW(out uint dn, instanceId, 0);
            if (cr != Win32.CR_SUCCESS)
            {
                s.Present = false;
                s.HasProblem = true;
                s.ProblemCode = 45;
                s.ProblemText = "Not present / removed  (code 45 — yellow triangle)";
                return s;
            }

            s.Present = true;
            s.Class = Win32.GetDevString(dn, Win32.CM_DRP_CLASS) ?? "";
            s.Description = Win32.GetDevString(dn, Win32.CM_DRP_DEVICEDESC)
                         ?? Win32.GetDevString(dn, Win32.CM_DRP_FRIENDLYNAME);

            uint status, problem;
            Win32.CM_Get_DevNode_Status(out status, out problem, dn, 0);
            s.ProblemCode = problem;

            if ((status & (Win32.DN_HAS_PROBLEM | Win32.DN_PRIVATE_PROBLEM)) != 0 || problem != 0)
            {
                s.HasProblem = true;
                s.ProblemText = ProblemDescription(problem);
            }
            else
            {
                s.HasProblem = false;
                s.ProblemText = "OK";
            }
            return s;
        }

        /// <summary>
        /// All USB / USB-disk devices with their Device-Manager status, INCLUDING ones
        /// that are currently ejected (yellow triangle, code 45/47).
        ///
        /// Ejected drives have no drive letter, so they vanish from the drive dropdown
        /// and from most "present device" queries. To keep them visible we:
        ///   1) enumerate currently-present USB storage devices (Config Manager + WMI), and
        ///   2) merge in every device we have ever seen this session (Remember()).
        /// Status for each is read live via CM_Get_DevNode_Status, which works on the
        /// lingering "ghost" node that carries code 45.
        /// </summary>
        public static List<UsbDevView> GetUsbMassStorageDevices()
        {
            var dict = new Dictionary<string, UsbDevView>(StringComparer.OrdinalIgnoreCase);

            foreach (var inst in EnumerateUsbStorageInstances())
            {
                Remember(inst);
                if (!dict.ContainsKey(inst)) dict[inst] = BuildView(inst);
            }

            // merge remembered devices (covers ejected / no-drive-letter drives)
            foreach (var inst in _known)
                if (!dict.ContainsKey(inst)) dict[inst] = BuildView(inst);

            return dict.Values
                .OrderByDescending(v => v.ErrorCode != 0)   // problem / "off" first
                .ThenBy(v => v.Name)
                .ToList();
        }

        private static UsbDevView BuildView(string instance)
        {
            var v = new UsbDevView { InstanceId = instance };
            var st = GetStatus(instance);

            uint cr = Win32.CM_Locate_DevNodeW(out uint dn, instance, 0);
            if (cr == Win32.CR_SUCCESS)
            {
                v.Name = Win32.GetDevString(dn, Win32.CM_DRP_DEVICEDESC)
                      ?? Win32.GetDevString(dn, Win32.CM_DRP_FRIENDLYNAME)
                      ?? instance;
            }
            else
            {
                v.Name = instance;   // ghost / removed node — keep it identifiable
            }

            v.ErrorCode = (int)st.ProblemCode;
            v.Status = st.HasProblem ? ("\u26A0  " + st.ProblemText) : "OK";
            v.Problem = st.HasProblem ? st.ProblemText : "";
            return v;
        }

        /// <summary>Discover currently-present USB storage device instance IDs.</summary>
        private static List<string> EnumerateUsbStorageInstances()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // (a) USBSTOR disk nodes via the Config Manager (+ their USB parent).
            foreach (var id in CmIdList("USBSTOR"))
            {
                if (string.IsNullOrEmpty(id)) continue;
                set.Add(id);
                AddParent(set, id);
            }

            // (b) Present disks via WMI (+ their USB parent).
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_DiskDrive"))
                    foreach (ManagementObject d in s.Get())
                    {
                        string id = d["PNPDeviceID"] as string;
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!id.StartsWith("USB", StringComparison.OrdinalIgnoreCase)) continue;
                        set.Add(id);
                        AddParent(set, id);
                    }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("DiskDrive query failed: " + ex.Message); }

            // (c) Any PnP entity named "*Mass Storage*" (the USB enclosure nodes).
            try
            {
                using (var s = new ManagementObjectSearcher(
                    "SELECT PNPDeviceID FROM Win32_PnPEntity WHERE " +
                    "Caption LIKE '%Mass Storage%' OR Name LIKE '%Mass Storage%'"))
                    foreach (ManagementObject mo in s.Get())
                    {
                        string id = mo["PNPDeviceID"] as string;
                        if (!string.IsNullOrEmpty(id)) set.Add(id);
                    }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("PnP entity query failed: " + ex.Message); }

            return set.ToList();
        }

        private static void AddParent(HashSet<string> set, string childInstance)
        {
            if (Win32.CM_Locate_DevNodeW(out uint dn, childInstance, 0) != Win32.CR_SUCCESS) return;
            uint parent;
            if (Win32.CM_Get_Parent(out parent, dn, 0) == Win32.CR_SUCCESS && parent != 0)
            {
                string p = Win32.GetInstanceID(parent);
                if (!string.IsNullOrEmpty(p)) set.Add(p);
            }
        }

        /// <summary>Read a MULTI_SZ list of device instance IDs for an enumerator/prefix.</summary>
        private static List<string> CmIdList(string filter)
        {
            var result = new List<string>();
            try
            {
                uint size;
                if (Win32.CM_Get_Device_ID_List_SizeW(out size, filter, 0) != Win32.CR_SUCCESS) return result;
                if (size == 0) return result;
                IntPtr buf = Marshal.AllocHGlobal((int)(size * 2)); // WCHARs
                try
                {
                    if (Win32.CM_Get_Device_ID_ListW(filter, buf, size, 0) != Win32.CR_SUCCESS) return result;
                    int offset = 0;
                    int limit = (int)(size * 2);
                    while (offset < limit)
                    {
                        string s = Marshal.PtrToStringUni((IntPtr)((long)buf + offset));
                        if (string.IsNullOrEmpty(s)) break;
                        result.Add(s);
                        offset += (s.Length + 1) * 2;
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { }
            return result;
        }

        // ------------------------------------------------------------- text maps

        private static string AppTypeName(uint t)
        {
            switch (t)
            {
                case 1: return "Main window";
                case 2: return "Other window";
                case 3: return "Service";
                case 4: return "Explorer";
                case 5: return "Console";
                case 6: return "Critical";
                default: return "Unknown";
            }
        }

        public static string ProblemDescription(uint code)
        {
            switch (code)
            {
                case 0: return "OK";
                case 10: return "This device cannot start. (Code 10)";
                case 12: return "Not enough free resources. (Code 12)";
                case 19: return "Configuration info is incomplete/damaged. (Code 19)";
                case 22: return "This device is disabled. (Code 22)";
                case 24: return "Not present / not working / drivers missing. (Code 24)";
                case 28: return "Drivers for this device are not installed. (Code 28)";
                case 38: return "Driver is still loading. (Code 38)";
                case 41: return "Driver loaded but device not found. (Code 41)";
                case 43: return "Windows stopped this device (reported problems). (Code 43)";
                case 45: return "Currently not connected. (Code 45 — yellow triangle / 'off')";
                case 47: return "Prepared for safe removal but not removed. (Code 47 — 'off')";
                case 48: return "Blocked from starting (known problems). (Code 48)";
                case 54: return "Failed a device-level restart. (Code 54)";
                default: return $"Device reported a problem. (Code {code})";
            }
        }

        public static string VetoDescription(uint vt)
        {
            switch (vt)
            {
                case 0: return "None";
                case 1: return "Legacy device";
                case 2: return "Pending close (device is being closed)";
                case 3: return "A Windows application is using it";
                case 4: return "A Windows service is using it";
                case 5: return "Outstanding open handle(s)";
                case 6: return "A child device is blocking removal";
                case 7: return "Driver";
                case 8: return "Illegal device request";
                case 9: return "Insufficient power";
                case 10: return "Non-disableable";
                case 11: return "Legacy driver";
                case 12: return "Insufficient rights (need administrator?)";
                default: return "Veto type " + vt;
            }
        }
    }
}
