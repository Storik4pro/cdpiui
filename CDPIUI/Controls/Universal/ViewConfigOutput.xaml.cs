using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.PrettyErrorConvertionService;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinUI3Localizer;

namespace CDPIUI.Controls.Universal;

public sealed partial class ViewConfigOutput : UserControl
{
    public static readonly DependencyProperty ComponentIdProperty = DependencyProperty.Register(
        nameof(ComponentId),
        typeof(string),
        typeof(ViewConfigOutput),
        new PropertyMetadata(string.Empty, OnComponentIdChanged));

    private readonly StringBuilder outputBuffer = new();
    private readonly ILocalizer localizer = Localizer.Get();
    private ProcessService processManager;
    private int attachmentVersion;
    private bool isLoaded;

    public ViewConfigOutput()
    {
        InitializeComponent();
        Loaded += ViewConfigOutput_Loaded;
        Unloaded += ViewConfigOutput_Unloaded;

        UpdateComponentLabel();
        UpdateRunningState(false, showMessage: false);
    }

    public string ComponentId
    {
        get => (string)GetValue(ComponentIdProperty);
        set => SetValue(ComponentIdProperty, value);
    }

    public bool IsProcessRunning => processManager?.IsProcessRunning ?? false;
    public string OutputText => outputBuffer.ToString();

    public event Action<bool> RunningStateChanged;

    public async Task RefreshComponentAsync()
    {
        int version = ++attachmentVersion;
        DisconnectHandlers();
        ClearOutput();
        UpdateComponentLabel();

        if (!isLoaded || string.IsNullOrWhiteSpace(ComponentId))
        {
            UpdateRunningState(false, showMessage: false);
            return;
        }

        ProcessService candidate = (await ComponentTasksManager.Instance.GetTaskFromId(ComponentId))?.ProcessManager;
        if (version != attachmentVersion)
        {
            return;
        }

        processManager = candidate;
        if (processManager == null)
        {
            UpdateRunningState(false, showMessage: false);
            return;
        }

        processManager.OutputReceived += ProcessManager_OutputReceived;
        processManager.OutputReset += ProcessManager_OutputReset;
        processManager.ProcessStateChanged += ProcessManager_ProcessStateChanged;
        processManager.ErrorHappens += ProcessManager_ErrorHappens;
        processManager.ProcessNameChanged += ProcessManager_ProcessNameChanged;

        RefreshOutput();
        UpdateRunningState(processManager.IsProcessRunning, showMessage: false);
        await processManager.GetReady(true);
    }

    public void RefreshOutput()
    {
        ClearOutput();
        AppendOutput(GetConfiguredOutput());
    }

    public void ClearOutput()
    {
        outputBuffer.Clear();
        OutputParagraph.Inlines.Clear();
    }

    public void CopyAll()
    {
        DataPackage package = new();
        package.SetText(OutputText);
        Clipboard.SetContent(package);
    }

    private static void OnComponentIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ViewConfigOutput control)
        {
            _ = control.RefreshComponentAsync();
        }
    }

    private void ViewConfigOutput_Loaded(object sender, RoutedEventArgs e)
    {
        isLoaded = true;
        _ = RefreshComponentAsync();
    }

    private void ViewConfigOutput_Unloaded(object sender, RoutedEventArgs e)
    {
        isLoaded = false;
        ++attachmentVersion;
        DisconnectHandlers();
    }

    private void DisconnectHandlers()
    {
        if (processManager == null)
        {
            return;
        }

        processManager.OutputReceived -= ProcessManager_OutputReceived;
        processManager.OutputReset -= ProcessManager_OutputReset;
        processManager.ProcessStateChanged -= ProcessManager_ProcessStateChanged;
        processManager.ErrorHappens -= ProcessManager_ErrorHappens;
        processManager.ProcessNameChanged -= ProcessManager_ProcessNameChanged;
        processManager = null;
    }

    private void ProcessManager_OutputReceived(string output)
    {
        QueueRefresh(() => SynchronizeOutput());
    }

    private void ProcessManager_OutputReset() => QueueRefresh(() => SynchronizeOutput());

    private void QueueRefresh(Action action)
    {
        int version = attachmentVersion;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isLoaded && version == attachmentVersion) action();
        });
    }

    private void SynchronizeOutput()
    {
        // Read the current snapshot: queued output and full-buffer replies may overlap.
        string text = GetConfiguredOutput();
        string displayed = OutputText;
        if (text.StartsWith(displayed, StringComparison.Ordinal))
        {
            AppendOutput(text[displayed.Length..]);
        }
        else
        {
            ClearOutput();
            AppendOutput(text);
        }
    }

    private void ProcessManager_ProcessStateChanged(Tuple<string, bool> state)
    {
        if (!string.Equals(state.Item1, ComponentId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        QueueRefresh(() =>
        {
            SynchronizeOutput();
            UpdateRunningState(IsProcessRunning, showMessage: true);
        });
    }

    private void ProcessManager_ProcessNameChanged(string processName)
    {
        QueueRefresh(() =>
        {
            UpdateComponentLabel();
            UpdateRunningState(IsProcessRunning, showMessage: true);
        });
    }

    private void ProcessManager_ErrorHappens(ErrorModel error)
    {
        QueueRefresh(() => UpdateRunningState(IsProcessRunning, showMessage: true));
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        outputBuffer.Append(text);
        OutputParagraph.Inlines.Add(new Run
        {
            Text = text,
            Foreground = new SolidColorBrush(Colors.LightGray),
        });
        OutputScrollViewer.ChangeView(null, OutputScrollViewer.ScrollableHeight, null, disableAnimation: true);
    }

    private string GetConfiguredOutput()
    {
        if (processManager == null)
        {
            return string.Empty;
        }

        string text = SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "outputMode")
            ? processManager.GetProcessOutput()
            : processManager.GetDefaultProcessOutput();

        return SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "prettyPathView")
            ? ProcessService.ReplacePath(text)
            : text;
    }

    private void UpdateComponentLabel()
    {
        DatabaseStoreItem item = string.IsNullOrWhiteSpace(ComponentId)
            ? null
            : DatabaseHelper.Instance.GetItemById(ComponentId);
        SelectedComponentTextBlock.Text = item == null
            ? localizer.GetLocalizedString("NoComponent")
            : string.Format(
                localizer.GetLocalizedString("NowViewOutputFromComponent"),
                item.ShortName ?? item.Name ?? item.Id);
    }

    private void UpdateRunningState(bool isRunning, bool showMessage)
    {
        bool hasError = processManager?.IsErrorHappens == true;
        isRunning = isRunning && !hasError;
        StatusIcon.Glyph = isRunning ? "\uEC61" : "\uEB90";
        StatusIcon.Foreground = new SolidColorBrush((Color)Application.Current.Resources[
            isRunning ? "SystemFillColorSuccess" : "SystemFillColorCritical"]);
        StatusTextBlock.Text = localizer.GetLocalizedString(hasError ? "ErrorHappens" : isRunning ? "ProcessStarted" : "ProcessStopped");

        if (hasError)
        {
            StatusInfoBar.Title = localizer.GetLocalizedString("ComponentFailureTitle");
            StatusInfoBar.Message = processManager.LastError?.ErrorCode ?? "UNKNOWN_ERROR";
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.IsOpen = true;
        }
        else if (showMessage)
        {
            string processName = GetProcessName();
            StatusInfoBar.Title = localizer.GetLocalizedString(
                isRunning ? "ProcessStartedMessageTitle" : "ProcessStoppedMessageTitle");
            StatusInfoBar.Message = string.IsNullOrWhiteSpace(processName)
                ? string.Empty
                : string.Format(
                    localizer.GetLocalizedString(
                        isRunning ? "ProcessStartedMessageMessage" : "ProcessStoppedMessageMessage"),
                    processName);
            StatusInfoBar.Severity = isRunning
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Informational;
            StatusInfoBar.IsOpen = true;
        }
        else
        {
            StatusInfoBar.IsOpen = false;
        }

        RunningStateChanged?.Invoke(isRunning);
    }

    private string GetProcessName()
    {
        if (!string.IsNullOrWhiteSpace(processManager?.ProcessName))
        {
            return processManager.ProcessName;
        }

        DatabaseStoreItem item = string.IsNullOrWhiteSpace(ComponentId)
            ? null
            : DatabaseHelper.Instance.GetItemById(ComponentId);
        return string.IsNullOrWhiteSpace(item?.Executable) ? string.Empty : $"{item.Executable}.exe";
    }
}
