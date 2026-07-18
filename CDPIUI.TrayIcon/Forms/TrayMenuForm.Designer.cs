using CDPIUI_TrayIcon.Helper;
using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace CDPIUI_TrayIcon.Forms
{
    partial class TrayMenuForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;


        

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            ExitButton = new CDPIUI_TrayIcon.Controls.ModernButtonUserControl();
            MaximizeButton = new CDPIUI_TrayIcon.Controls.ModernButtonUserControl();
            Separator = new CDPIUI_TrayIcon.Controls.ModernSeparatorUserControl();
            MainPanel = new CDPIUI_TrayIcon.Controls.BackgroundUserControl();
            BasicActionsPanel = new Panel();
            ComponentPanel = new Panel();
            MainPanel.SuspendLayout();
            BasicActionsPanel.SuspendLayout();
            SuspendLayout();
            // 
            // ExitButton
            // 
            ExitButton.BackColor = Color.Transparent;
            ExitButton.Bold = false;
            ExitButton.DisplayImage = null;
            ExitButton.DisplayText = "Exit";
            ExitButton.ForeColor = Color.White;
            ExitButton.Glyph = "";
            ExitButton.Location = new Point(0, 36);
            ExitButton.Margin = new Padding(0, 2, 0, 2);
            ExitButton.Name = "ExitButton";
            ExitButton.Size = new Size(275, 32);
            ExitButton.TabIndex = 0;
            // 
            // MaximizeButton
            // 
            MaximizeButton.BackColor = Color.Transparent;
            MaximizeButton.Bold = true;
            MaximizeButton.DisplayImage = null;
            MaximizeButton.DisplayText = "Maximize";
            MaximizeButton.ForeColor = Color.White;
            MaximizeButton.Glyph = "";
            MaximizeButton.Location = new Point(0, 2);
            MaximizeButton.Margin = new Padding(0, 2, 0, 2);
            MaximizeButton.Name = "MaximizeButton";
            MaximizeButton.Size = new Size(275, 32);
            MaximizeButton.TabIndex = 1;
            // 
            // Separator
            // 
            Separator.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Separator.BackColor = Color.FromArgb(53, 53, 53);
            Separator.Location = new Point(0, 368);
            Separator.Margin = new Padding(0, 2, 0, 2);
            Separator.Name = "Separator";
            Separator.Size = new Size(283, 1);
            Separator.TabIndex = 2;
            // 
            // MainPanel
            // 
            MainPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            MainPanel.BackColor = Color.FromArgb(44, 44, 44);
            MainPanel.BackgroundColor = Color.Transparent;
            MainPanel.BorderColor = Color.FromArgb(0, 0, 0, 0);
            MainPanel.BorderWidth = 1F;
            MainPanel.Controls.Add(BasicActionsPanel);
            MainPanel.Controls.Add(Separator);
            MainPanel.Controls.Add(ComponentPanel);
            MainPanel.Location = new Point(1, 1);
            MainPanel.Margin = new Padding(0);
            MainPanel.Name = "MainPanel";
            MainPanel.Padding = new Padding(4, 2, 4, 2);
            MainPanel.Radius = 0;
            MainPanel.Size = new Size(283, 442);
            MainPanel.TabIndex = 3;
            // 
            // BasicActionsPanel
            // 
            BasicActionsPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            BasicActionsPanel.Controls.Add(MaximizeButton);
            BasicActionsPanel.Controls.Add(ExitButton);
            BasicActionsPanel.Location = new Point(4, 371);
            BasicActionsPanel.Margin = new Padding(4, 0, 4, 2);
            BasicActionsPanel.Name = "BasicActionsPanel";
            BasicActionsPanel.Size = new Size(275, 73);
            BasicActionsPanel.TabIndex = 5;
            // 
            // ComponentPanel
            // 
            ComponentPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            ComponentPanel.BackColor = Color.Transparent;
            ComponentPanel.Location = new Point(4, 4);
            ComponentPanel.Margin = new Padding(0);
            ComponentPanel.Name = "ComponentPanel";
            ComponentPanel.Padding = new Padding(4, 2, 4, 0);
            ComponentPanel.Size = new Size(279, 362);
            ComponentPanel.TabIndex = 4;
            // 
            // TrayMenuForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.DimGray;
            ClientSize = new Size(285, 444);
            ControlBox = false;
            Controls.Add(MainPanel);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MdiChildrenMinimizedAnchorBottom = false;
            MinimizeBox = false;
            Name = "TrayMenuForm";
            Padding = new Padding(1);
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            Text = "TrayMenuForm";
            TopMost = true;
            MainPanel.ResumeLayout(false);
            BasicActionsPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TextBox textBox1;
        private FlowLayoutPanel flowLayoutPanel1;
        private Controls.ModernListView MainFlowLayoutPanel;
        private Controls.ModernButtonUserControl ExitButton;
        private Controls.ModernButtonUserControl MaximizeButton;
        private Controls.ModernSeparatorUserControl Separator;
        private Controls.BackgroundUserControl MainPanel;
        private Panel ComponentPanel;
        private Panel BasicActionsPanel;
    }
}