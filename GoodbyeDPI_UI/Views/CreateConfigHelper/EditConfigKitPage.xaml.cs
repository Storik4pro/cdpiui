using CDPI_UI.Controls.CreateConfigHelper;
using CDPI_UI.Controls.Dialogs.CreateConfigHelper;
using CDPI_UI.DataModel;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.CreateConfigHelper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EditConfigKitPage : Page
    {
        private string KitId = string.Empty;
        private readonly ObservableCollection<ViewConfigInKitModel> Configs = [];
        private ConfigHelper ConfigHelper = null;

        private readonly ILocalizer localizer = Localizer.Get();

        private readonly ObservableCollection<MenuItemViewModel> Folders = [];

        public ICommand RemoveConfigCommand { get; }
        public ICommand RenameConfigCommand { get; }
        public ICommand EditConfigCommand { get; }
        public ICommand OpenDirectoryCommand { get; }

        private object NavigationParameter = null;

        public EditConfigKitPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;

            ConfigsListView.ItemsSource = Configs;

            RemoveConfigCommand = new RelayCommand(p => RemoveConfig((Tuple<string>)p));
            RenameConfigCommand = new RelayCommand(p => RenameConfig((Tuple<string, string>)p));
            EditConfigCommand = new RelayCommand(p => EditConfig((Tuple<string>)p));
            OpenDirectoryCommand = new RelayCommand(p => OpenConfigDirectory((Tuple<string, OpenFileDirectoryTypes>)p));
        }

        private void EditConfigKitPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadedActions();
            Loaded -= EditConfigKitPage_Loaded;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.SourcePageType != typeof(CreateNewConfigPage))
            {
                Configs.Clear();
                KitId = string.Empty;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            NavigationParameter = e.Parameter;

            var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();

            if (window?.NavigateBackParameter is ConfigItem configItem)
            {
                ReplaceConfig(configItem);
            }
            window?.ClearNavigateBackParameter();

            if (IsLoaded) LoadedActions();
            else Loaded += EditConfigKitPage_Loaded;
        }

        private async void LoadedActions()
        {
            if (NavigationParameter is string kitId && KitId != kitId)
            {
                KitId = kitId;
                await Task.Run(() => LoadConfigItems());
            }
        }

        private async void LoadConfigItems()
        {
            ToggleLoadingMode(true, localizer.GetLocalizedString("LoadingConfigs"));

            DispatcherQueue.TryEnqueue(() =>
            {
                Folders.Clear();
                Configs.Clear();
            });
            ConfigHelper = new(target:"$ANY");
            ConfigHelper.Init(KitId);

            await Task.Delay(100);

            List<ConfigItem> items = ConfigHelper.GetConfigItems();

            foreach (ConfigItem item in items)
            {
                List<string> usedSiteLists = 
                    [.. (await ConfigHelper.GetSiteListItemsAsync(item.file_name, item.packId, ignoreNull:true)).Select(x => Path.GetFileName(x.FilePath))];
                List<string> excludedSiteLists = 
                    [.. (await ConfigHelper.GetExcludedSiteListItemsAsync(item.file_name, item.packId, ignoreNull: true)).Select(x => Path.GetFileName(x.FilePath))];

                DispatcherQueue.TryEnqueue(() =>
                {
                    Configs.Add(new()
                    {
                        Guid = Guid.NewGuid().ToString(),
                        PackId = item.packId,
                        FileName = item.file_name,

                        TargetComponentId = item.target[0],
                        DisplayName = item.name,
                        UsedSiteLists = usedSiteLists,
                        ExcludedSiteLists = excludedSiteLists,
                        LastEditTime = ConfigHelper.GetLastEditTimeFromConfigFile(item.file_name, item.packId),
                    });
                });
            }
            ToggleLoadingMode(false);
            await Task.CompletedTask;
        }

        private void ToggleLoadingMode(bool loadingMode, string text = "")
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SaveAsButton.IsEnabled = !loadingMode;
                SaveButton.IsEnabled = !loadingMode;
            });
            var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();
            window?.SetStatus(loadingMode, text);
        }

        private void RemoveConfig(Tuple<string> parameter)
        {
            var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

            if (item != null)
            {
                Configs.Remove(item);
                ConfigHelper.RemoveConfig(item.FileName, item.PackId, false);
            }
        }

        private void RenameConfig(Tuple<string, string> parameter)
        {
            var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

            if (item != null)
            {
                item.DisplayName = parameter.Item2;
                ConfigItem configItem = ConfigHelper.GetConfigItem(item.FileName, item.PackId);
                if (configItem != null)
                {
                    configItem.name = parameter.Item2;
                }
                ConfigHelper.SetConfigItem(configItem);
            }
        }

        private void EditConfig(Tuple<string> parameter)
        {
            var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

            if (item != null)
            {
                ConfigItem configItem = ConfigHelper.GetConfigItem(item.FileName, item.PackId);
                Frame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGRETURNEDITED", configItem), new DrillInNavigationTransitionInfo());
            }
        }
        private void OpenConfigDirectory(Tuple<string, OpenFileDirectoryTypes> parameter)
        {
            var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

            if (item != null)
            {
                Utils.OpenFolderInExplorer(ConfigHelper.GetItemFolderFromPackId(KitId));
            }
        }

        private void ReplaceConfig(ConfigItem newItem)
        {
            var item = Configs.FirstOrDefault(x => x.FileName == newItem.file_name && x.PackId == newItem.packId);

            if (item != null)
            {
                item.DisplayName = string.IsNullOrEmpty(newItem.name) ? newItem.not_converted_name : newItem.name;
                item.LastEditTime = DateTime.Now.ToString();
                ConfigHelper.SetConfigItem(newItem);
            }
        }


        private async void SaveKit()
        {
            ToggleLoadingMode(true, localizer.GetLocalizedString("SavingConfigKit"));
            foreach (ConfigItem configItem in ConfigHelper.GetConfigItems())
            {
                var item = Configs.FirstOrDefault(x => x.PackId == configItem.packId && x.FileName == configItem.file_name);
                if (item == null) 
                {
                    string errorCode =
                        ConfigHelper.RemoveConfig(configItem.file_name, configItem.packId, true);
                    if (!string.IsNullOrEmpty(errorCode))
                    {
                        ShowErrorDialog(string.Format(localizer.GetLocalizedString("SaveConfigException"), errorCode), localizer.GetLocalizedString("SomethingWentWrong"));
                        SaveButton.IsEnabled = true;
                        SaveAsButton.IsEnabled = true;
                        return;
                    }
                }
                else
                { 
                    await ConfigHelper.SaveConfigItem(configItem.file_name, configItem.packId, configItem);
                }
            }

            ToggleLoadingMode(false);

            ComponentItemsLoaderHelper.Instance.Init(forse: true);

            if (Frame.CanGoBack) Frame.GoBack();
            else Frame.Navigate(typeof(CreateConfigHelper.MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void ShowErrorDialog(string message, string title)
        {
            var dlg = new MessageDialog(message, title);
            InitializeWithWindow.Initialize(
                dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>())
                );
            await dlg.ShowAsync();

            
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfirmationFlyout.Hide();
            SaveKit();
        }

        private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLoadingMode(true, localizer.GetLocalizedString("SavingConfigKit"));

            SaveConfigKitAsContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                DisplayName = DatabaseHelper.Instance.GetItemById(KitId)?.ShortName + $" ({localizer.GetLocalizedString("Edited")})" ?? string.Empty,
            };

            await dialog.ShowAsync();

            var result = dialog.Result;

            if (result.Mode == SaveConfigKitAsModes.None)
            {
                ToggleLoadingMode(false);
                return;
            }

            if (result.Mode == SaveConfigKitAsModes.SaveForMe)
            {
                string newPackId = $"{Guid.NewGuid()}_FM";
                string directory = ConfigHelper.GetItemFolderFromPackId(newPackId);
                string lstFile = directory;

                ConfigPackMakeHelper.ConfigPackInitModel templateModel = new()
                {
                    StoreId = newPackId,
                    ShortName = result.DisplayName,
                    Name = result.DisplayName,
                    Developer = SettingsManager.Instance.GetValueOrDefault("CONFIGKIT", "lastUsedDevName", defaultValue: Environment.UserName),
                };

                var operationResult = await Task.Run(() =>
                {
                    return ConfigPackMakeHelper.CreateConfigPack(templateModel, ConfigHelper.GetConfigItems(), directory, autoImport:true).Result;
                });

                if (operationResult?.IsSuccess != true)
                {
                    ErrorContentDialog errorDialog = new() { };
                    await errorDialog.ShowErrorDialogAsync(
                        content: string.Format(localizer.GetLocalizedString("FileSaveErrorMessage"), directory, operationResult?.ErrorCode),
                        errorDetails: operationResult?.ErrorMessage,
                        xamlRoot: this.Content.XamlRoot);
                }
            }
            else
            {
                var _dialog = new Microsoft.Win32.SaveFileDialog
                {
                    OverwritePrompt = true,
                    FileName = $"{result.DisplayName}.cdpiconfigpack",
                    DefaultExt = ".cdpiconfigpack",
                    Filter = $"{localizer.GetLocalizedString("ConfigPack")}|*.cdpiconfigpack"
                };
                var saveDialogResult = _dialog.ShowDialog();
                if (saveDialogResult.HasValue && saveDialogResult.Value)
                {
                    string filename = _dialog.FileName;

                    ConfigPackMakeHelper.ConfigPackInitModel templateModel = new()
                    {
                        StoreId = result.Id,
                        ShortName = result.DisplayName,
                        Name = result.DisplayName,
                        Version = result.Version,
                        Developer = result.Developer,
                        Color = result.HexAccentColor,
                        Icon = result.ImageSourceUri == new Uri("ms-appx:///Assets/Store/empty.png") ? $"$LOADSTATIC(Store/empty.png)" : result.ImageSourceUri.LocalPath
                    };

                    var operationResult = await Task.Run(() =>
                    {
                        return ConfigPackMakeHelper.CreateConfigPack(templateModel, ConfigHelper.GetConfigItems(), filename).Result;
                    });

                    if (operationResult?.IsSuccess != true)
                    {
                        ErrorContentDialog errorDialog = new() { };
                        await errorDialog.ShowErrorDialogAsync(
                            content: string.Format(localizer.GetLocalizedString("FileSaveErrorMessage"), _dialog.FileName, operationResult?.ErrorCode),
                            errorDetails: operationResult?.ErrorMessage,
                            xamlRoot: this.Content.XamlRoot);
                    }

                }
            }

            await Task.Delay(200);

            ToggleLoadingMode(false);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                CreateConfigHelperWindow.RemoveAndGoBackTo(typeof(MainPage), Frame);
            else
                ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>()?.Close();
        }

        private void CloseFlyoutButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfirmationFlyout.Hide();
        }

        private void SaveConfirmationFlyout_Opened(object sender, object e)
        {
            SaveAsButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
        }

        private void SaveConfirmationFlyout_Closed(object sender, object e)
        {
            SaveAsButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
        }
    }
}
