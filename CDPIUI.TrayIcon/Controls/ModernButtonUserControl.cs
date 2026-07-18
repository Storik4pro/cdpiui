using CDPIUI_TrayIcon.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.UI.Core;

namespace CDPIUI_TrayIcon.Controls
{
    public partial class ModernButtonUserControl : UserControl
    {
        private string _text = string.Empty;
        public string DisplayText
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
                MainTextBox.Text = value;
            }
        }

        private bool _bold = false;
        public bool Bold
        {
            get { return _bold; }
            set
            {
                _bold = value;
                MainTextBox.Font = new Font(MainTextBox.Font, _bold? FontStyle.Bold : FontStyle.Regular);
            }
        }

        private string _glyph = string.Empty;
        public string Glyph
        {
            get
            {
                return _glyph;
            }
            set
            {
                _glyph = value;
                IconGlyph.Text = value;
                ChangeVisibility();
            }
        }

        private Image? _image;
        public Image? DisplayImage
        {
            get
            {
                return _image;
            }
            set
            {
                _image = value;
                MainPictureBox.Image = value;
                ChangeVisibility();
            }
        }

        public EventHandler? Clicked;


        public ModernButtonUserControl()
        {
            InitializeComponent();

            this.BackColor = Color.FromArgb(0, 45, 45, 45);
            this.ForeColor = Color.White;

            ConnectHandlers();

            SetFont();
        }

        #region HandlersManagement

        private void ConnectHandlers()
        {
            this.MouseEnter += ModernButtonUserControl_MouseEnter;
            this.MouseLeave += ModernButtonUserControl_MouseLeave;

            this.MouseClick += ModernButtonUserControl_MouseClick;


            foreach (Control control in Controls)
            {
                control.MouseEnter += new EventHandler(this.ModernButtonUserControl_MouseEnter);
                control.MouseLeave += new EventHandler(this.ModernButtonUserControl_MouseLeave);

                control.MouseClick += this.ModernButtonUserControl_MouseClick;
            }

            this.Disposed += ModernButtonUserControl_Disposed; ;
        }


        private void DisconnectHandlers()
        {
            this.MouseEnter -= ModernButtonUserControl_MouseEnter;
            this.MouseLeave -= ModernButtonUserControl_MouseLeave;

            this.MouseClick -= ModernButtonUserControl_MouseClick;


            foreach (Control control in Controls)
            {
                control.MouseEnter -= new EventHandler(this.ModernButtonUserControl_MouseEnter);
                control.MouseLeave -= new EventHandler(this.ModernButtonUserControl_MouseLeave);

                control.MouseClick -= this.ModernButtonUserControl_MouseClick;
            }

            this.Disposed -= ModernButtonUserControl_Disposed;
        }

        #endregion

        private void SetFont()
        {
            IconGlyph.Font = new Font(Utils.IsOsSupportedNewGlyph() ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", IconGlyph.Font.SizeInPoints);
        }

        private void ModernButtonUserControl_MouseClick(object? sender, MouseEventArgs e)
        {
            Clicked?.Invoke(this, e);
        }

        private void ChangeVisibility()
        {
            if (string.IsNullOrEmpty(IconGlyph.Text))
            {
                IconGlyph.Visible = false;
                MainPictureBox.Visible = true;
            }
            else
            {
                IconGlyph.Visible = true;
                MainPictureBox.Visible = false;
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
        }

        private void ModernButtonUserControl_MouseLeave(object? sender, EventArgs e)
        {
            this.BackColor = Color.FromArgb(0, 45, 45, 45);
        }

        private void ModernButtonUserControl_MouseEnter(object? sender, EventArgs e)
        {
            this.BackColor = Color.FromArgb(50, 50, 50);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void MainTextBox_Click(object sender, EventArgs e)
        {

        }


        private void ModernButtonUserControl_Disposed(object? sender, EventArgs e)
        {
            DisconnectHandlers();
        }
    }
}

