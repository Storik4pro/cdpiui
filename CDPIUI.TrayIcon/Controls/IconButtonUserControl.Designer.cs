namespace CDPIUI_TrayIcon.Controls
{
    partial class IconButtonUserControl
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
            IconGlyph = new Label();
            SuspendLayout();
            // 
            // IconGlyph
            // 
            IconGlyph.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            IconGlyph.BackColor = Color.Transparent;
            IconGlyph.Font = new Font("Segoe Fluent Icons", 16F, FontStyle.Regular, GraphicsUnit.Pixel, 0);
            IconGlyph.ForeColor = Color.White;
            IconGlyph.Location = new Point(0, 0);
            IconGlyph.Margin = new Padding(0);
            IconGlyph.Name = "IconGlyph";
            IconGlyph.Size = new Size(30, 30);
            IconGlyph.TabIndex = 4;
            IconGlyph.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // IconButtonUserControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Transparent;
            Controls.Add(IconGlyph);
            Name = "IconButtonUserControl";
            Size = new Size(30, 30);
            ResumeLayout(false);
        }

        #endregion

        private Label IconGlyph;
    }
}
