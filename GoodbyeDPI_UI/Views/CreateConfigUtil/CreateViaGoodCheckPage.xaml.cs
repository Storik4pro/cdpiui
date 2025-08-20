using CommunityToolkit.WinUI;
using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.CreateConfigUtil;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Unidecode.NET;
using GoodbyeDPI_UI.Helper.Static;
using GoodbyeDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using GoodbyeDPI_UI.Helper.Settings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views.CreateConfigUtil
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public class OneSiteListOneStrategyModeElement
    {
        public string SiteListFilePath { get; set; }
        public string StrategiesListFilePath { get; set; }
    }
    public sealed partial class CreateViaGoodCheck : Page
    {
        private readonly DispatcherQueue _uiDispatcher;

        private enum States
        {
            None,
            LetsBegin,
            PrepareLoading,
            SiteListsSettings,
            StrategyListSettings,
            AdditionalSettings,
        }

        private States _currentState = States.None;

        private List<GoodCheckStrategiesList> StrategiesLists = [];
        private List<GoodCheckSitelistButton> SitelistButtons = [];

        private List<OneSiteListOneStrategyModeElement> OneSiteListOneStrategyModeElements = [];

        private IniSettingsHelper IniSettingsHelper { get; set; }

        private const string AddOnId = "ASGKOI001";

        public CreateViaGoodCheck()
        {
            InitializeComponent();
            _uiDispatcher = DispatcherQueue.GetForCurrentThread();

            HideAll();
            SetLoadingMode(false);
            SwitchState(States.LetsBegin);
        
        }

        private void HideAll()
        {
            LoadingStackPanel.Visibility = Visibility.Collapsed;
            LetsBeginStackPanel.Visibility = Visibility.Collapsed;
            GoodCheckSettingsStackPanel.Visibility = Visibility.Collapsed;
        }

        private void SetLoadingMode(
            bool isLoading,
            bool isIndeterminate=true, 
            bool cancellationAvailable=false, 
            string headerText=""
            )
        {
            if (!_uiDispatcher.HasThreadAccess)
            {
                _uiDispatcher.TryEnqueue(() => SetLoadingMode(isLoading, isIndeterminate, cancellationAvailable, headerText));
                return;
            }

            if (isLoading)
            {
                GoBackButton.Visibility = Visibility.Collapsed;
                GoForwardButton.Visibility = Visibility.Collapsed;

                LoadingProgressBar.IsIndeterminate = isIndeterminate;
                LoadingHeader.Text = headerText;
                CancelButton.Visibility = Visibility.Visible;
                CancelButton.IsEnabled = cancellationAvailable;
                LoadingStateGrid.Visibility = Visibility.Visible;
            }
            else
            {
                GoBackButton.Visibility = Visibility.Visible;
                GoForwardButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Collapsed;

                LoadingStateGrid.Visibility = Visibility.Collapsed;
            }
        }

        public void LoadSiteList(IEnumerable<SiteListElement> items)
        {
            if (!_uiDispatcher.HasThreadAccess)
            {
                _uiDispatcher.TryEnqueue(() => LoadSiteList(items));
                return;
            }

            SiteListTemplatesStackPanel.Children.Clear();

            foreach (var item in items)
            {
                var btn = new SitelistButton
                {
                    Title = item.Name,
                    PackName = item.PackName,
                    PackId = item.PackId,
                    Directory = item.Directory,
                    Tag = item,
                };

                btn.Click += () =>
                {
                    SelectSiteListFlyout.Hide();
                    AddSiteListElement(item);
                };

                SiteListTemplatesStackPanel.Children.Add(btn);
            }
        }

        private async void InitGoodCheckSiteListsPage()
        {

            SetLoadingMode(true, isIndeterminate: true, cancellationAvailable: false, headerText: "Идет работа над этим");

            List<SiteListElement> siteLists = await SiteListHelper.Instance.GetAllAvailableSiteListTemplatesAsync();

            await Task.Delay(2000); //For testing only

            LoadSiteList(siteLists);

            SwitchState(States.SiteListsSettings);
            GoForwardButton.IsEnabled = false;

            StrategiesLists = BasicGoodCheckHelper.GetAvailableStrategiesLists("CSZTBN012");
            
            StrategiesListCombobox.ItemsSource = StrategiesLists; // TODO: rase exception if elements count zero

            SetLoadingMode(false);

            await Task.CompletedTask;
        }

        public void InitGoodCheckStrategiesListsPage()
        {
            SitelistButtons.Clear();
            OneSiteListOneStrategyModeElements.Clear();

            foreach (GoodCheckSitelistButton button in SelectedSitelistStackPanel.Children.OfType<GoodCheckSitelistButton>())
            {
                SitelistButtons.Add(button);
            }

            OneListOneStrategyStackPanel.Children.Clear();
            foreach (GoodCheckSitelistButton button in SitelistButtons)
            {
                GoodCheckSitelistStrategyChooserControl checkListChooseControl = new() 
                {
                    Title = button.Title,
                    PackName = button.PackName,
                    FilePath = button.FilePath,
                    StrategiesLists = StrategiesLists
                };
                checkListChooseControl.StrategyChanged += (tuple) =>
                {
                    string filePath = tuple.Item1;
                    string strategyFilePath = tuple.Item2;

                    var existing = OneSiteListOneStrategyModeElements
                        .FirstOrDefault(x => x.SiteListFilePath == filePath);

                    if (existing == null)
                    {
                        OneSiteListOneStrategyModeElements.Add(new OneSiteListOneStrategyModeElement
                        {
                            SiteListFilePath = filePath,
                            StrategiesListFilePath = strategyFilePath
                        });
                    }
                    else
                    {
                        existing.StrategiesListFilePath = strategyFilePath;
                    }
                };

                OneSiteListOneStrategyModeElements.Add(new OneSiteListOneStrategyModeElement
                {
                    SiteListFilePath = button.FilePath,
                    StrategiesListFilePath = StrategiesLists[0].FilePath,
                });

                OneListOneStrategyStackPanel.Children.Add(checkListChooseControl);
            }
        }

        private void LoadGoodCheckSettings()
        {
            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string targetFolder = Path.Combine(
                localAppData, 
                StateHelper.StoreDirName, 
                StateHelper.StoreItemsDirName, 
                AddOnId,
                "config.ini");

            if (!File.Exists(targetFolder))
            {
                // TODO: Raise NeedRestore error
            }

            if (IniSettingsHelper is null)
                IniSettingsHelper = new(targetFolder, System.Text.Encoding.UTF8);

            PValue.Text = SettingsManager.Instance.GetValue<string>(["ADDONS", AddOnId], "passesValue");
            ResolverComboBox.SelectedIndex = SettingsManager.Instance.GetValue<bool>(["ADDONS", AddOnId], "UseCurl") ? 1 : 0;
            ConnectionTimeout.Text = IniSettingsHelper.GetValue<string>("General", "ConnectionTimeout");
            
            ResolverNativeTimeout.Text = IniSettingsHelper.GetValue<string>("Advanced", "ResolverNativeTimeout");
            ResolverNativeRetries.Text = IniSettingsHelper.GetValue<string>("Advanced", "ResolverNativeRetries");
            InternalTimeoutMs.Text = IniSettingsHelper.GetValue<string>("Advanced", "InternalTimeoutMs");
            AutomaticConnectivityTest.IsOn = IniSettingsHelper.GetValue<bool>("Advanced", "AutomaticConnectivityTest");
            ConnectivityTestURL.Text = IniSettingsHelper.GetValue<string>("Advanced", "ConnectivityTestURL");
            SkipCertVerify.IsOn = IniSettingsHelper.GetValue<bool>("Advanced", "SkipCertVerify");

            AutomaticGoogleCacheTest.IsOn = IniSettingsHelper.GetValue<bool>("General", "AutomaticGoogleCacheTest");
            GoogleCacheMappingURLs.Text = IniSettingsHelper.GetValue<string>("Advanced", "GoogleCacheMappingURLs");

            UseDoH.IsOn = IniSettingsHelper.GetValue<bool>("Resolvers", "UseDoH");
            DoHResolvers.Text = IniSettingsHelper.GetValue<string>("Resolvers", "DoHResolvers");

            FakeSNI.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeSNI");
            FakeHexStreamTCP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexStreamTCP");
            FakeHexStreamUDP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexStreamUDP");
            FakeHexBytesTCP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexBytesTCP");
            FakeHexBytesUDP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexBytesUDP");
            PayloadTCP.Text = IniSettingsHelper.GetValue<string>("Fakes", "PayloadTCP");
            PayloadUDP.Text = IniSettingsHelper.GetValue<string>("Fakes", "PayloadUDP");
        }

        private void SaveGoodCheckSettings()
        {
            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string targetFolder = Path.Combine(
                localAppData,
                StateHelper.StoreDirName,
                StateHelper.StoreItemsDirName,
                AddOnId,
                "config.ini");

            if (IniSettingsHelper is null)
                IniSettingsHelper = new(targetFolder, System.Text.Encoding.UTF8);

            SettingsManager.Instance.SetValue<string>(["ADDONS", AddOnId], "passesValue", PValue.Text);
            SettingsManager.Instance.SetValue<bool>(["ADDONS", AddOnId], "UseCurl", ResolverComboBox.SelectedIndex == 1 ? true : false);

            IniSettingsHelper.SetValue<string>("General", "ConnectionTimeout", ConnectionTimeout.Text);

            IniSettingsHelper.SetValue<string>("Advanced", "ResolverNativeTimeout", ResolverNativeTimeout.Text);
            IniSettingsHelper.SetValue<string>("Advanced", "ResolverNativeRetries", ResolverNativeRetries.Text);
            IniSettingsHelper.SetValue<string>("Advanced", "InternalTimeoutMs", InternalTimeoutMs.Text);
            IniSettingsHelper.SetValue<bool>("Advanced", "AutomaticConnectivityTest", AutomaticConnectivityTest.IsOn);
            IniSettingsHelper.SetValue<string>("Advanced", "ConnectivityTestURL", ConnectivityTestURL.Text);
            IniSettingsHelper.SetValue<bool>("Advanced", "SkipCertVerify", SkipCertVerify.IsOn);

            IniSettingsHelper.SetValue<bool>("General", "AutomaticGoogleCacheTest", AutomaticGoogleCacheTest.IsOn);
            IniSettingsHelper.SetValue<string>("Advanced", "GoogleCacheMappingURLs", GoogleCacheMappingURLs.Text);

            IniSettingsHelper.SetValue<bool>("Resolvers", "UseDoH", UseDoH.IsOn);
            IniSettingsHelper.SetValue<string>("Resolvers", "DoHResolvers", DoHResolvers.Text);

            IniSettingsHelper.SetValue<string>("Fakes", "FakeSNI", FakeSNI.Text);
            IniSettingsHelper.SetValue<string>("Fakes", "FakeHexStreamTCP", FakeHexStreamTCP.Text);
            IniSettingsHelper.SetValue<string>("Fakes", "FakeHexStreamUDP", FakeHexStreamUDP.Text);
            IniSettingsHelper.SetValue<string>("Fakes", "FakeHexBytesTCP", FakeHexBytesTCP.Text);
            IniSettingsHelper.SetValue<string>("Fakes", "FakeHexBytesUDP", FakeHexBytesUDP.Text);
            IniSettingsHelper.SetValue<string>("Fakes", "PayloadTCP", PayloadTCP.Text);
            IniSettingsHelper.SetValue<string>("Fakes", "PayloadUDP", PayloadUDP.Text);

            string itemsFolder = Path.Combine(localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName);

            IniSettingsHelper.SetValue<string>("Zapret", "ZapretFolder", Path.Combine(itemsFolder, StateHelper.Instance.FindKeyByValue("Zapret")));
            IniSettingsHelper.SetValue<string>("Zapret", "ZapretExecutableName", DatabaseHelper.Instance.GetItemById(StateHelper.Instance.FindKeyByValue("Zapret")).Executable + ".exe");

            IniSettingsHelper.Save();
        }

        private void StartGoodCheck()
        {
            List<GoodCheckSiteListModel> listModels = new();

            if (!ChangeModeComboBox.IsChecked == true)
            {
                foreach (
                    GoodCheckSitelistButton button in
                    SelectedSitelistStackPanel.Children.OfType<GoodCheckSitelistButton>())
                {
                    var strategiesList = StrategiesListCombobox.Items[StrategiesListCombobox.SelectedIndex] as GoodCheckStrategiesList;
                    GoodCheckSiteListModel model = new()
                    {
                        SiteListPath = button.FilePath,
                        StrategyListPath = strategiesList.FilePath,
                    };
                    listModels.Add(model);
                }
            }
            else
            {
                foreach (
                    OneSiteListOneStrategyModeElement element in OneSiteListOneStrategyModeElements)
                {
                    GoodCheckSiteListModel model = new()
                    {
                        SiteListPath = element.SiteListFilePath,
                        StrategyListPath = element.StrategiesListFilePath,
                    };
                    listModels.Add(model);
                }
            }

            GoodCheckProcessHelper.Instance.InitGoodCheck(
                StateHelper.Instance.FindKeyByValue("Zapret"),
                (bool)ChangeModeComboBox.IsChecked ? GoodCheckProcessMode.AnyAsAny : GoodCheckProcessMode.AllAsOne,
                listModels
                );

            GoodCheckProcessHelper.Instance.ErrorHappens += (e) =>
            {
                Logger.Instance.CreateErrorLog(nameof(CreateViaGoodCheck), $"{e}");
            };

            GoodCheckProcessHelper.Instance.Start();
        }


        private void GoBackButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_currentState)
            {
                case States.SiteListsSettings:
                    GoForwardButton.IsEnabled = true;
                    SwitchState(States.LetsBegin, slideToLeft:false);
                    break;
                default:
                    SwitchState(_currentState - 1, slideToLeft:false);
                    break;
            }
        }

        private async void GoForwardButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_currentState)
            {
                case States.LetsBegin:
                    SwitchState(States.PrepareLoading);
                    InitGoodCheckSiteListsPage();
                    break;
                case States.SiteListsSettings:
                    SwitchState(States.StrategyListSettings);
                    InitGoodCheckStrategiesListsPage();
                    if (StrategiesListCombobox.SelectedIndex != -1 || ChangeModeComboBox.IsChecked == true)
                    {
                        GoForwardButton.IsEnabled = true;
                    }
                    else
                    {
                        GoForwardButton.IsEnabled = false;
                    }
                    break;
                case States.StrategyListSettings:
                    SwitchState(States.AdditionalSettings);
                    LoadGoodCheckSettings();
                    break;
                case States.AdditionalSettings:
                    SaveGoodCheckSettings();
                    CreateConfigUtilWindow.Instance.NavigateToPage<GoodCheckWorkPage>();
                    StartGoodCheck();
                    break;
                default:
                    SwitchState(_currentState + 1);
                    break;
            }
            await Task.CompletedTask;
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Выберите список сайтов";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                openFileDialog.Filter = "TXT files (*.txt)|*.txt";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                } 
                else
                {
                    return;
                }
            }

            bool isCorrect = await DomainValidationHelper.IsFileCorrectSiteList(filePath, DomainValidationHelper.CheckMode.Quick);

            if (!isCorrect)
            {
                FileIncorrectWarning.IsOpen = true;
                FileIncorrectWarning.Visibility = Visibility.Visible;
                OpenStoryboard.Begin();
                return;
            }

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string targetFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, StateHelper.LocalUserItemsId, StateHelper.LocalUserItemSiteListsFolder);

            string newFilePath = Utils.CopyTxtWithUniqueName(filePath, targetFolder);

            SiteListElement siteListElement = new()
            {
                Directory = newFilePath,
                Name = Path.GetFileName(newFilePath),
                PackId = StateHelper.LocalUserItemsId,
                PackName = "Личные данные текущего пользователя",
            };

            AddSiteListElement(siteListElement);

        }

        public bool AddSiteListElement(SiteListElement site)
        {
            CloseStoryboard.Begin();
            if (site == null) return false;

            bool exists = false;

            exists = SelectedSitelistStackPanel.Children
                        .OfType<GoodCheckSitelistButton>()
                        .Any(c =>
                        {
                            return (c.FilePath == site.Directory);
                        });

            if (exists) return false;

            var element = site;
            var btn = new GoodCheckSitelistButton
            {
                Title = element.Name,
                PackName = element.PackName,
                FilePath = element.Directory,
                Tag = element,
            };

            btn.RemoveElement = () =>
            {
                _ = RemoveSiteListElementByTagAsync(element);
            };

            DispatcherQueue.TryEnqueue(() =>
            {
                SelectedSitelistStackPanel.Children.Add(btn);
            });

            return true;
        }

        public Task RemoveSiteListElementByTagAsync(SiteListElement tagElement)
        {
            if (tagElement == null) return Task.CompletedTask;

            DispatcherQueue.TryEnqueue(() =>
            {
                var target = SelectedSitelistStackPanel.Children
                             .OfType<GoodCheckSitelistButton>()
                             .FirstOrDefault(c => c.Tag is SiteListElement se && ReferenceEquals(se, tagElement));

                if (target == null)
                {
                    var normalized = Helper.Static.Utils.NormalizeDirectory(tagElement.Directory);
                    target = SelectedSitelistStackPanel.Children
                             .OfType<GoodCheckSitelistButton>()
                             .FirstOrDefault(c => c.Tag is SiteListElement se &&
                                                  string.Equals(Helper.Static.Utils.NormalizeDirectory(se.Directory), normalized, StringComparison.OrdinalIgnoreCase));
                }

                if (target == null) return;

                try
                {
                    target.RemoveElement = null;
                    SelectedSitelistStackPanel.Children.Remove(target);
                }
                catch { }              
            });

            return Task.CompletedTask;
        }

        

        public void ClearAllSiteListElements()
        {
            DispatcherQueue.TryEnqueue(() => SelectedSitelistStackPanel.Children.Clear());
        }

        private UIElement GetPanelForState(States s)
        {
            return s switch
            {
                States.PrepareLoading => LoadingStackPanel,
                States.LetsBegin => LetsBeginStackPanel,
                States.SiteListsSettings => GoodCheckSettingsStackPanel,
                States.StrategyListSettings => ChooseStrategiesListStackPanel,
                States.AdditionalSettings => AdditionalSettingsStackPanel,
                _ => null
            };
        }

        private void SwitchState(States newState, bool slideToLeft = true)
        {
            if (newState == _currentState) return;

            var oldPanel = GetPanelForState(_currentState);
            var newPanel = GetPanelForState(newState);
            if (newPanel == null) return;

            double width = ContentGrid.ActualWidth;
            if (width <= 0) width = DisplayArea.Primary.WorkArea.Width;

            EnsureTranslateTransform(oldPanel);
            EnsureTranslateTransform(newPanel);

            var duration = TimeSpan.FromMilliseconds(320);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            newPanel.Opacity = 0;
            newPanel.Visibility = Visibility.Visible;
            var newTT = GetTranslateTransform(newPanel);
            newTT.X = slideToLeft ? width : -width;

            var outSb = new Storyboard();
            if (oldPanel != null)
            {
                var outAnim = new DoubleAnimation
                {
                    From = 0,
                    To = slideToLeft ? -width : width,
                    Duration = new Duration(duration),
                    EasingFunction = ease
                };
                Storyboard.SetTarget(outAnim, oldPanel);
                Storyboard.SetTargetProperty(outAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");
                outSb.Children.Add(outAnim);

                var outFade = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(duration)
                };
                Storyboard.SetTarget(outFade, oldPanel);
                Storyboard.SetTargetProperty(outFade, "Opacity");
                outSb.Children.Add(outFade);
            }

            var inSb = new Storyboard();

            var inAnim = new DoubleAnimation
            {
                From = newTT.X,
                To = 0,
                Duration = new Duration(duration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(inAnim, newPanel);
            Storyboard.SetTargetProperty(inAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");
            inSb.Children.Add(inAnim);

            var inFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(duration)
            };
            Storyboard.SetTarget(inFade, newPanel);
            Storyboard.SetTargetProperty(inFade, "Opacity");
            inSb.Children.Add(inFade);

            if (oldPanel != null)
            {
                outSb.Completed += (s, e) =>
                {
                    oldPanel.Visibility = Visibility.Collapsed;
                    var tt = GetTranslateTransform(oldPanel);
                    tt.X = 0;
                    oldPanel.Opacity = 1;
                };
            }

            outSb.Begin();
            inSb.Begin();

            _currentState = newState;
        }

        private void EnsureTranslateTransform(UIElement el)
        {
            if (el == null) return;
            if (el.RenderTransform is TranslateTransform) return;
            el.RenderTransform = new TranslateTransform();
            el.RenderTransformOrigin = new Windows.Foundation.Point(0, 0);
        }

        private TranslateTransform GetTranslateTransform(UIElement el)
        {
            if (el?.RenderTransform is TranslateTransform tt)
                return tt;

            var t = new TranslateTransform();
            if (el != null) el.RenderTransform = t;
            return t;
        }

        private void SelectedSitelistScrollViewer_LayoutUpdated(object sender, object e)
        {
            SelectedSitelistScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - GoodCheckSettingsTextContent.ActualHeight;
            SelectedSitelistScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - GoodCheckSettingsTextContent.ActualHeight;
        }

        private void SelectedSitelistStackPanel_LayoutUpdated(object sender, object e)
        {
            if (_currentState == States.SiteListsSettings)
            {
                if (SelectedSitelistStackPanel.Children.Count > 0)
                {
                    GoForwardButton.IsEnabled = true;
                    SelectedSitelistTip.Visibility = Visibility.Collapsed;
                }
                else
                {
                    GoForwardButton.IsEnabled = false;
                    SelectedSitelistTip.Visibility = Visibility.Visible;
                }
            }
        }

        private void FileIncorrectWarning_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void FileIncorrectWarning_Closing(InfoBar sender, InfoBarClosingEventArgs args)
        {
            args.Cancel = true;

            EventHandler<object> onDone = null;
            onDone = (s, ev) =>
            {
                CloseStoryboard.Completed -= onDone;
                InfoBarTranslate.X = 200;
                sender.Opacity = 0;
            };

            CloseStoryboard.Completed += onDone;
            CloseStoryboard.Begin();
        }

        private void CloseStoryboard_Completed(object sender, object e)
        {
            FileIncorrectWarning.Visibility = Visibility.Collapsed;
        }

        private void StrategiesListCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrategiesListCombobox.SelectedIndex != -1)
            {
                GoForwardButton.IsEnabled = true;
            }
            else
            {
                GoForwardButton.IsEnabled = false;
            }
        }

        private void ChangeModeComboBox_Click(object sender, RoutedEventArgs e)
        {
            if (ChangeModeComboBox.IsChecked == true)
            {
                OneListOneStrategyStackPanel.Visibility = Visibility.Visible;
                AllListsOneStrategyPanel.Visibility = Visibility.Collapsed;


            }
            else
            {
                OneListOneStrategyStackPanel.Visibility = Visibility.Collapsed;
                AllListsOneStrategyPanel.Visibility = Visibility.Visible;
            }

            if (StrategiesListCombobox.SelectedIndex != -1 || ChangeModeComboBox.IsChecked == true)
            {
                GoForwardButton.IsEnabled = true;
            }
            else
            {
                GoForwardButton.IsEnabled = false;
            }
        }

        private void OneListOneStrategyScrollViewer_LayoutUpdated(object sender, object e)
        {
            OneListOneStrategyScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - ChooseStrategiesListTextContent.ActualHeight;
            OneListOneStrategyScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - ChooseStrategiesListTextContent.ActualHeight;
        }

        private void AdditionalSettingsScrollViewer_LayoutUpdated(object sender, object e)
        {
            AdditionalSettingsScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - AdditionalSettingsListTextContent.ActualHeight;
            AdditionalSettingsScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - AdditionalSettingsListTextContent.ActualHeight;
        }

        private void ResolverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
