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

        private ObservableCollection<ViewComponentModel> Models = [];

        public ConfigTestWindow()
        {
            InitializeComponent();

            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("PresetTestWindowTitle"));

            TitleIcon = TitleImageRectagle;
            TitleBar = WindowMoveAera;
            IconUri = @"Assets/Icons/GoodCheck.ico";
            WindowMinSize = new System.Windows.Size(900, 520);

            SetTitleBar(WindowMoveAera);


            StartTestButton.IsEnabled = false;
            ComponentComboBox.ItemsSource = Models;
        }

        private void LoadComponents()
        {
            try
            {
                var components = DatabaseHelper.Instance.GetItemsByType("component");
                UIHelper.LoadInstalledComponentsList(Models);

                Models.Remove(Models.FirstOrDefault(x => x.StoreId == "CSTYFL050"));




                if (Models.Count > 0)
                {
                    ComponentComboBox.SelectedItem = Models.FirstOrDefault(x => x.StoreId == ComponentIdToTest) ?? Models.First();
                    StartTestButton.IsEnabled = true;
                }
                else
                {
                    StartTestButton.IsEnabled = false;
                }
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
            SetTestingUi(true);
            _testCts = new CancellationTokenSource();

            var progress = new Progress<List<TestLogSegment>>(AppendLine);

            try
            {
                var helper = new PresetTestHelper();
                await Task.Run(() => helper.RunAsync(entry.StoreId, processManager, testType, presets, progress, _testCts.Token));
            }
            catch (OperationCanceledException)
            {
                AppendLine(new TestLogSegment(localizer.GetLocalizedString("PT_Cancelled"), TestLogColor.Yellow));
                try { await processManager.StopProcess(false); } catch { }
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
                _testCts?.Dispose();
                _testCts = null;
            }
        }

        private void StopTestButton_Click(object sender, RoutedEventArgs e)
        {
            try { _testCts?.Cancel(); } catch { }
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
}
