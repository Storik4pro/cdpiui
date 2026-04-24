namespace CDPIUI_TrayIcon.Controls
{
    partial class ModernButtonUserControl
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
            MainTextBox = new Label();
            KeyboardAcceleratorTextBlock = new Label();
            IconGlyph = new Label();
            MainPictureBox = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)MainPictureBox).BeginInit();
            SuspendLayout();
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
            MainTextBox.TabIndex = 1;
            MainTextBox.Text = "SampleName";
            MainTextBox.TextAlign = ContentAlignment.MiddleLeft;
            MainTextBox.Click += MainTextBox_Click;
            // 
            // KeyboardAcceleratorTextBlock
            // 
            KeyboardAcceleratorTextBlock.Anchor = AnchorStyles.Right;
            KeyboardAcceleratorTextBlock.BackColor = Color.Transparent;
            KeyboardAcceleratorTextBlock.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel);
            KeyboardAcceleratorTextBlock.ForeColor = Color.FromArgb(134, 134, 134);
            KeyboardAcceleratorTextBlock.Location = new Point(239, 0);
            KeyboardAcceleratorTextBlock.Name = "KeyboardAcceleratorTextBlock";
            KeyboardAcceleratorTextBlock.Size = new Size(68, 32);
            KeyboardAcceleratorTextBlock.TabIndex = 2;
            KeyboardAcceleratorTextBlock.TextAlign = ContentAlignment.MiddleRight;
            // 
            // IconGlyph
            // 
            IconGlyph.BackColor = Color.Transparent;
            IconGlyph.Font = new Font("Segoe Fluent Icons", 16F, FontStyle.Regular, GraphicsUnit.Pixel, 0);
            IconGlyph.Location = new Point(13, 8);
            IconGlyph.Margin = new Padding(0);
            IconGlyph.Name = "IconGlyph";
            IconGlyph.Size = new Size(17, 17);
            IconGlyph.TabIndex = 3;
            IconGlyph.TextAlign = ContentAlignment.MiddleCenter;
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
            MainPictureBox.TabIndex = 4;
            MainPictureBox.TabStop = false;
            // 
            // ModernButtonUserControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Transparent;
            Controls.Add(MainPictureBox);
            Controls.Add(KeyboardAcceleratorTextBlock);
            Controls.Add(MainTextBox);
            Controls.Add(IconGlyph);
            Margin = new Padding(0, 2, 0, 2);
            Name = "ModernButtonUserControl";
            Size = new Size(307, 32);
            ((System.ComponentModel.ISupportInitialize)MainPictureBox).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private PictureBox MainPictureBox;
        private Label MainTextBox;
        private Label KeyboardAcceleratorTextBlock;
        private Label IconGlyph;
    }
}
