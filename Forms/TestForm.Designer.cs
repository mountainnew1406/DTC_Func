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
            this.btnGetChannels    = new System.Windows.Forms.Button();
            this.btnOpenBus        = new System.Windows.Forms.Button();
            this.btnCloseBus       = new System.Windows.Forms.Button();
            this.btnSendFrame      = new System.Windows.Forms.Button();
            this.btnRecvFrame      = new System.Windows.Forms.Button();
            this.btnReadDtc        = new System.Windows.Forms.Button();
            this.btnReadVin        = new System.Windows.Forms.Button();
            this.btnClear          = new System.Windows.Forms.Button();
            this.txtLog            = new System.Windows.Forms.RichTextBox();
            this.lblReqId          = new System.Windows.Forms.Label();
            this.lblRespId         = new System.Windows.Forms.Label();
            this.txtRequestId      = new System.Windows.Forms.TextBox();
            this.txtResponseId     = new System.Windows.Forms.TextBox();
            this.lblStatus         = new System.Windows.Forms.Label();
            this.cmbChannels       = new System.Windows.Forms.ComboBox();
            this.btnSetAppConfig   = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // ── Channel selector ─────────────────────────────────────────
            this.cmbChannels.Location = new System.Drawing.Point(12, 12);
            this.cmbChannels.Size     = new System.Drawing.Size(340, 24);
            this.cmbChannels.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // ── Buttons row 1 ────────────────────────────────────────────
            SetButton(this.btnGetChannels,  "1. Get Channels",    12,  42,  160);
            SetButton(this.btnSetAppConfig, "2. Set App Config", 178,  42,  160);
            SetButton(this.btnOpenBus,      "3. Open Bus",       344,  42,  120);
            SetButton(this.btnCloseBus,     "Close Bus",         470,  42,  100);

            // ── ID fields ────────────────────────────────────────────────
            this.lblReqId.Text     = "Request ID (hex):";
            this.lblReqId.Location = new System.Drawing.Point(12, 78);
            this.lblReqId.AutoSize = true;

            this.txtRequestId.Text     = "7DF";
            this.txtRequestId.Location = new System.Drawing.Point(130, 75);
            this.txtRequestId.Size     = new System.Drawing.Size(80, 22);

            this.lblRespId.Text     = "Response ID (hex):";
            this.lblRespId.Location = new System.Drawing.Point(225, 78);
            this.lblRespId.AutoSize = true;

            this.txtResponseId.Text     = "7E8";
            this.txtResponseId.Location = new System.Drawing.Point(345, 75);
            this.txtResponseId.Size     = new System.Drawing.Size(80, 22);

            // ── Buttons row 2 ────────────────────────────────────────────
            SetButton(this.btnSendFrame,  "Send Test Frame",  12, 106, 140);
            SetButton(this.btnRecvFrame,  "Recv Frame",      158, 106, 120);
            SetButton(this.btnReadDtc,    "Read DTC (19 02 09)", 284, 106, 160);
            SetButton(this.btnReadVin,    "Read VIN (22 F1 90)", 450, 106, 160);

            // ── Log area ─────────────────────────────────────────────────
            this.txtLog.Location  = new System.Drawing.Point(12, 138);
            this.txtLog.Size      = new System.Drawing.Size(760, 380);
            this.txtLog.Font      = new System.Drawing.Font("Consolas", 9F);
            this.txtLog.ReadOnly  = true;
            this.txtLog.BackColor = System.Drawing.Color.Black;
            this.txtLog.ForeColor = System.Drawing.Color.LimeGreen;

            // ── Clear button ─────────────────────────────────────────────
            SetButton(this.btnClear, "Clear Log", 12, 528, 100);
            this.btnClear.BackColor = System.Drawing.Color.DimGray;

            // ── Status label ─────────────────────────────────────────────
            this.lblStatus.Location  = new System.Drawing.Point(120, 530);
            this.lblStatus.Size      = new System.Drawing.Size(652, 22);
            this.lblStatus.Text      = "Ready";
            this.lblStatus.ForeColor = System.Drawing.Color.Gray;

            // ── Form ─────────────────────────────────────────────────────
            this.ClientSize  = new System.Drawing.Size(784, 561);
            this.Text        = "Vector CAN Test — vxlapi_NET.dll";
            this.MinimizeBox = true;
            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                cmbChannels, btnGetChannels, btnSetAppConfig, btnOpenBus, btnCloseBus,
                lblReqId, txtRequestId, lblRespId, txtResponseId,
                btnSendFrame, btnRecvFrame, btnReadDtc, btnReadVin,
                txtLog, btnClear, lblStatus
            });

            this.ResumeLayout(false);
        }

        private static void SetButton(System.Windows.Forms.Button b, string text, int x, int y, int w)
        {
            b.Text      = text;
            b.Location  = new System.Drawing.Point(x, y);
            b.Size      = new System.Drawing.Size(w, 26);
            b.BackColor = System.Drawing.Color.SteelBlue;
            b.ForeColor = System.Drawing.Color.White;
            b.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        }

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
        private System.Windows.Forms.ComboBox    cmbChannels;
    }
}
