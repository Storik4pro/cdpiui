using CDPI_UI.Default;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using WinUI3Localizer;

namespace CDPI_UI
{
    public sealed partial class ConfigTestWindow : TemplateWindow
    {
        private readonly ILocalizer localizer = Localizer.Get();

        private CancellationTokenSource _testCts;
        private bool _isTesting = false;

        private string componentIdToTest = string.Empty;
        public string ComponentIdToTest
        {
            get { 
                return componentIdToTest; 
            }
            set { 
                componentIdToTest = value;
                LoadComponents();
            }
        }

        private string TestedComponentId = string.Empty;

        private readonly ObservableCollection<PresetResultViewModel> _resultItems = new();
        private readonly ObservableCollection<ViewComponentModel> Models = new();

        private PresetTestHelper _activeHelper;
        private PresetTestType _lastTestType = PresetTestType.Standard;

        private class ComponentEntry
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public ConfigTestWindow()
        {
            InitializeComponent();

            WindowTitle = localizer.GetLocalizedString("PresetTestWindowTitle");

            IconUri = @"Assets/Icons/GoodCheck.ico";
            this.CustomTitleBarUserControl = TitleBarUserControl;

            WindowMinSize = new System.Windows.Size(1100, 520);


            ComponentComboBox.ItemsSource = Models;
            ResultsListView.ItemsSource = _resultItems;
            LoadComponents();
        }

        private void LoadComponents()
        {
            try
            {
                UIHelper.LoadInstalledComponentsList(Models);

                Models.Remove(Models.FirstOrDefault(x => x.StoreId == "CSTYFL050"));


                if (Models.Count > 0)
                {
                    ComponentComboBox.SelectedItem = Models.FirstOrDefault(x => x.StoreId == ComponentIdToTest) ?? Models.First();
                    StartTestButton.IsEnabled = true;
                }
                else
                    StartTestButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ConfigTestWindow), $"Can't load components: {ex.Message}");
            }
        }

        private List<ConfigItem> GetPresetsForSelectedComponent()
        {
            if (ComponentComboBox.SelectedItem is not ViewComponentModel entry)
                return new List<ConfigItem>();

            try
            {
                ComponentItemsLoaderHelper.Instance.Init();
                ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(entry.StoreId);
                if (componentHelper == null) return new List<ConfigItem>();

                ConfigHelper configHelper = componentHelper.GetConfigHelper();
                if (configHelper == null) return new List<ConfigItem>();

                return configHelper.GetConfigItems().Where(c => c != null && !c.MarkAsRemoved).ToList();
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ConfigTestWindow), $"Can't load presets: {ex.Message}");
                return new List<ConfigItem>();
            }
        }

        private void ComponentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetListView == null) return;
            PresetListView.ItemsSource = GetPresetsForSelectedComponent();
        }

        private void ConfigSelectionRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (PresetListView == null) return;
            PresetListView.IsEnabled = SelectedConfigsRadio.IsChecked == true;
        }

        private async void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTesting) return;


            if (ComponentComboBox.SelectedItem is not ViewComponentModel entry)
                return;

            List<ConfigItem> presets;
            if (SelectedConfigsRadio.IsChecked == true)
            {
                presets = PresetListView.SelectedItems.OfType<ConfigItem>().ToList();
                if (presets.Count == 0)
                {
                    AppendLine(new TestLogSegment($"[{localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                               new TestLogSegment(localizer.GetLocalizedString("PT_NoSelection")));
                    return;
                }
            }
            else
            {
                presets = GetPresetsForSelectedComponent();
            }

            PresetTestType testType = DpiTestRadio.IsChecked == true ? PresetTestType.DpiChecker : PresetTestType.Standard;

            ProcessManager processManager = (await TasksHelper.Instance.GetTaskFromId(entry.StoreId))?.ProcessManager;
            if (processManager == null)
            {
                AppendLine(new TestLogSegment($"[{localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                           new TestLogSegment(localizer.GetLocalizedString("PT_ComponentNotFound")));
                return;
            }

            ClearOutput();
            ClearResults();
            SetTestingUi(true);
            _testCts = new CancellationTokenSource();
            _activeHelper = new PresetTestHelper();

            TestedComponentId = entry.StoreId;

            var logProgress = new Progress<List<TestLogSegment>>(AppendLine);
            var testProgress = new Progress<PresetTestProgress>(UpdateProgress);

            try
            {
                PresetTestRunResult runResult = await Task.Run(() =>
                    _activeHelper.RunAsync(entry.StoreId, processManager, _lastTestType, presets, logProgress, testProgress, _testCts.Token));

                if (runResult.WasCancelled)
                    AppendLine(new TestLogSegment(localizer.GetLocalizedString("PT_Cancelled"), TestLogColor.Yellow));

                ShowResults(runResult, _lastTestType);
            }
            catch (Exception ex)
            {
                AppendLine(new TestLogSegment($"[{localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                           new TestLogSegment(ex.Message, TestLogColor.Red));
                try { await processManager.StopProcess(false); } catch { }
            }
            finally
            {
                SetTestingUi(false);
                HideProgress();
                _activeHelper = null;
                _testCts?.Dispose();
                _testCts = null;
            }
        }

        private void StopTestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _activeHelper?.CancelAllCurls();
                _testCts?.Cancel();
            }
            catch { }
        }

        private async void ApplyPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PresetResultViewModel vm)
                await ApplyPresetAsync(vm, restartIfRunning: false);
        }

        private async void RunPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PresetResultViewModel vm)
                await ApplyPresetAsync(vm, restartIfRunning: true);
        }

        private async Task ApplyPresetAsync(PresetResultViewModel vm, bool restartIfRunning)
        {
            if (vm?.Preset == null || string.IsNullOrEmpty(TestedComponentId))
                return;

            try
            {
                SettingsManager.Instance.SetValue<string>(["CONFIGS", TestedComponentId], "configFile", vm.Preset.file_name);
                SettingsManager.Instance.SetValue<string>(["CONFIGS", TestedComponentId], "configId", vm.Preset.packId);

                ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(TestedComponentId);
                componentHelper.ReInitConfigs();

                AppendLine(new TestLogSegment(string.Format(localizer.GetLocalizedString("PT_PresetApplied"), vm.PresetName), TestLogColor.Green));

                if (restartIfRunning)
                {
                    if (await TasksHelper.Instance.IsTaskRunned(TestedComponentId))
                        await TasksHelper.Instance.RestartTask(TestedComponentId);
                    else
                        TasksHelper.Instance.CreateAndRunNewTask(TestedComponentId);
                }
                else if (await TasksHelper.Instance.IsTaskRunned(TestedComponentId))
                {
                    await TasksHelper.Instance.RestartTask(TestedComponentId);
                }
            }
            catch (Exception ex)
            {
                AppendLine(new TestLogSegment($"[{localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                           new TestLogSegment(ex.Message, TestLogColor.Red));
            }
        }

        private void ShowResults(PresetTestRunResult runResult, PresetTestType testType)
        {
            _resultItems.Clear();
            if (runResult?.Ranked == null || runResult.Ranked.Count == 0)
            {
                ResultsListView.Visibility = Visibility.Collapsed;
                ResultsPlaceholderTextBlock.Visibility = Visibility.Visible;
                return;
            }

            string applyLabel = localizer.GetLocalizedString("PresetTestApplyButton");
            string runLabel = localizer.GetLocalizedString("PresetTestRunButton");
            string recommended = localizer.GetLocalizedString("PresetTestRecommendedBadge");
            ConfigItem bestPreset = runResult.Best?.Preset;

            foreach (PresetTestResult r in runResult.Ranked)
            {
                bool isBest = bestPreset != null && r.Preset != null &&
                    r.Preset.file_name == bestPreset.file_name &&
                    r.Preset.packId == bestPreset.packId;

                _resultItems.Add(new PresetResultViewModel
                {
                    Preset = r.Preset,
                    PresetName = r.PresetName,
                    PackName = string.IsNullOrEmpty(r.PackName) ? r.Preset?.file_name : r.PackName,
                    MetricsText = r.GetMetricsSummary(testType, localizer),
                    IsBest = isBest,
                    RecommendedBadge = recommended,
                    ApplyLabel = applyLabel,
                    RunLabel = runLabel
                });
            }

            ResultsPlaceholderTextBlock.Visibility = Visibility.Collapsed;
            ResultsListView.Visibility = Visibility.Visible;
        }

        private void ClearResults()
        {
            _resultItems.Clear();
            ResultsListView.Visibility = Visibility.Collapsed;
            ResultsPlaceholderTextBlock.Visibility = Visibility.Visible;
        }

        private void UpdateProgress(PresetTestProgress p)
        {
            if (p == null) return;

            ProgressPanel.Visibility = Visibility.Visible;
            TestProgressBar.Value = p.Percent;
            ProgressStatusTextBlock.Text = string.Format(
                localizer.GetLocalizedString("PT_ProgressStatus"),
                p.CurrentIndex, p.TotalPresets, p.CurrentPresetName);

            if (p.EstimatedRemaining.HasValue && p.CompletedPresets > 0 && p.CompletedPresets < p.TotalPresets)
            {
                var eta = p.EstimatedRemaining.Value;
                ProgressEtaTextBlock.Text = Utils.ConvertMinutesToPrettyText(eta.Minutes);
                
            }
            else
            {
                ProgressEtaTextBlock.Text = localizer.GetLocalizedString("Calculating");
            }
        }

        private void HideProgress()
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            TestProgressBar.Value = 0;
            ProgressStatusTextBlock.Text = string.Empty;
            ProgressEtaTextBlock.Text = string.Empty;
        }

        private void TargetsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PresetTestHelper.EnsureTargetsFile();
                Process.Start(new ProcessStartInfo
                {
                    FileName = PresetTestHelper.GetTargetsFilePath(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ConfigTestWindow), $"Can't open targets file: {ex.Message}");
            }
        }

        private void SetTestingUi(bool testing)
        {
            _isTesting = testing;
            StartTestButton.IsEnabled = !testing && ComponentComboBox.Items.Count > 0;
            StopTestButton.IsEnabled = testing;
            ComponentComboBox.IsEnabled = !testing;
            StandardTestRadio.IsEnabled = !testing;
            DpiTestRadio.IsEnabled = !testing;
            AllConfigsRadio.IsEnabled = !testing;
            SelectedConfigsRadio.IsEnabled = !testing;
            PresetListView.IsEnabled = !testing && SelectedConfigsRadio.IsChecked == true;

            if (testing)
                ProgressPanel.Visibility = Visibility.Visible;
        }

        private void ClearOutput()
        {
            OutputParagraph.Inlines.Clear();
        }

        private void AppendLine(List<TestLogSegment> segments)
        {
            if (segments == null) return;
            foreach (TestLogSegment segment in segments)
            {
                OutputParagraph.Inlines.Add(new Run
                {
                    Text = segment.Text,
                    Foreground = BrushFor(segment.Color)
                });
            }
            OutputParagraph.Inlines.Add(new LineBreak());
            OutputScrollViewer.ChangeView(null, OutputScrollViewer.ScrollableHeight, null);
        }

        private void AppendLine(params TestLogSegment[] segments)
        {
            AppendLine(segments.ToList());
        }

        private static SolidColorBrush BrushFor(TestLogColor color)
        {
            Color c = color switch
            {
                TestLogColor.Green => Color.FromArgb(255, 87, 166, 74),
                TestLogColor.Cyan => Color.FromArgb(255, 78, 201, 176),
                TestLogColor.Yellow => Color.FromArgb(255, 229, 192, 123),
                TestLogColor.Red => Color.FromArgb(255, 244, 71, 71),
                TestLogColor.Gray => Color.FromArgb(255, 150, 150, 150),
                TestLogColor.White => Color.FromArgb(255, 230, 230, 230),
                _ => Color.FromArgb(255, 220, 220, 220),
            };
            return new SolidColorBrush(c);
        }

        private async void HelpHyperLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
            window.NavigateToPage("/Autoselection/BestConfigSelection");
        }
    }

    public class PresetResultViewModel
    {
        public ConfigItem Preset { get; set; }
        public string PresetName { get; set; }
        public string PackName { get; set; }
        public string MetricsText { get; set; }
        public bool IsBest { get; set; }
        public string RecommendedBadge { get; set; }
        public string ApplyLabel { get; set; }
        public string RunLabel { get; set; }

        public Visibility RecommendedBadgeVisibility => IsBest ? Visibility.Visible : Visibility.Collapsed;
    }

        
}
