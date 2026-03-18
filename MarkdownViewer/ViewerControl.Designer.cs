namespace MarkdownViewer
{
    partial class ViewerControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.loadingPanel = new System.Windows.Forms.Panel();
            this.loadingPicture = new System.Windows.Forms.PictureBox();
            this.loadingTimer = new System.Windows.Forms.Timer(this.components);
            this.loadingPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.loadingPicture)).BeginInit();
            this.SuspendLayout();
            // 
            // webView2
            // 
            this.webView2.AllowExternalDrop = true;
            this.webView2.CreationProperties = null;
            this.webView2.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webView2.Location = new System.Drawing.Point(0, 0);
            this.webView2.Name = "webView2";
            this.webView2.TabIndex = 0;
            this.webView2.ZoomFactor = 1D;
            // 
            // loadingPanel
            // 
            this.loadingPanel.BackColor = System.Drawing.Color.White;
            this.loadingPanel.Controls.Add(this.loadingPicture);
            this.loadingPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.loadingPanel.Location = new System.Drawing.Point(0, 0);
            this.loadingPanel.Name = "loadingPanel";
            this.loadingPanel.Size = new System.Drawing.Size(406, 361);
            this.loadingPanel.TabIndex = 1;
            // 
            // loadingPicture
            // 
            this.loadingPicture.Location = new System.Drawing.Point(178, 145);
            this.loadingPicture.Name = "loadingPicture";
            this.loadingPicture.Size = new System.Drawing.Size(50, 50);
            this.loadingPicture.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.loadingPicture.TabIndex = 0;
            this.loadingPicture.TabStop = false;
            // 
            // loadingTimer
            // 
            this.loadingTimer.Interval = 50;
            this.loadingTimer.Tick += new System.EventHandler(this.loadingTimer_Tick);
            // 
            // ViewerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.webView2);
            this.Controls.Add(this.loadingPanel);
            this.Name = "ViewerControl";
            this.Size = new System.Drawing.Size(406, 361);
            this.loadingPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.loadingPicture)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        internal Microsoft.Web.WebView2.WinForms.WebView2 webView2;
        private System.Windows.Forms.Panel loadingPanel;
        private System.Windows.Forms.PictureBox loadingPicture;
        private System.Windows.Forms.Timer loadingTimer;
    }
}
