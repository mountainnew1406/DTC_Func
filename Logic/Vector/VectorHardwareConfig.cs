using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using vxlapi_NET;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// Persists the selected Vector hardware channel to a local JSON file.
    /// Solves the problem that XL_GetApplConfig reads Windows Registry —
    /// which is empty on first run or after hardware changes.
    ///
    /// Workflow:
    ///   1. AutoDetectOrLoad()  → returns saved config or best-match from live hardware
    ///   2. Apply()             → calls XL_SetApplConfig to write Registry + saves JSON
    ///   3. VectorBus ctor      → XL_GetApplConfig reads Registry → success
    /// </summary>
    public sealed class VectorHardwareConfig
    {
        // ── Saved fields ─────────────────────────────────────────────────────
        public string AppName    { get; set; }
        public int    AppChannel { get; set; }
        public int    HwType     { get; set; }    // XL_HardwareType numeric value
        public uint   HwIndex    { get; set; }
        public uint   HwChannel  { get; set; }
        public uint   SerialNumber { get; set; }  // for change-detection
        public string ChannelName  { get; set; }  // human-readable, for display
        public int    Bitrate      { get; set; }

        // ── Static path ──────────────────────────────────────────────────────
        private static string ConfigPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vector_config.json");

        // ─────────────────────────────────────────────────────────────────────
        // Main entry point
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tries to return a valid hardware config by:
        ///   1. Loading saved JSON and verifying the hardware is still present
        ///   2. If not found / hardware changed → auto-pick first available CAN channel
        /// Returns null if no CAN hardware is connected at all.
        /// </summary>
        public static VectorHardwareConfig AutoDetectOrLoad(
            string appName = DtcCanConstants.AppName,
            int    appChannel = DtcCanConstants.AppChannel,
            int    bitrate = DtcCanConstants.Bitrate)
        {
            // Get live hardware list
            IReadOnlyList<VectorChannelConfig> liveChannels;
            try { liveChannels = VectorBus.GetChannelConfigs(); }
            catch { return null; }

            var canChannels = liveChannels.Where(c => c.IsCan).ToList();
            if (canChannels.Count == 0) return null;

            // Try to match saved config against live hardware
            var saved = TryLoad();
            if (saved != null)
            {
                var match = canChannels.FirstOrDefault(c =>
                    c.SerialNumber == saved.SerialNumber &&
                    c.HwChannel    == saved.HwChannel);

                if (match != null)
                {
                    // Hardware still present — update indices (may have changed on re-plug)
                    saved.HwType    = (int)match.HwType;
                    saved.HwIndex   = match.HwIndex;
                    saved.HwChannel = match.HwChannel;
                    saved.AppName   = appName;
                    saved.AppChannel = appChannel;
                    return saved;
                }
            }

            // No saved config or hardware changed → auto-pick first CAN channel
            var first = canChannels[0];
            return new VectorHardwareConfig
            {
                AppName     = appName,
                AppChannel  = appChannel,
                HwType      = (int)first.HwType,
                HwIndex     = first.HwIndex,
                HwChannel   = first.HwChannel,
                SerialNumber = first.SerialNumber,
                ChannelName = first.Name,
                Bitrate     = bitrate
            };
        }

        /// <summary>
        /// Writes config to Windows Registry (via XL_SetApplConfig) and saves JSON.
        /// Must be called before opening a VectorBus.
        /// </summary>
        public void Apply()
        {
            VectorBus.SetApplicationConfig(
                AppName, AppChannel,
                (XLDefine.XL_HardwareType)HwType,
                HwIndex, HwChannel);

            Save();
        }

        // ─────────────────────────────────────────────────────────────────────
        // JSON persistence
        // ─────────────────────────────────────────────────────────────────────

        public void Save()
        {
            try
            {
                File.WriteAllText(ConfigPath,
                    JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { /* non-fatal */ }
        }

        public static VectorHardwareConfig TryLoad()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;
                return JsonConvert.DeserializeObject<VectorHardwareConfig>(
                    File.ReadAllText(ConfigPath));
            }
            catch { return null; }
        }

        public static bool HasSavedConfig() => File.Exists(ConfigPath);

        public override string ToString() =>
            $"{ChannelName ?? "?"} | S/N:{SerialNumber} | HwCh:{HwChannel} | {Bitrate / 1000} kbit/s";
    }
}
