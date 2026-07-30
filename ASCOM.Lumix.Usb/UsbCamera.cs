using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ASCOM.Lumix.Usb
{
    /// <summary>Result of a one-shot capture.</summary>
    public sealed class CaptureResult
    {
        public bool Success;
        public uint Format;        // NativeMethods.OBJ_FORMAT_* (1=JPEG, 2=RAW)
        public string FilePath;    // the file written (extension picked from Format)
        public string Error;
    }

    /// <summary>
    /// A connected LUMIX camera over USB (public/Standard ABI, Phase 1). One instance
    /// per session; the SDK's native context is process-global so only one at a time.
    /// Capture is a one-shot release ≤ 60 s; the image object arrives on the SDK's
    /// callback thread and is written to disk for the driver's normal RAW/JPG→TIFF path.
    /// </summary>
    public sealed class UsbCamera : IDisposable
    {
        private readonly NativeMethods.LMX_CALLBACK_FUNC _callback; // kept alive for the whole session
        private IntPtr _devInfoBuf = IntPtr.Zero;
        private bool _connected;

        // capture handshake (callback thread -> CaptureOneShot thread)
        private readonly ManualResetEventSlim _captureDone = new ManualResetEventSlim(false);
        private string _captureDir;
        private CaptureResult _result;

        // Supported shutter list (parallel: seconds[i] <-> raw[i]) and ISO list, read on connect.
        private readonly List<double> _ssSeconds = new List<double>();
        private readonly List<long> _ssRaw = new List<long>();
        private readonly List<uint> _isoValues = new List<uint>();

        public string ModelName { get; private set; }
        public bool IsConnected { get { return _connected; } }

        /// <summary>Supported whole/fractional shutter speeds in seconds (nearest-match source).</summary>
        public IReadOnlyList<double> ShutterSeconds { get { return _ssSeconds; } }
        /// <summary>Supported ISO values as raw SDK codes (index = ASCOM gain).</summary>
        public IReadOnlyList<uint> IsoValues { get { return _isoValues; } }

        private UsbCamera()
        {
            _callback = OnNativeEvent; // one delegate instance, referenced by a field so it is not GC'd
        }

        /// <summary>
        /// Select device <paramref name="index"/>, open a session, and register the
        /// capture callbacks. Requires <see cref="LumixUsb.Initialize"/> first.
        /// </summary>
        public static UsbCamera Open(int index)
        {
            if (!LumixUsb.IsInitialized) throw new InvalidOperationException("LumixUsb.Initialize() must be called first.");

            var cam = new UsbCamera();
            uint err;

            // Enumerate into a buffer, capture this index's model name, and hand the
            // buffer to Select_PnPDevice (kept alive for the session).
            cam._devInfoBuf = Marshal.AllocHGlobal(NativeMethods.DEVICE_INFO_SIZE);
            Marshal.Copy(new byte[NativeMethods.DEVICE_INFO_SIZE], 0, cam._devInfoBuf, NativeMethods.DEVICE_INFO_SIZE);
            NativeMethods.LMX_func_api_Get_PnPDeviceInfo(cam._devInfoBuf, out err);
            int count = Marshal.ReadInt32(cam._devInfoBuf, 0);
            if (index < 0 || index >= count)
            {
                cam.FreeBuf();
                throw new ArgumentOutOfRangeException(nameof(index), $"index {index} out of range (device count {count}).");
            }
            IntPtr namePtr = (IntPtr)(cam._devInfoBuf.ToInt64() + NativeMethods.INFO_BASE
                                      + (long)index * NativeMethods.INFO_STRIDE + NativeMethods.MODELNAME_OFF);
            cam.ModelName = Marshal.PtrToStringUni(namePtr) ?? string.Empty;

            byte sel = NativeMethods.LMX_func_api_Select_PnPDevice((uint)index, cam._devInfoBuf, out err);
            if (sel == 0) { cam.FreeBuf(); throw new InvalidOperationException($"Select_PnPDevice failed (err 0x{err:X8})."); }

            uint devVer;
            byte op = NativeMethods.LMX_func_api_Open_Session(NativeMethods.CONNECT_VERSION, out devVer, out err);
            if (op == 0) { cam.FreeBuf(); throw new InvalidOperationException($"Open_Session failed (err 0x{err:X8})."); }

            // Register the object-transfer / release callbacks (delegate stays alive in _callback).
            NativeMethods.LMX_func_api_Reg_NotifyCallback(NativeMethods.EV_OBJCT_ADD, cam._callback);
            NativeMethods.LMX_func_api_Reg_NotifyCallback(NativeMethods.EV_OBJCT_REQ_TRNSFER, cam._callback);
            NativeMethods.LMX_func_api_Reg_NotifyCallback(NativeMethods.EV_REC_CTRL_RELEASE, cam._callback);

            cam._connected = true;
            cam.ReadCapabilities();
            return cam;
        }

        /// <summary>Read the camera's supported shutter + ISO lists (call after Open).</summary>
        public void ReadCapabilities()
        {
            _ssSeconds.Clear(); _ssRaw.Clear(); _isoValues.Clear();

            foreach (uint v in ReadEnumList(true))   // shutter
            {
                double sec = DecodeShutterSeconds(v);
                if (sec <= 0) continue;
                _ssSeconds.Add(sec);
                _ssRaw.Add(v); // raw code for SS_Set_Param (long)
            }
            foreach (uint iso in ReadEnumList(false)) // ISO
                if (iso != 0) _isoValues.Add(iso);
        }

        // Read a capability enum's supported values via a raw buffer (NumOfVal@0, SupportVal@4).
        private static uint[] ReadEnumList(bool shutter)
        {
            IntPtr buf = Marshal.AllocHGlobal(NativeMethods.CAPA_BUF_SIZE);
            try
            {
                Marshal.Copy(new byte[NativeMethods.CAPA_BUF_SIZE], 0, buf, NativeMethods.CAPA_BUF_SIZE);
                uint err;
                if (shutter) NativeMethods.LMX_func_api_SS_Get_Capability(buf, out err);
                else NativeMethods.LMX_func_api_ISO_Get_Capability(buf, out err);

                int count = (ushort)Marshal.ReadInt16(buf, NativeMethods.CAPA_NUMOFVAL_OFF);
                if (count < 0 || count > NativeMethods.USER_PTP_ARRAY_MAX) count = 0;
                var list = new uint[count];
                for (int i = 0; i < count; i++)
                    list[i] = (uint)Marshal.ReadInt32(buf, NativeMethods.CAPA_SUPPORTVAL_OFF + i * 4);
                return list;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        /// <summary>
        /// Decode a raw shutter code to seconds: whole seconds carry 0x80000000
        /// (sec=(v&amp;0x7fffffff)/1000), fractional are 1/x (sec=1000/v). Returns -1 for
        /// BULB/AUTO/UNKNOWN/0 or any non-positive result.
        /// </summary>
        public static double DecodeShutterSeconds(uint v)
        {
            if (v == 0 || v == NativeMethods.SS_BULB || v == NativeMethods.SS_UNKNOWN || v == NativeMethods.SS_AUTO) return -1;
            double sec = (v & 0x80000000) != 0 ? (double)(v & 0x7fffffff) / 1000.0 : 1000.0 / v;
            return sec > 0 ? sec : -1;
        }

        /// <summary>
        /// Nearest supported shutter speed (raw code for <see cref="SetShutter"/>) to the
        /// requested seconds. Returns 0 and actualSeconds=requested if the list is unknown.
        /// </summary>
        public long NearestShutterRaw(double requestedSeconds, out double actualSeconds)
        {
            actualSeconds = requestedSeconds;
            if (_ssSeconds.Count == 0) return 0;
            int bestIdx = 0;
            double bestErr = Math.Abs(_ssSeconds[0] - requestedSeconds);
            for (int i = 1; i < _ssSeconds.Count; i++)
            {
                double err = Math.Abs(_ssSeconds[i] - requestedSeconds);
                if (err < bestErr) { bestErr = err; bestIdx = i; }
            }
            actualSeconds = _ssSeconds[bestIdx];
            return _ssRaw[bestIdx];
        }

        /// <summary>Set ISO to a raw SDK value (from the camera's supported list).</summary>
        public bool SetIso(uint rawIso)
        {
            uint err;
            return NativeMethods.LMX_func_api_ISO_Set_Param(rawIso, out err) != 0;
        }

        /// <summary>Set shutter speed to a raw SDK value (from the camera's supported list).</summary>
        public bool SetShutter(long rawSs)
        {
            uint err;
            return NativeMethods.LMX_func_api_SS_Set_Param(rawSs, out err) != 0;
        }

        /// <summary>
        /// Fire a one-shot exposure (Standard mode ≤ 60 s; the shutter speed must be set
        /// beforehand), wait for the resulting image object, and write it to
        /// <paramref name="outputDir"/> as usbcap.rw2 / usbcap.jpg.
        /// </summary>
        public CaptureResult CaptureOneShot(string outputDir, int timeoutMs)
        {
            if (!_connected) throw new InvalidOperationException("Not connected.");

            _captureDir = outputDir;
            _result = null;
            _captureDone.Reset();

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
            uint err;
            byte r = NativeMethods.LMX_func_api_Rec_Ctrl_Release(ref rc, out err);
            if (r == 0) return new CaptureResult { Success = false, Error = $"Rec_Ctrl_Release failed (err 0x{err:X8})." };

            if (!_captureDone.Wait(timeoutMs))
                return new CaptureResult { Success = false, Error = $"Timed out after {timeoutMs} ms waiting for the image object." };

            return _result ?? new CaptureResult { Success = false, Error = "No image object was produced." };
        }

        // Runs on the SDK's own callback thread.
        private int OnNativeEvent(uint eventType, uint eventParam)
        {
            switch (eventType)
            {
                case NativeMethods.EV_OBJCT_ADD:
                case NativeMethods.EV_OBJCT_REQ_TRNSFER:
                case NativeMethods.EV_REC_CTRL_RELEASE:
                    HandleObject(eventParam);
                    return 0;
                default:
                    return -1;
            }
        }

        private void HandleObject(uint objectHandle)
        {
            if (_captureDone.IsSet) return; // already handled this exposure

            uint err, format = 0, size = 0;
            NativeMethods.LMX_func_api_Get_Object_FormatType(objectHandle, out format, out err);
            NativeMethods.LMX_func_api_Get_Object_DataSize(objectHandle, out size, out err);
            if (size == 0)
            {
                // Placeholder / already-consumed object: release it and keep waiting.
                NativeMethods.LMX_func_api_Skip_Object_Transfer(objectHandle, out err);
                return;
            }

            var buffer = new byte[size];
            byte gr = NativeMethods.LMX_func_api_Get_Object(objectHandle, ref buffer[0], size, out err);

            // First-frame guard: the first object of a session can be a zero/placeholder
            // that libraw would choke on. Only accept a recognised image header.
            if (gr == 0 || !IsKnownImageHeader(buffer))
            {
                NativeMethods.LMX_func_api_Skip_Object_Transfer(objectHandle, out err);
                return;
            }

            string ext = format == NativeMethods.OBJ_FORMAT_JPEG ? ".jpg" : ".rw2";
            string path = Path.Combine(_captureDir ?? Path.GetTempPath(), "usbcap" + ext);
            try
            {
                File.WriteAllBytes(path, buffer);
                _result = new CaptureResult { Success = true, Format = format, FilePath = path };
            }
            catch (Exception ex)
            {
                _result = new CaptureResult { Success = false, Error = "Write failed: " + ex.Message };
            }
            _captureDone.Set();
        }

        private static bool IsKnownImageHeader(byte[] b)
        {
            if (b == null || b.Length < 4) return false;
            // RW2 "IIU\0"
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x55 && b[3] == 0x00) return true;
            // TIFF LE / BE
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0x00) return true;
            if (b[0] == 0x4D && b[1] == 0x4D && b[2] == 0x00 && b[3] == 0x2A) return true;
            // JPEG
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return true;
            return false;
        }

        public void Disconnect()
        {
            if (!_connected) return;
            uint err;
            try
            {
                NativeMethods.LMX_func_api_Delete_CallBackInfo(NativeMethods.EV_OBJCT_ADD);
                NativeMethods.LMX_func_api_Delete_CallBackInfo(NativeMethods.EV_OBJCT_REQ_TRNSFER);
                NativeMethods.LMX_func_api_Delete_CallBackInfo(NativeMethods.EV_REC_CTRL_RELEASE);
                NativeMethods.LMX_func_api_Close_Session(out err);
                NativeMethods.LMX_func_api_Close_Device(out err);
            }
            catch { /* teardown best-effort */ }
            _connected = false;
            FreeBuf();
        }

        private void FreeBuf()
        {
            if (_devInfoBuf != IntPtr.Zero) { Marshal.FreeHGlobal(_devInfoBuf); _devInfoBuf = IntPtr.Zero; }
        }

        public void Dispose()
        {
            Disconnect();
            _captureDone.Dispose();
        }
    }
}
