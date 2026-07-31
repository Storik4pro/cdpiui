using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Views.CreateConfigUtil;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Features;
using CDPIUI.Core.Store.Data;
using CDPIUI.Shared.Extentions;
using CDPIUI.Shared.Models;
using CDPIUI.Core.System;
using System.Collections.Specialized;
using CDPIUI.Controls.Default;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Main.Components
{
    public sealed partial class DefaultComponentSettingsPage : TemplatePage
    {
        private string ComponentId = string.Empty;
        private ObservableCollection<ConfigSelectorItem> _configItems = new();

        private readonly ObservableCollection<UIElement> _tiles = new();

        private bool ShowAnim = true;
        private ILocalizer localizer = Localizer.Get();
        public DefaultComponentSettingsPage()
        {
            InitializeComponent();

            ConfigChooseCombobox.ItemsSource = _configItems;

            _configItems.CollectionChanged += ConfigItems_CollectionChanged;

            StaggeredRepeater.ItemsSource = _tiles;

            DataContext = this;
            ToggleLoading(true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (Parameter != null)
            {
                ComponentId = Parameter.Get("componentId") ?? string.Empty;
            }

            DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(ComponentId);
            string componentName = databaseStoreItem != null ? databaseStoreItem.ShortName : ComponentId;
            
            var item = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
            Task.Run(() => InitPage(item));
            
        }


        private void InitPage(ConfigSelectorItem item)
        {
            LoadConfigItems();
            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    ComponentId);
            if (componentHelper is null) return;

            componentHelper.ConfigListUpdated += LoadConfigItems;

            Debug.WriteLine(">>>>Working");

            InitSettingsTiles(item);
        }

        private void InitSettingsTiles(ConfigSelectorItem sel)
        {
            bool _flag;

            DispatcherQueue.TryEnqueue(() =>
            {
                _tiles.Clear();
            });

            if (sel == null)
                return;

            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

            List<VariableItem> variables = componentHelper.GetConfigHelper().GetVariables(sel.FileName, sel.PackId);
            List<string> toggleLists = componentHelper.GetConfigHelper().GetToggleLists(sel.FileName, sel.PackId);

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
                        Title = $"{componentHelper.GetConfigHelper().GetLocalizedConfigVarName(variable.name, sel.PackId)}",
                        ShowTopRectangle = _flag,
                    };

                    settingsTileItem.Contents.Add(new SettingTileContentDefinition
                    {
                        ContentType = SettingTileContentType.ToggleSwitch,
                        VariableName = variable.name,
                        InitialToggleState = variable.value,
                        PackId = sel.PackId,
                        FileName = sel.FileName,
                    });

                    variablesItem.Items.Add(settingsTileItem);

                    _flag = true;
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    _tiles.Add(CreateSettingTile(variablesItem, HandleSettingTileElementClick));
                });
            }

            List<SiteListItem> list = componentHelper.GetConfigHelper().GetSiteListItems(sel.FileName, sel.PackId, ignoreNull: true);
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

                    string hardLinkTargetFile = FileSystemLinksManager.IsFileLinked(sel.PackId, item.FilePath);

                    string title =
                        localizer.GetLocalizedString($"/SettingTiles/{item.Type}") +
                        $" {item.Name}" + (string.IsNullOrEmpty(hardLinkTargetFile) ? "" : $" ({Path.GetFileName(hardLinkTargetFile)})");

                    SettingsTileItem settingsTileItem = new()
                    {
                        Title = title,
                        ShowTopRectangle = _flag,
                    };
                    
                    settingsTileItem.Contents.Add(new SettingTileContentDefinition
                    {
                        ContentType = item.Type == "AutoSiteList" ? SettingTileContentType.OnlyViewButton : SettingTileContentType.EditViewButtons,
                        EditFilePath = item.FilePath,
                        PackId = sel.PackId,
                        ViewParams = item.ApplyParams,
                        PrettyViewParams = item.PrettyApplyParams,
                        IsFileHardLinked = !string.IsNullOrEmpty(hardLinkTargetFile),
                        HardLinkTargetFile = hardLinkTargetFile,
                        IsIPSet = item.Type == "IpList"
                    });

                    sitelistTile.Items.Add(settingsTileItem);

                    _flag = true;
                }

                DispatcherQueue.TryEnqueue(() => { _tiles.Add(CreateSettingTile(sitelistTile, HandleSettingTileElementClick)); });
            }
            List<SiteListItem> excludeList = componentHelper.GetConfigHelper().GetExcludedSiteListItems(sel.FileName, sel.PackId, ignoreNull: true);
            if (excludeList.Count > 0)
            {
                SettingsTile sitelistTile = new()
                {
                    IconGlyph = "\uE7C3",
                    Title = localizer.GetLocalizedString("/SettingTiles/UsedExcludedSiteLists"),
                    Description = localizer.GetLocalizedString("/SettingTiles/UsedExcludedSiteListsTip")
                };

                _flag = false;
                foreach (SiteListItem item in excludeList)
                {
                    if (item.Type == "NULL")
                        continue;

                    string hardLinkTargetFile = FileSystemLinksManager.IsFileLinked(sel.PackId, item.FilePath);

                    string title =
                        localizer.GetLocalizedString($"/SettingTiles/{item.Type}") +
                        $" {item.Name}" + (string.IsNullOrEmpty(hardLinkTargetFile) ? "" : $" ({Path.GetFileName(hardLinkTargetFile)})");

                    SettingsTileItem settingsTileItem = new()
                    {
                        Title = title,
                        ShowTopRectangle = _flag,
                    };
                    
                    settingsTileItem.Contents.Add(new SettingTileContentDefinition
                    {
                        ContentType = item.Type == "AutoSiteList" ? SettingTileContentType.OnlyViewButton : SettingTileContentType.EditViewButtons,
                        PackId = sel.PackId,
                        EditFilePath = item.FilePath,
                        ViewParams = item.ApplyParams,
                        PrettyViewParams = item.PrettyApplyParams,
                        IsFileHardLinked = !string.IsNullOrEmpty(hardLinkTargetFile),
                        HardLinkTargetFile = hardLinkTargetFile,
                        IsIPSet = item.Type == "IpList"
                    });

                    sitelistTile.Items.Add(settingsTileItem);

                    _flag = true;
                }

                DispatcherQueue.TryEnqueue(() => { _tiles.Add(CreateSettingTile(sitelistTile, HandleSettingTileElementClick)); });
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

            if (HardcodedItemIds.GoodCheckSupportedComponents.Contains(HardcodedItemIds.ComponentIds.GetKeyByValue(ComponentId)))
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

            DispatcherQueue.TryEnqueue(() => { _tiles.Add(CreateSettingTile(advancedTile, HandleSettingTileElementClick)); });

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

            SettingsTileItem whatConfigChooseHelpTileItem = new()
            {
                Title = localizer.GetLocalizedString("/Flashlight/WhatConfigChooseHelp"),
                ShowTopRectangle = true,
            };
            whatConfigChooseHelpTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.FullButton,
                ClickId = "HELPOFFLINECONFIGCHOOISE"
            });

            helpTile.Items.Add(whatConfigChooseHelpTileItem);

            // TODO: add dynamic help

            DispatcherQueue.TryEnqueue(() => { _tiles.Add(CreateSettingTile(helpTile, HandleSettingTileElementClick)); });
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
                case ActionIds.OpenFolderClicked:
                    ShellHelper.LookupFileInDirectory(contentDefinition.EditFilePath);
                    break;
                case ActionIds.ChangeSiteListClicked:
                    if (contentDefinition.IsFileHardLinked)
                    {
                        RevertFileLink(arguments[0], contentDefinition.PackId, contentDefinition.EditFilePath, contentDefinition.HardLinkTargetFile);
                    }
                    else
                    {
                        ShowChangeListDialog(arguments[0], contentDefinition.PackId, contentDefinition.EditFilePath, contentDefinition.IsIPSet);
                    }
                    break;
                case ActionIds.EditButtonClicked:
                    if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || !SettingsManager.Instance.GetValueOrDefault<bool>("FILEOPENACTIONS", "doNotRemindAgain", defaultValue: true))
                    {
                        ShowEditAskDialog(contentDefinition.EditFilePath);
                    }
                    else
                    {
                        ShellHelper.OpenFile(contentDefinition.EditFilePath);
                    }
                    break;
                case ActionIds.SwitchToggled:
                    ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                    bool.TryParse(arguments[0], out bool result);
                    componentHelper.GetConfigHelper().ChangeVariableValue(contentDefinition.FileName, contentDefinition.PackId, contentDefinition.VariableName, result);

                    ShowAnim = false;
                    var item = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
                    Task.Run(() => InitSettingsTiles(item));
                    if (ComponentTasksManager.Instance.IsTaskRunned(ComponentId).Result) _ = ComponentTasksManager.Instance.RestartTask(ComponentId);
                    break;
                case ActionIds.FullButtonElementClicked:
                    ButtonClick(contentDefinition.ClickId);
                    break;

            }
        }

        private async void RevertFileLink(string file, string packId, string fileName, string linkName)
        {
            var result = await FileSystemLinksManager.RemoveLinkForItemId(packId, linkName, fileName);

            Debug.WriteLine(result.Success);

            if (!result.Success)
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(
                    string.Format(localizer.GetLocalizedString("ReplaceSiteListException"), 
                    Path.GetFileName(linkName),
                    Path.GetFileName(fileName),
                    result.Error.ErrorCode),
                    result.Error.FriendlyDescription, 
                    this.XamlRoot
                    );
            }
            else
            {

                ShowAnim = false;
                var item = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
                _ = Task.Run(() => InitSettingsTiles(item));
            }
        }

        private async void ShowChangeListDialog(string file, string packId, string fileName, bool isIPSet)
        {
            ChangeSiteListContentDialog changeListContentDialog = new()
            {
                XamlRoot = this.XamlRoot,
                ListTitle = file,
                PackId = packId,
                FileName = fileName,
                IsIpSet = isIPSet,
            };
            await changeListContentDialog.ShowAsync();

            if (changeListContentDialog.IsDialogFinishedSuccessfully)
            {
                OperationResultModel<EmptyResult> result;
                if (changeListContentDialog.SelectionType == FileSelectionType.FromTheStore)
                {
                    result = await FileSystemLinksManager.CreateHardLinkForItemId(packId, changeListContentDialog.NewFileName, fileName);
                }
                else
                {
                    result = await FileSystemLinksManager.CreateSymbolicLinkForItemId(packId, changeListContentDialog.NewFileName, fileName);
                }

                if (!result.Success)
                {
                    ErrorContentDialog dialog = new();
                    await dialog.ShowErrorDialogAsync(
                        string.Format(localizer.GetLocalizedString("ReplaceSiteListException"), Path.GetFileName(fileName), Path.GetFileName(changeListContentDialog.NewFileName), result.Error.ErrorCode), 
                        result.Error.FriendlyDescription, 
                        this.XamlRoot
                        );
                }
                else
                {
                    ShowAnim = false;
                    var item = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
                    _ = Task.Run(() => InitSettingsTiles(item));
                }

                
            }
        }
        private async void ShowEditAskDialog(string file)
        {
            EditSitelistAskApplicationContentDialog editSitelistAskApplicationContentDialog = new()
            {
                XamlRoot = this.XamlRoot,
                FilePath = file
            };
            await editSitelistAskApplicationContentDialog.ShowAsync();
            if (editSitelistAskApplicationContentDialog.IsSuccess)
                SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
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
                    ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                    var item = (ConfigSelectorItem)ConfigChooseCombobox.SelectedItem;

                    CreateConfigHelperWindow _window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
                    if (componentHelper != null)
                        _window.OpenConfigEditPage(skp: false, configItem: componentHelper.GetConfigHelper().GetConfigItems().FirstOrDefault(x => x.packId == item.PackId && x.file_name == item.FileName));
                    break;
                case "CFGGOODCHECK":
                    CreateConfigUtilWindow gwindow = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
                    gwindow.NavigateToPage<CreateViaGoodCheck>(new NameValueCollection() { { "componentId", ComponentId } });
                    break;
                case "HELPOFFLINE":
                    Commands.CommandsHandler.HandleCommand("cdpiui://Help/");
                    break;
                case "HELPOFFLINECONFIGCHOOISE":
                    Commands.CommandsHandler.HandleCommand(
                        "cdpiui://Help/Autoselection/BestConfigSelection/");
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

            DispatcherQueue.TryEnqueue(() => _configItems.Clear());

            foreach (ConfigItem item in items)
            {
                ConfigSelectorItem configItem = new()
                {
                    FileName = item.file_name,
                    PackId = item.packId,
                    DisplayName = item.name,
                    PackDisplayName = DatabaseHelper.Instance.GetItemById(item.packId)?.ShortName ?? item.packId,
                    IsLegacyConfig = item.IsLegacy
                };

                DispatcherQueue.TryEnqueue(() => _configItems.Add(configItem));
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_configItems.Count == 0)
                {
                    ToggleVisibility(false);
                }
                else
                {
                    ToggleVisibility(true);
                }
            });
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

        private void ConfigItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ApplySavedSelection();
        }

        private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigChooseCombobox.SelectedItem is ConfigSelectorItem sel)
            {
                string oldCfg = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
                string oldId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", sel.FileName);
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", sel.PackId);

                ComponentHelper componentHelper =
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                if (componentHelper != null)
                {
                    try
                    {
                        await Task.Run(() => componentHelper.PrepareSelectedConfig(sel.FileName, sel.PackId));
                    }
                    catch (Exception ex)
                    {
                        CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(
                            nameof(DefaultComponentSettingsPage),
                            $"Cannot prepare selected config '{sel.PackId}/{sel.FileName}': {ex}");
                    }
                }

                if ((oldCfg != sel.FileName || oldId != sel.PackId) && await ComponentTasksManager.Instance.IsTaskRunned(ComponentId)) await ComponentTasksManager.Instance.RestartTask(ComponentId);

                _ = Task.Run(() => InitSettingsTiles(sel));
            }
            ShowAnim = true;

        }

        private void ApplySavedSelection()
        {
            var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
            var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");

            if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
                return;

            var match = _configItems
                .FirstOrDefault(ci => ci.FileName == savedFile
                                   && ci.PackId == savedPackId);
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
            ToggleLoading(false);
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

        private void ToggleLoading(bool isLoading)
        {
            LoadingGrid.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
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
    }
}
