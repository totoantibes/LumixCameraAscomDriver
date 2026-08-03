using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ASCOM.Lumix.Usb
{
    /// <summary>
    /// High-level entry point to the LUMIX USB SDK. Phase 1: load the chosen
    /// Lmxptpif.dll (public SDK) and enumerate connected cameras. Connect/capture
    /// build on top of this in subsequent phases.
    /// </summary>
    public static class LumixUsb
    {
        private static readonly object _gate = new object();
        private static bool _initialized;

        /// <summary>The Lmxptpif.dll actually loaded (public SDK or the user's Tether DLL).</summary>
        public static string ActiveDllPath { get; private set; }

        /// <summary>True once the SDK has been loaded and Init() called.</summary>
        public static bool IsInitialized { get { lock (_gate) return _initialized; } }

        /// <summary>
        /// Load the chosen Lmxptpif.dll and initialise the SDK once for the process.
        /// <paramref name="dllPath"/> is a full path to a bitness-matched Lmxptpif.dll
        /// (or a directory containing it). Safe to call more than once.
        /// </summary>
        public static void Initialize(string dllPath, bool extended = false)
        {
            lock (_gate)
            {
                if (_initialized) return;

                string resolved = ResolveDll(dllPath);
                if (resolved == null)
                    throw new FileNotFoundException("Lmxptpif.dll not found", dllPath ?? "(null)");

                // Pre-load by full path so the later DllImport("Lmxptpif.dll") binds to
                // this exact module (the net472 substitute for SetDllImportResolver).
                IntPtr h = NativeMethods.LoadLibraryEx(resolved, IntPtr.Zero, NativeMethods.LOAD_WITH_ALTERED_SEARCH_PATH);
                if (h == IntPtr.Zero)
                    throw new InvalidOperationException(
                        $"LoadLibraryEx('{resolved}') failed (Win32 error {Marshal.GetLastWin32Error()}). " +
                        (IntPtr.Size == 8 ? "Process is x64 - ensure an x64 Lmxptpif.dll." : "Process is x86 - ensure an x86 Lmxptpif.dll."));

                NativeMethods.Extended = extended;
                if (extended) NativeMethods.EnsureTetherBuffers();
                NativeMethods.LMX_func_api_Init();
                ActiveDllPath = resolved;
                _initialized = true;
            }
        }

        /// <summary>True if running against the Tether (Extended) DLL.</summary>
        public static bool IsExtended { get { lock (_gate) return NativeMethods.Extended; } }

        /// <summary>
        /// Enumerate connected cameras and return their model names (as the USB SDK
        /// reports them, e.g. "DC-GH5S"). Note the SDK enumerates any PTP device, so
        /// callers should match against the known LUMIX model set.
        /// </summary>
        public static IReadOnlyList<string> Enumerate()
        {
            lock (_gate)
            {
                if (!_initialized) throw new InvalidOperationException("Initialize() must be called first.");

                if (NativeMethods.Extended)
                {
                    NativeMethods.EnsureTetherBuffers();
                    uint err;
                    NativeMethods.Ext_GetPnPDeviceInfo(NativeMethods.DevBuf, out err);
                    return ReadModels(NativeMethods.DevBuf, NativeMethods.EXT_INFO_BASE);
                }
                else
                {
                    IntPtr buf = Marshal.AllocHGlobal(NativeMethods.PUB_DEVICE_INFO_SIZE);
                    try
                    {
                        Marshal.Copy(new byte[NativeMethods.PUB_DEVICE_INFO_SIZE], 0, buf, NativeMethods.PUB_DEVICE_INFO_SIZE);
                        uint err;
                        NativeMethods.Pub_Get_PnPDeviceInfo(buf, out err);
                        return ReadModels(buf, NativeMethods.PUB_INFO_BASE);
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
        }

        /// <summary>
        /// Camera-free diagnostic: round-trip the capture struct through the native
        /// marshaller to prove net472 can handle it (the ~535 KB enumerate struct
        /// could NOT be blitted by-ref; the ~2 KB REC_CTRL can). No SDK/device needed.
        /// </summary>
        public static string DiagnoseCaptureMarshalling()
        {
            int size = Marshal.SizeOf(typeof(NativeMethods.LMX_STRUCT_REC_CTRL));
            var rc = new NativeMethods.LMX_STRUCT_REC_CTRL
            {
                CtrlID = NativeMethods.TAG_RELEASE_ONESHOT,
                ParamData = new NativeMethods.LMX_STRUCT_PTP_FORM_ENUM_UInt32
                {
                    NumOfVal = 0,
                    SupportVal = new int[NativeMethods.USER_PTP_ARRAY_MAX],
                    Available = 0,
                },
            };
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(rc, p, false);
                var back = (NativeMethods.LMX_STRUCT_REC_CTRL)Marshal.PtrToStructure(p, typeof(NativeMethods.LMX_STRUCT_REC_CTRL));
                bool ok = back.CtrlID == NativeMethods.TAG_RELEASE_ONESHOT
                          && back.ParamData.SupportVal != null
                          && back.ParamData.SupportVal.Length == NativeMethods.USER_PTP_ARRAY_MAX;
                return $"LMX_STRUCT_REC_CTRL size={size} bytes; round-trip {(ok ? "OK" : "MISMATCH")} (CtrlID=0x{back.CtrlID:X}).";
            }
            finally { Marshal.DestroyStructure(p, typeof(NativeMethods.LMX_STRUCT_REC_CTRL)); Marshal.FreeHGlobal(p); }
        }

        /// <summary>
        /// Camera-free check of the shutter decode + nearest-match algorithm against a
        /// synthetic supported list (4 s whole, 1/8 s and 1/125 s fractional, + BULB/AUTO
        /// which must be dropped). No SDK/device needed.
        /// </summary>
        public static string DiagnoseShutterMapping()
        {
            uint[] synthetic = { 0x80000FA0u /*4s*/, 8000u /*1/8s*/, 125000u /*1/125s*/,
                                 NativeMethods.SS_BULB, NativeMethods.SS_AUTO /*dropped*/ };
            var secs = new List<double>(); var raws = new List<long>();
            foreach (uint v in synthetic)
            {
                double s = UsbCamera.DecodeShutterSeconds(v);
                if (s > 0) { secs.Add(s); raws.Add(v); }
            }
            Func<double, string> nearest = req =>
            {
                int bi = 0; double be = Math.Abs(secs[0] - req);
                for (int i = 1; i < secs.Count; i++) { double e = Math.Abs(secs[i] - req); if (e < be) { be = e; bi = i; } }
                return $"{req}s->{secs[bi]}s(0x{raws[bi]:X})";
            };
            return $"decoded {secs.Count} of {synthetic.Length} (BULB/AUTO dropped): [{string.Join(", ", secs)}] ; "
                   + $"nearest {nearest(3.9)}, {nearest(0.1)}, {nearest(0.007)}";
        }

        private static IReadOnlyList<string> ReadModels(IntPtr buf, int infoBase)
        {
            int count = Marshal.ReadInt32(buf, 0);
            var models = new List<string>(Math.Max(0, count));
            for (int i = 0; i < count && i < NativeMethods.DEVINFO_ARRAY_MAX; i++)
            {
                IntPtr namePtr = (IntPtr)(buf.ToInt64() + infoBase
                                          + (long)i * NativeMethods.INFO_STRIDE + NativeMethods.MODELNAME_OFF);
                models.Add(Marshal.PtrToStringUni(namePtr) ?? string.Empty);
            }
            return models;
        }

        private static string ResolveDll(string dllPath)
        {
            if (string.IsNullOrEmpty(dllPath)) return null;
            if (File.Exists(dllPath)) return Path.GetFullPath(dllPath);
            if (Directory.Exists(dllPath))
            {
                string candidate = Path.Combine(dllPath, NativeMethods.DLLNAME);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            return null;
        }
    }
}
