using System;
using System.Runtime.InteropServices;

namespace ASCOM.Lumix.Usb
{
    /// <summary>
    /// P/Invoke surface for the Panasonic LUMIX USB SDK (Lmxptpif.dll), public ABI.
    /// The DLL is chosen and pre-loaded by <see cref="NativeLoader"/> before any of
    /// these are called, so the DllImport name binds to the already-loaded module.
    /// (net472 substitute for the plugin's NativeLibrary.SetDllImportResolver.)
    /// </summary>
    internal static class NativeMethods
    {
        internal const string DLLNAME = "Lmxptpif.dll";
        // On x64 there is a single native calling convention; Winapi resolves to it.
        internal const CallingConvention CC = CallingConvention.Winapi;

        // ---- LMX_CONNECT_DEVICE_INFO raw layout (x64), confirmed against live data ----
        //   count @0 (uint) | pad(4) | IDs IntPtr[512] @8 | Info LMX_DEVINFO[512] @4104
        // LMX_DEVINFO (1036B): dev_Index(4) makerName(512) makerLen(4) modelName(512) modelLen(4)
        internal const int DEVINFO_ARRAY_MAX = 512;
        internal const int DEVINFO_STRING_MAX = 256; // WCHARs
        internal const int IDS_OFF = 8;
        internal const int INFO_BASE = 4104;
        internal const int INFO_STRIDE = 1036;
        internal const int MAKERNAME_OFF = 4;
        internal const int MODELNAME_OFF = 520;
        internal const int DEVICE_INFO_SIZE = INFO_BASE + DEVINFO_ARRAY_MAX * INFO_STRIDE;

        // ---- kernel32 module loading ----
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);
        internal const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

        // ---- SDK: lifecycle + enumeration ----
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern void LMX_func_api_Init();

        // Public struct is ~535 KB; net472's marshaller can't blit it by-ref, so pass a
        // raw buffer and read fields with Marshal.* (proven in the POC).
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Get_PnPDeviceInfo(IntPtr plmxPnpDevInfo, out uint retError);

        // ---- SDK: connect / session (public ABI; extended/Tether ctx variant added in Phase 2) ----
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Select_PnPDevice(uint index, IntPtr plmxPnpDevInfo, out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Open_Session(uint connectVersion, out uint deviceConnectVersion, out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Close_Session(out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Close_Device(out uint retError);

        internal const uint CONNECT_VERSION = 0x00010001;

        // ---- capture: PTP array sizes ----
        internal const int USER_PTP_ARRAY_MAX = 512;
        internal const int USER_PTP_STRING_MAX = 256;

        // ---- event IDs (callback registration + dispatch) ----
        internal const uint EV_ISO = 0x02000020;
        internal const uint EV_SHUTTER = 0x02000030;
        internal const uint EV_REC_CTRL_RELEASE = 0x03000010;
        internal const uint EV_OBJCT_ADD = 0x10000040;
        internal const uint EV_OBJCT_REQ_TRNSFER = 0x10000043;

        // ---- Rec_Ctrl_Release tags ----
        internal const uint TAG_RELEASE_ONESHOT = 0x03000011;
        // Bulb tags (extended/Tether only; Phase 2): START 0x03000012, STOP 0x03000013, FINALIZE 0x03000019

        // ---- object formats returned by Get_Object_FormatType ----
        internal const uint OBJ_FORMAT_JPEG = 1;
        internal const uint OBJ_FORMAT_RAW = 2;

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

        // typedef int (WINAPI* LMX_CALLBACK_FUNC)(UInt32 eventType, UInt32 eventParam) — StdCall.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int LMX_CALLBACK_FUNC(uint param1, uint param2);

        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern uint LMX_func_api_Reg_NotifyCallback(uint callBackType, LMX_CALLBACK_FUNC appfunc);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern uint LMX_func_api_Delete_CallBackInfo(uint callBackType);

        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_ISO_Set_Param(uint ulParam, out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_SS_Set_Param(long ulParam, out uint retError);

        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Rec_Ctrl_Release(ref LMX_STRUCT_REC_CTRL lpRecCtrl, out uint retError);

        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Get_Object_FormatType(uint objHandle, out uint pFormatType, out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Get_Object_DataSize(uint objHandle, out uint pDataSize, out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Get_Object(uint objectHandle, ref byte lpStoreBufAdder, uint storeBufSize, out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_Skip_Object_Transfer(uint objectHandle, out uint retError);

        // ---- capability read (ISO / shutter supported lists) ----
        // The ISO/SS capability structs are a few KB and begin with a
        // LMX_STRUCT_PTP_FORM_ENUM_UInt32 Capa_Enum (NumOfVal@0 ushort, SupportVal@4
        // int[512]). Read via a raw buffer (like enumerate) so we don't have to
        // pre-allocate the ByValArrays for by-ref marshalling.
        internal const int CAPA_BUF_SIZE = 8192;   // >= sizeof(either CAPA struct)
        internal const int CAPA_NUMOFVAL_OFF = 0;  // Capa_Enum.NumOfVal (ushort)
        internal const int CAPA_SUPPORTVAL_OFF = 4; // Capa_Enum.SupportVal[0] (int)

        // Shutter sentinels to skip when decoding the supported list.
        internal const uint SS_BULB = 0xFFFFFFFF;
        internal const uint SS_UNKNOWN = 0x0FFFFFFE;
        internal const uint SS_AUTO = 0x0FFFFFFF;

        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_SS_Get_Capability(IntPtr pSS_CapaInfo, out uint retError);
        [DllImport(DLLNAME, ExactSpelling = true, CallingConvention = CC)]
        internal static extern byte LMX_func_api_ISO_Get_Capability(IntPtr pIsoCapaInfo, out uint retError);
    }
}
