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
using Windows.Devices.Radios;

namespace CDPIUI_TrayIcon.Controls
{
    public partial class BackgroundUserControl : Panel
    {
        private int _radius = 6;
        public int Radius
        {
            get { return _radius; }
            set
            {
                _radius = value;
                Invalidate();
            }
        }

        private SolidBrush _backgroundBrush = new SolidBrush(SystemColors.Control);
        private Color _backgroundColor = SystemColors.Control;
        public Color BackgroundColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundBrush = new SolidBrush(_backgroundColor = value);
                Invalidate();
            }
        }

        private Color _borderColor = Color.FromArgb(255, 0, 0, 0);
        private Pen _borderPen = new Pen(Color.FromArgb(0, 0, 0, 0), 0);
        public Color BorderColor
        {
            get { return _borderColor; }
            set
            {
                _borderColor = value;
                _borderPen = new Pen(_borderColor, _borderWidth);
                Invalidate();
            }
        }

        private float _borderWidth = 1.0f;
        public float BorderWidth
        {
            get { return _borderWidth; }
            set
            {
                _borderWidth = value;
                _borderPen = new Pen(_borderColor, _borderWidth);
                Invalidate();
            }
        }

        public BackgroundUserControl()
        {
            InitializeComponent();
        }
    }
}
