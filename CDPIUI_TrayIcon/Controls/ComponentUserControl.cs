using CDPIUI_TrayIcon.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel.Chat;

namespace CDPIUI_TrayIcon.Controls
{
    public partial class ComponentUserControl : UserControl
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

        private string _componentId = string.Empty;
        public string ComponentId
        {
            get => _componentId;
            set => _componentId = value;
        }

        private bool _isRunned = false;
        public bool IsRunned
        {
            get { return _isRunned; }
            set
            {
                _isRunned = value;
                StateChangedActions();
            }
        }

        private bool _isSetupComplete = false;
        public bool IsSetupComplete
        {
            get { return _isSetupComplete; }
            set
            {
                _isSetupComplete = value;
                SetupStateChanged();
            }
        }

        public Action? EventHappens;

        public ComponentUserControl()
        {
            InitializeComponent();
            ConnectHandlers();
            StateChangedActions();

            this.Load += ComponentUserControl_Load;
            this.Disposed += ComponentUserControl_Disposed;
        }

        #region HandlersManagement
        private void ConnectHandlers()
        {
            ComponentWorkButton.Clicked += ComponentWorkButton_Clicked;
            OpenPseudoConsoleButton.Clicked += OpenPseudoConsoleButton_Clicked;
            OpenSettingsButton.Clicked += OpenSettingsButton_Clicked;


        }

        private void DisconnectHandlers()
        {
            ComponentWorkButton.Clicked -= ComponentWorkButton_Clicked;
            OpenPseudoConsoleButton.Clicked -= OpenPseudoConsoleButton_Clicked;
            OpenSettingsButton.Clicked -= OpenSettingsButton_Clicked;
        }
        #endregion

        private void ComponentUserControl_Load(object? sender, EventArgs e)
        {
            InitToolTips();
            this.Load -= ComponentUserControl_Load;
        }

        private void InitToolTips()
        {
            ComponentWorkButton.ToolTip = BasicToolTip;
            OpenPseudoConsoleButton.ToolTip = BasicToolTip;
            OpenSettingsButton.ToolTip = BasicToolTip;

            ComponentWorkButton.ToolTipMessage = LocaleHelper.GetLocaleString("ToggleState");
            OpenPseudoConsoleButton.ToolTipMessage = LocaleHelper.GetLocaleString("Pseudoconsole");
            OpenSettingsButton.ToolTipMessage = LocaleHelper.GetLocaleString("Setup");
        }

        
        private void StateChangedActions()
        {
            ComponentWorkButton.IsHighlighted = IsRunned;
            ComponentWorkButton.Glyph = IsRunned ? "\uE62E" : "\uF5B0";
        }

        private void SetupStateChanged()
        {
            ComponentWorkButton.Enabled = IsSetupComplete;
        }

        private async void OpenSettingsButton_Clicked(object? sender, EventArgs e)
        {
            EventHappens?.Invoke();

            await PipeServer.Instance.SendMessage($"WINDOW:SHOW_COMPONENT_SETTINGS({ComponentId})");
        }

        private async void OpenPseudoConsoleButton_Clicked(object? sender, EventArgs e)
        {
            EventHappens?.Invoke();

            await PipeServer.Instance.SendMessage($"WINDOW:SHOW_PSEUDOCONSOLE({ComponentId})");
        }

        private async void ComponentWorkButton_Clicked(object? sender, EventArgs e)
        {
            EventHappens?.Invoke();

            if (!await TasksHelper.Instance.IsTaskRunned(ComponentId))
            {
                TasksHelper.Instance.CreateAndRunNewTask(ComponentId);
            }
            else
            {
                await TasksHelper.Instance.StopTask(ComponentId);
            }
        }

        private void ComponentUserControl_Disposed(object? sender, EventArgs e)
        {
            DisconnectHandlers();
            this.Disposed -= ComponentUserControl_Disposed;
        }
    }
}
