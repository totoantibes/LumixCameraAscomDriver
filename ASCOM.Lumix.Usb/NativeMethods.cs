using System;
using System.Runtime.InteropServices;

namespace ASCOM.Lumix.Usb
{
    /// <summary>
    /// P/Invoke surface for the Panasonic LUMIX USB SDK (Lmxptpif.dll). Two ABIs:
    ///   * Public/Standard (Pub_*) — snake_case entry points, no context arg.
    ///   * Extended/Tether  (Ext_*) — CamelCase entry points; post-connection ops take
    ///     an extra device-context pointer inserted just before retError.
    /// The dispatcher methods (LMX_func_api_*) route to whichever is active per
    /// <see cref="Extended"/>. The DLL is pre-loaded by full path (LoadLibraryEx) so
    /// the DllImport name binds to the already-loaded module (net472 has no
    /// NativeLibrary resolver).
    /// </summary>
    internal static class NativeMethods
    {
        internal const string DLLNAME = "Lmxptpif.dll";
        internal const CallingConvention CC = CallingConvention.Winapi;

        // ---- ABI mode + Tether context ----
        internal static bool Extended;
        internal static IntPtr Ctx = IntPtr.Zero;          // device context (Tether), threaded into every ext op
        internal static IntPtr CapBuf = IntPtr.Zero;       // 16 KB scratch for *_GetCapability
        internal static IntPtr DevBuf = IntPtr.Zero;       // 256 KB extended device-info buffer
        private static byte[] _ctxB, _capB, _devB;
        private static GCHandle _ctxH, _capH, _devH;

        internal static void EnsureTetherBuffers()
        {
            if (Ctx == IntPtr.Zero) { _ctxB = new byte[8192]; _ctxH = GCHandle.Alloc(_ctxB, GCHandleType.Pinned); Ctx = _ctxH.AddrOfPinnedObject(); }
            if (CapBuf == IntPtr.Zero) { _capB = new byte[16384]; _capH = GCHandle.Alloc(_capB, GCHandleType.Pinned); CapBuf = _capH.AddrOfPinnedObject(); }
            if (DevBuf == IntPtr.Zero) { _devB = new byte[262144]; _devH = GCHandle.Alloc(_devB, GCHandleType.Pinned); DevBuf = _devH.AddrOfPinnedObject(); }
        }

        // ---- device-info layouts ----
        internal const int DEVINFO_ARRAY_MAX = 512;
        internal const int INFO_STRIDE = 1036;
        internal const int MODELNAME_OFF = 520;    // within a DEVINFO entry
        // Public struct (x64): count@0 | IDs IntPtr[512]@8 | LMX_DEVINFO[512]@4104
        internal const int PUB_INFO_BASE = 4104;
        internal const int PUB_DEVICE_INFO_SIZE = PUB_INFO_BASE + DEVINFO_ARRAY_MAX * INFO_STRIDE;
        // Extended (Tether) buffer: count@0 | ... | DEVINFO entries @520 (dev_Index@+0)
        internal const int EXT_INFO_BASE = 520;

        // ---- capability layout ----
        internal const int CAPA_BUF_SIZE = 16384;
        internal const int CAPA_NUMOFVAL_OFF = 0;
        internal const int CAPA_SUPPORTVAL_OFF = 4;
        internal const int USER_PTP_ARRAY_MAX = 512;

        // ---- constants ----
        internal const uint CONNECT_VERSION = 0x00010001;
        internal const uint EV_ISO = 0x02000020, EV_SHUTTER = 0x02000030, EV_REC_CTRL_RELEASE = 0x03000010;
        internal const uint EV_OBJCT_ADD = 0x10000040, EV_OBJCT_REQ_TRNSFER = 0x10000043;
        internal const uint TAG_RELEASE_ONESHOT = 0x03000011;
        internal const uint TAG_BULB_START = 0x03000012, TAG_BULB_STOP = 0x03000013, TAG_BULB_FINALIZE = 0x03000019;
        internal const uint OBJ_FORMAT_JPEG = 1, OBJ_FORMAT_RAW = 2;
        internal const uint SS_BULB = 0xFFFFFFFF, SS_UNKNOWN = 0x0FFFFFFE, SS_AUTO = 0x0FFFFFFF;
        internal const uint SS_ONE_SECOND = 0x800003E8;   // 1 s: whole seconds are (sec*1000)|0x80000000
        // ISO capability codes: AUTO/i-ISO sentinels, plus a flag nibble marking extended lows/highs.
        internal const uint ISO_AUTO = 0xFFFFFFFF, ISO_INTELLIGENT = 0xFFFFFFFE;
        internal const uint ISO_FLAG_MASK = 0xFF000000, ISO_FLAG_EXT_LOW = 0x10000000, ISO_FLAG_EXT_HIGH = 0x20000000;
        internal const uint IMGQ_JPEG_FINE = 0, IMGQ_RAW = 3, IMGQ_RAW_JPEG = 4;
        internal const uint CARDLESS_TRNSFER_HDL = 0x12345678;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LMX_STRUCT_PTP_FORM_ENUM_UInt32
        {
            public ushort NumOfVal;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = USER_PTP_ARRAY_MAX)] public int[] SupportVal;
            public byte Available;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LMX_STRUCT_REC_CTRL
        {
            public uint CtrlID;
            public LMX_STRUCT_PTP_FORM_ENUM_UInt32 ParamData;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int LMX_CALLBACK_FUNC(uint param1, uint param2);

        // ---- kernel32 ----
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);
        internal const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

        // ---- ABI-agnostic ----
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)] internal static extern void LMX_func_api_Init();
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)] internal static extern uint LMX_func_api_Reg_NotifyCallback(uint t, LMX_CALLBACK_FUNC f);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)] internal static extern uint LMX_func_api_Delete_CallBackInfo(uint t);

        // ---- Public (Pub_*) ----
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_PnPDeviceInfo", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Get_PnPDeviceInfo(IntPtr buf, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Select_PnPDevice", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Select_PnPDevice(uint i, IntPtr buf, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Open_Session", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Open_Session(uint cv, out uint dcv, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Close_Session", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Close_Session(out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Close_Device", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Close_Device(out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ISO_Set_Param", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_ISO_Set_Param(uint p, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_Set_Param", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_SS_Set_Param(long p, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_Get_Param", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_SS_Get_Param(out int p, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_Get_Capability", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_SS_Get_Capability(IntPtr buf, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ISO_Get_Capability", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_ISO_Get_Capability(IntPtr buf, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Rec_Ctrl_Release", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Rec_Ctrl_Release(ref LMX_STRUCT_REC_CTRL c, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Get_Object(uint h, ref byte buf, uint sz, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_FormatType", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Get_Object_FormatType(uint h, out uint f, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_DataSize", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Get_Object_DataSize(uint h, out uint s, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Skip_Object_Transfer", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_Skip_Object_Transfer(uint h, out uint e);

        // ---- Extended (Ext_*, CamelCase + ctx) ----
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_GetPnPDeviceInfo", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_GetPnPDeviceInfo(IntPtr buf, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SelectPnPDevice", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_SelectPnPDevice(uint i, IntPtr buf, IntPtr ctxOut, uint z4, IntPtr z5, uint z6, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_OpenSession", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_OpenSession(uint cv, out uint dcv, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_CloseSession", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_CloseSession(IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_CloseDevice", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_CloseDevice(IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ISO_SetParam", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_ISO_SetParam(uint p, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_SetParam", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_SS_SetParam(long p, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_GetParam", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_SS_GetParam(out int p, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_GetCapability", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_SS_GetCapability(IntPtr buf, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ISO_GetCapability", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_ISO_GetCapability(IntPtr buf, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Rec_Ctrl_Release", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_Rec_Ctrl_Release(ref LMX_STRUCT_REC_CTRL c, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_Get_Object(uint h, ref byte buf, uint sz, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_FormatType", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_Get_Object_FormatType(uint h, out uint f, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_DataSize", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_Get_Object_DataSize(uint h, out uint s, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Skip_Object_Transfer", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_Skip_Object_Transfer(uint h, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_Set_ImageQuality", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_ImageInfo_SetImageQuality(uint q, IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_GetCapability", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_ImageInfo_GetCapability(IntPtr buf, IntPtr ctx, out uint e);

        // ---- dispatchers (post-connection ops) ----
        internal static byte SS_Set_Param(long p, out uint e) => Extended ? Ext_SS_SetParam(p, Ctx, out e) : Pub_SS_Set_Param(p, out e);
        internal static byte SS_Get_Param(out int p, out uint e) => Extended ? Ext_SS_GetParam(out p, Ctx, out e) : Pub_SS_Get_Param(out p, out e);
        internal static byte ISO_Set_Param(uint p, out uint e) => Extended ? Ext_ISO_SetParam(p, Ctx, out e) : Pub_ISO_Set_Param(p, out e);
        internal static byte SS_Get_Capability(IntPtr buf, out uint e) => Extended ? Ext_SS_GetCapability(buf, Ctx, out e) : Pub_SS_Get_Capability(buf, out e);
        internal static byte ISO_Get_Capability(IntPtr buf, out uint e) => Extended ? Ext_ISO_GetCapability(buf, Ctx, out e) : Pub_ISO_Get_Capability(buf, out e);
        internal static byte Rec_Ctrl_Release(ref LMX_STRUCT_REC_CTRL c, out uint e) => Extended ? Ext_Rec_Ctrl_Release(ref c, Ctx, out e) : Pub_Rec_Ctrl_Release(ref c, out e);
        internal static byte Get_Object(uint h, ref byte buf, uint sz, out uint e) => Extended ? Ext_Get_Object(h, ref buf, sz, Ctx, out e) : Pub_Get_Object(h, ref buf, sz, out e);
        internal static byte Get_Object_FormatType(uint h, out uint f, out uint e) => Extended ? Ext_Get_Object_FormatType(h, out f, Ctx, out e) : Pub_Get_Object_FormatType(h, out f, out e);
        internal static byte Get_Object_DataSize(uint h, out uint s, out uint e) => Extended ? Ext_Get_Object_DataSize(h, out s, Ctx, out e) : Pub_Get_Object_DataSize(h, out s, out e);
        internal static byte Skip_Object_Transfer(uint h, out uint e) => Extended ? Ext_Skip_Object_Transfer(h, Ctx, out e) : Pub_Skip_Object_Transfer(h, out e);
        internal static byte Close_Session(out uint e) => Extended ? Ext_CloseSession(Ctx, out e) : Pub_Close_Session(out e);
        internal static byte Close_Device(out uint e) => Extended ? Ext_CloseDevice(Ctx, out e) : Pub_Close_Device(out e);

        /// <summary>Extended-only: switch the body to RAW (refresh the range first, like BULB).</summary>
        internal static byte SetImageQualityRaw(out uint e)
        {
            EnsureTetherBuffers();
            uint ignore;
            Ext_ImageInfo_GetCapability(CapBuf, Ctx, out ignore);
            return Ext_ImageInfo_SetImageQuality(IMGQ_RAW, Ctx, out e);
        }

        // ---- live view (both DLLs export it; Ext adds ctx) ----
        internal const uint LIVEVIEW_STREAMDATA_SIZE_MAX = 1 * 1024 * 1024; // JPEG frame buffer
        internal const int LIVEVIEW_HISTGRAM_ELEMENT_SIZE = 64;

        [StructLayout(LayoutKind.Sequential)]
        internal struct LMX_LIVEVIEW_HISTGRAM
        {
            public uint valid, samples, elems;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = LIVEVIEW_HISTGRAM_ELEMENT_SIZE)] public byte[] element;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct LMX_LIVEVIEW_POSTURE { public ushort posture; }
        [StructLayout(LayoutKind.Sequential)] internal struct LMX_LIVEVIEW_LEVEL { public ushort roll, pitch; }

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Ctrl_LiveView_Start", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_LiveView_Start(out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Ctrl_LiveView_Stop", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Pub_LiveView_Stop(out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_LiveView_data", ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte Pub_Get_LiveView_data(ref LMX_LIVEVIEW_HISTGRAM h, out uint hs, ref LMX_LIVEVIEW_POSTURE p, out uint ps, ref LMX_LIVEVIEW_LEVEL l, out uint ls, ref byte jpeg, out uint js, out uint e);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Ctrl_LiveView_Start", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_LiveView_Start(IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Ctrl_LiveView_Stop", ExactSpelling = true, CallingConvention = CC)] internal static extern byte Ext_LiveView_Stop(IntPtr ctx, out uint e);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_LiveView_data", ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte Ext_Get_LiveView_data(ref LMX_LIVEVIEW_HISTGRAM h, out uint hs, ref LMX_LIVEVIEW_POSTURE p, out uint ps, ref LMX_LIVEVIEW_LEVEL l, out uint ls, ref byte jpeg, out uint js, IntPtr ctx, out uint e);

        internal static byte LiveView_Start(out uint e) => Extended ? Ext_LiveView_Start(Ctx, out e) : Pub_LiveView_Start(out e);
        internal static byte LiveView_Stop(out uint e) => Extended ? Ext_LiveView_Stop(Ctx, out e) : Pub_LiveView_Stop(out e);
        internal static byte Get_LiveView_data(ref LMX_LIVEVIEW_HISTGRAM h, out uint hs, ref LMX_LIVEVIEW_POSTURE p, out uint ps, ref LMX_LIVEVIEW_LEVEL l, out uint ls, ref byte jpeg, out uint js, out uint e)
            => Extended
                ? Ext_Get_LiveView_data(ref h, out hs, ref p, out ps, ref l, out ls, ref jpeg, out js, Ctx, out e)
                : Pub_Get_LiveView_data(ref h, out hs, ref p, out ps, ref l, out ls, ref jpeg, out js, out e);
    }
}
