using CDPI_UI.Controls.Dialogs.ComponentSettings;
using CDPI_UI.Controls.Universal;
using CDPI_UI.Extensions;
using CDPI_UI.Helper;
using CDPI_UI.Helper.CreateConfigHelper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.Helper.UserExperience;
using CDPI_UI.ViewModels;
using CDPI_UI.Views.CreateConfigHelper;
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;
using static System.Net.Mime.MediaTypeNames;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Components
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TgWsProxyComponentPage : Page
    {
        private readonly string DefaultTgWsProxyStartString = 
            $"--host=127.0.0.1 --port=1443 --secret={Utils.GetRandomHexNumber(32)} --dc-ip=2:149.154.167.220 --dc-ip=4:149.154.167.220 --buf-kb=256 --pool-size=4 --log-max-mb=5";


        private readonly ObservableCollection<ViewTelegramDataCenterModel> TelegramDataCenterModels = [];
        private readonly ObservableCollection<ComboboxItem> _comboboxItems = [];

        public ICommand TgWsProxyEditCommand { get; }
        public ICommand TgWsProxyRemoveCommand { get; }

        public ICommand LinkedSettingClickCommand { get; }

        private ILocalizer localizer = Localizer.Get();

        public string ComponentId { get; private set; } = string.Empty;
        private ComponentPageNavigationModel Model;

        public readonly ObservableCollection<SettingLinkModel> LinkedSettings = [];

        public TgWsProxyComponentPage()
        {
            InitializeComponent();
            this.DataContext = this;

            ConfigChooseCombobox.ItemsSource = _comboboxItems;
            _comboboxItems.CollectionChanged += ComboboxItems_CollectionChanged;

            TgWsProxyEditCommand = new RelayCommand(p => EditElement((string)p));
            TgWsProxyRemoveCommand = new RelayCommand(p => RemoveElement((string)p));
            LinkedSettingClickCommand = new RelayCommand(p => LinkedSettingClicked(p));

            DataCentersListView.ItemsSource = TelegramDataCenterModels;
            LinkedSettingsUserControl.SettingModelsList = LinkedSettings;

            HideInfoBar();
            LoadLinks();
        }

        private void LoadLinks()
        {
            LinkedSettingsHelper.LoadLinksForPage(LinkedSettingsHelper.Pages.TgWsProxyPage, LinkedSettings);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ComponentPageNavigationModel model)
            {
                ComponentId = model.Id;
                Model = model;
            }

            var item = ConfigChooseCombobox.SelectedItem as ComboboxItem;
            Task.Run(() => InitPage(item));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    ComponentId);

            if (componentHelper is null) return;
            
            componentHelper.ConfigListUpdated -= LoadConfigItems;
        }

        private void ToggleVisibility(bool visible)
        {
            if (!visible)
            {
                ContentStackPanel.Visibility = Visibility.Collapsed;
                EmptyStackPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ContentStackPanel.Visibility = Visibility.Visible;
                EmptyStackPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadConfigItems()
        {
            ComponentItemsLoaderHelper.Instance.Init();

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    ComponentId);

            if (componentHelper is null)
                return;

            List<ConfigItem> items = componentHelper.GetConfigHelper().GetConfigItems();

            DispatcherQueue.TryEnqueue(() => _comboboxItems.Clear());

            foreach (ConfigItem item in items)
            {
                ComboboxItem comboboxItem = new ComboboxItem();

                comboboxItem.file_name = item.file_name;
                comboboxItem.packId = item.packId;
                comboboxItem.name = $"{item.name}";
                comboboxItem.packName = DatabaseHelper.Instance.GetItemById(item.packId).ShortName;

                DispatcherQueue.TryEnqueue(() => _comboboxItems.Add(comboboxItem));
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_comboboxItems.Count == 0)
                {
                    ToggleVisibility(false);
                }
                else
                {
                    ToggleVisibility(true);
                }
            });
        }

        private void InitPage(ComboboxItem item)
        {
            LoadConfigItems();
            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    ComponentId);
            if (componentHelper is null) return;

            componentHelper.ConfigListUpdated += LoadConfigItems;

            Debug.WriteLine(">>>>Working");

            InitSettings(item);
        }

        private void ComboboxItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ApplySavedSelection();
        }

        private void ApplySavedSelection()
        {
            var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
            var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");

            if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
                return;

            var match = _comboboxItems
                .FirstOrDefault(ci => ci.file_name == savedFile
                                   && ci.packId == savedPackId);
            if (match != null)
                ConfigChooseCombobox.SelectedItem = match;
        }

        private void InitSettings(ComboboxItem comboboxItem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TelegramDataCenterModels.Clear();
            });
            if (comboboxItem == null) return;
            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

            string startupString = componentHelper.GetConfigHelper().GetStartupParameters(comboboxItem.file_name, comboboxItem.packId);

            ObservableCollection<GraphicDesignerSettingItemModel> lst = [];
            GraphicDesignerHelper.LoadTgWsProxyDesignerConfig(lst, []);
            GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(lst, [], startupString, exclusive: false, model: GraphicDesignerHelper.TgWsProxyDesignerConfig);

            DispatcherQueue.TryEnqueue(() =>
            {
                AdvancedLoggingToogleSwitch.IsOn = false;
                foreach (var item in lst)
                {
                    if (!item.IsChecked && item.Type == "string") continue;
                    switch (item.DisplayName)
                    {
                        case "--port":
                            PortValue.Text = item.Value;
                            break;
                        case "--host":
                            IPValue.Text = item.Value;
                            break;
                        case "--secret":
                            KeyValue.Text = item.Value;
                            break;
                        case "--buf-kb":
                            BufferSizeTextBox.Text = item.Value;
                            break;
                        case "--pool-size":
                            WebSocketSizeTextBox.Text = item.Value;
                            break;
                        case "--log-max-mb":
                            LogSizeTextBox.Text = item.Value;
                            break;
                        case "--verbose":
                            AdvancedLoggingToogleSwitch.IsOn = item.IsChecked;
                            break;
                        case "--dc-ip":
                            var matchResult = FindSupportedString().Match(item.Value);
                            if (matchResult.Groups.Count >= 3)
                            {
                                TelegramDataCenterModels.Add(new()
                                {
                                    Guid = item.Guid,
                                    Number = matchResult.Groups[1].Value,
                                    Ip = matchResult.Groups[2].Value,
                                });
                            }
                            break;
                    }
                }
            });

        }

        private void ShowInfoBar(string title, InfoBarSeverity severity)
        {
            HideInfoBar();
            ActionInfoBar.IsOpen = true;
            ActionInfoBar.Title = title;
            ActionInfoBar.Severity = severity;
            HideInfoBarAnimation.Stop();
            ShowInfoBarAnimation.Begin();
        }

        private void HideInfoBar()
        {
            ShowInfoBarAnimation.Stop();
            HideInfoBarAnimation.Begin();
        }

        private void RemoveElement(string guid)
        {
            TelegramDataCenterModels.Remove(TelegramDataCenterModels.FirstOrDefault(x => x.Guid == guid));
        }

        private async void EditElement(string guid)
        {
            var el = TelegramDataCenterModels.FirstOrDefault(x => x.Guid == guid);
            var dialog = new TgWsDataCenterEditContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Number = el?.Number,
                Ip = el?.Ip,
                Title = localizer.GetLocalizedString("EditDataCenter")
            };

            await dialog.ShowAsync();

            if (dialog.Result)
            {
                el.Number = dialog.Number;
                el.Ip = dialog.Ip;
            }
        }

        

        private async void PasteFromBufferButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackageView dataPackageView = Clipboard.GetContent();

            if (dataPackageView.Contains(StandardDataFormats.Text) is true)
            {
                string text = await dataPackageView.GetTextAsync();

                var matchResult = FindSupportedString().Matches(text);
                Debug.WriteLine(matchResult.Count);
                if (matchResult.Count > 0)
                {
                    foreach (Match item in matchResult)
                    {
                        var existItem = TelegramDataCenterModels.FirstOrDefault(x => x.Number == item.Groups[1].Value);
                        if (existItem != null)
                        {
                            existItem.Ip = item.Groups[2].Value;
                        }
                        else
                        {
                            TelegramDataCenterModels.Add(new()
                            {
                                Guid = Guid.NewGuid().ToString(),
                                Number = item.Groups[1].Value,
                                Ip = item.Groups[2].Value
                            });
                        }
                    }

                    ShowInfoBar(localizer.GetLocalizedString("PasteComplete"), InfoBarSeverity.Success);
                    return;
                }
                ShowInfoBar(localizer.GetLocalizedString("PasteFailure"), InfoBarSeverity.Warning);

            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TgWsDataCenterEditContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Title = localizer.GetLocalizedString("AddDataCenter")
            };

            await dialog.ShowAsync();
            if (dialog.Result)
            {
                TelegramDataCenterModels.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Number = dialog.Number,
                    Ip = dialog.Ip,
                });
            }
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            string targetText = string.Empty;
            foreach (var model in TelegramDataCenterModels)
            {
                targetText += $"{model.Number}:{model.Ip}\n";
            }

            DataPackage dataPackage = new();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(targetText);
            Clipboard.SetContent(dataPackage);

            ShowInfoBar(localizer.GetLocalizedString("CopyComplete"), InfoBarSeverity.Success);
            return;
        }

        private void ActionInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
        {
            HideInfoBar();
            args.Cancel = true;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            IPValue.Text = "127.0.0.1";
            PortValue.Text = "1080";
        }

        private void ResetKeyButton_Click(object sender, RoutedEventArgs e)
        {
            KeyValue.Text = $"{Utils.GetRandomHexNumber(32)}";
        }

        private void ResetWebSocketSizeButton_Click(object sender, RoutedEventArgs e)
        {
            WebSocketSizeTextBox.Text = "4";
        }

        private void ResetBufferButton_Click(object sender, RoutedEventArgs e)
        {
            BufferSizeTextBox.Text = "256";
        }

        private void ResetLogSizeButton_Click(object sender, RoutedEventArgs e)
        {
            LogSizeTextBox.Text = "5";
        }

        private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigChooseCombobox.SelectedItem is ComboboxItem sel)
            {
                string oldCfg = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
                string oldId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", sel.file_name);
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", sel.packId);

                if ((oldCfg != sel.file_name || oldId != sel.packId) && await TasksHelper.Instance.IsTaskRunned(ComponentId)) await TasksHelper.Instance.RestartTask(ComponentId);

                _ = Task.Run(() => InitSettings(sel));
            }

            if (ConfigChooseCombobox.SelectedItem is null)
            {
                TilesStackPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                TilesStackPanel.Visibility = Visibility.Visible;
            }
        }

        private async void CreateConfig()
        {
            var dialog = new CreateNewConfigForComponentContentDialog()
            {
                XamlRoot = this.XamlRoot,
            };

            await dialog.ShowAsync();

            if (dialog.Result == CreateNewConfigForComponentContentDialog.ConfigCreationVariants.FromTemplate)
            {
                var item = CreateConfigPageHelper.CreateConfigItem(
                StateHelper.LocalUserItemsId, localizer.GetLocalizedString("DefaultConfigPlaceholder"), ComponentId, [], [], [], [], DefaultTgWsProxyStartString
                );
                string fileName = $"{Guid.NewGuid()}.json";
                string errorCode = await ConfigHelper.SaveConfigItem(fileName, StateHelper.LocalUserItemsId, item);

                if (!string.IsNullOrEmpty(errorCode))
                    ShowErrorDialog(string.Format(localizer.GetLocalizedString("SaveConfigException"), errorCode), localizer.GetLocalizedString("SomethingWentWrong"));

                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", fileName);
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", StateHelper.LocalUserItemsId);

                ComponentHelper componentHelper =
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                componentHelper.ReInitConfigs();
            }
            else if (dialog.Result == CreateNewConfigForComponentContentDialog.ConfigCreationVariants.Manual)
            {
                CreateConfigHelperWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
                window.CreateNewConfigForComponentId(ComponentId);
            }
        }

        private void CreateConfigButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfig();
        }

        private void SetValueToGraphicDesignerItemsList(string key, string value, ObservableCollection<GraphicDesignerSettingItemModel> list, bool isChecked = true)
        {
            var flag = list.FirstOrDefault(x => x.DisplayName == key);
            if (flag != null)
            {
                flag.IsChecked = isChecked;
                flag.Value = value;
            }
            else
            {
                list.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    DisplayName = key,
                    IsChecked = isChecked,
                    Value = value,
                    EnableTextInput = true,
                    Type = "string",
                    Description = string.Empty
                });
            }
        }

        private string GetStartupString(string startupString)
        {
            ObservableCollection<GraphicDesignerSettingItemModel> lst = [];
            string addArgs = GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(lst, [], startupString, exclusive: false, model: GraphicDesignerHelper.TgWsProxyDesignerConfig);

            SetValueToGraphicDesignerItemsList("--port", PortValue.Text, lst);
            SetValueToGraphicDesignerItemsList("--host", IPValue.Text, lst);
            SetValueToGraphicDesignerItemsList("--secret", KeyValue.Text, lst);
            SetValueToGraphicDesignerItemsList("--buf-kb", BufferSizeTextBox.Text, lst);
            SetValueToGraphicDesignerItemsList("--pool-size", WebSocketSizeTextBox.Text, lst);
            SetValueToGraphicDesignerItemsList("--log-max-mb", LogSizeTextBox.Text, lst);
            SetValueToGraphicDesignerItemsList("--verbose", string.Empty, lst, AdvancedLoggingToogleSwitch.IsOn);

            lst.RemoveAll(x => x.DisplayName == "--dc-ip");

            foreach (var item in TelegramDataCenterModels)
            {
                lst.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    DisplayName = "--dc-ip",
                    IsChecked = true,
                    Value = $"{item.Number}:{item.Ip}",
                    EnableTextInput = true,
                    Type = "string",
                    Description = string.Empty
                });
            }
            startupString = GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(lst, [], addArgs);
            return startupString;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Model?.GoBackSignal?.Invoke(Model);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = ConfigChooseCombobox.SelectedItem as ComboboxItem;
            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

            var item = componentHelper.GetConfigHelper().GetConfigItem(sel.file_name, sel.packId);

            item.startup_string = GetStartupString(item.startup_string);
            
            string errorCode = await ConfigHelper.SaveConfigItem(sel.file_name, sel.packId, item);
            if (!string.IsNullOrEmpty(errorCode))
            {
                ShowErrorDialog(string.Format(localizer.GetLocalizedString("SaveConfigException"), errorCode), localizer.GetLocalizedString("SomethingWentWrong"));
                return;
            }

            componentHelper.ReInitConfigs();
        }

        private async void ShowErrorDialog(string message, string title)
        {
            var dlg = new MessageDialog(message, title);
            InitializeWithWindow.Initialize(
                dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>())
                );
            await dlg.ShowAsync();


        }

        private async void LinkedSettingClicked(object param)
        {
            if (param is LinkedActions action)
            {
                switch (action)
                {
                    case LinkedActions.EditCurrentConfig:
                        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                        var item = (ComboboxItem)ConfigChooseCombobox.SelectedItem;

                        CreateConfigHelperWindow _window = await((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
                        if (componentHelper != null)
                            _window.OpenConfigEditPage(skp: false, configItem: componentHelper.GetConfigHelper().GetConfigItems().FirstOrDefault(x => x.packId == item.packId && x.file_name == item.file_name));
                        break;
                    case LinkedActions.CreateNewConfigForComponent:
                        CreateConfig();
                        break;
                    case LinkedActions.OpenProxyInTelegram:
                        ConnectTelegramProxyContentDialog dialog = new()
                        {
                            XamlRoot = this.XamlRoot,
                        };
                        await dialog.ShowAsync();
                        break;
                }
            }
        }

        [GeneratedRegex(@"(\d.*?):(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\D*", RegexOptions.Singleline)]
        private static partial Regex FindSupportedString();

        
    }
}
