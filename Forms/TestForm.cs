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

            btnGetChannels.Click  += BtnGetChannels_Click;
            btnSetAppConfig.Click += BtnSetAppConfig_Click;
            btnOpenBus.Click      += BtnOpenBus_Click;
            btnCloseBus.Click     += BtnCloseBus_Click;
            btnSendFrame.Click    += BtnSendFrame_Click;
            btnRecvFrame.Click    += BtnRecvFrame_Click;
            btnReadDtc.Click      += BtnReadDtc_Click;
            btnReadVin.Click      += BtnReadVin_Click;
            btnClear.Click        += (s, e) => txtLog.Clear();
            FormClosing           += (s, e) => _bus?.Dispose();
        }

        // ── STEP 1: Enumerate channels ────────────────────────────────────────
        private void BtnGetChannels_Click(object sender, EventArgs e)
        {
            Try("Get Channels", () =>
            {
                _channels = VectorBus.GetChannelConfigs();
                cmbChannels.Items.Clear();

                foreach (var ch in _channels)
                {
                    cmbChannels.Items.Add(ch.ToString());
                    Log($"  {ch}");
                }

                if (cmbChannels.Items.Count > 0)
                    cmbChannels.SelectedIndex = 0;

                Log($"Found {_channels.Count} channel(s) total.");
            });
        }

        // ── STEP 2: Register app config to selected channel ──────────────────
        private void BtnSetAppConfig_Click(object sender, EventArgs e)
        {
            Try("Set App Config", () =>
            {
                var ch = GetSelectedChannel();
                VectorConnectionService.AddDtcCanApplicationToChannel(ch);
                Log($"App '{DtcCanConstants.AppName}:{DtcCanConstants.AppChannel}' mapped to → {ch}");
            });
        }

        // ── STEP 3: Open bus ─────────────────────────────────────────────────
        private void BtnOpenBus_Click(object sender, EventArgs e)
        {
            Try("Open Bus", () =>
            {
                _bus?.Dispose();
                _bus = VectorConnectionService.OpenDtcCanBus();
                SetBusState(true);
                Log($"Bus opened — {DtcCanConstants.Bitrate / 1000} kbit/s");
            });
        }

        // ── Close bus ─────────────────────────────────────────────────────────
        private void BtnCloseBus_Click(object sender, EventArgs e)
        {
            Try("Close Bus", () =>
            {
                _bus?.Dispose();
                _bus = null;
                SetBusState(false);
                Log("Bus closed.");
            });
        }

        // ── Send test frame (raw) ─────────────────────────────────────────────
        private void BtnSendFrame_Click(object sender, EventArgs e)
        {
            Try("Send Frame", () =>
            {
                RequireBus();
                uint txId = ParseId(txtRequestId.Text);
                // Single byte ping: 0x02 10 03 (DiagnosticSessionControl default)
                var msg = new CanMessage(txId, new byte[] { 0x02, 0x10, 0x03, 0, 0, 0, 0, 0 });
                _bus.Send(msg);
                Log($"TX → ID=0x{txId:X3}  {BitConverter.ToString(msg.Data)}");
            });
        }

        // ── Recv one frame ────────────────────────────────────────────────────
        private void BtnRecvFrame_Click(object sender, EventArgs e)
        {
            Try("Recv Frame", () =>
            {
                RequireBus();
                var msg = _bus.Recv(2000);
                if (msg == null)
                    Log("RX ← (timeout — no frame received in 2 s)");
                else
                    Log($"RX ← ID=0x{msg.ArbitrationId:X3}  {BitConverter.ToString(msg.Data)}  Ext={msg.IsExtendedId}");
            });
        }

        // ── Read DTC (UDS 19 02 09) ──────────────────────────────────────────
        private void BtnReadDtc_Click(object sender, EventArgs e)
        {
            Try("Read DTC", () =>
            {
                RequireBus();
                uint reqId  = ParseId(txtRequestId.Text);
                uint respId = ParseId(txtResponseId.Text);

                var isoTp  = new IsoTpClient(_bus);
                var uds    = new UdsClient(isoTp);
                byte[] raw = uds.ReadDtcByStatusMask(reqId, respId, 0x09);
                Log($"DTC response raw: {BitConverter.ToString(raw)}");

                var records = DtcDecoder.ParseReadDtcResponse(raw);
                if (records.Count == 0)
                {
                    Log("  → No DTCs stored.");
                }
                else
                {
                    Log($"  → {records.Count} DTC(s):");
                    foreach (var r in records)
                        Log($"     {r.Code}  Status=0x{r.Status:X2}");
                }
            });
        }

        // ── Read VIN (UDS 22 F1 90) ──────────────────────────────────────────
        private void BtnReadVin_Click(object sender, EventArgs e)
        {
            Try("Read VIN", () =>
            {
                RequireBus();
                uint reqId  = ParseId(txtRequestId.Text);
                uint respId = ParseId(txtResponseId.Text);

                var isoTp = new IsoTpClient(_bus);
                var uds   = new UdsClient(isoTp);
                string vin = uds.ReadVin(reqId, respId);
                Log($"VIN: {vin}");
            });
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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
                throw new InvalidOperationException("Bus is not open. Run Steps 1-3 first.");
        }

        private static uint ParseId(string text)
        {
            text = text.Trim().TrimStart('0', 'x', 'X');
            return Convert.ToUInt32(text, 16);
        }

        private void SetBusState(bool open)
        {
            btnOpenBus.Enabled    = !open;
            btnSetAppConfig.Enabled = !open;
            btnCloseBus.Enabled   = open;
            btnSendFrame.Enabled  = open;
            btnRecvFrame.Enabled  = open;
            btnReadDtc.Enabled    = open;
            btnReadVin.Enabled    = open;
            lblStatus.Text        = open ? "Bus OPEN" : "Bus CLOSED";
            lblStatus.ForeColor   = open ? Color.LimeGreen : Color.Gray;
        }

        private void Try(string action, Action body)
        {
            SetStatus($"{action}...");
            try
            {
                Log($"\n▶ {action}");
                body();
                SetStatus($"{action} — OK");
            }
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
