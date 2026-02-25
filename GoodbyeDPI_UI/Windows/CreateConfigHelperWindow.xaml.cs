using CDPI_UI.Controls.CreateConfigHelper;
using CDPI_UI.Controls.Dialogs.CreateConfigHelper;
using CDPI_UI.Default;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigHelper;
using CDPI_UI.Views.CreateConfigUtil;
using Microsoft.UI;
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
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;
using static CDPI_UI.Win32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CreateConfigHelperWindow : TemplateWindow
    {
        public static CreateConfigHelperWindow Instanse { get; private set; }
        public bool IsOperationExitAskAvailable { get; set; } = false;

        private bool IsDialogRequested = false;
        private ConfigItem ConfigItemToEditRequsted = null;

        private ILocalizer localizer = Localizer.Get();

        public CreateConfigHelperWindow()
        {
            this.InitializeComponent();

            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("CreateConfigHelperWindowTitle"));
            IconUri = @"Assets/Icons/Edit.ico";
            TitleIcon = TitleImageRectagle;
            TitleBar = WindowMoveAera;

            WindowHelper.TrySetMicaBackdrop(true, this, MainGrid);

            SetTitleBar(WindowMoveAera);

            Instanse = this;

            ContentFrame.Navigate(typeof(Views.CreateConfigHelper.MainPage), null, new DrillInNavigationTransitionInfo());
            this.Closed += CreateConfigHelperWindow_Closed;

            if (this.Content is FrameworkElement fe)
            {
                fe.Loaded += Fe_Loaded;
            }

            SetEditorBackgroundSettings();

            SetStatus();
        }
        private object NavigateBackParameterProperty = null;
        public object NavigateBackParameter 
        { 
            get => NavigateBackParameterProperty;
            private set => NavigateBackParameterProperty = value; 
        }

        public void NavigateBackWithParameter(object parameter)
        {
            NavigateBackParameter = parameter;
            ContentFrame.GoBack();
        }

        public void ClearNavigateBackParameter()
        {
            NavigateBackParameter = null;
        }

        private void Fe_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsDialogRequested)
            {
                OpenConfigEditPage(true, configItem: ConfigItemToEditRequsted);
            }

            BackButton.Visibility = Visibility.Collapsed;

            if (this.Content is FrameworkElement fe)
            {
                fe.Loaded -= Fe_Loaded;
            }
        }

        private void CreateConfigHelperWindow_Closed(object sender, WindowEventArgs args)
        {
            Instanse = null;
        }

        private async void AskForExit(NavigatingCancelEventArgs e)
        {
            ContentDialog exitDialog = new ContentDialog()
            {
                Title = localizer.GetLocalizedString("Exit"),
                Content = localizer.GetLocalizedString("ExitAsk"),
                PrimaryButtonText = localizer.GetLocalizedString("Yes"),
                CloseButtonText = localizer.GetLocalizedString("No"),
                XamlRoot = this.Content.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
            };
            var result = await exitDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ContentFrame.DispatcherQueue.TryEnqueue(() =>
                {
                    IsOperationExitAskAvailable = false;
                    if (ContentFrame.CanGoBack)
                    {
                        NavigateBackWithParameter(e.Parameter);
                        return;
                    }
                    ContentFrame.Navigate(e.SourcePageType, e.Parameter, new DrillInNavigationTransitionInfo());
                    
                });
            }
        }

        private void AuditMenuItemsEnabled(Type pageType)
        {
            HomeItem.IsEnabled = true;
            CreateNewConfigButton.IsEnabled = true;
            EditMenuItem.Visibility = Visibility.Collapsed;
            ViewMenuItem.Visibility = Visibility.Collapsed;

            ContentFrame.Style = (Style)MainGrid.Resources["ContentFrameDefaultStyle"];

            if (pageType == typeof(Views.CreateConfigHelper.MainPage))
            {
                HomeItem.IsEnabled = false;
            }
            else if (pageType == typeof(CreateNewConfigPage))
            {
                CreateNewConfigButton.IsEnabled = false;
                EditMenuItem.Visibility = Visibility.Visible;
                ViewMenuItem.Visibility = Visibility.Visible;
                ContentFrame.Style = (Style)MainGrid.Resources["ContentFrameTransparentStyle"];
            }
        }

        private void ContentFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (IsOperationExitAskAvailable)
            {
                AskForExit(e);
                e.Cancel = true;
                return;

            }

            if (e.Cancel != true)
            {
                AuditMenuItemsEnabled(e.SourcePageType);
                if (e.SourcePageType == typeof(Views.CreateConfigHelper.MainPage))
                {
                    ContentFrame.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (ContentFrame.CanGoBack)
                            ContentFrame.BackStack.Clear();
                    });
                }
                
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            BackButton.Visibility = ContentFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;

            UpdateCurrentHelpItem(e.SourcePageType);

            ContentFrame.ForwardStack.Clear();
        }

        private void UpdateCurrentHelpItem(Type page)
        {
            CurrentHelpMenuFlyoutItem.Text = string.Format(localizer.GetLocalizedString("/Flashlight/GetHelpFor"), localizer.GetLocalizedString($"/Flashlight/{page.Name}"));
        }

        private void HomeItem_Click(object sender, RoutedEventArgs e)
        {
            bool result = RemoveAndGoBackTo(typeof(Views.CreateConfigHelper.MainPage), ContentFrame);
            if (!result)
            {
                ContentFrame.Navigate(typeof(Views.CreateConfigHelper.MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        public void OpenConfigEditPage(bool skp = false, ConfigItem configItem = null)
        {
            IsDialogRequested = true;
            ConfigItemToEditRequsted = configItem;
            if (skp)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (configItem == null)
                    {
                        SelectConfigToEditContentDialog dialog = new SelectConfigToEditContentDialog()
                        {
                            XamlRoot = this.Content.XamlRoot
                        };
                        await dialog.ShowAsync();
                        if (dialog.SelectedConfigResult == SelectResult.Selected)
                        {
                            configItem = dialog.SelectedConfigItem;

                        }
                    }

                    ContentFrame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGEDIT", configItem), new DrillInNavigationTransitionInfo());
                });
            }
        }

        public void CreateNewConfigForComponentId(string componentId)
        {
            ContentFrame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGCREATEBYID", componentId), new DrillInNavigationTransitionInfo());
        }

        private void CreateNewConfigButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(CreateNewConfigPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void ImportConfigFromFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.Instance.GetValue<bool>("AD", "ImportConfigFromFile")) {
                ImportConfigFromFileDialog dialog = new ImportConfigFromFileDialog() { XamlRoot = this.Content.XamlRoot };
                await dialog.ShowAsync();
                SettingsManager.Instance.SetValue("AD", "ImportConfigFromFile", true);
            }

            string filePath;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Choose config file";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                openFileDialog.FilterIndex = 4;

                openFileDialog.Filter = "JSON configs (*.json)|*.json|BAT config files (*.bat)|*.bat|CMD config files (*.cmd)|*.cmd|All compacible config files (*.bat, *.cmd, *.json)|*.bat;*.cmd;*.json";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                    var (configItem, errorHappens) = ConfigHelper.LoadConfigFromFile(filePath);
                    ContentFrame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGIMPORT", configItem, errorHappens, filePath), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    return;
                }
            }
        }

        private void EditConfigButton_Click(object sender, RoutedEventArgs e)
        {
            OpenConfigEditPage(true);
        }

        public void OpenGoodCheckReportFromFile(string filePath)
        {
            ContentFrame.Navigate(typeof(ViewGoodCheckReportPage), Tuple.Create(NavigationState.LoadFileFromPath, filePath), new DrillInNavigationTransitionInfo());
        }

        private void OpenGoodCheckReportButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Choose GoodCheck report file";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                openFileDialog.Filter = "XML data files (*.xml)|*.xml";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                    ContentFrame.Navigate(typeof(ViewGoodCheckReportPage), Tuple.Create(NavigationState.LoadFileFromPath, filePath), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    return;
                }
            }
            
        }

        private async void RecentGoodCheckSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            RecentGoodCheckSelectionsContentDialog dialog = new RecentGoodCheckSelectionsContentDialog()
            {
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
            if (dialog.SelectedResult == SelectResult.Selected)
            {
                string directory = dialog.SelectedReport;
                ContentFrame.Navigate(typeof(ViewGoodCheckReportPage), Tuple.Create(NavigationState.LoadFileFromPath, directory), new DrillInNavigationTransitionInfo());
            }
        }

        private async void BeginNewGoodCheckSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigUtilWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
            // window.NavigateToPage<CreateViaGoodCheck>();
        }

        private async void ComponentsStore_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C001CS", new SuppressNavigationTransitionInfo());
        }

        private async void AddOnsStore_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C003AS", new SuppressNavigationTransitionInfo());
        }

        private async void ConfigsStore_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C002CS", new SuppressNavigationTransitionInfo());
        }

        private void Store_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        }

        private async void ExtiMenuButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigHelperWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
            window.Close();
        }

        private void ReportIssueButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchReportUrl();
        }

        private async void OfflineHelpButton_Click(object sender, RoutedEventArgs e)
        {
            OfflineHelpWindow window = await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
            window.NavigateToPage($"/Utils/{nameof(CreateConfigHelperWindow)}");
        }

        private void OnlineHelpButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchWikiUrl();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.Content is CreateNewConfigPage configPage) 
            {
                configPage.OpenSearch();
            }
        }

        private void SearchAndReplace_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.Content is CreateNewConfigPage configPage)
            {
                configPage.OpenSearchAndReplace();
            }
        }

        private void SetEditorBackgroundSettings()
        {
            string key = SettingsManager.Instance.GetValue<string>("APPEARANCE", "configEditorBackground");
            switch (key)
            {
                case "Mica":
                    MicaBackgroundMenuItem.IsChecked = true;
                    break;
                case "MicaTransparent":
                    MicaAltMenuItem.IsChecked = true;
                    break;
                case "MicaSmoke":
                    MicaBackgroundMenuItem.IsChecked = true;
                    break;
                default:
                    DefaultBackgroundMenuItem.IsChecked = true;
                    break;
            }
        }

        private void EditorBackgroundMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string key = "Black";
            if (MicaBackgroundMenuItem.IsChecked)
            {
                key = "Mica";
            }
            else if (MicaAltMenuItem.IsChecked)
            {
                key = "MicaTransparent";
            }
            else if (MicaSmokeMenuItem.IsChecked)
            {
                key = "MicaSmoke";
            }

            SettingsManager.Instance.SetValue("APPEARANCE", "configEditorBackground", key);

            if (ContentFrame.Content is CreateNewConfigPage configPage)
            {
                configPage.ChangeBackground();
            }
        }

        private void ZoomInMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.Content is CreateNewConfigPage configPage)
            {
                configPage.ChangeZoom(5);
            }
        }

        private void ZoomOutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.Content is CreateNewConfigPage configPage)
            {
                configPage.ChangeZoom(-5);
            }
        }

        private void ZoomReturnMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.Content is CreateNewConfigPage configPage)
            {
                configPage.ChangeZoom(0);
            }
        }

        private async void EditConfigKitButton_Click(object sender, RoutedEventArgs e)
        {
            SelectConfigKitToEditContentDialog dialog = new()
            {
                XamlRoot = this.Content.XamlRoot,
            };

            await dialog.ShowAsync();

            if (!string.IsNullOrEmpty(dialog.Result))
            {
                ContentFrame.Navigate(typeof(EditConfigKitPage), dialog.Result, new DrillInNavigationTransitionInfo());
            }
        }

        public void EditConfigKit(string kitId)
        {
            ContentFrame.Navigate(typeof(EditConfigKitPage), kitId, new DrillInNavigationTransitionInfo());
        }

        private void BackButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(this.SearchAnimatedIcon, "PointerOver");
        }

        private void BackButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(this.SearchAnimatedIcon, "Normal");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private async void CurrentHelpMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
            window.NavigateToPage($"/CreateConfigHelper/{ContentFrame.SourcePageType.Name}");
        }

        public void SetStatus(bool isWorking = false, string text = "")
        {
            ContentFrame.DispatcherQueue.TryEnqueue(() =>
            {
                StatusProgressIcon.Visibility = isWorking ? Visibility.Collapsed : Visibility.Visible;
                StatusProgressRing.Visibility = isWorking ? Visibility.Visible : Visibility.Collapsed;
                StatusTextBlock.Text = string.IsNullOrEmpty(text) ? localizer.GetLocalizedString("Ready") : text;
            });
        }
    }
}
