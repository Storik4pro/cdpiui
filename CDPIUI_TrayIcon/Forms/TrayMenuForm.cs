using CDPIUI_TrayIcon.Controls;
using CDPIUI_TrayIcon.Helper;
using System.Diagnostics;

namespace CDPIUI_TrayIcon.Forms
{
    public partial class TrayMenuForm : Form
    {
        private readonly int DefaultWidth;

        public Action? Hided;

        public TrayMenuForm()
        {
            DefaultWidth = 285;

            HideWindow();
            this.ShowInTaskbar = false;
            InitializeComponent();

            StartPosition = FormStartPosition.Manual;
            MaximizeButton.DisplayImage = Utils.GetBitmapFromResourses("CDPIUI_TrayIcon.Assets.trayLogoNormal.ico");
            AllowTransparency = false;
            this.Visible = false;

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            ConnectHandlers();

            CheckWindowHeight();

            ExitButton.DisplayText = LocaleHelper.GetLocaleString("Exit");
            MaximizeButton.DisplayText = LocaleHelper.GetLocaleString("ShowMainWindow");

            this.Load += TrayMenuForm_Load;
            
        }

        private void TrayMenuForm_Load(object? sender, EventArgs e)
        {
            this.Load -= TrayMenuForm_Load;
            HandleTasksListUpdate();
        }

        #region HandlersManage
        private void ConnectHandlers()
        {
            TasksHelper.Instance.TaskStateUpdated += HandleTaskStateUpdate;
            TasksHelper.Instance.TaskListUpdated += HandleTasksListUpdate;
            TasksHelper.Instance.TaskSetupStateUpdated += HandleTaskSetupStateUpdate;

            MaximizeButton.Clicked += MaximizeButton_MouseClick;
            ExitButton.Clicked += ExitButton_MouseClick;

            this.Disposed += TrayMenuForm_Disposed;
        }

        private void DisconnectHandlers()
        {
            TasksHelper.Instance.TaskStateUpdated -= HandleTaskStateUpdate;
            TasksHelper.Instance.TaskListUpdated -= HandleTasksListUpdate;
            TasksHelper.Instance.TaskSetupStateUpdated -= HandleTaskSetupStateUpdate;

            MaximizeButton.Clicked -= MaximizeButton_MouseClick;
            ExitButton.Clicked -= ExitButton_MouseClick;

            DisconnectComponentPanelChildrensHandlers();

            this.Disposed -= TrayMenuForm_Disposed;
        }

        private void DisconnectComponentPanelChildrensHandlers()
        {
            foreach (Control control in ComponentPanel.Controls)
            {
                if (control is ComponentUserControl componentUserControl)
                {
                    componentUserControl.EventHappens -= HandleEventHappens;
                    componentUserControl.Dispose();
                }
                else if (control is ModernButtonUserControl buttonUserControl)
                {
                    buttonUserControl.Clicked -= HandleButtonClick;
                    buttonUserControl.Dispose();
                }
            }
        }

        #endregion

        private void ExitButton_MouseClick(object? sender, EventArgs e)
        {
            HandleEventHappens();
            _ = PipeServer.Instance.SendMessage("MAIN:EXIT_ALL");
            this.Dispose();
            Application.Exit();
        }

        private async void MaximizeButton_MouseClick(object? sender, EventArgs e)
        {
            HandleEventHappens();
            if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_MAIN"))
            {
                RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
            }
        }

        private void HandleEventHappens()
        {
            HideWindow();
        }

        

        private void HandleTasksListUpdate()
        {
            this.Invoke(() =>
            {
                DisconnectComponentPanelChildrensHandlers();
                ComponentPanel.Controls.Clear();
            });

            int targetWidth = ComponentPanel.Width;
            int offset = 0;

            if (TasksHelper.Instance.Tasks.Count == 0)
            {
                this.Invoke(() =>
                {
                    var storeButton = new ModernButtonUserControl()
                    {
                        Name = "OpenStoreButton",
                        DisplayText = LocaleHelper.GetLocaleString("Store"),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                        Width = targetWidth - ComponentPanel.Padding.Right,
                        Margin = new(0, 2, 4, 2),
                    };
                    storeButton.Clicked += HandleButtonClick;
                    ComponentPanel.Controls.Add(storeButton);
                    storeButton.Invalidate();
                });

            }
            else
            {
                foreach (var task in TasksHelper.Instance.Tasks)
                {
                    this.Invoke(() =>
                    {
                        var control = new ComponentUserControl()
                        {
                            Name = task.Id,
                            DisplayText = LocaleHelper.GetLocalizedComponentName(task.Id),
                            ComponentId = task.Id,
                            IsRunned = task.ProcessManager.GetState(),
                            IsSetupComplete = task.IsSetupComplete ?? true,
                            Width = targetWidth,
                            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                            Margin = new(0, 2, 0, 2),
                            Top = offset
                        };



                        control.EventHappens += HandleEventHappens;

                        ComponentPanel.Controls.Add(control);
                        control.Invalidate();
                        offset += control.Height + control.Margin.Top + control.Margin.Bottom;
                    });
                }
            }
            this.Invoke(() =>
            {
                CheckWindowHeight();
                ComponentPanel.Invalidate();
            });
        }

        private async void HandleTaskStateUpdate(Tuple<string, bool> taskStateUpdate)
        {
            this.Invoke(() =>
            {
                Control[] controls = ComponentPanel.Controls.Find(taskStateUpdate.Item1, false);
                if (controls.Length > 0)
                {
                    if (controls[0] is ComponentUserControl componentUserControl)
                    {
                        componentUserControl.IsRunned = taskStateUpdate.Item2;
                    }
                }
                else
                {
                    HandleTasksListUpdate();
                }
            });

            await Task.CompletedTask;
        }

        private async void HandleTaskSetupStateUpdate(Tuple<string, bool> taskStateUpdate)
        {
            this.Invoke(() =>
            {
                Control[] controls = ComponentPanel.Controls.Find(taskStateUpdate.Item1, false);
                if (controls.Length > 0)
                {
                    if (controls[0] is ComponentUserControl componentUserControl)
                    {
                        componentUserControl.IsSetupComplete = taskStateUpdate.Item2;
                    }
                }
                else
                {
                    HandleTasksListUpdate();
                }
            });

            await Task.CompletedTask;
        }

        private async void HandleButtonClick(object? sender, EventArgs eventArgs)
        {
            if (sender is ModernButtonUserControl button)
            {
                switch (button.Name)
                {
                    case "OpenStoreButton":
                        HandleEventHappens();
                        if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_STORE"))
                        {
                            RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--show-store");
                        }
                        break;
                }
            }
        }

        #region AuditHeight

        private void SetBasicActionsPanelHeight()
        {
            BasicActionsPanel.Height = ExitButton.Height + ExitButton.Margin.Bottom + ExitButton.Margin.Top;
            BasicActionsPanel.Height += MaximizeButton.Height + MaximizeButton.Margin.Bottom + MaximizeButton.Margin.Top;
        }

        private int GetComponentsHeight()
        {
            int total = 0;
            foreach (Control control in ComponentPanel.Controls) 
            {
                total += control.Height + control.Margin.Top + control.Margin.Bottom;
            }
            ComponentPanel.Height = total;
            Debug.WriteLine(total);
            return total;
        }

        private int CheckWindowHeight()
        {
            SetBasicActionsPanelHeight();
            int requestedHeight = BasicActionsPanel.Height + this.Padding.Bottom + this.Padding.Top +
                Separator.Height + Separator.Margin.Top + Separator.Margin.Bottom + GetComponentsHeight();
            this.Height = requestedHeight;
            MainPanel.Height = requestedHeight - this.Padding.Bottom - this.Padding.Top;
            return requestedHeight;
        }

        #endregion

        #region PublicMethods
        public void HideWindow()
        {
            this.Visible = false;
            Location = new Point(-2000, -2000);
            Size = new Size(1, 1);

            GC.Collect();
            Hided?.Invoke();
        }
        public void ShowWindow(Point location)
        {
            this.Visible = true;
            Size = new Size(DefaultWidth, CheckWindowHeight());
            Location = new Point(x: location.X - this.Width, y: location.Y - this.Height);
            this.BringToFront();
            this.Activate();
        }
        #endregion

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ACTIVATEAPP)
            {
                if (m.WParam.ToInt64() == 0) /* Being deactivated */
                {
                    Debug.WriteLine("Hide");
                    HideWindow();
                }
            }
            base.WndProc(ref m);
        }

        private void TrayMenuForm_Disposed(object? sender, EventArgs e)
        {
            DisconnectHandlers();
        }

        #region WinAPI
        const int WM_ACTIVATEAPP = 0x001C;
        #endregion
    }
}
