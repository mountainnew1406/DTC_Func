using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VectorCanTest.Logic.Vector;

namespace VectorCanTest.Forms
{
    public partial class TestForm : Form
    {
        private VectorBus _bus;
        private IReadOnlyList<VectorChannelConfig> _channels;

        public TestForm()
        {
            InitializeComponent();
            SetBusState(false);

            btnGetChannels.Click   += BtnGetChannels_Click;
            btnSetAppConfig.Click  += BtnSetAppConfig_Click;
            btnAutoDetect.Click    += BtnAutoDetect_Click;
            btnOpenBus.Click       += BtnOpenBus_Click;
            btnCloseBus.Click      += BtnCloseBus_Click;
            btnSendFrame.Click     += BtnSendFrame_Click;
            btnRecvFrame.Click     += BtnRecvFrame_Click;
            btnReadDtc.Click       += BtnReadDtc_Click;
            btnReadVin.Click       += BtnReadVin_Click;
            btnClear.Click         += (s, e) => txtLog.Clear();
            FormClosing            += (s, e) => _bus?.Dispose();

            // On startup: try auto-detect silently
            TryAutoDetectOnStartup();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Startup auto-detect
        // ─────────────────────────────────────────────────────────────────────

        private void TryAutoDetectOnStartup()
        {
            try
            {
                if (VectorHardwareConfig.HasSavedConfig())
                {
                    var cfg = VectorHardwareConfig.TryLoad();
                    if (cfg != null)
                    {
                        Log($"▶ Saved config found: {cfg}");
                        Log("  → Click [Auto-Detect & Open] to connect, or run Steps 1-3 manually.");
                        lblStatus.Text = $"Saved: {cfg.ChannelName} (S/N {cfg.SerialNumber})";
                        return;
                    }
                }
                Log("▶ No saved config — click [1. Get Channels] or [Auto-Detect & Open].");
            }
            catch { /* non-fatal */ }
        }

        // ─────────────────────────────────────────────────────────────────────
        // AUTO-DETECT (single button — recommended flow)
        // ─────────────────────────────────────────────────────────────────────

        private void BtnAutoDetect_Click(object sender, EventArgs e)
        {
            Try("Auto-Detect & Open", () =>
            {
                _bus?.Dispose();
                _bus = null;

                // 1. Find best hardware (saved → verify live, else first CAN channel)
                var cfg = VectorHardwareConfig.AutoDetectOrLoad();
                if (cfg == null)
                    throw new InvalidOperationException(
                        "No CAN hardware detected. Check Vector Hardware Config and cable.");

                Log($"  Hardware : {cfg.ChannelName}");
                Log($"  S/N      : {cfg.SerialNumber}");
                Log($"  HwType   : {(vxlapi_NET.XLDefine.XL_HardwareType)cfg.HwType}");
                Log($"  HwIndex  : {cfg.HwIndex}  HwChannel: {cfg.HwChannel}");
                Log($"  Bitrate  : {cfg.Bitrate / 1000} kbit/s");

                // 2. Write to Registry + save JSON
                cfg.Apply();
                Log("  → Config applied (Registry + vector_config.json)");

                // 3. Open bus
                _bus = new VectorBus(cfg.AppName, cfg.AppChannel, cfg.Bitrate);
                SetBusState(true);
                Log("  → Bus opened.");
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // MANUAL flow (Steps 1-3)
        // ─────────────────────────────────────────────────────────────────────

        private void BtnGetChannels_Click(object sender, EventArgs e)
        {
            Try("Get Channels", () =>
            {
                _channels = VectorBus.GetChannelConfigs();
                cmbChannels.Items.Clear();

                foreach (var ch in _channels)
                {
                    cmbChannels.Items.Add(ch.ToString());
                    string mark = ch.IsCan ? "✔ CAN" : "  ---";
                    Log($"  [{ch.ChannelIndex}] {mark} | {ch.Name} | S/N:{ch.SerialNumber} | {(vxlapi_NET.XLDefine.XL_HardwareType)ch.HwType}");
                }

                if (cmbChannels.Items.Count > 0)
                    cmbChannels.SelectedIndex = 0;

                Log($"Found {_channels.Count} channel(s). Select a CAN channel then click [2. Set App Config].");
            });
        }

        private void BtnSetAppConfig_Click(object sender, EventArgs e)
        {
            Try("Set App Config", () =>
            {
                var ch = GetSelectedChannel();

                // Build config, apply to Registry and save JSON
                var cfg = new VectorHardwareConfig
                {
                    AppName      = DtcCanConstants.AppName,
                    AppChannel   = DtcCanConstants.AppChannel,
                    HwType       = (int)ch.HwType,
                    HwIndex      = ch.HwIndex,
                    HwChannel    = ch.HwChannel,
                    SerialNumber = ch.SerialNumber,
                    ChannelName  = ch.Name,
                    Bitrate      = DtcCanConstants.Bitrate
                };
                cfg.Apply();

                Log($"  App '{DtcCanConstants.AppName}:{DtcCanConstants.AppChannel}' → {ch}");
                Log($"  Saved to: {AppDomain.CurrentDomain.BaseDirectory}vector_config.json");
            });
        }

        private void BtnOpenBus_Click(object sender, EventArgs e)
        {
            Try("Open Bus", () =>
            {
                _bus?.Dispose();
                _bus = new VectorBus(
                    DtcCanConstants.AppName,
                    DtcCanConstants.AppChannel,
                    DtcCanConstants.Bitrate);
                SetBusState(true);
                Log($"  Bus opened — {DtcCanConstants.Bitrate / 1000} kbit/s");
            });
        }

        private void BtnCloseBus_Click(object sender, EventArgs e)
        {
            Try("Close Bus", () =>
            {
                _bus?.Dispose();
                _bus = null;
                SetBusState(false);
                Log("  Bus closed.");
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // TX / RX
        // ─────────────────────────────────────────────────────────────────────

        private void BtnSendFrame_Click(object sender, EventArgs e)
        {
            Try("Send Frame", () =>
            {
                RequireBus();
                uint txId = ParseId(txtRequestId.Text);
                var msg   = new CanMessage(txId, new byte[] { 0x02, 0x10, 0x03, 0, 0, 0, 0, 0 });
                _bus.Send(msg);
                Log($"  TX → ID=0x{txId:X3}  {BitConverter.ToString(msg.Data)}");
            });
        }

        private void BtnRecvFrame_Click(object sender, EventArgs e)
        {
            Try("Recv Frame", () =>
            {
                RequireBus();
                var msg = _bus.Recv(2000);
                if (msg == null)
                    Log("  RX ← (timeout — no frame in 2 s)");
                else
                    Log($"  RX ← ID=0x{msg.ArbitrationId:X3}  {BitConverter.ToString(msg.Data)}  Ext={msg.IsExtendedId}");
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // UDS / ISO-TP
        // ─────────────────────────────────────────────────────────────────────

        private void BtnReadDtc_Click(object sender, EventArgs e)
        {
            Try("Read DTC (19 02 09)", () =>
            {
                RequireBus();
                uint reqId  = ParseId(txtRequestId.Text);
                uint respId = ParseId(txtResponseId.Text);

                var isoTp  = new IsoTpClient(_bus);
                var uds    = new UdsClient(isoTp);
                byte[] raw = uds.ReadDtcByStatusMask(reqId, respId, 0x09);
                Log($"  Raw: {BitConverter.ToString(raw)}");

                var records = DtcDecoder.ParseReadDtcResponse(raw);
                if (records.Count == 0)
                    Log("  → No DTCs stored.");
                else
                {
                    Log($"  → {records.Count} DTC(s):");
                    foreach (var r in records)
                        Log($"     {r.Code}  Status=0x{r.Status:X2}  [{StatusBits(r.Status)}]");
                }
            });
        }

        private void BtnReadVin_Click(object sender, EventArgs e)
        {
            Try("Read VIN (22 F1 90)", () =>
            {
                RequireBus();
                uint reqId  = ParseId(txtRequestId.Text);
                uint respId = ParseId(txtResponseId.Text);

                var isoTp = new IsoTpClient(_bus);
                var uds   = new UdsClient(isoTp);
                string vin = uds.ReadVin(reqId, respId);
                Log($"  VIN: {vin}");
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private VectorChannelConfig GetSelectedChannel()
        {
            if (_channels == null || _channels.Count == 0)
                throw new InvalidOperationException("Run 'Get Channels' first.");
            int idx = cmbChannels.SelectedIndex;
            if (idx < 0) throw new InvalidOperationException("Select a channel.");
            return _channels[idx];
        }

        private void RequireBus()
        {
            if (_bus == null)
                throw new InvalidOperationException("Bus not open. Click [Auto-Detect & Open] or Steps 1-3.");
        }

        private static uint ParseId(string text)
        {
            text = text.Trim().TrimStart('0', 'x', 'X');
            return Convert.ToUInt32(text, 16);
        }

        private static string StatusBits(byte s)
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((s & 0x01) != 0) parts.Add("testFailed");
            if ((s & 0x02) != 0) parts.Add("testFailedThisOC");
            if ((s & 0x04) != 0) parts.Add("pendingDTC");
            if ((s & 0x08) != 0) parts.Add("confirmedDTC");
            if ((s & 0x10) != 0) parts.Add("testNotComp");
            if ((s & 0x20) != 0) parts.Add("testFailed since clear");
            if ((s & 0x40) != 0) parts.Add("warningIndicator");
            if ((s & 0x80) != 0) parts.Add("controlSupport");
            return parts.Count > 0 ? string.Join("|", parts) : "none";
        }

        private void SetBusState(bool open)
        {
            btnOpenBus.Enabled     = !open;
            btnSetAppConfig.Enabled = !open;
            btnCloseBus.Enabled    = open;
            btnSendFrame.Enabled   = open;
            btnRecvFrame.Enabled   = open;
            btnReadDtc.Enabled     = open;
            btnReadVin.Enabled     = open;
            lblStatus.Text         = open ? "● Bus OPEN" : "○ Bus CLOSED";
            lblStatus.ForeColor    = open ? Color.LimeGreen : Color.Gray;
        }

        private void Try(string action, Action body)
        {
            SetStatus($"{action}...");
            try { Log($"\n▶ {action}"); body(); SetStatus($"{action} — OK"); }
            catch (Exception ex)
            {
                LogError($"  ✖ {ex.GetType().Name}: {ex.Message}");
                SetStatus($"{action} — FAILED");
            }
        }

        private void Log(string msg)
        {
            if (InvokeRequired) { Invoke(new Action<string>(Log), msg); return; }
            txtLog.AppendText(msg + "\n");
            txtLog.ScrollToCaret();
        }

        private void LogError(string msg)
        {
            if (InvokeRequired) { Invoke(new Action<string>(LogError), msg); return; }
            int start = txtLog.TextLength;
            txtLog.AppendText(msg + "\n");
            txtLog.Select(start, msg.Length);
            txtLog.SelectionColor = Color.Tomato;
            txtLog.SelectionLength = 0;
            txtLog.ScrollToCaret();
        }

        private void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action<string>(SetStatus), msg); return; }
            lblStatus.Text = msg;
        }
    }
}
