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
        public static void Initialize(string dllPath)
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

                NativeMethods.LMX_func_api_Init();
                ActiveDllPath = resolved;
                _initialized = true;
            }
        }

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

                IntPtr buf = Marshal.AllocHGlobal(NativeMethods.DEVICE_INFO_SIZE);
                try
                {
                    Marshal.Copy(new byte[NativeMethods.DEVICE_INFO_SIZE], 0, buf, NativeMethods.DEVICE_INFO_SIZE);
                    uint err;
                    NativeMethods.LMX_func_api_Get_PnPDeviceInfo(buf, out err);

                    int count = Marshal.ReadInt32(buf, 0);
                    var models = new List<string>(Math.Max(0, count));
                    for (int i = 0; i < count && i < NativeMethods.DEVINFO_ARRAY_MAX; i++)
                    {
                        IntPtr namePtr = (IntPtr)(buf.ToInt64() + NativeMethods.INFO_BASE
                                                  + (long)i * NativeMethods.INFO_STRIDE + NativeMethods.MODELNAME_OFF);
                        models.Add(Marshal.PtrToStringUni(namePtr) ?? string.Empty);
                    }
                    return models;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
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
