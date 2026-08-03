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

        /// <summary>Serialises captures against Disconnect - see Disconnect().</summary>
        private readonly object _opGate = new object();
        private volatile bool _abortRequested;
        private const int DisconnectWaitMs = 15000;

        /// <summary>Why the last operation gave up, if it did. Null when all is well.</summary>
        public string LastError { get; private set; }

        private readonly ManualResetEventSlim _captureDone = new ManualResetEventSlim(false);
        private string _captureDir;
        private CaptureResult _result;

        private readonly List<double> _ssSeconds = new List<double>();
        private readonly List<long> _ssRaw = new List<long>();
        private readonly List<uint> _isoValues = new List<uint>();
        private readonly List<uint> _isoNumbers = new List<uint>();

        public string ModelName { get; private set; }
        public bool IsConnected { get { return _connected; } }
        public bool IsExtended { get { return NativeMethods.Extended; } }
        public IReadOnlyList<double> ShutterSeconds { get { return _ssSeconds; } }
        /// <summary>Raw ISO codes to pass back to the SDK, AUTO excluded.</summary>
        public IReadOnlyList<uint> IsoValues { get { return _isoValues; } }
        /// <summary>The same entries as plain ISO numbers (160, 51200, ...) for display.</summary>
        public IReadOnlyList<uint> IsoNumbers { get { return _isoNumbers; } }

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
        public bool SetShutterByIndex(int i) { return i >= 0 && i < _ssRaw.Count && SetShutter(_ssRaw[i]); }
        public bool SetIsoByIndex(int i) { return i >= 0 && i < _isoValues.Count && SetIso(_isoValues[i]); }

        // ---- live view (JPEG frames; works in Standard or Extended) ----
        private byte[] _lvJpeg;
        private NativeMethods.LMX_LIVEVIEW_HISTGRAM _lvHist;
        private bool _lvActive;
        public bool LiveViewActive { get { return _lvActive; } }

        public bool StartLiveView()
        {
            if (!_connected) return false;
            _lvJpeg = new byte[NativeMethods.LIVEVIEW_STREAMDATA_SIZE_MAX];
            _lvHist = new NativeMethods.LMX_LIVEVIEW_HISTGRAM { element = new byte[NativeMethods.LIVEVIEW_HISTGRAM_ELEMENT_SIZE] };
            uint e;
            _lvActive = NativeMethods.LiveView_Start(out e) != 0;
            return _lvActive;
        }

        public void StopLiveView()
        {
            if (!_lvActive) return;
            uint e; NativeMethods.LiveView_Stop(out e);
            _lvActive = false;
        }

        /// <summary>Grab one live-view JPEG frame (or null if none/failed).</summary>
        public byte[] GetLiveViewJpeg()
        {
            if (!_lvActive || _lvJpeg == null) return null;
            var post = new NativeMethods.LMX_LIVEVIEW_POSTURE();
            var lvl = new NativeMethods.LMX_LIVEVIEW_LEVEL();
            uint hs, ps, ls, js, e;
            byte r = NativeMethods.Get_LiveView_data(ref _lvHist, out hs, ref post, out ps, ref lvl, out ls, ref _lvJpeg[0], out js, out e);
            if (r == 0 || js == 0 || js > _lvJpeg.Length) return null;
            var frame = new byte[js];
            Array.Copy(_lvJpeg, frame, (int)js);
            return frame;
        }

        /// <summary>One-shot exposure (≤ the camera's discrete list; Standard or Extended).</summary>
        public CaptureResult CaptureOneShot(string outputDir, int timeoutMs)
        {
            if (!_connected) throw new InvalidOperationException("Not connected.");
            lock (_opGate)   // Disconnect must not close the session under this
            {
                BeginCapture(outputDir);
                var rc = MakeRecCtrl(NativeMethods.TAG_RELEASE_ONESHOT);
                uint err;
                if (NativeMethods.Rec_Ctrl_Release(ref rc, out err) == 0)
                    return new CaptureResult { Success = false, Error = $"Rec_Ctrl_Release failed (err 0x{err:X8})." };
                return AwaitObject(timeoutMs);
            }
        }

        /// <summary>
        /// Bulb exposure of <paramref name="seconds"/> (Extended only): SS=BULB, open the
        /// shutter, hold, close+finalize. Supports arbitrary durations (&gt;60 s).
        /// </summary>
        public CaptureResult CaptureBulb(string outputDir, double seconds, int timeoutMs)
        {
            if (!_connected) throw new InvalidOperationException("Not connected.");
            if (!NativeMethods.Extended) return new CaptureResult { Success = false, Error = "Bulb requires Extended (Tether) mode." };
            lock (_opGate)   // Disconnect must not close the session under this
            {
                BeginCapture(outputDir);
                if (!EnsureBulb()) return new CaptureResult { Success = false, Error = "Could not put the camera into BULB." };

                uint err;
                var open = MakeRecCtrl(NativeMethods.TAG_BULB_START);
                if (NativeMethods.Rec_Ctrl_Release(ref open, out err) == 0)
                    return new CaptureResult { Success = false, Error = $"BULB_START failed (err 0x{err:X8})." };

                // Sleep in slices so an abort ends the exposure promptly instead of
                // making a client wait out a long bulb before it can disconnect.
                var until = DateTime.UtcNow.AddSeconds(Math.Max(0, seconds));
                while (!_abortRequested && DateTime.UtcNow < until)
                    Thread.Sleep(Math.Min(200, Math.Max(1, (int)(until - DateTime.UtcNow).TotalMilliseconds)));

                var stop = MakeRecCtrl(NativeMethods.TAG_BULB_STOP);
                NativeMethods.Rec_Ctrl_Release(ref stop, out err);
                var fin = MakeRecCtrl(NativeMethods.TAG_BULB_FINALIZE);
                NativeMethods.Rec_Ctrl_Release(ref fin, out err);
                if (_abortRequested) return new CaptureResult { Success = false, Error = "Aborted." };
                return AwaitObject(timeoutMs);
            }
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

        /// <summary>
        /// Leave BULB before closing the session — the body keeps the setting across
        /// connections, so a disconnect mid-bulb would strand it on B for the next client
        /// (and for the camera's own dial). Best effort; never throws.
        /// </summary>
        private void ExitBulb()
        {
            if (!NativeMethods.Extended) return;   // only Extended can select BULB
            try
            {
                uint e;
                int cur;
                if (NativeMethods.SS_Get_Param(out cur, out e) == 0) return;
                if ((uint)cur != NativeMethods.SS_BULB) return;
                double actual;
                long raw = NearestShutterRaw(1.0, out actual);
                NativeMethods.SS_Set_Param(raw != 0 ? raw : NativeMethods.SS_ONE_SECOND, out e);
            }
            catch { }
        }

        private void BeginCapture(string outputDir)
        {
            _captureDir = outputDir;
            _result = null;
            _abortRequested = false;
            LastError = null;
            _captureDone.Reset();
        }

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
            _ssSeconds.Clear(); _ssRaw.Clear(); _isoValues.Clear(); _isoNumbers.Clear();
            foreach (uint v in ReadEnumList(true))
            {
                double sec = DecodeShutterSeconds(v);
                if (sec <= 0) continue;
                _ssSeconds.Add(sec); _ssRaw.Add(v);
            }
            foreach (uint raw in ReadEnumList(false))
            {
                uint iso = DecodeIso(raw);
                if (iso == 0) continue;                // AUTO / i-ISO / unknown
                _isoValues.Add(raw); _isoNumbers.Add(iso);
            }
        }

        /// <summary>
        /// Decode a raw ISO code to its plain value; 0 for AUTO / i-ISO / unknown.
        /// The capability list mixes plain values with flagged extended ones — on a GH5S:
        /// 0xFFFFFFFF=AUTO, 0x100000xx=extended low (80/100), 0xA0..0xC800=native 160..51200,
        /// 0x200xxxxx=extended high (102400/204800).
        /// </summary>
        public static uint DecodeIso(uint raw)
        {
            if (raw == 0 || raw == NativeMethods.ISO_AUTO || raw == NativeMethods.ISO_INTELLIGENT) return 0;
            uint flag = raw & NativeMethods.ISO_FLAG_MASK;
            if (flag == 0) return raw;
            if (flag == NativeMethods.ISO_FLAG_EXT_LOW || flag == NativeMethods.ISO_FLAG_EXT_HIGH)
                return raw & ~NativeMethods.ISO_FLAG_MASK;
            return 0;
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

        /// <summary>
        /// End any capture in progress so a Disconnect does not have to wait for it.
        /// For a bulb that means closing the shutter; the worker then unwinds normally.
        /// </summary>
        public void RequestAbort()
        {
            _abortRequested = true;
            if (!NativeMethods.Extended) return;
            try
            {
                uint err;
                var stop = MakeRecCtrl(NativeMethods.TAG_BULB_STOP);
                NativeMethods.Rec_Ctrl_Release(ref stop, out err);
                var fin = MakeRecCtrl(NativeMethods.TAG_BULB_FINALIZE);
                NativeMethods.Rec_Ctrl_Release(ref fin, out err);
            }
            catch { }
            _captureDone.Set();   // release anyone blocked in AwaitObject
        }

        /// <summary>
        /// Close the session. Waits for an in-flight capture first: the SDK delivers
        /// images on its own callback thread, and closing the session (or freeing the
        /// device-info buffer it still references) underneath that thread faults the
        /// native library - an AccessViolation that kills the whole host process, not a
        /// catchable exception. If the capture will not finish in time we deliberately
        /// leave the session open rather than crash the client.
        /// </summary>
        public void Disconnect()
        {
            if (!_connected) return;

            RequestAbort();
            bool exclusive = Monitor.TryEnter(_opGate, DisconnectWaitMs);
            if (!exclusive)
            {
                LastError = "a capture was still running; the USB session was left open rather than risk a crash";
                return;
            }
            try
            {
                StopLiveView();
                ExitBulb();
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
            finally { Monitor.Exit(_opGate); }
        }

        private void FreeBuf()
        {
            if (_devInfoBuf != IntPtr.Zero) { Marshal.FreeHGlobal(_devInfoBuf); _devInfoBuf = IntPtr.Zero; }
        }

        public void Dispose() { Disconnect(); _captureDone.Dispose(); }
    }
}
