namespace CDPIUI_TrayIcon.Controls
{
    partial class ComponentUserControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            MainPictureBox = new PictureBox();
            MainTextBox = new Label();
            ComponentWorkButton = new IconButtonUserControl();
            OpenPseudoConsoleButton = new IconButtonUserControl();
            OpenSettingsButton = new IconButtonUserControl();
            BasicToolTip = new ToolTip(components);
            ((System.ComponentModel.ISupportInitialize)MainPictureBox).BeginInit();
            SuspendLayout();
            // 
            // MainPictureBox
            // 
            MainPictureBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            MainPictureBox.BackColor = Color.Transparent;
            MainPictureBox.Location = new Point(14, 8);
            MainPictureBox.Margin = new Padding(14, 8, 14, 8);
            MainPictureBox.Name = "MainPictureBox";
            MainPictureBox.Size = new Size(16, 16);
            MainPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            MainPictureBox.TabIndex = 6;
            MainPictureBox.TabStop = false;
            // 
            // MainTextBox
            // 
            MainTextBox.Anchor = AnchorStyles.Left;
            MainTextBox.BackColor = Color.Transparent;
            MainTextBox.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel);
            MainTextBox.Location = new Point(44, 0);
            MainTextBox.Margin = new Padding(44, 0, 0, 0);
            MainTextBox.Name = "MainTextBox";
            MainTextBox.Size = new Size(192, 32);
            MainTextBox.TabIndex = 5;
            MainTextBox.Text = "SampleName";
            MainTextBox.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // ComponentWorkButton
            // 
            ComponentWorkButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ComponentWorkButton.BackColor = Color.Transparent;
            ComponentWorkButton.Glyph = "";
            ComponentWorkButton.IsHighlighted = false;
            ComponentWorkButton.Location = new Point(237, 1);
            ComponentWorkButton.Margin = new Padding(1);
            ComponentWorkButton.Name = "ComponentWorkButton";
            ComponentWorkButton.Size = new Size(30, 30);
            ComponentWorkButton.TabIndex = 7;
            ComponentWorkButton.ToolTip = null;
            ComponentWorkButton.ToolTipMessage = null;
            // 
            // OpenPseudoConsoleButton
            // 
            OpenPseudoConsoleButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            OpenPseudoConsoleButton.BackColor = Color.Transparent;
            OpenPseudoConsoleButton.Glyph = "";
            OpenPseudoConsoleButton.IsHighlighted = false;
            OpenPseudoConsoleButton.Location = new Point(269, 1);
            OpenPseudoConsoleButton.Margin = new Padding(1);
            OpenPseudoConsoleButton.Name = "OpenPseudoConsoleButton";
            OpenPseudoConsoleButton.Size = new Size(30, 30);
            OpenPseudoConsoleButton.TabIndex = 8;
            OpenPseudoConsoleButton.ToolTip = null;
            OpenPseudoConsoleButton.ToolTipMessage = null;
            // 
            // OpenSettingsButton
            // 
            OpenSettingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            OpenSettingsButton.BackColor = Color.Transparent;
            OpenSettingsButton.Glyph = "";
            OpenSettingsButton.IsHighlighted = false;
            OpenSettingsButton.Location = new Point(301, 1);
            OpenSettingsButton.Margin = new Padding(1);
            OpenSettingsButton.Name = "OpenSettingsButton";
            OpenSettingsButton.Size = new Size(30, 30);
            OpenSettingsButton.TabIndex = 9;
            BasicToolTip.SetToolTip(OpenSettingsButton, "What a hell");
            OpenSettingsButton.ToolTip = null;
            OpenSettingsButton.ToolTipMessage = null;
            // 
            // BasicToolTip
            // 
            BasicToolTip.ShowAlways = true;
            // 
            // ComponentUserControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Transparent;
            Controls.Add(OpenSettingsButton);
            Controls.Add(OpenPseudoConsoleButton);
            Controls.Add(ComponentWorkButton);
            Controls.Add(MainPictureBox);
            Controls.Add(MainTextBox);
            ForeColor = Color.White;
            Margin = new Padding(0, 2, 0, 2);
            Name = "ComponentUserControl";
            Size = new Size(337, 32);
            ((System.ComponentModel.ISupportInitialize)MainPictureBox).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private PictureBox MainPictureBox;
        private Label MainTextBox;
        private IconButtonUserControl ComponentWorkButton;
        private IconButtonUserControl OpenPseudoConsoleButton;
        private IconButtonUserControl OpenSettingsButton;
        private ToolTip BasicToolTip;
    }
}
