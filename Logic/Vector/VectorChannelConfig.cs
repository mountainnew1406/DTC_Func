using vxlapi_NET;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// Friendly wrapper around XLClass.xl_channel_config.
    /// </summary>
    public sealed class VectorChannelConfig
    {
        public int    ChannelIndex    { get; }
        public ulong  ChannelMask     { get; }
        public string Name            { get; }
        public XLDefine.XL_HardwareType HwType { get; }
        public uint   HwIndex         { get; }
        public uint   HwChannel       { get; }
        public uint   SerialNumber    { get; }
        public XLDefine.XL_BusTypes ConnectedBusType { get; }
        public string TransceiverName { get; }

        public bool IsCan =>
            (ConnectedBusType & XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN) != 0;

        public VectorChannelConfig(XLClass.xl_channel_config native, int index)
        {
            ChannelIndex    = index;
            ChannelMask     = native.channelMask;
            Name            = native.name;
            HwType          = native.hwType;
            HwIndex         = native.hwIndex;
            HwChannel       = native.hwChannel;
            SerialNumber    = native.serialNumber;
            ConnectedBusType = native.connectedBusType;
            TransceiverName = native.transceiverName;
        }

        public override string ToString() =>
            $"[{ChannelIndex}] {Name} | {HwType}({HwIndex},{HwChannel}) | S/N:{SerialNumber} | CAN:{IsCan}";
    }
}
