using System;
using System.Runtime.InteropServices;

namespace AegisWall.Core
{
    internal static class WfpNative
    {
        public const uint RPC_C_AUTHN_WINNT = 10;
        public const uint ERROR_SUCCESS = 0;
        public const uint ERROR_ACCESS_DENIED = 5;
        public const uint FWP_E_FILTER_NOT_FOUND = 0x80320003;
        public const uint FWP_E_SUBLAYER_NOT_FOUND = 0x80320007;
        public const uint FWP_E_ALREADY_EXISTS = 0x80320009;

        public const uint FWPM_FILTER_FLAG_PERSISTENT = 0x00000001;
        public const uint FWPM_SUBLAYER_FLAG_PERSISTENT = 0x00000001;

        public const uint FWP_ACTION_BLOCK = 0x00001001;
        public const uint FWP_ACTION_PERMIT = 0x00001002;

        public const uint FWP_MATCH_EQUAL = 0;

        public const uint FWP_UINT8 = 1;
        public const uint FWP_UINT16 = 2;
        public const uint FWP_UINT32 = 3;
        public const uint FWP_UINT64 = 4;
        public const uint FWP_BYTE_BLOB_TYPE = 12;
        public const uint FWP_V4_ADDR_MASK = 0x100;
        public const uint FWP_V6_ADDR_MASK = 0x101;

        public static readonly Guid LayerAleAuthConnectV4 = new Guid("c38d57d1-05a7-4c33-904f-7fbceee60e82");
        public static readonly Guid LayerAleAuthConnectV6 = new Guid("4a72393b-319f-44bc-84c3-ba54dcb3b6b4");
        public static readonly Guid LayerAleAuthRecvAcceptV4 = new Guid("e1cd9fe7-f4b5-4273-96c0-592e487b8650");
        public static readonly Guid LayerAleAuthRecvAcceptV6 = new Guid("a3b42c97-9f04-4672-b87e-cee9c483257f");

        public static readonly Guid ConditionAleAppId = new Guid("d78e1e87-8644-4ea5-9437-d809ecefc971");
        public static readonly Guid ConditionIpProtocol = new Guid("3971ef2b-623e-4f9a-8cb1-6e79b806b9a7");
        public static readonly Guid ConditionIpRemotePort = new Guid("c35a604d-d22b-4e1a-91b4-68f674ee674b");
        public static readonly Guid ConditionIpLocalPort = new Guid("0c1ba1af-5765-453f-af22-a8f791ac775b");
        public static readonly Guid ConditionIpRemoteAddress = new Guid("b235ae9a-1d64-49b8-a44c-5ff3d9095045");
        public static readonly Guid ConditionIpLocalAddress = new Guid("d9ee00de-c1ef-4617-bfe3-ffd8f5a08957");
        public static readonly Guid ConditionIpRemoteAddressV4 = new Guid("1febb610-3bcc-45e1-bc36-2e067e2cb186");
        public static readonly Guid ConditionIpRemoteAddressV6 = new Guid("246e1d8c-8bee-4018-9b98-31d4582f3361");
        public static readonly Guid ConditionIpLocalAddressV4 = new Guid("03a629cb-6e52-49f8-9c41-5709633c09cf");
        public static readonly Guid ConditionIpLocalAddressV6 = new Guid("2381be84-7524-45b3-a05b-1e637d9c7a6a");

        [StructLayout(LayoutKind.Sequential)]
        public struct FWP_BYTE_BLOB
        {
            public uint size;
            public IntPtr data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FWPM_DISPLAY_DATA0
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string name;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string description;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct FWP_VALUE0
        {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(8)]
            public IntPtr pointer;
            [FieldOffset(8)]
            public byte uint8;
            [FieldOffset(8)]
            public ushort uint16;
            [FieldOffset(8)]
            public uint uint32;
            [FieldOffset(8)]
            public ulong uint64;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FWP_V4_ADDR_AND_MASK
        {
            public uint addr;
            public uint mask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FWP_V6_ADDR_AND_MASK
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] addr;
            public byte prefixLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FWPM_ACTION0
        {
            public uint type;
            public Guid filterType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FWPM_FILTER_CONDITION0
        {
            public Guid fieldKey;
            public uint matchType;
            public FWP_VALUE0 conditionValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FWPM_SUBLAYER0
        {
            public Guid subLayerKey;
            public FWPM_DISPLAY_DATA0 displayData;
            public uint flags;
            public IntPtr providerKey;
            public FWP_BYTE_BLOB providerData;
            public ushort weight;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct FWPM_FILTER_CONTEXT_UNION
        {
            [FieldOffset(0)]
            public ulong rawContext;
            [FieldOffset(0)]
            public Guid providerContextKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FWPM_FILTER0
        {
            public Guid filterKey;
            public FWPM_DISPLAY_DATA0 displayData;
            public uint flags;
            public IntPtr providerKey;
            public FWP_BYTE_BLOB providerData;
            public Guid layerKey;
            public Guid subLayerKey;
            public FWP_VALUE0 weight;
            public uint numFilterConditions;
            public IntPtr filterCondition;
            public FWPM_ACTION0 action;
            public FWPM_FILTER_CONTEXT_UNION context;
            public IntPtr reserved;
            public ulong filterId;
            public FWP_VALUE0 effectiveWeight;
        }

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmEngineOpen0(
            IntPtr serverName,
            uint authnService,
            IntPtr authIdentity,
            IntPtr session,
            out IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmEngineClose0(IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmTransactionBegin0(IntPtr engineHandle, uint flags);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmTransactionCommit0(IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmTransactionAbort0(IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmSubLayerAdd0(
            IntPtr engineHandle,
            ref FWPM_SUBLAYER0 subLayer,
            IntPtr sd);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmSubLayerDeleteByKey0(
            IntPtr engineHandle,
            ref Guid key);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmFilterAdd0(
            IntPtr engineHandle,
            ref FWPM_FILTER0 filter,
            IntPtr sd,
            out ulong id);

        [DllImport("fwpuclnt.dll")]
        public static extern uint FwpmFilterDeleteByKey0(
            IntPtr engineHandle,
            ref Guid key);

        [DllImport("fwpuclnt.dll", CharSet = CharSet.Unicode)]
        public static extern uint FwpmGetAppIdFromFileName0(
            string fileName,
            out IntPtr appId);

        [DllImport("fwpuclnt.dll")]
        public static extern void FwpmFreeMemory0(ref IntPtr p);
    }

    public sealed class WfpException : Exception
    {
        public uint Code { get; private set; }

        public WfpException(string message, uint code) : base(message + " (0x" + code.ToString("X8") + ")")
        {
            Code = code;
        }
    }
}
