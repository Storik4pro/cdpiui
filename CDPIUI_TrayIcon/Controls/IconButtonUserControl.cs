using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.UI.ViewManagement;

namespace CDPIUI_TrayIcon.Controls
{
    public partial class IconButtonUserControl : UserControl
    {
        private Color AccentColor = GetAccentColor();

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
            }
        }

        private ToolTip? _toolTip;
        public ToolTip? ToolTip
        {
            get { return _toolTip; }
            set
            {
                _toolTip = value;

            }
        }

        private string? _toolTipMessage;
        public string? ToolTipMessage
        {
            get { return _toolTipMessage; }
            set
            {
                _toolTipMessage = value;

            }
        }

        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get { return _isHighlighted; }
            set
            {
                _isHighlighted = value;
                if (_isHighlighted) IconGlyph.ForeColor = AccentColor;
                else IconGlyph.ForeColor = Color.White;
            }
        }

        private bool _enabled = true;
        public new bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (_enabled)
                {
                    this.BackColor = Color.FromArgb(0, 50, 50, 50);
                    if (!IsHighlighted) IconGlyph.ForeColor = Color.White;
                }
                else
                {
                    this.BackColor = Color.FromArgb(50, 50, 50);
                    if (!IsHighlighted) IconGlyph.ForeColor = Color.FromArgb(113, 113, 113);
                }
            }
        }

        public event EventHandler? Clicked;

        public IconButtonUserControl()
        {
            InitializeComponent();

            ConnectHandlers();
            
        }

        #region HandlersManagement

        private void ConnectHandlers()
        {
            this.MouseEnter += IconButtonUserControl_MouseEnter;
            this.MouseLeave += IconButtonUserControl_MouseLeave;

            this.MouseClick += IconButtonUserControl_MouseClick;


            foreach (Control control in Controls)
            {
                control.MouseEnter += new EventHandler(this.IconButtonUserControl_MouseEnter);
                control.MouseLeave += new EventHandler(this.IconButtonUserControl_MouseLeave);

                control.MouseClick += this.IconButtonUserControl_MouseClick;
            }

            this.Disposed += IconButtonUserControl_Disposed;
        }

        private void DisconnectHandlers()
        {
            this.MouseEnter -= IconButtonUserControl_MouseEnter;
            this.MouseLeave -= IconButtonUserControl_MouseLeave;

            this.MouseClick -= IconButtonUserControl_MouseClick;


            foreach (Control control in Controls)
            {
                control.MouseEnter -= new EventHandler(this.IconButtonUserControl_MouseEnter);
                control.MouseLeave -= new EventHandler(this.IconButtonUserControl_MouseLeave);

                control.MouseClick -= this.IconButtonUserControl_MouseClick;
            }

            this.Disposed -= IconButtonUserControl_Disposed;
        }

        #endregion

        private void IconButtonUserControl_MouseClick(object? sender, MouseEventArgs e)
        {
            Clicked?.Invoke(this, e);
        }

        private void IconButtonUserControl_MouseLeave(object? sender, EventArgs e)
        {
            if (!Enabled) return;
            if (!IsHighlighted) IconGlyph.ForeColor = Color.White;
            this.BackColor = Color.FromArgb(0, 50, 50, 50);

            ToolTip?.Hide(this);
        }

        private void IconButtonUserControl_MouseEnter(object? sender, EventArgs e)
        {
            if (!Enabled) return;
            if (!IsHighlighted) IconGlyph.ForeColor = AccentColor;
            this.BackColor = Color.FromArgb(50, 50, 50);

            Point pnt = PointToClient(PointToScreen(Point.Empty));

            pnt.Y -= 20;

            ToolTip?.Show(ToolTipMessage, this, pnt);

        }

        private static Windows.UI.ViewManagement.UISettings uiSettings = new Windows.UI.ViewManagement.UISettings();
        private static System.Drawing.Color GetAccentColor()
        {
            Windows.UI.Color c = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        private void IconButtonUserControl_Disposed(object? sender, EventArgs e)
        {
            DisconnectHandlers();
        }
    }
}
