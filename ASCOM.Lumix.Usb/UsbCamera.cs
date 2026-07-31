using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ASCOM.Lumix.Usb
{
    /// <summary>Result of a capture.</summary>
    public sealed class CaptureResult
    {
        public bool Success;
        public uint Format;        // NativeMethods.OBJ_FORMAT_* (1=JPEG, 2=RAW)
        public string FilePath;
        public string Error;
    }

    /// <summary>
    /// A connected LUMIX camera over USB. Works in Standard (public SDK) or Extended
    /// (Tether DLL) mode per <see cref="NativeMethods.Extended"/> — the dispatchers add
    /// the device-context arg transparently. Extended adds bulb / &gt;60 s and auto-RAW.
    /// </summary>
    public sealed class UsbCamera : IDisposable
    {
        private readonly NativeMethods.LMX_CALLBACK_FUNC _callback;
        private IntPtr _devInfoBuf = IntPtr.Zero;
        private bool _connected;

        private readonly ManualResetEventSlim _captureDone = new ManualResetEventSlim(false);
        private string _captureDir;
        private CaptureResult _result;

        private readonly List<double> _ssSeconds = new List<double>();
        private readonly List<long> _ssRaw = new List<long>();
        private readonly List<uint> _isoValues = new List<uint>();

        public string ModelName { get; private set; }
        public bool IsConnected { get { return _connected; } }
        public bool IsExtended { get { return NativeMethods.Extended; } }
        public IReadOnlyList<double> ShutterSeconds { get { return _ssSeconds; } }
        public IReadOnlyList<uint> IsoValues { get { return _isoValues; } }

        private UsbCamera() { _callback = OnNativeEvent; }

        /// <summary>Select device <paramref name="index"/>, open a session, register callbacks.</summary>
        public static UsbCamera Open(int index)
        {
            if (!LumixUsb.IsInitialized) throw new InvalidOperationException("LumixUsb.Initialize() must be called first.");
            var cam = new UsbCamera();
            uint err;

            if (NativeMethods.Extended)
            {
                NativeMethods.EnsureTetherBuffers();
                NativeMethods.Ext_GetPnPDeviceInfo(NativeMethods.DevBuf, out err);
                int count = Marshal.ReadInt32(NativeMethods.DevBuf, 0);
                if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index), $"index {index} of {count}.");
                cam.ModelName = ReadModel(NativeMethods.DevBuf, NativeMethods.EXT_INFO_BASE, index);
                int devIndex = Marshal.ReadInt32(NativeMethods.DevBuf, NativeMethods.EXT_INFO_BASE + index * NativeMethods.INFO_STRIDE); // dev_Index @ entry+0
                if (NativeMethods.Ext_SelectPnPDevice((uint)devIndex, NativeMethods.DevBuf, NativeMethods.Ctx, 0, IntPtr.Zero, 0, out err) == 0)
                    throw new InvalidOperationException($"SelectPnPDevice failed (err 0x{err:X8}).");
                uint dv;
                if (NativeMethods.Ext_OpenSession(NativeMethods.CONNECT_VERSION, out dv, NativeMethods.Ctx, out err) == 0)
                    throw new InvalidOperationException($"OpenSession failed (err 0x{err:X8}).");
                NativeMethods.SetImageQualityRaw(out err); // auto-RAW on connect
            }
            else
            {
                cam._devInfoBuf = Marshal.AllocHGlobal(NativeMethods.PUB_DEVICE_INFO_SIZE);
                Marshal.Copy(new byte[NativeMethods.PUB_DEVICE_INFO_SIZE], 0, cam._devInfoBuf, NativeMethods.PUB_DEVICE_INFO_SIZE);
                NativeMethods.Pub_Get_PnPDeviceInfo(cam._devInfoBuf, out err);
                int count = Marshal.ReadInt32(cam._devInfoBuf, 0);
                if (index < 0 || index >= count) { cam.FreeBuf(); throw new ArgumentOutOfRangeException(nameof(index), $"index {index} of {count}."); }
                cam.ModelName = ReadModel(cam._devInfoBuf, NativeMethods.PUB_INFO_BASE, index);
                if (NativeMethods.Pub_Select_PnPDevice((uint)index, cam._devInfoBuf, out err) == 0)
                    { cam.FreeBuf(); throw new InvalidOperationException($"Select_PnPDevice failed (err 0x{err:X8})."); }
                uint dv;
                if (NativeMethods.Pub_Open_Session(NativeMethods.CONNECT_VERSION, out dv, out err) == 0)
                    { cam.FreeBuf(); throw new InvalidOperationException($"Open_Session failed (err 0x{err:X8})."); }
            }

            NativeMethods.LMX_func_api_Reg_NotifyCallback(NativeMethods.EV_OBJCT_ADD, cam._callback);
            NativeMethods.LMX_func_api_Reg_NotifyCallback(NativeMethods.EV_OBJCT_REQ_TRNSFER, cam._callback);
            NativeMethods.LMX_func_api_Reg_NotifyCallback(NativeMethods.EV_REC_CTRL_RELEASE, cam._callback);

            cam._connected = true;
            cam.ReadCapabilities();
            return cam;
        }

        private static string ReadModel(IntPtr buf, int infoBase, int index)
        {
            IntPtr p = (IntPtr)(buf.ToInt64() + infoBase + (long)index * NativeMethods.INFO_STRIDE + NativeMethods.MODELNAME_OFF);
            return Marshal.PtrToStringUni(p) ?? string.Empty;
        }

        public bool SetIso(uint rawIso) { uint e; return NativeMethods.ISO_Set_Param(rawIso, out e) != 0; }
        public bool SetShutter(long rawSs) { uint e; return NativeMethods.SS_Set_Param(rawSs, out e) != 0; }

        /// <summary>One-shot exposure (≤ the camera's discrete list; Standard or Extended).</summary>
        public CaptureResult CaptureOneShot(string outputDir, int timeoutMs)
        {
            if (!_connected) throw new InvalidOperationException("Not connected.");
            BeginCapture(outputDir);
            var rc = MakeRecCtrl(NativeMethods.TAG_RELEASE_ONESHOT);
            uint err;
            if (NativeMethods.Rec_Ctrl_Release(ref rc, out err) == 0)
                return new CaptureResult { Success = false, Error = $"Rec_Ctrl_Release failed (err 0x{err:X8})." };
            return AwaitObject(timeoutMs);
        }

        /// <summary>
        /// Bulb exposure of <paramref name="seconds"/> (Extended only): SS=BULB, open the
        /// shutter, hold, close+finalize. Supports arbitrary durations (&gt;60 s).
        /// </summary>
        public CaptureResult CaptureBulb(string outputDir, double seconds, int timeoutMs)
        {
            if (!_connected) throw new InvalidOperationException("Not connected.");
            if (!NativeMethods.Extended) return new CaptureResult { Success = false, Error = "Bulb requires Extended (Tether) mode." };
            BeginCapture(outputDir);
            if (!EnsureBulb()) return new CaptureResult { Success = false, Error = "Could not put the camera into BULB." };

            uint err;
            var open = MakeRecCtrl(NativeMethods.TAG_BULB_START);
            if (NativeMethods.Rec_Ctrl_Release(ref open, out err) == 0)
                return new CaptureResult { Success = false, Error = $"BULB_START failed (err 0x{err:X8})." };
            Thread.Sleep((int)Math.Max(0, seconds * 1000.0));
            var stop = MakeRecCtrl(NativeMethods.TAG_BULB_STOP);
            NativeMethods.Rec_Ctrl_Release(ref stop, out err);
            var fin = MakeRecCtrl(NativeMethods.TAG_BULB_FINALIZE);
            NativeMethods.Rec_Ctrl_Release(ref fin, out err);
            return AwaitObject(timeoutMs);
        }

        // Refresh the SS range so BULB (0xFFFFFFFF) sticks instead of clamping to 60 s.
        private bool EnsureBulb()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                uint e;
                NativeMethods.SS_Get_Capability(NativeMethods.CapBuf, out e);
                Thread.Sleep(250);
                NativeMethods.SS_Set_Param((long)NativeMethods.SS_BULB, out e);
                Thread.Sleep(150);
                int cur;
                NativeMethods.SS_Get_Param(out cur, out e);
                if ((uint)cur == NativeMethods.SS_BULB) return true;
            }
            return false;
        }

        private void BeginCapture(string outputDir) { _captureDir = outputDir; _result = null; _captureDone.Reset(); }

        private CaptureResult AwaitObject(int timeoutMs)
        {
            if (!_captureDone.Wait(timeoutMs))
                return new CaptureResult { Success = false, Error = $"Timed out after {timeoutMs} ms waiting for the image object." };
            return _result ?? new CaptureResult { Success = false, Error = "No image object was produced." };
        }

        private static NativeMethods.LMX_STRUCT_REC_CTRL MakeRecCtrl(uint tag)
        {
            return new NativeMethods.LMX_STRUCT_REC_CTRL
            {
                CtrlID = tag,
                ParamData = new NativeMethods.LMX_STRUCT_PTP_FORM_ENUM_UInt32
                { NumOfVal = 0, SupportVal = new int[NativeMethods.USER_PTP_ARRAY_MAX], Available = 0 },
            };
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
            if (_captureDone.IsSet) return;
            uint err, format = 0, size = 0;
            NativeMethods.Get_Object_FormatType(objectHandle, out format, out err);
            NativeMethods.Get_Object_DataSize(objectHandle, out size, out err);
            if (size == 0) { NativeMethods.Skip_Object_Transfer(objectHandle, out err); return; }

            var buffer = new byte[size];
            byte gr = NativeMethods.Get_Object(objectHandle, ref buffer[0], size, out err);
            if (gr == 0 || !IsKnownImageHeader(buffer)) { NativeMethods.Skip_Object_Transfer(objectHandle, out err); return; }

            string ext = format == NativeMethods.OBJ_FORMAT_JPEG ? ".jpg" : ".rw2";
            string path = Path.Combine(_captureDir ?? Path.GetTempPath(), "usbcap" + ext);
            try { File.WriteAllBytes(path, buffer); _result = new CaptureResult { Success = true, Format = format, FilePath = path }; }
            catch (Exception ex) { _result = new CaptureResult { Success = false, Error = "Write failed: " + ex.Message }; }
            _captureDone.Set();
        }

        private static bool IsKnownImageHeader(byte[] b)
        {
            if (b == null || b.Length < 4) return false;
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x55 && b[3] == 0x00) return true; // RW2 "IIU\0"
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0x00) return true; // TIFF LE
            if (b[0] == 0x4D && b[1] == 0x4D && b[2] == 0x00 && b[3] == 0x2A) return true; // TIFF BE
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return true;                 // JPEG
            return false;
        }

        public void ReadCapabilities()
        {
            _ssSeconds.Clear(); _ssRaw.Clear(); _isoValues.Clear();
            foreach (uint v in ReadEnumList(true))
            {
                double sec = DecodeShutterSeconds(v);
                if (sec <= 0) continue;
                _ssSeconds.Add(sec); _ssRaw.Add(v);
            }
            foreach (uint iso in ReadEnumList(false)) if (iso != 0) _isoValues.Add(iso);
        }

        private static uint[] ReadEnumList(bool shutter)
        {
            IntPtr buf = Marshal.AllocHGlobal(NativeMethods.CAPA_BUF_SIZE);
            try
            {
                Marshal.Copy(new byte[NativeMethods.CAPA_BUF_SIZE], 0, buf, NativeMethods.CAPA_BUF_SIZE);
                uint err;
                if (shutter) NativeMethods.SS_Get_Capability(buf, out err);
                else NativeMethods.ISO_Get_Capability(buf, out err);
                int count = (ushort)Marshal.ReadInt16(buf, NativeMethods.CAPA_NUMOFVAL_OFF);
                if (count < 0 || count > NativeMethods.USER_PTP_ARRAY_MAX) count = 0;
                var list = new uint[count];
                for (int i = 0; i < count; i++)
                    list[i] = (uint)Marshal.ReadInt32(buf, NativeMethods.CAPA_SUPPORTVAL_OFF + i * 4);
                return list;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        /// <summary>Decode a raw shutter code to seconds; -1 for BULB/AUTO/UNKNOWN/0.</summary>
        public static double DecodeShutterSeconds(uint v)
        {
            if (v == 0 || v == NativeMethods.SS_BULB || v == NativeMethods.SS_UNKNOWN || v == NativeMethods.SS_AUTO) return -1;
            double sec = (v & 0x80000000) != 0 ? (double)(v & 0x7fffffff) / 1000.0 : 1000.0 / v;
            return sec > 0 ? sec : -1;
        }

        /// <summary>Nearest supported shutter (raw code) to the requested seconds; 0 if unknown.</summary>
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

        public void Disconnect()
        {
            if (!_connected) return;
            uint err;
            try
            {
                NativeMethods.LMX_func_api_Delete_CallBackInfo(NativeMethods.EV_OBJCT_ADD);
                NativeMethods.LMX_func_api_Delete_CallBackInfo(NativeMethods.EV_OBJCT_REQ_TRNSFER);
                NativeMethods.LMX_func_api_Delete_CallBackInfo(NativeMethods.EV_REC_CTRL_RELEASE);
                NativeMethods.Close_Session(out err);
                NativeMethods.Close_Device(out err);
            }
            catch { }
            _connected = false;
            FreeBuf();
        }

        private void FreeBuf()
        {
            if (_devInfoBuf != IntPtr.Zero) { Marshal.FreeHGlobal(_devInfoBuf); _devInfoBuf = IntPtr.Zero; }
        }

        public void Dispose() { Disconnect(); _captureDone.Dispose(); }
    }
}
