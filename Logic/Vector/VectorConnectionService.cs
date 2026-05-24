using System.Collections.Generic;
using System.Linq;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// High-level factory for VectorBus.
    /// </summary>
    public static class VectorConnectionService
    {
        public static IReadOnlyList<VectorChannelConfig> DetectVectorCanChannels() =>
            VectorBus.GetChannelConfigs().Where(c => c.IsCan).ToList();

        public static void AddDtcCanApplicationToChannel(VectorChannelConfig ch)
        {
            VectorBus.SetApplicationConfig(
                DtcCanConstants.AppName,
                DtcCanConstants.AppChannel,
                ch.HwType,
                ch.HwIndex,
                ch.HwChannel);
        }

        public static VectorBus OpenDtcCanBus() =>
            new VectorBus(DtcCanConstants.AppName, DtcCanConstants.AppChannel, DtcCanConstants.Bitrate);

        public static VectorBus ConfigureAndOpen(VectorChannelConfig ch)
        {
            AddDtcCanApplicationToChannel(ch);
            return OpenDtcCanBus();
        }
    }
}
