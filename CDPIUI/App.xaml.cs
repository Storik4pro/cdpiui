using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Messages;
using CDPIUI.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using WinRT;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;
using WASDK = Microsoft.WindowsAppSDK;
using CDPIUI.Helper.Basic;
using CDPIUI.Helper.Database;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Features;
using CDPIUI.Default;
using CDPIUI.Commands;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.Helper.Migration;
using CDPIUI.Shared.Migration;

namespace CDPIUI
{

    public partial class App : Application
    {
        public ElementTheme CurrentTheme { get; set; } = ElementTheme.Default;

        public List<Window> OpenWindows { get; private set; } = new List<Window>();

        private readonly Dictionary<Window, ModalSession> modalSessions = new();
        private readonly Dictionary<Window, DisabledOwner> disabledOwners = new();
        private readonly Microsoft.UI.Dispatching.DispatcherQueue uiDispatcher =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        private sealed class DisabledOwner
        {
            public IntPtr Handle;
            public bool WasEnabled;
            public int Count;
        }

        private sealed class ModalSession
        {
            public IntPtr Handle;
            public IntPtr PreviousOwner;
            public Window Owner;
            public readonly List<Window> Disabled = new();
            public readonly TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Windows.Foundation.TypedEventHandler<object, WindowEventArgs> ClosedHandler;
        }
        private readonly SemaphoreSlim migrationWindowActivationLock = new(1, 1);

        public App()
        {
            this.InitializeComponent();
            PipeClientService.Instance.MessageReceived += (m) => CommandsHandler.HandleCommand(m);
            CoreErrorsLookupService.Instance.Init();

            UpdateThemeForAllWindows(GetThemeFromString(SettingsManager.Instance.GetValue<string>("APPEARANCE", "Theme")));

            PipeClientService.Instance.Init();
            PipeClientService.Instance.Connected += PipeConnected;

            ApplicationTaskMonitor.Instance.StoreStateChanged += StoreStateChanged;

            GetReadyFeatures();
        }

        private void StoreStateChanged(bool isWorking)
        {
            try
            {
                if (isWorking && OpenWindows.Count == 0 &&
                    !Program.IsMigrationActivationPending)
                {
                    _ = SafeCreateNewWindow<PrepareWindow>(activate: false);
                }
                if (!isWorking)
                {
                    GetCurrentWindowFromType<PrepareWindow>()?.Close();
                    ExitIfIdle();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(App),
                    $"Cannot update background operation state: {ex.Message}");
                ExitIfIdle();
            }
        }

        private async Task PipeConnectedActions()
        {
            _ = TasksManagerHelper.Instance;
            string[] arguments = Environment.GetCommandLineArgs();

            await PipeHelper.SendSettingsPacket(Shared.Pipe.Models.SettingsMessageIds.ReloadSettings);
            await PipeHelper.SendConditionalTasksReloadPacket();
            await PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.GetAllProcessStates);

            ComponentTasksManager.Instance.RequestComponentItemsInit();

            bool isFileProcessed = false;
            bool isActionPreffered = false;
            bool isMigrationProcessed = false;

            try
            {
                isMigrationProcessed = await TryHandleMigrationActivationAsync(arguments);
                if (!isMigrationProcessed &&
                    Program.InitialActivationArguments?.Kind is ExtendedActivationKind.Launch &&
                    Program.InitialActivationArguments.Data is ILaunchActivatedEventArgs initialLaunchArgs)
                {
                    isMigrationProcessed = await TryHandleMigrationActivationAsync(initialLaunchArgs.Arguments);
                }

                if (Program.InitialActivationArguments?.Kind is ExtendedActivationKind.File &&
                    Program.InitialActivationArguments.Data is IFileActivatedEventArgs initialFileArgs)
                {
                    isFileProcessed = await ProcessFiles(
                        initialFileArgs.Files.Select(file => file.Path).ToArray());
                }

                if (Program.InitialActivationArguments?.Kind is ExtendedActivationKind.Protocol &&
                    Program.InitialActivationArguments.Data is IProtocolActivatedEventArgs initialProtocolArgs)
                {
                    isActionPreffered = await CommandsHandler.HandleCommandAsync(
                        initialProtocolArgs.Uri.ToString());
                }

                if (!isFileProcessed)
                    isFileProcessed = await ProcessFiles(arguments);

                string protocolArg = isActionPreffered
                    ? null
                    : arguments.FirstOrDefault(x => x.StartsWith("----ms-protocol:"));

                if (protocolArg != null)
                {
                    string value = protocolArg["----ms-protocol:".Length..];
                    isActionPreffered = await CommandsHandler.HandleCommandAsync(value);
                }

                string directArgs = arguments.FirstOrDefault(x => x.StartsWith("--direct:"));

                if (directArgs != null)
                {
                    string value = directArgs["--direct:".Length..];
                    isActionPreffered = await CoreCommandsHandler.HandleCommandAsync(
                        PipeModelConvertor.ConvertBack(value));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(App),
                    $"Cannot process startup activation: {ex}");
            }

            if (!isFileProcessed && !isActionPreffered && !isMigrationProcessed &&
                !Program.IsMigrationActivationPending)
                await OpenStartupWindowAsync();

            GetCurrentWindowFromType<PrepareWindow>()?.Close();
            await ShowSettingsRecoveryWarningAsync();
            ExitIfIdle();
            Logger.Instance.CreateDebugLog(nameof(App), $"Arguments {arguments}");
            
        }

        private static readonly string[] SupportedFilePaths =
            [".cdpisignedpack", ".cdpiconfigpack", ".cdpipatch", ".cdpitask", ".cdpiconfig"];

        private async Task<bool> ProcessFiles(string[] files)
        {
            foreach (string file in files)
            {
                var _file = file.Replace("\"", "");
                Logger.Instance.CreateDebugLog(nameof(App), $"Working on file {_file}");

                if (Path.Exists(_file) && SupportedFilePaths.Contains(
                    Path.GetExtension(_file),
                    StringComparer.OrdinalIgnoreCase))
                {
                    if (CDPIUI.AddOns.ConfigShare.ConfigShareService.IsSupported(_file))
                    {
                        var importDialog = await UnsafeCreateNewWindow<ConfigShareImportDialog>(activate: false);
                        ActivateWindow(importDialog);
                        await importDialog.SetFileAsync(_file);
                        return true;
                    }

                    if (Path.GetExtension(_file).Equals(".cdpitask", StringComparison.OrdinalIgnoreCase))
                    {
                        var imported = ConditionalTaskFileService.Load(_file);
                        var tasksDirectory = ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
                            SettingsManager.Instance.SettingsFilePath);
                        if (ConditionalTaskFileService.LoadDirectory(tasksDirectory).Any(task =>
                            string.Equals(task.Id, imported.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            imported.Id = Guid.NewGuid().ToString("D");
                        }

                        imported.FilePath = null;
                        imported.IsEnabled = false;
                        var editor = await UnsafeCreateNewWindow<ConditionalTaskEditorWindow>(
                            activate: false,
                            id: ConditionalTaskEditorWindow.WindowIdPrefix + imported.Id);
                        editor.SetTask(imported, tasksDirectory, isImport: true);
                        ActivateWindow(editor);
                        return true;
                    }

                    if (Path.GetExtension(_file).Equals(".cdpipatch", StringComparison.OrdinalIgnoreCase))
                    {
                        var updateDialog = await SafeCreateNewWindow<ApplicationUpdateFileDialogWindow>(activate: false);
                        updateDialog.SetUpdateFilePath(_file);
                        ActivateWindow(updateDialog);
                        return true;
                    }

                    var window = await SafeCreateNewWindow<StoreLocalItemInstallingDialog>();
                    window.SetPackFilePath(_file);
                    return true; // Only first file will be processed.
                }
            }
            return false;
        }

        private void PipeConnected()
        {
            SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "procState");
            SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide");
            SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "appUpdates");
            SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "storeUpdates");
            SettingsManager.Instance.GetValueOrDefault<bool>(
                "NOTIFICATIONS",
                "conditionalLaunchActions",
                defaultValue: true);

            _ = PipeConnectedActions();

            PipeClientService.Instance.Connected -= PipeConnected;
        }

        public async Task NavigateToUpdatesPage()
        {
            MainWindow mainWindow = await SafeCreateNewWindow<MainWindow>();
            mainWindow.NavView_Navigate(typeof(AboutPage), "START_CHECK", new DrillInNavigationTransitionInfo());
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string directArgs = arguments.FirstOrDefault(x => x.StartsWith("--direct:"));

            if (GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out _) ||
                GoodbyeDpiMigrationActivation.TryFindArgument(args.Arguments, out _))
            {
                Program.MarkMigrationActivationPending();
            }

            if (!Program.IsMigrationActivationPending)
            {
                _ = SafeCreateNewWindow<PrepareWindow>(string.IsNullOrEmpty(directArgs));
            }

            PipeClientService.Instance.Start();
        }

        public async void PrefferRequestedActions(AppActivationArguments appActivationArguments)
        {
            bool result = false;
            try
            {
                if (appActivationArguments.Kind is ExtendedActivationKind.Protocol &&
                    appActivationArguments.Data is IProtocolActivatedEventArgs protocolActivatedEventArgs)
                {
                    string value = protocolActivatedEventArgs.Uri.ToString();
                    result = await CommandsHandler.HandleCommandAsync(value);
                }

                if (appActivationArguments.Kind is ExtendedActivationKind.File &&
                    appActivationArguments.Data is IFileActivatedEventArgs activatedFileArgs)
                {
                    result = await ProcessFiles(
                        activatedFileArgs.Files.Select(file => file.Path).ToArray());
                }
                else if (appActivationArguments.Kind is ExtendedActivationKind.Launch &&
                    appActivationArguments.Data is ILaunchActivatedEventArgs fileActivatedEventArgs)
                {
                    result = await TryHandleMigrationActivationAsync(fileActivatedEventArgs.Arguments);
                    if (!result)
                    {
                        string[] args = GetFilesFromStringRegex().Split(fileActivatedEventArgs.Arguments);
                        result = await ProcessFiles(args);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(App),
                    $"Cannot process redirected activation: {ex}");
                result = Program.IsMigrationActivationPending;
            }

            if (!result && !Program.IsMigrationActivationPending)
                await OpenStartupWindowAsync();
            else ExitIfIdle();
        }

        internal async Task<Window> OpenStartupWindowAsync(bool activate = true)
        {
            var welcome = OpenWindows.OfType<WelcomeWindow>().FirstOrDefault();
            if (welcome != null)
            {
                if (activate) ActivateWindow(welcome);
                return welcome;
            }

            if (!SettingsManager.Instance.GetValueOrDefault("WELCOMEWIZARD", "Shown", defaultValue: false))
                return await SafeCreateNewWindow<WelcomeWindow>(activate);

            return await SafeCreateNewWindow<ModernMainWindow>(activate);
        }

        private Task<bool> TryHandleMigrationActivationAsync(string? arguments)
        {
            if (!GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out var request))
                return Task.FromResult(false);
            Program.MarkMigrationActivationPending();
            return OpenMigrationWelcomeAsync(request!);
        }

        private Task<bool> TryHandleMigrationActivationAsync(IEnumerable<string> arguments)
        {
            if (!GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out var request))
                return Task.FromResult(false);
            Program.MarkMigrationActivationPending();
            return OpenMigrationWelcomeAsync(request!);
        }

        private async Task<bool> OpenMigrationWelcomeAsync(
            GoodbyeDpiMigrationActivationRequest request)
        {
            await migrationWindowActivationLock.WaitAsync();
            try
            {
                if (!GoodbyeDpiMigrationCoordinator.Instance.TryAccept(request, out var session) ||
                    session == null)
                {
                    WelcomeWindow existingWindow = OpenWindows
                        .OfType<WelcomeWindow>()
                        .FirstOrDefault(window => string.Equals(
                            window.Id,
                            WelcomeWindow.MigrationWindowId,
                            StringComparison.OrdinalIgnoreCase));
                    if (existingWindow != null)
                        ActivateWindow(existingWindow);
                    return true;
                }

                WelcomeWindow window = await UnsafeCreateNewWindow<WelcomeWindow>(
                    activate: false,
                    id: WelcomeWindow.MigrationWindowId);
                window.SetMigrationSession(session);
                CloseWindow<ModernMainWindow>();
                CloseWindow<MainWindow>();
                CloseWindow<PrepareWindow>();
                ActivateWindow(window);
                return true;
            }
            finally
            {
                if (!OpenWindows.OfType<WelcomeWindow>().Any(window => window.Id == WelcomeWindow.MigrationWindowId))
                    Program.CompleteMigrationActivation();
                migrationWindowActivationLock.Release();
            }
        }

        private bool settingsRecoveryWarningShown;
        private async Task ShowSettingsRecoveryWarningAsync()
        {
            var settings = SettingsManager.Instance;
            if (settingsRecoveryWarningShown ||
                (!settings.HasUnrecoverableLoadError && !File.Exists(settings.RecoveryNoticePath))) return;
            var owner = OpenWindows.LastOrDefault();
            if (owner == null) return;
            settingsRecoveryWarningShown = true;
            try
            {
                var dialog = new Windows.UI.Popups.MessageDialog(
                    Localizer.Get().GetLocalizedString("SettingsRecoveryWarningMessage"),
                    Localizer.Get().GetLocalizedString("SettingsRecoveryWarningTitle"));
                InitializeWithWindow.Initialize(dialog, WindowNative.GetWindowHandle(owner));
                await dialog.ShowAsync();
                try { File.Delete(settings.RecoveryNoticePath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            catch (Exception ex)
            {
                settingsRecoveryWarningShown = false;
                Logger.Instance.CreateWarningLog(nameof(App), ex.ToString());
            }
        }


        public async void GetReadyFeatures()
        {
            DatabaseInitializationService.QuickRestore();
            await InitializeLocalizer();
            ApplicationInfo.Instance.SetLocalization(Localizer.Get().GetCurrentLanguage());
            FileAssociationService.EnsureRegistered();
        }

        public bool CheckWindow<TWindow>() where TWindow : Window
        {
            return (OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null) != null);
        }

        public void ShowWindow<TWindow>() where TWindow : Window
        {
            var activeFindWindow = OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);
            if (activeFindWindow != null)
            {
                activeFindWindow.Activate();
            }
        }

        public async void CreateEmptyWindow()
        {
            Window window = await SafeCreateNewWindow<ViewWindow>();
            window.Hide();
        }

        public void CloseWindow<TWindow>() where TWindow : Window
        {
            foreach (var viewWindow in OpenWindows.OfType<TWindow>().ToList())
            {
                viewWindow.Close();
                OpenWindows.Remove(viewWindow);
            }
        }

        public void CloseWindow<TWindow>(string id) where TWindow : TemplateWindow
        {
            OpenWindows
                .OfType<TWindow>()
                .FirstOrDefault(window => string.Equals(
                    window.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
                ?.Close();
        }

        public static void ActivateWindow(Window window)
        {
            window.Activate();
            try
            {
                var windowHandle = WindowNative.GetWindowHandle(window);
                if (windowHandle == IntPtr.Zero)
                    return;

                SetWindowPos(
                    windowHandle,
                    HWND_TOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetWindowPos(
                    windowHandle,
                    HWND_NOTOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetForegroundWindow(windowHandle);
            }
            catch
            {
            }
        }

        public async Task<Window> UnsafeCreateNewWindow(Type windowType, bool activate = true, string id = "")
        {
            var findWindows = OpenWindows.Where(w => windowType.IsInstanceOfType(w)).ToList();
            int findWindowCount = findWindows.Count;

            var activeFindWindow = findWindows.FirstOrDefault(w => w.DispatcherQueue != null);

            foreach (TemplateWindow _win in findWindows)
            {
                if (!string.IsNullOrEmpty(_win.Id) && string.Equals(
                    _win.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (activate) ActivateWindow(_win);
                    return _win;
                }
            }

            var newWindow = (TemplateWindow)Activator.CreateInstance(windowType);
            newWindow.Id = id;
            WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(newWindow);
            RegisterWindow(newWindow, isUnsafe: true);
            if (activate) ActivateWindow(newWindow);
            await Task.CompletedTask;

            return newWindow;
        }

        public async Task<TWindow> UnsafeCreateNewWindow<TWindow>(bool activate = true, string id = "") where TWindow : TemplateWindow, new()
        {
            var findWindows = OpenWindows.OfType<TWindow>().ToList();
            int findWindowCount = findWindows.Count;

            var activeFindWindow = OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);

            foreach (TemplateWindow _win in findWindows)
            {
                if (!string.IsNullOrEmpty(_win.Id) && string.Equals(
                    _win.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
                {     
                    if (activate) ActivateWindow(_win);
                    return (TWindow)_win;
                }
            }

            var newViewWindow = new TWindow();
            newViewWindow.Id = id;
            WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(newViewWindow);
            RegisterWindow(newViewWindow, isUnsafe:true);
            if (activate) ActivateWindow(newViewWindow);
            await Task.CompletedTask;

            return newViewWindow;
        }
        public async Task<Window> SafeCreateNewWindow(Type windowType, bool activate = true)
        {
            var findWindows = OpenWindows.Where(w => windowType.IsInstanceOfType(w)).ToList();
            int findWindowCount = findWindows.Count;

            var activeFindWindow = findWindows.FirstOrDefault(w => w.DispatcherQueue != null);

            if (activeFindWindow != null && findWindowCount == 1)
            {
                if (activate) ActivateWindow(activeFindWindow);
                await Task.CompletedTask;
                return activeFindWindow;
            }
            else
            {
                foreach (var viewWindow in findWindows)
                {
                    viewWindow.Close();
                    OpenWindows.Remove(viewWindow);
                }

                var newWindow = (Window)Activator.CreateInstance(windowType);

                WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(newWindow);
                RegisterWindow(newWindow);

                if (activate) ActivateWindow(newWindow);
                await Task.CompletedTask;

                return newWindow;
            }
        }

        public async Task<TWindow> SafeCreateNewWindow<TWindow>(bool activate = true) where TWindow : Window, new()
        {
            var findWindows = OpenWindows.OfType<TWindow>().ToList();
            int findWindowCount = findWindows.Count;

            var activeFindWindow = OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);

            if (activeFindWindow != null && findWindowCount == 1)
            {
                if (activate) ActivateWindow(activeFindWindow);
                await Task.CompletedTask;
                return activeFindWindow;
            }
            else
            {
                foreach (var viewWindow in OpenWindows.OfType<TWindow>().ToList())
                {
                    viewWindow.Close();
                    OpenWindows.Remove(viewWindow);
                }

                var newViewWindow = new TWindow();
                WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(newViewWindow);
                RegisterWindow(newViewWindow);

                if (activate) ActivateWindow(newViewWindow);
                await Task.CompletedTask;

                return newViewWindow;
            }
        }

        private void RegisterWindow(Window window, bool isUnsafe = false)
        {
            if (window == null) return;

            UpdateThemeForWindow(window, CurrentTheme);

            if (!OpenWindows.Contains(window))
                    OpenWindows.Add(window);


            window.Closed -= Window_ClosedHandler;
            window.Closed += Window_ClosedHandler;
            

            // Looks pretty bad for weak PC
            // window.SizeChanged -= Window_SizeChanged;
            // window.SizeChanged += Window_SizeChanged;
        }

        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (sender is not Window window) return;
            if (args.Handled) return;

            try
            {
                WindowsPositionHelper.SaveWindowSizeAndPostionsettings(window);
            }
            catch { }
        }

        private void Window_ClosedHandler(object sender, WindowEventArgs e)
        {
            if (sender is not Window window) return;
            if (e.Handled) return;
            var dispatcherQueue = window.DispatcherQueue;
            bool migrationWindowClosed = window is WelcomeWindow welcomeWindow &&
                string.Equals(
                    welcomeWindow.Id,
                    WelcomeWindow.MigrationWindowId,
                    StringComparison.OrdinalIgnoreCase);

            try
            {
                WindowsPositionHelper.SaveWindowSizeAndPostionsettings(window);
            }
            catch { }
            finally
            {
                window.Closed -= Window_ClosedHandler;
                window.SizeChanged -= Window_SizeChanged;

                // Leave visual-tree teardown to WinUI, including diagnostics notifications.
                try { ReleaseModal(window, isClosing: true); }
                catch (Exception exception) { Logger.Instance.CreateWarningLog(nameof(App), exception.ToString()); }
                try { OpenWindows.Remove(window); } catch { }
                if (migrationWindowClosed)
                    Program.CompleteMigrationActivation();
                disabledOwners.Remove(window);
            }

            if (migrationWindowClosed)
            {
                if (!dispatcherQueue.TryEnqueue(
                    Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                    ExitIfIdle))
                {
                    ExitIfIdle();
                }
                return;
            }

            if (OpenWindows.Count == 0 && ApplicationTaskMonitor.IsStoreWorking())
            {
                _ = SafeCreateNewWindow<PrepareWindow>(activate:false);
            }
            else
            {
                if (!dispatcherQueue.TryEnqueue(
                    Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                    ExitIfIdle))
                {
                    ExitIfIdle();
                }
            }
            
        }

        private void ExitIfIdle()
        {
            if (Program.IsMigrationActivationPending && OpenWindows.Count == 0)
            {
                return;
            }
            if (OpenWindows.Count != 0 ||
                ApplicationTaskMonitor.IsStoreWorking() ||
                ApplicationTaskMonitor.IsGoodCheckRunned())
            {
                return;
            }

            Exit();
        }

        public TWindow GetCurrentWindowFromType<TWindow>() where TWindow:Window
        {
            return OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);
        }

        public static TEnum GetEnum<TEnum>(string text) where TEnum : struct
        {
            if (!typeof(TEnum).GetTypeInfo().IsEnum)
            {
                throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
            }
            return (TEnum)Enum.Parse(typeof(TEnum), text);
        }

        public Task ShowWindowModalAsync(Window modalWindow) => ShowWindowModalAsync(modalWindow, null);

        public Task ShowWindowModalAsync(Window modalWindow, Window ownerWindow)
        {
            ArgumentNullException.ThrowIfNull(modalWindow);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            async void Begin()
            {
                try { await BeginModal(modalWindow, ownerWindow); completion.TrySetResult(true); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }
            if (uiDispatcher.HasThreadAccess) Begin();
            else if (!uiDispatcher.TryEnqueue(Begin)) completion.TrySetCanceled();
            return completion.Task;
        }

        private Task BeginModal(Window modalWindow, Window ownerWindow)
        {
            if (modalSessions.TryGetValue(modalWindow, out var existing)) return existing.Completion.Task;
            if (!OpenWindows.Contains(modalWindow)) return Task.CompletedTask;
            var handle = WindowNative.GetWindowHandle(modalWindow);
            if (!IsWindow(handle)) return Task.CompletedTask;
            var session = new ModalSession { Handle = handle, Owner = ownerWindow };
            modalSessions.Add(modalWindow, session);
            try
            {
                if (ownerWindow != null && OpenWindows.Contains(ownerWindow))
                {
                    var ownerHandle = WindowNative.GetWindowHandle(ownerWindow);
                    if (IsWindow(ownerHandle))
                    {
                        session.PreviousOwner = GetWindowLongPtr(handle, GWLP_HWNDPARENT);
                        SetWindowLongPtr(handle, GWLP_HWNDPARENT, ownerHandle);
                    }
                }
                var owners = ownerWindow == null ? OpenWindows.ToArray() : new[] { ownerWindow };
                foreach (var owner in owners)
                {
                    if (owner == modalWindow || !OpenWindows.Contains(owner)) continue;
                    var ownerHandle = WindowNative.GetWindowHandle(owner);
                    if (!IsWindow(ownerHandle)) continue;
                    if (!disabledOwners.TryGetValue(owner, out var state))
                    {
                        state = new DisabledOwner { Handle = ownerHandle, WasEnabled = IsWindowEnabled(ownerHandle) };
                        disabledOwners.Add(owner, state);
                    }
                    state.Count++;
                    session.Disabled.Add(owner);
                    EnableWindow(ownerHandle, false);
                }
                session.ClosedHandler = (_, args) =>
                {
                    if (!args.Handled) ReleaseModal(modalWindow, isClosing: true);
                };
                modalWindow.Closed += session.ClosedHandler;
                return session.Completion.Task;
            }
            catch
            {
                ReleaseModal(modalWindow, isClosing: false);
                throw;
            }
        }

        public Task MakeWindowNormal(Window modalWindow)
        {
            ArgumentNullException.ThrowIfNull(modalWindow);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Restore()
            {
                try { ReleaseModal(modalWindow, isClosing: false); completion.TrySetResult(true); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }
            if (uiDispatcher.HasThreadAccess) Restore();
            else if (!uiDispatcher.TryEnqueue(Restore)) completion.TrySetCanceled();
            return completion.Task;
        }

        private void ReleaseModal(Window modalWindow, bool isClosing)
        {
            if (!modalSessions.Remove(modalWindow, out var session)) return;
            if (session.ClosedHandler != null) modalWindow.Closed -= session.ClosedHandler;
            try
            {
                // Never show, reposition or query a modal window after Closed.
                if (!isClosing && session.Owner != null && IsWindow(session.Handle))
                    SetWindowLongPtr(session.Handle, GWLP_HWNDPARENT,
                        IsWindow(session.PreviousOwner) ? session.PreviousOwner : IntPtr.Zero);
                foreach (var owner in session.Disabled)
                {
                    if (!disabledOwners.TryGetValue(owner, out var state)) continue;
                    if (--state.Count > 0) continue;
                    disabledOwners.Remove(owner);
                    if (state.WasEnabled && OpenWindows.Contains(owner) && IsWindow(state.Handle))
                        EnableWindow(state.Handle, true);
                }
                if (session.Owner != null && OpenWindows.Contains(session.Owner))
                {
                    var ownerHandle = WindowNative.GetWindowHandle(session.Owner);
                    if (IsWindow(ownerHandle) && IsWindowEnabled(ownerHandle)) SetForegroundWindow(ownerHandle);
                }
            }
            finally { session.Completion.TrySetResult(true); }
        }

        public FrameworkElement GetRootFrame()
        {
            foreach (var window in OpenWindows)
            {
                try
                {
                    if (window.Content is FrameworkElement rootElement)
                    {
                        return rootElement;
                    }
                } catch
                {

                }
            }
            throw new Exception($"Unable to get root frame");
        }

        private ElementTheme GetThemeFromString(string theme)
        {
            if (theme == "Dark")
            {
                return ElementTheme.Dark;
            } else if (theme == "Light")
            {
                return ElementTheme.Light;
            } else
            {
                return ElementTheme.Default;
            }
        }

        public ElementTheme GetCurrentTheme()
        {
            return CurrentTheme;
        }

        public void UpdateThemeForWindow(Window window, ElementTheme theme)
        {
            if (window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme;
                if (theme == ElementTheme.Dark)
                {
                    TitleBarHelper.SetCaptionButtonColors(window, Colors.White);
                }
                else if (theme == ElementTheme.Light)
                {
                    TitleBarHelper.SetCaptionButtonColors(window, Colors.Black);
                }
                else
                {
                    TitleBarHelper.ApplySystemThemeToCaptionButtons(window);
                }
            }
        }

        public void UpdateThemeForAllWindows(ElementTheme theme)
        {
            CurrentTheme = theme;
            

            foreach (var window in OpenWindows)
            {
                try
                {
                    UpdateThemeForWindow(window, theme);
                }
                catch
                {
                    Debug.WriteLine("Something went wrong");
                }
            }
        }

        private async Task InitializeLocalizer()
        {
            string stringsFolderPath = Path.Combine(AppContext.BaseDirectory, "Strings");
            StorageFolder stringsFolder = await StorageFolder.GetFolderFromPathAsync(stringsFolderPath);

            string lang = SettingsManager.Instance.GetValue<string>("SYSTEM", "language");
            var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (lang == "NaN")
            {
                lang = culture switch
                {
                    "en" => "en-us",
                    "ru" => "ru",
                    _ => "en-us",
                };
            }

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                .SetOptions(options =>
                {
                    options.DefaultLanguage = lang;
                })
                .Build();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWLP_HWNDPARENT = -8;

        [GeneratedRegex(@"""(.*?)\""")]
        private static partial Regex GetFilesFromStringRegex();
    }
}
