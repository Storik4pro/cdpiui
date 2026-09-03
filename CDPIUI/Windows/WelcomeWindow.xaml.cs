using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Controls.Store;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Default;
using CDPIUI.Extensions;
using CDPIUI.Helper;
using CDPIUI.Helper.Migration;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.ViewModels;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;


namespace CDPIUI
{
    public sealed class MigrationComponentDisplayItem
    {
        public string StoreId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    public sealed partial class WelcomeWindow : TemplateWindow
    {
        public const string MigrationWindowId = "GoodbyeDpiMigrationWelcome";

        private ILocalizer localizer = Localizer.Get();
        private GoodbyeDpiMigrationSession? migrationSession;
        private CancellationTokenSource migrationCancellation = new();
        private List<RepoItemModel> migrationComponents = [];
        private readonly Dictionary<string, double> migrationComponentProgress = new(StringComparer.OrdinalIgnoreCase);
        private bool migrationFlowStarted;
        private bool migrationImportInProgress;
        private bool migrationReadyToContinue;
        private bool migrationHandlersConnected;
        private Action<Tuple<string, string>>? migrationStageHandler;
        private Action<Tuple<string, double>>? migrationProgressHandler;
        private Action<Tuple<string, ErrorModel>>? migrationErrorHandler;
        private Action<string>? migrationStoppedHandler;

        private MarkdownConfig _config;

        public MarkdownConfig MarkdownConfig
        {
            get => _config;
            set => _config = value;
        }

        public ObservableCollection<StoreViewBundleItem> Kits = [];

        public WelcomeWindow()
        {
            InitializeComponent();

            WindowTitle = localizer.GetLocalizedString("WelcomeWindowTitle");
            IconUri = @"Assets/Icons/find_error.png";
            this.CustomTitleBarUserControl = TitleBarUserControl;

            DisableResizeFeature();

            _config = new MarkdownConfig();
            Closed += WelcomeWindow_Closed;
            localizer.LanguageChanged += Localizer_LanguageChanged;

            StoreViewBundles.ItemsSource = Kits;
        }

        internal void SetMigrationSession(GoodbyeDpiMigrationSession session)
        {
            if (ReferenceEquals(migrationSession, session))
            {
                ApplyMigrationSessionState();
                return;
            }

            if (migrationSession != null)
                migrationSession.Changed -= MigrationSession_Changed;
            DisconnectMigrationQueueHandlers();
            migrationCancellation.Cancel();
            migrationCancellation.Dispose();
            migrationCancellation = new CancellationTokenSource();

            migrationSession = session;
            migrationSession.Changed += MigrationSession_Changed;
            migrationFlowStarted = false;
            migrationImportInProgress = false;
            migrationReadyToContinue = false;
            migrationComponents = [];
            migrationComponentProgress.Clear();

            ReadyBundlesContent.Visibility = Visibility.Collapsed;
            MigrationContent.Visibility = Visibility.Visible;
            UpdateMigrationLocalizedText();
            ApplyMigrationSessionState();
            CheckNavigation();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn &&
                btn == NextButton &&
                AnimatedHorizontalContentViewer.SelectedItem == StoreItem && 
                StoreViewBundles.SelectedItems.Count > 0) 
                DownloadItems();

            AnimatedHorizontalContentViewer.GoNext();
            CheckNavigation();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            AnimatedHorizontalContentViewer.GoPrevious();
            CheckNavigation();
        }

        private void CheckNavigation()
        {
            UtilityButtonControls.HelpUrl = string.Empty;
            NextButton.IsEnabled = true;
            UtilityButtonControls.IsLoading = false;
            var sel = AnimatedHorizontalContentViewer.SelectedItem;

            bool isComplete = sel == CompleteItem;
            UtilityButtonControls.SetButtonVisibilities(
                (BackButton, sel == WelcomeItem || isComplete
                    ? Visibility.Collapsed
                    : Visibility.Visible),
                (SkipButton, sel == StoreItem && migrationSession == null ? Visibility.Visible : Visibility.Collapsed),
                (NextButton, isComplete ? Visibility.Collapsed : Visibility.Visible),
                (CompleteButton, isComplete ? Visibility.Visible : Visibility.Collapsed));

            if (sel == LicenseItem)
            {
                TryLoadLicense();
                NextButton.IsEnabled = LicenseAgreeCheckBox.IsChecked ?? false;
            }
            else if (sel == AdItem)
            {
                UtilityButtonControls.HelpUrl = "/Other/Ad";
            }
            else if (sel == StoreItem)
            {
                if (migrationSession == null)
                {
                    TryLoadStore();
                }
                else
                {
                    UtilityButtonControls.SetButtonVisibilities(
                        (SkipButton, Visibility.Collapsed),
                        (NextButton, Visibility.Visible));
                    NextButton.IsEnabled = migrationReadyToContinue;
                    TryStartMigrationFlow();
                }
            }
        }

        private async void DownloadItems()
        {
            foreach (StoreViewBundleItem kit in StoreViewBundles.SelectedItems.Cast<StoreViewBundleItem>())
            {
                var _kit = StoreHelper.Instance.GetReadyKitFromStoreId(kit.KitId);
                if (_kit == null) continue;

                var _items = (_kit.items ?? [])
                    .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
                    .OfType<RepoItemModel>()
                    .ToList();

                List<RepoItemModel> itemsToInstall = _items
                    .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id))
                    .ToList();
                if (itemsToInstall.Count == 0)
                    return;

                List<ItemLicenseModel> licenses = itemsToInstall
                    .SelectMany(item => item.license ?? [])
                    .GroupBy(license => $"{license.name}\n{license.url}")
                    .Select(group => group.First())
                    .ToList();

                if (licenses.Count > 0)
                {
                    AcceptLicenseContentDialog dialog = new()
                    {
                        Licenses = licenses,
                        XamlRoot = this.Content.XamlRoot
                    };
                    if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                        return;
                }

                HashSet<string> pendingIds = itemsToInstall
                    .Select(item => item.store_id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string itemId in _kit.items ?? [])
                {
                    if (pendingIds.Contains(itemId))
                        StoreHelper.Instance.AddItemToQueue(itemId, string.Empty);
                }
            }
            
        }

        private async void TryLoadStore()
        {
            ErrorStackPanel.Visibility =Visibility.Collapsed;
            StoreViewBundles.Visibility = Visibility.Collapsed;

            UtilityButtonControls.SetButtonVisibilities(
                (NextButton, Visibility.Collapsed),
                (SkipButton, Visibility.Visible));
            UtilityButtonControls.IsLoading = true;
            UtilityButtonControls.LoadingStateText = localizer.GetLocalizedString("UpdatingStoreDatabase");

            bool result = await StoreHelper.Instance.LoadAllStoreDatabase();

            if (result) result = LoadReadyKits();

            ErrorStackPanel.Visibility = result ? Visibility.Collapsed : Visibility.Visible;
            StoreViewBundles.Visibility = result ? Visibility.Visible : Visibility.Collapsed;

            if (AnimatedHorizontalContentViewer.SelectedItem == StoreItem)
            {
                UtilityButtonControls.IsLoading = false;
                UtilityButtonControls.SetButtonVisibilities(
                    (NextButton, Visibility.Collapsed),
                    (SkipButton, Visibility.Visible));
            }
        }

        private bool LoadReadyKits()
        {
            Kits.Clear();
            List<ReadyKitModel> kits = StoreHelper.Instance.ReadyKits
                .OrderByDescending(kit => kit.IsRecommended)
                .ToList();

            if (kits.Count == 0)
            {
                Kits.Clear();
                return false;
            }
            foreach (var kit in kits.Select(ReadyKitViewModelFactory.Create))
                Kits.Add(kit);

            return true;
        }

        private void StoreViewBundles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UtilityButtonControls.SetButtonVisibilities(
                (NextButton, StoreViewBundles.SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed),
                (SkipButton, Visibility.Visible));
        }

        private void MigrationSession_Changed(object? sender, EventArgs e)
        {
            if (DispatcherQueue.HasThreadAccess)
                ApplyMigrationSessionState();
            else
                DispatcherQueue.TryEnqueue(ApplyMigrationSessionState);
        }

        private void ApplyMigrationSessionState()
        {
            if (migrationSession == null)
                return;
            MigrationProgressBar.Value = Math.Max(MigrationProgressBar.Value, migrationSession.Progress);
            switch (migrationSession.State)
            {
                case MigrationSessionState.Accepted:
                case MigrationSessionState.Preparing:
                    ShowMigrationStatus("MigrationPreparingTitle", "MigrationPreparingMessage", InfoBarSeverity.Informational);
                    break;
                case MigrationSessionState.ReadyForComponents:
                    ShowMigrationStatus("MigrationComponentsTitle", "MigrationComponentsReadyMessage", InfoBarSeverity.Informational);
                    if (AnimatedHorizontalContentViewer.SelectedItem == StoreItem)
                        TryStartMigrationFlow();
                    break;
                case MigrationSessionState.WaitingForLicense:
                    ShowMigrationStatus("MigrationComponentLicenseTitle", "MigrationComponentLicenseMessage", InfoBarSeverity.Warning);
                    break;
                case MigrationSessionState.LoadingComponents:
                    ShowMigrationStatus("MigrationComponentsTitle", "MigrationLoadingStoreMessage", InfoBarSeverity.Informational);
                    break;
                case MigrationSessionState.DownloadingComponents:
                    ShowMigrationStatus("MigrationComponentsTitle", "MigrationDownloadingMessage", InfoBarSeverity.Informational);
                    break;
                case MigrationSessionState.ReadyToImport:
                case MigrationSessionState.Importing:
                    ShowMigrationStatus("MigrationImportTitle", "MigrationImportMessage", InfoBarSeverity.Informational);
                    break;
                case MigrationSessionState.Completed:
                    migrationReadyToContinue = true;
                    bool completedWithWarnings = string.Equals(
                        migrationSession.ErrorCode,
                        "MIGRATION_IMPORT_INCOMPLETE",
                        StringComparison.Ordinal);
                    ShowMigrationStatus(
                        completedWithWarnings ? "MigrationCompletedWithWarningsTitle" : "MigrationCompletedTitle",
                        completedWithWarnings ? "MigrationImportIncompleteMessage" : "MigrationCompletedMessage",
                        completedWithWarnings ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
                    RetryMigrationButton.Visibility = Visibility.Collapsed;
                    MigrationProgressBar.Value = 100;
                    break;
                case MigrationSessionState.Failed:
                    ShowMigrationError(
                        migrationSession.Message ?? Localized("MigrationGenericError"),
                        migrationSession.ErrorCode ?? "MIGRATION_FAILED");
                    break;
            }
            if (AnimatedHorizontalContentViewer.SelectedItem == StoreItem)
                NextButton.IsEnabled = migrationReadyToContinue;
        }

        private async void TryStartMigrationFlow()
        {
            if (migrationSession == null || migrationReadyToContinue || migrationFlowStarted)
                return;
            if (migrationSession.Package == null)
            {
                if (migrationSession.State == MigrationSessionState.Failed)
                    ShowMigrationError(
                        migrationSession.Message ?? Localized("MigrationGenericError"),
                        migrationSession.ErrorCode ?? "MIGRATION_PREPARATION_FAILED");
                else
                    ShowMigrationStatus("MigrationPreparingTitle", "MigrationPreparingMessage", InfoBarSeverity.Informational);
                return;
            }

            VerifiedMigrationPackage package = migrationSession.Package;
            if (MigrationImportService.IsAlreadyCompleted(package))
            {
                MigrationSummaryTextBlock.Text = Localized("MigrationAlreadyCompleted");
                await migrationSession.UpdateAsync(MigrationSessionState.Completed, 100);
                return;
            }

            migrationFlowStarted = true;
            RetryMigrationButton.Visibility = Visibility.Collapsed;
            MigrationComponentsListView.Visibility = Visibility.Visible;
            await migrationSession.UpdateAsync(MigrationSessionState.LoadingComponents, 22);
            bool storeLoaded = await StoreHelper.Instance.LoadAllStoreDatabase();
            if (!storeLoaded)
            {
                await FailMigrationAsync(
                    Localized("MigrationStoreLoadFailed"),
                    "MIGRATION_STORE_LOAD_FAILED");
                return;
            }

            migrationComponents = package.Components
                .Select(component => StoreHelper.Instance.GetItemInfoFromStoreId(component.InstallItemId))
                .OfType<RepoItemModel>()
                .GroupBy(component => component.store_id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            if (migrationComponents.Count != package.Components.Count)
            {
                await FailMigrationAsync(
                    Localized("MigrationComponentMissingFromStore"),
                    "MIGRATION_COMPONENT_NOT_FOUND");
                return;
            }
            if (migrationComponents.Any(component => !IsItemSupported(component)))
            {
                await FailMigrationAsync(
                    Localized("MigrationComponentUnsupported"),
                    "MIGRATION_COMPONENT_UNSUPPORTED");
                return;
            }

            UpdateMigrationComponentList();
            List<RepoItemModel> itemsToInstall = migrationComponents
                .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id!))
                .ToList();
            if (itemsToInstall.Count == 0)
            {
                await BeginMigrationImportAsync();
                return;
            }

            List<ItemLicenseModel> licenses = itemsToInstall
                .SelectMany(item => item.license ?? [])
                .GroupBy(license => $"{license.name}\n{license.url}")
                .Select(group => group.First())
                .ToList();
            if (licenses.Count > 0)
            {
                await migrationSession.UpdateAsync(MigrationSessionState.WaitingForLicense, 25);
                AcceptLicenseContentDialog dialog = new()
                {
                    Licenses = licenses,
                    XamlRoot = Content.XamlRoot
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    migrationFlowStarted = false;
                    RetryMigrationButton.Visibility = Visibility.Visible;
                    MigrationSummaryTextBlock.Text = Localized("MigrationComponentLicenseDeclined");
                    return;
                }
            }

            ConnectMigrationQueueHandlers();
            foreach (RepoItemModel item in itemsToInstall)
            {
                migrationComponentProgress[item.store_id!] = 0;
                StoreHelper.Instance.RemoveItemFromQueue(item.store_id!);
                StoreHelper.Instance.AddItemToQueue(item.store_id!, string.Empty);
            }
            await migrationSession.UpdateAsync(MigrationSessionState.DownloadingComponents, 25);
            UpdateMigrationComponentList();
            UpdateMigrationComponentProgress();
        }

        private bool IsItemSupported(RepoItemModel item)
        {
            Logger.Instance.CreateDebugLog(nameof(WelcomeWindow), $"Checking is item supported for {item.store_id} ({item.short_name})");

            Version curVer = new Version(ApplicationInfo.Version);
            Version minV = new Version(item.target_minversion);

            if (curVer < minV) return false;

            if (item.target_maxversion != "NaN")
            {
                Version maxV = new Version(item.target_maxversion);
                if (curVer > maxV) return false;
            }
            return true;
        }


        private void ConnectMigrationQueueHandlers()
        {
            if (migrationHandlersConnected)
                return;
            migrationStageHandler = data =>
            {
                string? itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
                if (!ContainsMigrationComponent(itemId))
                    return;
                RunMigrationOnUiThread(() =>
                {
                    UpdateMigrationComponentList();
                    UpdateMigrationComponentProgress();
                });
            };
            migrationProgressHandler = data =>
            {
                string? itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
                if (!ContainsMigrationComponent(itemId))
                    return;
                RunMigrationOnUiThread(() =>
                {
                    migrationComponentProgress[itemId!] = data.Item2;
                    UpdateMigrationComponentProgress();
                });
            };
            migrationErrorHandler = data =>
            {
                string? itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
                if (!ContainsMigrationComponent(itemId))
                    return;
                RunMigrationOnUiThread(() => _ = FailMigrationAsync(
                    Localized("MigrationComponentInstallFailed"),
                    string.IsNullOrWhiteSpace(data.Item2.ErrorCode)
                        ? "MIGRATION_COMPONENT_INSTALL_FAILED"
                        : data.Item2.ErrorCode));
            };
            migrationStoppedHandler = itemId =>
            {
                if (!ContainsMigrationComponent(itemId))
                    return;
                RunMigrationOnUiThread(() => _ = HandleMigrationQueueStoppedAsync(itemId));
            };
            StoreHelper.Instance.ItemDownloadStageChanged += migrationStageHandler;
            StoreHelper.Instance.ItemDownloadProgressChanged += migrationProgressHandler;
            StoreHelper.Instance.ItemInstallingErrorHappens += migrationErrorHandler;
            StoreHelper.Instance.ItemActionsStopped += migrationStoppedHandler;
            migrationHandlersConnected = true;
        }

        private void DisconnectMigrationQueueHandlers()
        {
            if (!migrationHandlersConnected)
                return;
            StoreHelper.Instance.ItemDownloadStageChanged -= migrationStageHandler;
            StoreHelper.Instance.ItemDownloadProgressChanged -= migrationProgressHandler;
            StoreHelper.Instance.ItemInstallingErrorHappens -= migrationErrorHandler;
            StoreHelper.Instance.ItemActionsStopped -= migrationStoppedHandler;
            migrationHandlersConnected = false;
        }

        private async Task HandleMigrationQueueStoppedAsync(string itemId)
        {
            migrationComponentProgress[itemId] = DatabaseHelper.Instance.IsItemInstalled(itemId) ? 100 : 0;
            UpdateMigrationComponentList();
            UpdateMigrationComponentProgress();
            if (migrationComponents.Count > 0 && migrationComponents.All(
                    item => DatabaseHelper.Instance.IsItemInstalled(item.store_id!)))
            {
                DisconnectMigrationQueueHandlers();
                await BeginMigrationImportAsync();
                return;
            }

            bool hasQueuedItems = migrationComponents.Any(item =>
                !string.IsNullOrWhiteSpace(StoreHelper.Instance.GetOperationIdFromItemId(item.store_id!)));
            if (!hasQueuedItems)
            {
                await FailMigrationAsync(
                    Localized("MigrationComponentInstallStopped"),
                    "MIGRATION_COMPONENT_INSTALL_STOPPED");
            }
        }

        private async Task BeginMigrationImportAsync()
        {
            if (migrationSession?.Package == null || migrationImportInProgress || migrationReadyToContinue)
                return;
            migrationImportInProgress = true;
            DisconnectMigrationQueueHandlers();
            RetryMigrationButton.Visibility = Visibility.Collapsed;
            MigrationComponentsListView.Visibility = migrationComponents.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
            await migrationSession.UpdateAsync(MigrationSessionState.ReadyToImport, 65);
            await migrationSession.UpdateAsync(MigrationSessionState.Importing, 67);

            try
            {
                Progress<double> importProgress = new(value =>
                {
                    double overall = 67 + Math.Clamp(value, 0, 100) * 0.31;
                    MigrationProgressBar.Value = overall;
                    _ = migrationSession.UpdateAsync(MigrationSessionState.Importing, overall);
                });
                MigrationImportResult result = await Task.Run(() =>
                    new MigrationImportService().Import(
                        migrationSession.Package,
                        importProgress,
                        migrationCancellation.Token));

                try
                {
                    ComponentItemsLoaderHelper.Instance.Init();
                    foreach (MigrationComponentRequirement component in migrationSession.Package.Components)
                        ComponentItemsLoaderHelper.Instance
                            .GetComponentHelperFromId(component.InstallItemId)?
                            .ReInitConfigs();
                }
                catch (Exception exception)
                {
                    Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
                    result.Issues.Add(new MigrationImportIssue(
                        "configuration-cache",
                        "CDPIUI",
                        exception.Message));
                }

                string summary = string.Format(
                    Localized("MigrationImportSummary"),
                    result.ImportedPresetCount,
                    result.ImportedResourceCount,
                    result.ImportedSettingCount,
                    result.ReviewRequiredCount);
                if (result.Issues.Count > 0)
                {
                    summary += "\n\n" + Localized("MigrationImportIncompleteMessage") + " " +
                        string.Format(Localized("MigrationImportWarningsSummary"), result.Issues.Count);
                }
                MigrationSummaryTextBlock.Text = summary;
                migrationReadyToContinue = true;
                migrationImportInProgress = false;
                await migrationSession.UpdateAsync(
                    MigrationSessionState.Completed,
                    100,
                    result.Issues.Count > 0 ? Localized("MigrationImportIncompleteMessage") : null,
                    result.Issues.Count > 0 ? "MIGRATION_IMPORT_INCOMPLETE" : null);
                CheckNavigation();
            }
            catch (OperationCanceledException)
            {
                migrationImportInProgress = false;
                await FailMigrationAsync(Localized("MigrationCanceled"), "MIGRATION_CANCELED");
            }
            catch (Exception exception)
            {
                migrationImportInProgress = false;
                Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
                MigrationSummaryTextBlock.Text = Localized("MigrationImportIncompleteMessage");
                migrationReadyToContinue = true;
                await migrationSession.UpdateAsync(
                    MigrationSessionState.Completed,
                    100,
                    Localized("MigrationImportIncompleteMessage"),
                    "MIGRATION_IMPORT_INCOMPLETE");
                CheckNavigation();
            }
        }

        private void UpdateMigrationComponentProgress()
        {
            if (migrationComponents.Count == 0)
            {
                MigrationProgressBar.Value = 65;
                return;
            }
            double total = 0;
            foreach (RepoItemModel item in migrationComponents)
            {
                if (DatabaseHelper.Instance.IsItemInstalled(item.store_id!))
                {
                    total += 100;
                    continue;
                }
                string? operationId = StoreHelper.Instance.GetOperationIdFromItemId(item.store_id!);
                double value = operationId == null
                    ? migrationComponentProgress.GetValueOrDefault(item.store_id!, 0)
                    : StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadProgress ?? 0;
                total += Math.Clamp(value, 0, 100);
            }
            double aggregate = total / migrationComponents.Count;
            double overall = 25 + aggregate * 0.4;
            MigrationProgressBar.Value = overall;
            if (migrationSession != null)
                _ = migrationSession.UpdateAsync(MigrationSessionState.DownloadingComponents, overall);
        }

        private void UpdateMigrationComponentList()
        {
            List<MigrationComponentDisplayItem> items = [];
            foreach (RepoItemModel component in migrationComponents)
            {
                string id = component.store_id!;
                string status;
                if (DatabaseHelper.Instance.IsItemInstalled(id))
                {
                    status = Localized("MigrationComponentInstalled");
                }
                else
                {
                    string? operationId = StoreHelper.Instance.GetOperationIdFromItemId(id);
                    QueueItemModel? queueItem = string.IsNullOrWhiteSpace(operationId)
                        ? null
                        : StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
                    status = queueItem?.DownloadStage switch
                    {
                        "Downloading" => Localized("MigrationComponentDownloading"),
                        "Extracting" => Localized("MigrationComponentInstalling"),
                        "Completed" or "END" => Localized("MigrationComponentFinishing"),
                        "ErrorHappens" => Localized("MigrationComponentError"),
                        _ => Localized("MigrationComponentWaiting")
                    };
                }
                string displayName = component.short_name ??
                    StoreHelper.Instance.GetLocalizedStoreItemName(
                        component.name,
                        StoreLocalizationHelper.GetStoreLikeLocale());
                items.Add(new MigrationComponentDisplayItem
                {
                    StoreId = id,
                    DisplayName = displayName,
                    Status = status
                });
            }
            MigrationComponentsListView.ItemsSource = null;
            MigrationComponentsListView.ItemsSource = items;
            MigrationSummaryTextBlock.Text = items.Count == 0
                ? Localized("MigrationNoComponentsRequired")
                : string.Format(Localized("MigrationComponentsCount"), items.Count);
        }

        private async Task FailMigrationAsync(string message, string errorCode)
        {
            migrationFlowStarted = false;
            migrationReadyToContinue = false;
            DisconnectMigrationQueueHandlers();
            ShowMigrationError(message, errorCode);
            if (migrationSession != null)
                await migrationSession.UpdateAsync(MigrationSessionState.Failed,
                    MigrationProgressBar.Value, message, errorCode);
        }

        private async void RetryMigrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (migrationSession == null || migrationImportInProgress)
                return;
            RetryMigrationButton.Visibility = Visibility.Collapsed;
            MigrationSummaryTextBlock.Text = string.Empty;
            migrationFlowStarted = false;
            if (migrationSession.Package == null)
                await migrationSession.RetryPreparationAsync();
            if (migrationSession.Package != null)
                TryStartMigrationFlow();
        }

        private void ShowMigrationStatus(
            string titleKey,
            string messageKey,
            InfoBarSeverity severity)
        {
            MigrationStatusInfoBar.Title = Localized(titleKey);
            MigrationStatusInfoBar.Message = Localized(messageKey);
            MigrationStatusInfoBar.Severity = severity;
            MigrationStatusInfoBar.IsOpen = true;
        }

        private void ShowMigrationError(string message, string errorCode)
        {
            MigrationStatusInfoBar.Title = Localized("MigrationFailedTitle");
            MigrationStatusInfoBar.Message = $"{message}\n\n{errorCode}";
            MigrationStatusInfoBar.Severity = InfoBarSeverity.Error;
            MigrationStatusInfoBar.IsOpen = true;
            RetryMigrationButton.Visibility = Visibility.Visible;
            NextButton.IsEnabled = false;
        }

        private bool ContainsMigrationComponent(string? itemId) =>
            !string.IsNullOrWhiteSpace(itemId) && migrationComponents.Any(item =>
                string.Equals(item.store_id, itemId, StringComparison.OrdinalIgnoreCase));

        private void RunMigrationOnUiThread(Action action)
        {
            if (DispatcherQueue.HasThreadAccess)
                action();
            else
                DispatcherQueue.TryEnqueue(() => action());
        }

        private string Localized(string key) =>
            localizer.GetLocalizedString($"/WelcomeWizard/{key}");

        private void UpdateMigrationLocalizedText()
        {
            if (migrationSession == null)
                return;
            StoreItem.Header = Localized("MigrationAnimatedHorizontalContentItemHeader");
            StoreItem.Description = Localized("MigrationAnimatedHorizontalContentItemDescription");
            UpdateMigrationComponentList();
        }

        private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            UpdateMigrationLocalizedText();
            ApplyMigrationSessionState();
        }

        private void WelcomeWindow_Closed(object sender, WindowEventArgs args)
        {
            DisconnectMigrationQueueHandlers();
            migrationCancellation.Cancel();
            migrationCancellation.Dispose();
            localizer.LanguageChanged -= Localizer_LanguageChanged;
            Closed -= WelcomeWindow_Closed;
            if (migrationSession != null)
            {
                migrationSession.Changed -= MigrationSession_Changed;
                if (!migrationSession.IsTerminal)
                    _ = migrationSession.UpdateAsync(
                        MigrationSessionState.Failed,
                        MigrationProgressBar.Value,
                        "The migration wizard was closed before completion.",
                        "MIGRATION_WINDOW_CLOSED");
            }
        }

        private void TryLoadLicense()
        {
            string path = Path.Combine(Directories.ELUADirectory, StoreLocalizationHelper.GetStoreLikeLocale(), "ELUA.md");
            try
            {
                LicenseTextBlock.Text = ShellHelper.LoadAllTextFromFile(path);
                LicenseAgreeCheckBox.IsEnabled = true;
            }
            catch 
            {
                LicenseAgreeCheckBox.IsEnabled = false;
                LicenseTextBlock.Text = string.Format(localizer.GetLocalizedString("/WelcomeWizard/UnableLoadLicense"), path);
            }
        }

        private void LicenseAgreeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            NextButton.IsEnabled = LicenseAgreeCheckBox.IsChecked ?? false;
        }

        private void UtilityButtonControls_Loaded(object sender, RoutedEventArgs e)
        {
            CheckNavigation();
        }

        private void CompleteButton_Click(object sender, RoutedEventArgs e)
        {
            CommandsHandler.HandleCommand("cdpiui://");
            if (ShowAppFeaturesCheckBox.IsChecked == true) 
            {
                CommandsHandler.HandleCommand("cdpiui://AppFeatures");
            }
            this.Close();
        }

        
    }
}
