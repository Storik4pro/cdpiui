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

namespace CDPIUI_TrayIcon.Controls
{
    public partial class ModernListView : FlowLayoutPanel
    {

        private int _radius = 10;
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

        private Color _borderColor = SystemColors.Control;
        private Pen _borderPen = new Pen(ControlPaint.Light(SystemColors.Control, 0.0f), 0);
        public Color BorderColor
        {
            get { return _borderColor; }
            set
            {
                _borderColor = value;
                _borderPen = new Pen(ControlPaint.Light(_borderColor, 0.0f), _borderWidth);
                Invalidate();
            }
        }

        private float _borderWidth = 2.0f;
        public float BorderWidth
        {
            get { return _borderWidth; }
            set
            {
                _borderWidth = value;
                _borderPen = new Pen(ControlPaint.Light(_borderColor, 0.0f), _borderWidth);
                Invalidate();
            }
        }

        public ModernListView()
        {
            InitializeComponent();

            BackgroundColor = Color.FromArgb(244, 244, 244);

        }
    }
}
