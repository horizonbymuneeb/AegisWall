using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace AegisWall.Core
{
    public sealed class WfpEngine : IDisposable
    {
        private const int MaxFilterSlots = 512;
        private const uint SublayerWeight = 0x8000;
        private readonly Guid subLayerKey = new Guid("9c1f6a2e-7b3d-4e5f-8a90-1b2c3d4e5f60");
        private IntPtr engineHandle = IntPtr.Zero;
        private int nextSlot;

        public bool IsOpen
        {
            get { return engineHandle != IntPtr.Zero; }
        }

        public int LastFilterCount { get; private set; }

        public void Open()
        {
            if (IntPtr.Size != 8)
            {
                throw new WfpException("AegisWall requires a 64-bit process.", 0);
            }

            if (IsOpen)
            {
                return;
            }

            uint result = WfpNative.FwpmEngineOpen0(IntPtr.Zero, WfpNative.RPC_C_AUTHN_WINNT, IntPtr.Zero, IntPtr.Zero, out engineHandle);
            if (result != WfpNative.ERROR_SUCCESS)
            {
                engineHandle = IntPtr.Zero;
                throw new WfpException("Could not open the Windows Filtering Platform engine. Run as administrator.", result);
            }
        }

        public void Close()
        {
            if (engineHandle != IntPtr.Zero)
            {
                try { WfpNative.FwpmEngineClose0(engineHandle); } catch { }
                engineHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Close();
        }

        public void Apply(AegisConfig config, bool armed, bool lockdown, bool keyMissing, bool configVerified)
        {
            Open();
            if (!armed)
            {
                RemoveAll();
                return;
            }

            nextSlot = 0;
            BeginTransaction();
            try
            {
                EnsureSublayer();
                ResetFilterSlots();

                AddBlockAll(WfpNative.LayerAleAuthConnectV4, 0, "AegisWall: default deny outbound v4");
                AddBlockAll(WfpNative.LayerAleAuthConnectV6, 0, "AegisWall: default deny outbound v6");
                AddBlockAll(WfpNative.LayerAleAuthRecvAcceptV4, 0, "AegisWall: default deny inbound v4");
                AddBlockAll(WfpNative.LayerAleAuthRecvAcceptV6, 0, "AegisWall: default deny inbound v6");

                if (config.AllowLoopback)
                {
                    AddLoopback(WfpNative.LayerAleAuthConnectV4, 15, "AegisWall: allow loopback outbound v4", false);
                    AddLoopback(WfpNative.LayerAleAuthConnectV6, 15, "AegisWall: allow loopback outbound v6", true);
                    AddLoopback(WfpNative.LayerAleAuthRecvAcceptV4, 15, "AegisWall: allow loopback inbound v4", false);
                    AddLoopback(WfpNative.LayerAleAuthRecvAcceptV6, 15, "AegisWall: allow loopback inbound v6", true);
                }

                if (config.AllowDhcp)
                {
                    AddDhcp(WfpNative.LayerAleAuthConnectV4, 15, "AegisWall: allow dhcp outbound v4", false, true);
                    AddDhcp(WfpNative.LayerAleAuthRecvAcceptV4, 15, "AegisWall: allow dhcp inbound v4", false, false);
                    AddDhcp(WfpNative.LayerAleAuthConnectV6, 15, "AegisWall: allow dhcp outbound v6", true, true);
                    AddDhcp(WfpNative.LayerAleAuthRecvAcceptV6, 15, "AegisWall: allow dhcp inbound v6", true, false);
                }

                if (config.AllowSystemDns)
                {
                    AddSystemDns(WfpNative.LayerAleAuthConnectV4, 10, "AegisWall: allow system dns v4", false, config);
                    AddSystemDns(WfpNative.LayerAleAuthConnectV6, 10, "AegisWall: allow system dns v6", true, config);
                }

                if (configVerified && !keyMissing)
                {
                    foreach (AllowedApp app in config.AllowedApps)
                    {
                        if (!app.Enabled)
                        {
                            continue;
                        }

                        string path = PathUtil.Normalize(app.Path);
                        if (string.IsNullOrEmpty(path) || !File.Exists(path))
                        {
                            continue;
                        }

                        if (app.Outbound)
                        {
                            AddAppFilter(WfpNative.LayerAleAuthConnectV4, 10, "AegisWall: allow " + app.Name + " out v4", path);
                            AddAppFilter(WfpNative.LayerAleAuthConnectV6, 10, "AegisWall: allow " + app.Name + " out v6", path);
                        }
                        if (app.Inbound)
                        {
                            AddAppFilter(WfpNative.LayerAleAuthRecvAcceptV4, 10, "AegisWall: allow " + app.Name + " in v4", path);
                            AddAppFilter(WfpNative.LayerAleAuthRecvAcceptV6, 10, "AegisWall: allow " + app.Name + " in v6", path);
                        }
                    }
                }

                if (lockdown || keyMissing)
                {
                    AddBlockAll(WfpNative.LayerAleAuthConnectV4, 14, "AegisWall: lockdown outbound v4");
                    AddBlockAll(WfpNative.LayerAleAuthConnectV6, 14, "AegisWall: lockdown outbound v6");
                    AddBlockAll(WfpNative.LayerAleAuthRecvAcceptV4, 14, "AegisWall: lockdown inbound v4");
                    AddBlockAll(WfpNative.LayerAleAuthRecvAcceptV6, 14, "AegisWall: lockdown inbound v6");
                }

                CommitTransaction();
                LastFilterCount = nextSlot;
            }
            catch
            {
                AbortTransaction();
                throw;
            }
        }

        public void RemoveAll()
        {
            Open();
            BeginTransaction();
            try
            {
                ResetFilterSlots();
                Guid key = subLayerKey;
                uint result = WfpNative.FwpmSubLayerDeleteByKey0(engineHandle, ref key);
                if (result != WfpNative.ERROR_SUCCESS && result != WfpNative.FWP_E_SUBLAYER_NOT_FOUND)
                {
                    throw new WfpException("Could not delete AegisWall WFP sublayer", result);
                }
                CommitTransaction();
                LastFilterCount = 0;
            }
            catch
            {
                AbortTransaction();
                throw;
            }
        }

        private void BeginTransaction()
        {
            uint result = WfpNative.FwpmTransactionBegin0(engineHandle, 0);
            if (result != WfpNative.ERROR_SUCCESS)
            {
                throw new WfpException("Could not begin WFP transaction", result);
            }
        }

        private void CommitTransaction()
        {
            uint result = WfpNative.FwpmTransactionCommit0(engineHandle);
            if (result != WfpNative.ERROR_SUCCESS)
            {
                throw new WfpException("Could not commit WFP transaction", result);
            }
        }

        private void AbortTransaction()
        {
            try { WfpNative.FwpmTransactionAbort0(engineHandle); } catch { }
        }

        private void EnsureSublayer()
        {
            WfpNative.FWPM_SUBLAYER0 sublayer = new WfpNative.FWPM_SUBLAYER0();
            sublayer.subLayerKey = subLayerKey;
            sublayer.displayData.name = "AegisWall";
            sublayer.displayData.description = "AegisWall hardware-anchored application firewall";
            sublayer.flags = WfpNative.FWPM_SUBLAYER_FLAG_PERSISTENT;
            sublayer.providerKey = IntPtr.Zero;
            sublayer.providerData.size = 0;
            sublayer.providerData.data = IntPtr.Zero;
            sublayer.weight = (ushort)SublayerWeight;

            uint result = WfpNative.FwpmSubLayerAdd0(engineHandle, ref sublayer, IntPtr.Zero);
            if (result != WfpNative.ERROR_SUCCESS && result != WfpNative.FWP_E_ALREADY_EXISTS)
            {
                throw new WfpException("Could not add AegisWall WFP sublayer", result);
            }
        }

        private void ResetFilterSlots()
        {
            for (int i = 0; i < MaxFilterSlots; i++)
            {
                Guid key = MakeKey(i);
                uint result = WfpNative.FwpmFilterDeleteByKey0(engineHandle, ref key);
                if (result != WfpNative.ERROR_SUCCESS && result != WfpNative.FWP_E_FILTER_NOT_FOUND)
                {
                    throw new WfpException("Could not delete WFP filter slot " + i, result);
                }
            }
        }

        private Guid MakeKey(int index)
        {
            byte[] hash = Crypto.Sha256(Encoding.UTF8.GetBytes("AegisWall/filter/" + index));
            byte[] guidBytes = new byte[16];
            Buffer.BlockCopy(hash, 0, guidBytes, 0, 16);
            return new Guid(guidBytes);
        }

        private void AddBlockAll(Guid layer, byte weight, string name)
        {
            FilterSpec spec = new FilterSpec();
            spec.Layer = layer;
            spec.Weight = weight;
            spec.Action = WfpNative.FWP_ACTION_BLOCK;
            spec.Name = name;
            AddFilter(spec);
        }

        private void AddLoopback(Guid layer, byte weight, string name, bool v6)
        {
            FilterSpec spec = new FilterSpec();
            spec.Layer = layer;
            spec.Weight = weight;
            spec.Action = WfpNative.FWP_ACTION_PERMIT;
            spec.Name = name;

            if (v6)
            {
                byte[] loopback = new byte[16];
                loopback[15] = 1;
                AddV6AddressCondition(spec, WfpNative.ConditionIpRemoteAddressV6, loopback, 128);
            }
            else
            {
                AddV4AddressCondition(spec, WfpNative.ConditionIpRemoteAddressV4, 0x7F000001, 0xFFFFFFFF);
            }

            AddFilter(spec);
        }

        private void AddDhcp(Guid layer, byte weight, string name, bool v6, bool outbound)
        {
            FilterSpec spec = new FilterSpec();
            spec.Layer = layer;
            spec.Weight = weight;
            spec.Action = WfpNative.FWP_ACTION_PERMIT;
            spec.Name = name;
            AddUInt8Condition(spec, WfpNative.ConditionIpProtocol, 17);
            AddUInt16Condition(spec, WfpNative.ConditionIpLocalPort, v6 ? (ushort)546 : (ushort)68);
            AddUInt16Condition(spec, WfpNative.ConditionIpRemotePort, v6 ? (ushort)547 : (ushort)67);
            AddFilter(spec);
        }

        private void AddSystemDns(Guid layer, byte weight, string name, bool v6, AegisConfig config)
        {
            string svchost = Path.Combine(PathUtil.WindowsDirectory, "System32", "svchost.exe");
            if (!File.Exists(svchost))
            {
                return;
            }

            bool pinned = string.Equals(config.DnsMode, "Pinned", StringComparison.OrdinalIgnoreCase)
                && config.DnsServers != null
                && config.DnsServers.Count > 0;

            if (!pinned)
            {
                AddDnsFilter(layer, weight, name + " udp", svchost, 17, null, v6);
                AddDnsFilter(layer, weight, name + " tcp", svchost, 6, null, v6);
                return;
            }

            bool added = false;
            foreach (string serverText in config.DnsServers)
            {
                IPAddress server;
                if (!IPAddress.TryParse(serverText, out server))
                {
                    continue;
                }
                bool serverV6 = server.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
                if (serverV6 != v6)
                {
                    continue;
                }
                AddDnsFilter(layer, weight, name + " udp " + serverText, svchost, 17, server, v6);
                AddDnsFilter(layer, weight, name + " tcp " + serverText, svchost, 6, server, v6);
                added = true;
            }

            if (!added)
            {
                // Pinned mode with no matching family: fail closed for DNS.
                AuditLog.Append("dns.pinned.empty", "No " + (v6 ? "IPv6" : "IPv4") + " DNS server configured", "service");
            }
        }

        private void AddDnsFilter(Guid layer, byte weight, string name, string path, byte protocol, IPAddress server, bool v6)
        {
            FilterSpec spec = new FilterSpec();
            spec.Layer = layer;
            spec.Weight = weight;
            spec.Action = WfpNative.FWP_ACTION_PERMIT;
            spec.Name = name;
            if (!AddAppIdCondition(spec, path))
            {
                return;
            }
            AddUInt8Condition(spec, WfpNative.ConditionIpProtocol, protocol);
            AddUInt16Condition(spec, WfpNative.ConditionIpRemotePort, 53);
            if (server != null)
            {
                byte[] bytes = server.GetAddressBytes();
                if (v6)
                {
                    AddV6AddressCondition(spec, WfpNative.ConditionIpRemoteAddressV6, bytes, 128);
                }
                else
                {
                    uint address = BitConverter.ToUInt32(bytes, 0);
                    AddV4AddressCondition(spec, WfpNative.ConditionIpRemoteAddressV4, address, 0xFFFFFFFF);
                }
            }
            AddFilter(spec);
        }

        private void AddAppFilter(Guid layer, byte weight, string name, string path)
        {
            FilterSpec spec = new FilterSpec();
            spec.Layer = layer;
            spec.Weight = weight;
            spec.Action = WfpNative.FWP_ACTION_PERMIT;
            spec.Name = name;
            if (!AddAppIdCondition(spec, path))
            {
                return;
            }
            AddFilter(spec);
        }

        private bool AddAppIdCondition(FilterSpec spec, string path)
        {
            IntPtr appId = IntPtr.Zero;
            uint result = WfpNative.FwpmGetAppIdFromFileName0(path, out appId);
            if (result != WfpNative.ERROR_SUCCESS || appId == IntPtr.Zero)
            {
                return false;
            }

            WfpNative.FWPM_FILTER_CONDITION0 condition = new WfpNative.FWPM_FILTER_CONDITION0();
            condition.fieldKey = WfpNative.ConditionAleAppId;
            condition.matchType = WfpNative.FWP_MATCH_EQUAL;
            condition.conditionValue.type = WfpNative.FWP_BYTE_BLOB_TYPE;
            condition.conditionValue.pointer = appId;
            spec.Conditions.Add(condition);
            spec.WfpAllocations.Add(appId);
            return true;
        }

        private void AddUInt8Condition(FilterSpec spec, Guid field, byte value)
        {
            WfpNative.FWPM_FILTER_CONDITION0 condition = new WfpNative.FWPM_FILTER_CONDITION0();
            condition.fieldKey = field;
            condition.matchType = WfpNative.FWP_MATCH_EQUAL;
            condition.conditionValue.type = WfpNative.FWP_UINT8;
            condition.conditionValue.uint8 = value;
            spec.Conditions.Add(condition);
        }

        private void AddUInt16Condition(FilterSpec spec, Guid field, ushort value)
        {
            WfpNative.FWPM_FILTER_CONDITION0 condition = new WfpNative.FWPM_FILTER_CONDITION0();
            condition.fieldKey = field;
            condition.matchType = WfpNative.FWP_MATCH_EQUAL;
            condition.conditionValue.type = WfpNative.FWP_UINT16;
            condition.conditionValue.uint16 = value;
            spec.Conditions.Add(condition);
        }

        private void AddUInt32Condition(FilterSpec spec, Guid field, uint value)
        {
            WfpNative.FWPM_FILTER_CONDITION0 condition = new WfpNative.FWPM_FILTER_CONDITION0();
            condition.fieldKey = field;
            condition.matchType = WfpNative.FWP_MATCH_EQUAL;
            condition.conditionValue.type = WfpNative.FWP_UINT32;
            condition.conditionValue.uint32 = value;
            spec.Conditions.Add(condition);
        }

        private void AddV4AddressCondition(FilterSpec spec, Guid field, uint address, uint mask)
        {
            WfpNative.FWP_V4_ADDR_AND_MASK value = new WfpNative.FWP_V4_ADDR_AND_MASK();
            value.addr = address;
            value.mask = mask;
            IntPtr pointer = MarshalAlloc(value);
            WfpNative.FWPM_FILTER_CONDITION0 condition = new WfpNative.FWPM_FILTER_CONDITION0();
            condition.fieldKey = field;
            condition.matchType = WfpNative.FWP_MATCH_EQUAL;
            condition.conditionValue.type = WfpNative.FWP_V4_ADDR_MASK;
            condition.conditionValue.pointer = pointer;
            spec.Conditions.Add(condition);
            spec.HGlobalAllocations.Add(pointer);
        }

        private void AddV6AddressCondition(FilterSpec spec, Guid field, byte[] address, byte prefixLength)
        {
            WfpNative.FWP_V6_ADDR_AND_MASK value = new WfpNative.FWP_V6_ADDR_AND_MASK();
            value.addr = address;
            value.prefixLength = prefixLength;
            IntPtr pointer = MarshalAlloc(value);
            WfpNative.FWPM_FILTER_CONDITION0 condition = new WfpNative.FWPM_FILTER_CONDITION0();
            condition.fieldKey = field;
            condition.matchType = WfpNative.FWP_MATCH_EQUAL;
            condition.conditionValue.type = WfpNative.FWP_V6_ADDR_MASK;
            condition.conditionValue.pointer = pointer;
            spec.Conditions.Add(condition);
            spec.HGlobalAllocations.Add(pointer);
        }

        private static IntPtr MarshalAlloc<T>(T value)
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
            IntPtr pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            System.Runtime.InteropServices.Marshal.StructureToPtr(value, pointer, false);
            return pointer;
        }

        private void AddFilter(FilterSpec spec)
        {
            if (nextSlot >= MaxFilterSlots)
            {
                throw new WfpException("AegisWall filter limit reached (" + MaxFilterSlots + "). Remove unused apps.", 0);
            }

            int conditionSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(WfpNative.FWPM_FILTER_CONDITION0));
            IntPtr conditionsPointer = IntPtr.Zero;
            try
            {
                if (spec.Conditions.Count > 0)
                {
                    conditionsPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(conditionSize * spec.Conditions.Count);
                    for (int i = 0; i < spec.Conditions.Count; i++)
                    {
                        IntPtr target = new IntPtr(conditionsPointer.ToInt64() + (conditionSize * i));
                        System.Runtime.InteropServices.Marshal.StructureToPtr(spec.Conditions[i], target, false);
                    }
                }

                WfpNative.FWPM_FILTER0 filter = new WfpNative.FWPM_FILTER0();
                filter.filterKey = MakeKey(nextSlot);
                filter.displayData.name = spec.Name;
                filter.displayData.description = "AegisWall";
                filter.flags = WfpNative.FWPM_FILTER_FLAG_PERSISTENT;
                filter.providerKey = IntPtr.Zero;
                filter.providerData.size = 0;
                filter.providerData.data = IntPtr.Zero;
                filter.layerKey = spec.Layer;
                filter.subLayerKey = subLayerKey;
                filter.weight.type = WfpNative.FWP_UINT8;
                filter.weight.uint8 = spec.Weight;
                filter.numFilterConditions = (uint)spec.Conditions.Count;
                filter.filterCondition = conditionsPointer;
                filter.action.type = spec.Action;
                filter.action.filterType = Guid.Empty;
                filter.context.rawContext = 0;
                filter.reserved = IntPtr.Zero;
                filter.filterId = 0;
                filter.effectiveWeight.type = 0;

                ulong id;
                uint result = WfpNative.FwpmFilterAdd0(engineHandle, ref filter, IntPtr.Zero, out id);
                if (result != WfpNative.ERROR_SUCCESS)
                {
                    throw new WfpException("Could not add WFP filter '" + spec.Name + "'", result);
                }

                nextSlot++;
            }
            finally
            {
                if (conditionsPointer != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(conditionsPointer);
                }
                foreach (IntPtr pointer in spec.HGlobalAllocations)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);
                }
                foreach (IntPtr pointer in spec.WfpAllocations)
                {
                    IntPtr temp = pointer;
                    try { WfpNative.FwpmFreeMemory0(ref temp); } catch { }
                }
            }
        }

        private sealed class FilterSpec
        {
            public Guid Layer;
            public byte Weight;
            public uint Action;
            public string Name;
            public List<WfpNative.FWPM_FILTER_CONDITION0> Conditions = new List<WfpNative.FWPM_FILTER_CONDITION0>();
            public List<IntPtr> WfpAllocations = new List<IntPtr>();
            public List<IntPtr> HGlobalAllocations = new List<IntPtr>();
        }
    }
}
