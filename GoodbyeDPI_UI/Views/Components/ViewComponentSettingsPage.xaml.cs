using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigUtil;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPI_UI.Helper.Static.UIHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Components
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public class ComboboxItem
    {
        public string file_name { get; set; }
        public string packId { get; set; }
        public string name { get; set; }
        public string packName { get; set; }
    }

    public sealed partial class ViewComponentSettingsPage : Page
    {
        private string ComponentId = string.Empty;
        private ObservableCollection<ComboboxItem> _comboboxItems = new();

        private readonly ObservableCollection<UIElement> _tiles = new();

        private bool ShowAnim = true;

        private ILocalizer localizer = Localizer.Get();

        public ViewComponentSettingsPage()
        {
            InitializeComponent();

            ConfigChooseCombobox.ItemsSource = _comboboxItems;

            _comboboxItems.CollectionChanged += ComboboxItems_CollectionChanged;

            StaggeredRepeater.ItemsSource = _tiles;

            DataContext = this;          
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string id && !string.IsNullOrEmpty(id))
            {
                ComponentId = id;
            }

            AutorunCheckBox.IsChecked = SettingsManager.Instance.GetValue<bool>(["CONFIGS", ComponentId], "usedForAutorun");

            LoadConfigItems();
            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    ComponentId);
            if (componentHelper is null) return;

            DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(ComponentId);
            string componentName = databaseStoreItem != null ? databaseStoreItem.ShortName : ComponentId;

            PageHeader.Text = string.Format(localizer.GetLocalizedString("ComponentSettingsPageHeader"), componentName);

            componentHelper.ConfigListUpdated += LoadConfigItems;

            InitSettingsTiles();
        }

        private void InitSettingsTiles()
        {
            bool _flag;
            _tiles.Clear();

            var sel = ConfigChooseCombobox.SelectedItem as ComboboxItem;

            if (sel == null)
                return;

            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

            List<VariableItem> variables = componentHelper.GetConfigHelper().GetVariables(sel.file_name, sel.packId);
            List<string> toggleLists = componentHelper.GetConfigHelper().GetToggleLists(sel.file_name, sel.packId);

            if (variables.Count > 0 || toggleLists.Count > 0)
            {

                SettingsTile variablesItem = new()
                {
                    IconGlyph = "\uE713",
                    Title = localizer.GetLocalizedString("/SettingTiles/AvailableSettings"),
                    Description = localizer.GetLocalizedString("/SettingTiles/SetupConfig")
                };

                _flag = false;
                foreach (var variable in variables)
                {
                    SettingsTileItem settingsTileItem = new()
                    {
                        Title = $"{componentHelper.GetConfigHelper().GetLocalizedConfigVarName(variable.name, sel.packId)}",
                        ShowTopRectangle = _flag,
                    };

                    settingsTileItem.Contents.Add(new SettingTileContentDefinition
                    {
                        ContentType = SettingTileContentType.ToggleSwitch,
                        VariableName = variable.name,
                        InitialToggleState = variable.value,
                        PackId = sel.packId,
                        FileName = sel.file_name,
                    });

                    variablesItem.Items.Add(settingsTileItem);

                    _flag = true;
                }

                _tiles.Add(CreateSettingTile(variablesItem, HandleSettingTileElementClick));
            }

            List<SiteListItem> list = componentHelper.GetConfigHelper().GetSiteListItems(sel.file_name, sel.packId, ignoreNull: true);
            if (list.Count > 0)
            {
                SettingsTile sitelistTile = new()
                {
                    IconGlyph = "\uE7C3",
                    Title = localizer.GetLocalizedString("/SettingTiles/UsedSiteLists"),
                    Description = localizer.GetLocalizedString("/SettingTiles/UsedSiteListsTip")
                };

                _flag = false;
                foreach (SiteListItem item in list)
                {
                    if (item.Type == "NULL")
                        continue;

                    string title =
                        localizer.GetLocalizedString($"/SettingTiles/{item.Type}") +
                        $" {item.Name}";

                    SettingsTileItem settingsTileItem = new()
                    {
                        Title = title,
                        ShowTopRectangle = _flag,
                    };
                    settingsTileItem.Contents.Add(new SettingTileContentDefinition
                    {
                        ContentType = item.Type == "AutoSiteList" ? SettingTileContentType.OnlyViewButton : SettingTileContentType.EditViewButtons,
                        EditFilePath = item.FilePath,
                        ViewParams = item.ApplyParams,
                        PrettyViewParams = item.PrettyApplyParams,
                    });

                    sitelistTile.Items.Add(settingsTileItem);

                    _flag = true;
                }

                _tiles.Add(CreateSettingTile(sitelistTile, HandleSettingTileElementClick));
            }

            SettingsTile advancedTile = new()
            {
                IconGlyph = "\uEC7A",
                Title = localizer.GetLocalizedString("/SettingTiles/AdvancedSettings"),
                Description = localizer.GetLocalizedString("/SettingTiles/AdvancedSettingsTip")
            };

            SettingsTileItem createNewTileItem = new()
            {
                Title = localizer.GetLocalizedString("/SettingTiles/CreateNewConfig"),
                ShowTopRectangle = false,
            };
            createNewTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.FullButton,
                ClickId = "CFGCREATE"
            });

            advancedTile.Items.Add(createNewTileItem);

            SettingsTileItem editTileItem = new()
            {
                Title = localizer.GetLocalizedString("/SettingTiles/EditConfig"),
                ShowTopRectangle = true,
            };
            editTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.FullButton,
                ClickId = "CFGEDIT"
            });

            advancedTile.Items.Add(editTileItem);

            if (StateHelper.GoodCheckSupportedComponents.Contains(ComponentId))
            {
                SettingsTileItem autoTileItem = new()
                {
                    Title = localizer.GetLocalizedString("/SettingTiles/SelectAutomatically"),
                    ShowTopRectangle = true,
                };
                autoTileItem.Contents.Add(new SettingTileContentDefinition
                {
                    ContentType = SettingTileContentType.FullButton,
                    ClickId = "CFGGOODCHECK"
                });

                advancedTile.Items.Add(autoTileItem);
            }

            _tiles.Add(CreateSettingTile(advancedTile, HandleSettingTileElementClick));

            SettingsTile helpTile = new()
            {
                IconGlyph = "\uE754",
                Title = localizer.GetLocalizedString("/Flashlight/Title"),
                Description = localizer.GetLocalizedString("/Flashlight/DefaultTip")
            };

            SettingsTileItem helpTileItem = new()
            {
                Title = localizer.GetLocalizedString("/Flashlight/OpenOfflineHelp"),
                ShowTopRectangle = false,
            };
            helpTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.FullButton,
                ClickId = "HELPOFFLINE"
            });

            helpTile.Items.Add(helpTileItem);

            // TODO: add dynamic help

            _tiles.Add(CreateSettingTile(helpTile, HandleSettingTileElementClick));
        }

        private void HandleSettingTileElementClick(ActionIds actionId, List<string> arguments, SettingTileContentDefinition contentDefinition)
        {
            switch (actionId)
            {
                case ActionIds.ViewButtonClicked:
                    Controls.Dialogs.ViewApplyArgsContentDialog dialog = new()
                    {
                        DialogTitle = arguments[0],
                        Args = contentDefinition.PrettyViewParams,
                        XamlRoot = this.XamlRoot,
                    };
                    _ = dialog.ShowAsync();
                    break;
                case ActionIds.EditButtonClicked:
                    Utils.OpenFileInDefaultApp(contentDefinition.EditFilePath);
                    break;
                case ActionIds.SwitchToggled:
                    ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                    bool.TryParse(arguments[0], out bool result);
                    componentHelper.GetConfigHelper().ChangeVariableValue(contentDefinition.FileName, contentDefinition.PackId, contentDefinition.VariableName, result);

                    ShowAnim = false;
                    InitSettingsTiles();
                    if (TasksHelper.Instance.IsTaskRunned(ComponentId).Result) _ = TasksHelper.Instance.RestartTask(ComponentId);
                    break;
                case ActionIds.FullButtonElementClicked:
                    ButtonClick(contentDefinition.ClickId);
                    break;

            }
        }

        private async void ButtonClick(string targetId)
        {
            switch (targetId)
            {
                case "CFGCREATE":
                    CreateConfigHelperWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
                    window.CreateNewConfigForComponentId(ComponentId);
                    break;
                case "CFGEDIT":
                    CreateConfigHelperWindow _window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
                    _window.OpenConfigEditPage();
                    break;
                case "CFGGOODCHECK":
                    CreateConfigUtilWindow gwindow = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
                    gwindow.NavigateToPage<CreateViaGoodCheck>(ComponentId);
                    break;
                case "HELPOFFLINE":

                    break;
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

            _comboboxItems.Clear();

            foreach (ConfigItem item in items)
            {
                ComboboxItem comboboxItem = new ComboboxItem();

                comboboxItem.file_name = item.file_name;
                comboboxItem.packId = item.packId;
                comboboxItem.name = $"{item.name}";
                comboboxItem.packName = DatabaseHelper.Instance.GetItemById(item.packId).ShortName;

                _comboboxItems.Add(comboboxItem);
            }

            if (_comboboxItems.Count == 0)
            {
                ToggleVisibility(false);
            }
            else
            {
                ToggleVisibility(true);
            }
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

        private void ComboboxItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ApplySavedSelection();
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
            }
            ShowAnim = true;
            InitSettingsTiles();
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

        private void StaggeredRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (!(args.Element is UIElement element))
                return;

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            ElementCompositionPreview.SetIsTranslationEnabled(element, true);

            if (ShowAnim)
                visual.Properties.InsertVector3("Translation", new Vector3(0, 50, 0));

            visual.Opacity = 0f;

            element.DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Normal,
                () =>
                {
                    var translationAnim = compositor.CreateVector3KeyFrameAnimation();
                    translationAnim.Target = "Translation";
                    translationAnim.InsertKeyFrame(0f, new Vector3(0, 50, 0));
                    translationAnim.InsertKeyFrame(1f, Vector3.Zero);
                    translationAnim.Duration = TimeSpan.FromMilliseconds(300);

                    if (ShowAnim)
                        visual.StartAnimation("Translation", translationAnim);


                    var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
                    fadeAnim.Target = "Opacity";
                    if (ShowAnim)
                        fadeAnim.InsertKeyFrame(0f, 0f);
                    else
                        fadeAnim.InsertKeyFrame(0.5f, 0.5f);
                    fadeAnim.InsertKeyFrame(1f, 1f);
                    fadeAnim.Duration = TimeSpan.FromMilliseconds(ShowAnim ? 300 : 150);

                    visual.StartAnimation("Opacity", fadeAnim);


                });
        }

        private void ToggleVisibility(bool visible)
        {
            if (!visible)
            {
                MainPanel.Visibility = Visibility.Collapsed;
                EmptyPageGrid.Visibility = Visibility.Visible;
            }
            else
            {
                MainPanel.Visibility = Visibility.Visible;
                EmptyPageGrid.Visibility = Visibility.Collapsed;
            }
        }

        private async void CreateConfigButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigHelperWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
            window.CreateNewConfigForComponentId(ComponentId);
        }

        private async void OpenStoreButton_Click(object sender, RoutedEventArgs e)
        {
            await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        }

        private void AutorunCheckBox_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue<bool>(["CONFIGS", ComponentId], "usedForAutorun", (bool)AutorunCheckBox.IsChecked);
            if ((bool)AutorunCheckBox.IsChecked) AutoStartManager.AddToAutorun();
        }
    }
}
