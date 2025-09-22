using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Update
{
    public partial class MainWindow : Form
    {
        private const string ErrorMessage = 
            "An unexpected error occurred while updating the application. " +
            "Additionally, an attempt to downgrade to the previous version failed.\n" +
            "You may need to manually reinstall the application to resolve this issue.\n" +
            "Please follow these steps:\n" +
            "1. Uninstall the current version of the application.\n" +
            "2. Download the latest version from GitHub.\n" +
            "3. Install the downloaded version.\n" +
            "If the problem persists, contact support.\n" +
            "ERROR CODE: {0}";
        public MainWindow()
        {
            InitializeComponent();

            InstallHelper.Instance.ProgressChanged += ProgressChanged;
            InstallHelper.Instance.InstallationStateChanged += InstallationStateChanged;
            InstallHelper.Instance.CurrentFileChanged += CurrentFileChanged;
            InstallHelper.Instance.ErrorOccurred += ErrorOccurred;

            this.FormClosing += Form_FormClosing;

            InstallHelper.Instance.StartInstall();
        }

        private void ProgressChanged(double progress)
        {
            int _progress = (int)(progress * 100);
            if (ProgressBar.InvokeRequired)
            {
                ProgressBar.Invoke(new Action(() => ProgressBar.Value = _progress));
            }
            else
            {
                ProgressBar.Value = _progress;
            }
        }

        private void SetStatus(string status)
        {
            if (StatusLabel.InvokeRequired)
            {
                StatusLabel.Invoke(new Action(() => StatusLabel.Text = status));
            }
            else
            {
                StatusLabel.Text = status;
            }
        }

        private void InstallationStateChanged(InstallState state)
        {
            switch (state)
            {
                case (InstallState.Prepare):
                    SetStatus("Preparing...");
                    break;
                case (InstallState.Unpack):
                    SetStatus("Unpacking...");
                    break;
                case (InstallState.Copy):
                    SetStatus("Copying files...");
                    break;
                case (InstallState.Finalize):
                    SetStatus("Finalizing...");
                    break;
                case (InstallState.Completed):
                    SetStatus("Complete");
                    if (button1.InvokeRequired)
                    {
                        button1.Invoke(new Action(() => button1.Enabled = true));
                    }
                    else
                    {
                        button1.Enabled = true;
                    }
                    button1.Click += (s, e) => Application.Exit();
                    break;
                case (InstallState.Error):
                    SetStatus("Exception happens");
                    if (button1.InvokeRequired)
                    {
                        button1.Invoke(new Action(() => button1.Enabled = true));
                    }
                    else
                    {
                        button1.Enabled = true;
                    }
                    button1.Click += (s, e) => Application.Exit();
                    break;
            }
        }

        private void CurrentFileChanged(string filename)
        {
            SetStatus($"{filename}");
        }

        private void ErrorOccurred(InstallError installError, string error)
        {
            DialogResult dialogResult = MessageBox.Show(string.Format(ErrorMessage, error), "An error occurred", 
                                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }

        private void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (InstallHelper.Instance.FinishStatus == InstallError.None)
                e.Cancel = true;
        }
    }
}
