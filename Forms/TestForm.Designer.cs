namespace VectorCanTest.Forms
{
    partial class TestForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.btnAutoDetect   = new System.Windows.Forms.Button();
            this.lblManual       = new System.Windows.Forms.Label();
            this.cmbChannels     = new System.Windows.Forms.ComboBox();
            this.btnGetChannels  = new System.Windows.Forms.Button();
            this.btnSetAppConfig = new System.Windows.Forms.Button();
            this.btnOpenBus      = new System.Windows.Forms.Button();
            this.btnCloseBus     = new System.Windows.Forms.Button();
            this.lblReqId        = new System.Windows.Forms.Label();
            this.lblRespId       = new System.Windows.Forms.Label();
            this.txtRequestId    = new System.Windows.Forms.TextBox();
            this.txtResponseId   = new System.Windows.Forms.TextBox();
            this.btnSendFrame    = new System.Windows.Forms.Button();
            this.btnRecvFrame    = new System.Windows.Forms.Button();
            this.btnReadDtc      = new System.Windows.Forms.Button();
            this.btnReadVin      = new System.Windows.Forms.Button();
            this.btnClear        = new System.Windows.Forms.Button();
            this.txtLog          = new System.Windows.Forms.RichTextBox();
            this.lblStatus       = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // ── AUTO-DETECT (primary, green) ────────────────────────────
            this.btnAutoDetect.Text      = "⚡ Auto-Detect & Open";
            this.btnAutoDetect.Location  = new System.Drawing.Point(12, 12);
            this.btnAutoDetect.Size      = new System.Drawing.Size(200, 32);
            this.btnAutoDetect.BackColor = System.Drawing.Color.SeaGreen;
            this.btnAutoDetect.ForeColor = System.Drawing.Color.White;
            this.btnAutoDetect.Font      = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold);
            this.btnAutoDetect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.btnCloseBus.Text      = "Close Bus";
            this.btnCloseBus.Location  = new System.Drawing.Point(220, 12);
            this.btnCloseBus.Size      = new System.Drawing.Size(100, 32);
            this.btnCloseBus.BackColor = System.Drawing.Color.IndianRed;
            this.btnCloseBus.ForeColor = System.Drawing.Color.White;
            this.btnCloseBus.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            // ── MANUAL flow separator ───────────────────────────────────
            this.lblManual.Text      = "── Manual (if auto-detect fails) ──────────────────────────────";
            this.lblManual.Location  = new System.Drawing.Point(12, 54);
            this.lblManual.Size      = new System.Drawing.Size(760, 18);
            this.lblManual.ForeColor = System.Drawing.Color.Gray;

            // ── Channel selector ─────────────────────────────────────────
            this.cmbChannels.Location     = new System.Drawing.Point(12, 75);
            this.cmbChannels.Size         = new System.Drawing.Size(340, 24);
            this.cmbChannels.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // ── Manual buttons row ───────────────────────────────────────
            SetBtn(this.btnGetChannels,  "1. Get Channels",  358, 75, 140);
            SetBtn(this.btnSetAppConfig, "2. Set App Config",504, 75, 140);
            SetBtn(this.btnOpenBus,      "3. Open Bus",      650, 75, 110);

            // ── CAN IDs ──────────────────────────────────────────────────
            this.lblReqId.Text     = "Request ID (hex):";
            this.lblReqId.Location = new System.Drawing.Point(12, 112);
            this.lblReqId.AutoSize = true;

            this.txtRequestId.Text     = "7DF";
            this.txtRequestId.Location = new System.Drawing.Point(128, 109);
            this.txtRequestId.Size     = new System.Drawing.Size(70, 22);

            this.lblRespId.Text     = "Response ID (hex):";
            this.lblRespId.Location = new System.Drawing.Point(215, 112);
            this.lblRespId.AutoSize = true;

            this.txtResponseId.Text     = "7E8";
            this.txtResponseId.Location = new System.Drawing.Point(338, 109);
            this.txtResponseId.Size     = new System.Drawing.Size(70, 22);

            // ── TX/RX & UDS buttons ──────────────────────────────────────
            SetBtn(this.btnSendFrame, "Send Frame",           12, 140, 120);
            SetBtn(this.btnRecvFrame, "Recv Frame",          138, 140, 110);
            SetBtn(this.btnReadDtc,   "Read DTC (19 02 09)", 254, 140, 170);
            SetBtn(this.btnReadVin,   "Read VIN (22 F1 90)", 430, 140, 170);

            // ── Log ──────────────────────────────────────────────────────
            this.txtLog.Location  = new System.Drawing.Point(12, 174);
            this.txtLog.Size      = new System.Drawing.Size(760, 358);
            this.txtLog.Font      = new System.Drawing.Font("Consolas", 9F);
            this.txtLog.ReadOnly  = true;
            this.txtLog.BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
            this.txtLog.ForeColor = System.Drawing.Color.LimeGreen;

            // ── Bottom ───────────────────────────────────────────────────
            SetBtn(this.btnClear, "Clear", 12, 540, 70);
            this.btnClear.BackColor = System.Drawing.Color.DimGray;

            this.lblStatus.Location  = new System.Drawing.Point(90, 543);
            this.lblStatus.Size      = new System.Drawing.Size(682, 20);
            this.lblStatus.Text      = "○ Bus CLOSED";
            this.lblStatus.ForeColor = System.Drawing.Color.Gray;

            // ── Form ─────────────────────────────────────────────────────
            this.ClientSize  = new System.Drawing.Size(784, 571);
            this.Text        = "Vector CAN Test — vxlapi_NET.dll (.NET 4.8 x64)";
            this.BackColor   = System.Drawing.Color.FromArgb(30, 30, 30);
            this.ForeColor   = System.Drawing.Color.WhiteSmoke;

            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                btnAutoDetect, btnCloseBus,
                lblManual, cmbChannels,
                btnGetChannels, btnSetAppConfig, btnOpenBus,
                lblReqId, txtRequestId, lblRespId, txtResponseId,
                btnSendFrame, btnRecvFrame, btnReadDtc, btnReadVin,
                txtLog, btnClear, lblStatus
            });

            this.ResumeLayout(false);
        }

        private static void SetBtn(System.Windows.Forms.Button b, string text, int x, int y, int w)
        {
            b.Text      = text;
            b.Location  = new System.Drawing.Point(x, y);
            b.Size      = new System.Drawing.Size(w, 26);
            b.BackColor = System.Drawing.Color.SteelBlue;
            b.ForeColor = System.Drawing.Color.White;
            b.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        }

        private System.Windows.Forms.Button      btnAutoDetect;
        private System.Windows.Forms.Button      btnGetChannels;
        private System.Windows.Forms.Button      btnSetAppConfig;
        private System.Windows.Forms.Button      btnOpenBus;
        private System.Windows.Forms.Button      btnCloseBus;
        private System.Windows.Forms.Button      btnSendFrame;
        private System.Windows.Forms.Button      btnRecvFrame;
        private System.Windows.Forms.Button      btnReadDtc;
        private System.Windows.Forms.Button      btnReadVin;
        private System.Windows.Forms.Button      btnClear;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.Label       lblReqId;
        private System.Windows.Forms.Label       lblRespId;
        private System.Windows.Forms.TextBox     txtRequestId;
        private System.Windows.Forms.TextBox     txtResponseId;
        private System.Windows.Forms.Label       lblStatus;
        private System.Windows.Forms.Label       lblManual;
        private System.Windows.Forms.ComboBox    cmbChannels;
    }
}
