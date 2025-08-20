using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Items;
using GoodbyeDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public enum SiteListContentType
    {
        OnlyViewButton,
        EditViewButtons,
        ToggleSwitch,
        SiteListToggle,
        FullButton,
    }
    public class SiteListContentDefinition
    {
        public SiteListContentType ContentType { get; set; }

        public string ActionGlyph { get; set; }

        public string VariableName { get; set; }
        public bool InitialToggleState { get; set; }
        public string PackId { get; set; }
        public string FileName { get; set; }

        public string EditFilePath { get; set; }
        public List<string> ViewParams { get; set; }
        public List<string> PrettyViewParams { get; set; }
    }

    public class SettingsTileItem
    {
        public string Title { get; set; }
        public bool ShowTopRectangle { get; set; }
        public IList<SiteListContentDefinition> Contents { get; } = new List<SiteListContentDefinition>();
    }

    public class SettingsTile : INotifyPropertyChanged
    {
        public string IconGlyph { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public ObservableCollection<SettingsTileItem> Items { get; } = new ObservableCollection<SettingsTileItem>();

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed partial class ZapretSettingsPage : Page
    {
        private class ComboboxItem
        {
            public string file_name { get; set; }
            public string packId { get; set; }
            public string name { get; set; }
        }

        private string ComponentId = "CSZTBN012";
        private ObservableCollection<ComboboxItem> _comboboxItems = new();

        private readonly ObservableCollection<UIElement> _tiles = new();

        private bool ShowAnim = true;

        public ZapretSettingsPage()
        {
            InitializeComponent();

            ConfigChooseCombobox.ItemsSource = _comboboxItems;
            ConfigChooseCombobox.DisplayMemberPath = nameof(ConfigItem.name);

            _comboboxItems.CollectionChanged += ComboboxItems_CollectionChanged;

            StaggeredRepeater.ItemsSource = _tiles;

            DataContext = this;

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

            SettingsTile variablesItem = new()
            {
                IconGlyph = "\uE713",
                Title = "Доступные настройки",
                Description = "Настройте пресет под себя"
            };

            _flag = false;
            foreach (var variable in variables) 
            {
                SettingsTileItem settingsTileItem = new()
                {
                    Title = $"{componentHelper.GetConfigHelper().GetLocalizedConfigVarName(variable.name, sel.packId)}",
                    ShowTopRectangle = _flag,
                };

                settingsTileItem.Contents.Add(new SiteListContentDefinition
                {
                    ContentType = SiteListContentType.ToggleSwitch,
                    VariableName = variable.name,
                    InitialToggleState = variable.value,
                    PackId = sel.packId,
                    FileName = sel.file_name,
                });

                variablesItem.Items.Add(settingsTileItem);

                _flag = true;
            }

            _tiles.Add(CreateSettingTile(variablesItem));

            SettingsTile sitelistTile = new()
            {
                IconGlyph = "\uE7C3",
                Title = "Используемые списки сайтов",
                Description = "Списки сайтов, которые используются в пресете"
            };

            List<SiteListItem> list = componentHelper.GetConfigHelper().GetSiteListItems(sel.file_name, sel.packId);

            _flag = false;
            foreach (SiteListItem item in list)
            {
                if (item.Type == "NULL")
                    continue;

                string title = 
                    (item.Type == "blacklist" ? "Список сайтов" : item.Type == "iplist"? "Список IP-адресов" : "Автоматический список сайтов") +
                    $" {item.Name}";

                SettingsTileItem settingsTileItem = new()
                {
                    Title = title,
                    ShowTopRectangle = _flag,
                };
                settingsTileItem.Contents.Add(new SiteListContentDefinition
                {
                    ContentType = item.Type == "autoblacklist" ? SiteListContentType.OnlyViewButton : SiteListContentType.EditViewButtons,
                    EditFilePath = item.FilePath,
                    ViewParams = item.ApplyParams,
                    PrettyViewParams = item.PrettyApplyParams,
                });

                sitelistTile.Items.Add(settingsTileItem);

                _flag = true;
            }

            _tiles.Add(CreateSettingTile(sitelistTile));

            SettingsTile advancedTile = new()
            {
                IconGlyph = "\uEC7A",
                Title = "Продвинутые настройки",
                Description = "Создавайте свои собственные пресеты вручную, подбирайте параметры для каждого сайта автоматически или отредактируйте существующий"
            };

            SettingsTileItem createNewTileItem = new()
            {
                Title = "Создать новый набор настроек",
                ShowTopRectangle = false,
            };
            createNewTileItem.Contents.Add(new SiteListContentDefinition
            {
                ContentType = SiteListContentType.FullButton,
            });

            advancedTile.Items.Add(createNewTileItem);

            SettingsTileItem editTileItem = new()
            {
                Title = "Изменить существующий набор настроек",
                ShowTopRectangle = true,
            };
            editTileItem.Contents.Add(new SiteListContentDefinition
            {
                ContentType = SiteListContentType.FullButton,
            });

            advancedTile.Items.Add(editTileItem);

            SettingsTileItem autoTileItem = new()
            {
                Title = "Подобрать параметры запуска автоматически",
                ShowTopRectangle = true,
            };
            autoTileItem.Contents.Add(new SiteListContentDefinition
            {
                ContentType = SiteListContentType.FullButton,
            });

            advancedTile.Items.Add(autoTileItem);

            _tiles.Add(CreateSettingTile(advancedTile));

            SettingsTile helpTile = new()
            {
                IconGlyph = "\uE754",
                Title = "Советы",
                Description = "Что-то не работает или появились вопросы? Возможно, эти разделы справки могут вам помочь"
            };

            SettingsTileItem helpTileItem = new()
            {
                Title = "Открыть оффлайн-справку",
                ShowTopRectangle = false,
            };
            helpTileItem.Contents.Add(new SiteListContentDefinition
            {
                ContentType = SiteListContentType.FullButton,
            });

            helpTile.Items.Add(helpTileItem);

            _tiles.Add(CreateSettingTile(helpTile));

        }

        private UIElement CreateSettingTile(SettingsTile preset)
        {
            var tile = new SettingTile
            {
                IconGlyph = preset.IconGlyph,
                Title = preset.Title,
                Description = preset.Description
            };

            var rootStack = new StackPanel();

            foreach (var list in preset.Items)
            {
                var element = new SettingTileControlElement
                {
                    Title = list.Title,
                    ShowTopRectangle = list.ShowTopRectangle,

                };

                var contentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                foreach (var def in list.Contents)
                {
                    switch (def.ContentType)
                    {
                        case SiteListContentType.OnlyViewButton:
                            var _viewBtn = new Button { Padding = new Thickness(6) };
                            _viewBtn.Content = new FontIcon { Glyph = "\uE890", FontSize = 16 };
                            _viewBtn.Click += (s, e) =>
                            {
                                Controls.Dialogs.ViewApplyArgsContentDialog dialog = new()
                                {
                                    DialogTitle = $"{list.Title}",
                                    Args = def.PrettyViewParams,
                                    XamlRoot = this.XamlRoot,
                                };
                                _ = dialog.ShowAsync();
                            };

                            contentPanel.Children.Add(_viewBtn);
                            break;

                        case SiteListContentType.EditViewButtons:
                            var editBtn = new Button { Padding = new Thickness(6) };
                            editBtn.Content = new FontIcon { Glyph = "\uE70F", FontSize = 16 };
                            editBtn.Click += (s, e) =>
                            {
                                Utils.OpenFileInDefaultApp(def.EditFilePath);
                            };

                            var viewBtn = new Button { Padding = new Thickness(6) };
                            viewBtn.Content = new FontIcon { Glyph = "\uE890", FontSize = 16 };
                            viewBtn.Click += (s, e) =>
                            {
                                Controls.Dialogs.ViewApplyArgsContentDialog dialog = new()
                                {
                                    DialogTitle = $"{list.Title}",
                                    Args = def.PrettyViewParams,
                                    XamlRoot = this.XamlRoot,
                                };
                                _ = dialog.ShowAsync();
                            };

                            contentPanel.Children.Add(editBtn);
                            contentPanel.Children.Add(viewBtn);
                            break;

                        case SiteListContentType.ToggleSwitch:
                            var toggle = new ToggleSwitch
                            {
                                IsOn = def.InitialToggleState
                            };
                            toggle.Toggled += (s, e) =>
                            {
                                ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                                componentHelper.GetConfigHelper().ChangeVariableValue(def.FileName, def.PackId, def.VariableName, toggle.IsOn);

                                ShowAnim = false;
                                InitSettingsTiles();
                            };
                            contentPanel.Children.Add(toggle);
                            break;
                        case SiteListContentType.FullButton:
                            element.ActionIconGlyph = "\uE8A7";
                            element.IsClickEnabled = true;

                            element.Click += () =>
                            {

                            };
                            break;
                    }
                }

                element.InnerContent = contentPanel;
                rootStack.Children.Add(element);
            }

            tile.InnerContent = rootStack;
            return tile;
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

                _comboboxItems.Add(comboboxItem);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadConfigItems();
        }

        private void ComboboxItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ApplySavedSelection();
        }

        private void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigChooseCombobox.SelectedItem is ComboboxItem sel)
            {
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", sel.file_name);
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", sel.packId);
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

                    if(ShowAnim)
                        visual.StartAnimation("Translation", translationAnim);
                    

                    var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
                    fadeAnim.Target = "Opacity";
                    if (ShowAnim)
                        fadeAnim.InsertKeyFrame(0f, 0f);
                    else
                        fadeAnim.InsertKeyFrame(0.5f, 0.5f);
                    fadeAnim.InsertKeyFrame(1f, 1f);
                    fadeAnim.Duration = TimeSpan.FromMilliseconds(ShowAnim?300:150);

                    visual.StartAnimation("Opacity", fadeAnim);


                });
        }

    }
}
