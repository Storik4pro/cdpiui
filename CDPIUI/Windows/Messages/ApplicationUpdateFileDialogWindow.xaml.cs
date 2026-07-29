#nullable enable

using CDPIUI.Core;
using CDPIUI.Core.Features;
using CDPIUI.Core.Store;
using CDPIUI.Default;
using CDPIUI.Shared;
using CDPIUI.Shared.PrettyErrorConvertionService;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using WinUI3Localizer;

namespace CDPIUI.Messages
{
    public sealed partial class ApplicationUpdateFileDialogWindow : TemplateWindow
    {
        private readonly ILocalizer _localizer = Localizer.Get();
        private string _filePath = string.Empty;
        private bool _handlersConnected;

        private bool ErrorHappens = false;

        private Action<Tuple<string, string>>? _stageChangedHandler;
        private Action<Tuple<string, double>>? _progressChangedHandler;
        private Action<Tuple<string, ErrorModel>>? _errorHandler;
        private Action<string>? _actionsStoppedHandler;
        private Action? _applicationUpdateErrorHandler;

        public ApplicationUpdateFileDialogWindow()
        {
            InitializeComponent();

            WindowTitle = Text("UpdateFileDialogWindowTitle");
            IconUri = @"Assets/favicon.ico";
            CustomTitleBarUserControl = TitleBarUserControl;
            WindowMinSize = new System.Windows.Size(620, 380);
            DisableResizeFeature();
            Closed += ApplicationUpdateFileDialogWindow_Closed;
        }

        public void SetUpdateFilePath(string filePath)
        {
            if (!string.IsNullOrEmpty(_filePath))
                return;
            if (!File.Exists(filePath))
                throw new FileNotFoundException(Text("UpdateFileNotFound"), filePath);
            if (!string.Equals(
                Path.GetExtension(filePath),
                ".cdpipatch",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(Text("UpdateFileInvalidExtension"));
            }

            _filePath = Path.GetFullPath(filePath);
            UpdateFileNameTextBlock.Text = Path.GetFileName(_filePath);
            UpdateFilePathTextBlock.Text = _filePath;
            CurrentVersionTextBlock.Text = string.Format(
                Text("UpdateFileCurrentVersionFormat"),
                ApplicationInfo.Version);
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_filePath))
                return;

            ErrorHappens = false;

            try
            {
                ConnectHandlers();
                SetWorkingState(Text("UpdateFilePreparing"), indeterminate: true);
                InstallButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                ApplicationUpdate.Instance.InstallApplicationUpdateFromFile(_filePath);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ConnectHandlers()
        {
            if (_handlersConnected)
                return;
            _handlersConnected = true;

            _stageChangedHandler = data =>
            {
                if (!IsApplicationOperation(data.Item1))
                    return;

                RunOnUiThread(() =>
                {
                    var text = data.Item2 switch
                    {
                        "GETR" => Text("GettingReady"),
                        "Downloading" => Text("Downloading"),
                        "Extracting" => Text("Installing"),
                        "ConnectingToService" => Text("ConnectingToService"),
                        "END" or "Completed" => Text("Finishing"),
                        _ => Text("UpdateFilePreparing")
                    };
                    SetWorkingState(text, data.Item2 != "Downloading");
                });
            };
            StoreHelper.Instance.ItemDownloadStageChanged += _stageChangedHandler;

            _progressChangedHandler = data =>
            {
                if (!IsApplicationOperation(data.Item1))
                    return;

                RunOnUiThread(() =>
                {
                    StatusProgressbar.IsIndeterminate = false;
                    StatusProgressbar.Value = data.Item2;
                });
            };
            StoreHelper.Instance.ItemDownloadProgressChanged += _progressChangedHandler;

            _errorHandler = data =>
            {
                if (!IsApplicationOperation(data.Item1))
                    return;
                RunOnUiThread(() => ShowError(data.Item2.ErrorCode));
            };
            StoreHelper.Instance.ItemInstallingErrorHappens += _errorHandler;

            _actionsStoppedHandler = itemId =>
            {
                if (itemId != SharedConstants.ApplicationStoreId)
                    return;



                RunOnUiThread(() =>
                    SetWorkingState(Text("UpdateFileStartingInstaller"), indeterminate: true));
            };
            StoreHelper.Instance.ItemActionsStopped += _actionsStoppedHandler;

            _applicationUpdateErrorHandler = () =>
                RunOnUiThread(() => ShowError(ApplicationUpdate.Instance.ErrorInfo));
            ApplicationUpdate.Instance.ErrorHappens += _applicationUpdateErrorHandler;
        }

        private bool IsApplicationOperation(string operationId) =>
            StoreHelper.Instance.GetItemIdFromOperationId(operationId) ==
            SharedConstants.ApplicationStoreId;

        private void SetWorkingState(string status, bool indeterminate)
        {
            if (ErrorHappens) return;
            ErrorGrid.Visibility = Visibility.Collapsed;
            DownloadProgressStackPanel.Visibility = Visibility.Visible;
            CurrentStatusTextBlock.Text = status;
            StatusProgressbar.IsIndeterminate = indeterminate;
        }

        private void ShowError(string message)
        {
            ErrorHappens = true;
            DownloadProgressStackPanel.Visibility = Visibility.Collapsed;
            ErrorCodeTextBlock.Text = message;
            ErrorGrid.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
        }

        private void RunOnUiThread(Action action)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Content != null)
                    action();
            });
        }

        private void ApplicationUpdateFileDialogWindow_Closed(object sender, WindowEventArgs args)
        {
            Closed -= ApplicationUpdateFileDialogWindow_Closed;
            if (_stageChangedHandler != null)
                StoreHelper.Instance.ItemDownloadStageChanged -= _stageChangedHandler;
            if (_progressChangedHandler != null)
                StoreHelper.Instance.ItemDownloadProgressChanged -= _progressChangedHandler;
            if (_errorHandler != null)
                StoreHelper.Instance.ItemInstallingErrorHappens -= _errorHandler;
            if (_actionsStoppedHandler != null)
                StoreHelper.Instance.ItemActionsStopped -= _actionsStoppedHandler;
            if (_applicationUpdateErrorHandler != null)
                ApplicationUpdate.Instance.ErrorHappens -= _applicationUpdateErrorHandler;
        }

        private string Text(string key) => _localizer.GetLocalizedString(key);
    }
}
