using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Views.CreateConfigUtil;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Popups;
using WinUI3Localizer;
using static CDPI_UI.Helper.Static.UIHelper;

namespace CDPI_UI.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly ObservableCollection<UIElement> _tiles = new();
        private ILocalizer localizer = Localizer.Get();

        public MainPage()
        {
            this.InitializeComponent();
            // ProcessManager.Instance.onProcessStateChanged += OnProcessStateChanged;
            // ProcessManager.Instance.ErrorHappens += OnErrorHappens;
            // ProcessToggleSwitch.IsOn = ProcessManager.Instance.processState;

            ProcessToggleSwitch.Toggled += ToggleSwitch_Toggled;

            StaggeredRepeater.ItemsSource = _tiles;

            InitSettingsTiles();
            GetReadyComponentInfo();

            SettingsManager.Instance.PropertyChanged += SettingsManager_PropertyChanged;
            SettingsManager.Instance.EnumPropertyChanged += SettingsManager_EnumPropertyChanged;

            StoreHelper.Instance.ItemRemoved += StoreHelper_ItemRemoved;
            StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
        }

        private void StoreHelper_ItemRemoved(string obj)
        {
            GetReadyComponentInfo();
            InitSettingsTiles();
        }

        private void StoreHelper_ItemActionsStopped(string obj)
        {
            GetReadyComponentInfo();
            InitSettingsTiles();
        }

        private void SettingsManager_EnumPropertyChanged(IEnumerable<string> enumerable)
        {
            foreach (var group in enumerable)
            {
                if (group == "CONFIGS")
                {
                    GetReadyComponentInfo();
                    return;
                }
            }
        }
        private void SettingsManager_PropertyChanged(string group)
        {
            if (group == "COMPONENTS") GetReadyComponentInfo();
        }

        private void GetReadyComponentInfo()
        {
            ComponentControlSettingCard.Description = string.Empty;
            ComponentControlSettingCard.Header = string.Empty;

            string nowUsedComponentId = SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed");
            CheckRunAvailability(nowUsedComponentId);

            DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(nowUsedComponentId);
            if (string.IsNullOrEmpty(nowUsedComponentId) || databaseStoreItem is null)
            {
                ComponentControlSettingCard.Header = localizer.GetLocalizedString("/SettingTiles/NowUsedComponentError");
                return;
            }

            ComponentControlSettingCard.Header = $"{StateHelper.Instance.ComponentIdPairs[nowUsedComponentId].Normalize()} {databaseStoreItem.CurrentVersion}";

            if (!string.IsNullOrEmpty(GetConfigName(nowUsedComponentId)))
                ComponentControlSettingCard.Description = string.Format(localizer.GetLocalizedString("/SettingTiles/NowUsedComponentDescription"), GetConfigName(nowUsedComponentId));
            else
                ComponentControlSettingCard.Description = localizer.GetLocalizedString("/SettingTiles/NowUsedComponentDescriptionError");
        }

        private static string GetConfigName(string componentId)
        {
            var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", componentId], "configFile");
            var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", componentId], "configId");

            if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
                return string.Empty;

            var componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId);
            List<ConfigItem> items = componentHelper.GetConfigHelper().GetConfigItems();

            return items.FirstOrDefault(x => x.packId == savedPackId && x.file_name == savedFile)?.name ?? string.Empty;
        }

        private bool IsRunAvailable(string componentId)
        {
            try
            {
                if (string.IsNullOrEmpty(componentId) || !DatabaseHelper.Instance.IsItemInstalled(componentId))
                    return false;

                if (string.IsNullOrEmpty(ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId).GetStartupParams()))
                    return false;

                return true;
            }
            catch { }
            return false;
        }

        private void CheckRunAvailability(string componentId)
        {
            if (IsRunAvailable(componentId))
            {
                ComponentControlSettingCard.IsEnabled = true;
            }
            else
            {
                ComponentControlSettingCard.IsEnabled = false;
            }
        }

        private void OnErrorHappens(string message, string _object)
        {
            ProcessToggleSwitch.IsOn = false;
        }

        private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;

            if (toggleSwitch.IsOn)
            {
                // await ProcessManager.Instance.StartProcess();
            }
            else
            {
                // await ProcessManager.Instance.StopProcess();
            }
        }

        private void OnProcessStateChanged(string state)
        {
            ProcessToggleSwitch.Toggled -= ToggleSwitch_Toggled;
            if (state == "started")
            {
                ProcessToggleSwitch.IsOn = true;
            }
            else
            {
                ProcessToggleSwitch.IsOn = false;
            }
            ProcessToggleSwitch.Toggled += ToggleSwitch_Toggled;
        }

        private static Thickness _Padding = new(20, 10, 20, 10);

        private List<ComboBoxModel> GetComponents()
        {
            List<ComboBoxModel> components = new();
            foreach (var component in StateHelper.Instance.ComponentIdPairs)
            {
                if (component.Key == "ASGKOI001")
                    continue;
                if (!DatabaseHelper.Instance.IsItemInstalled(component.Key))
                    continue;
                components.Add(new()
                {
                    Id = component.Key,
                    DisplayName = component.Value
                });
            }
            return components;
        }

        private void InitSettingsTiles()
        {
            
            _tiles.Clear();

            SettingsTile featuresTile = new()
            {
                IconGlyph = "\uF133",
                Title = localizer.GetLocalizedString("/SettingTiles/Features"),
            };

            SettingsTileItem featuresTileItem = new()
            {
                Title = localizer.GetLocalizedString("PseudoconsoleSettingsCardHeader"),
                ShowTopRectangle = false,
            };
            featuresTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.FullButton,
                ClickId = "PSEUDOCONSOLEOPEN"
            });

            featuresTile.Items.Add(featuresTileItem);

            _tiles.Add(CreateSettingTile(featuresTile, HandleSettingTileElementClick, TileType.Basic, padding: _Padding));

            SettingsTile componentTile = new()
            {
                IconGlyph = "\uE9F5",
                Title = localizer.GetLocalizedString("/SettingTiles/QuickSettingsTile"),
            };

            SettingsTileItem componentTileItem = new()
            {
                Title = localizer.GetLocalizedString("/SettingTiles/NowUsedComponent"),
                ShowTopRectangle = false,
            };
            componentTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.ComboBoxSelector,
                ComboBoxItems = GetComponents(),
                SelectedComboBoxItemId = SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed")
            });

            componentTile.Items.Add(componentTileItem);

            _tiles.Add(CreateSettingTile(componentTile, HandleSettingTileElementClick, TileType.Basic, padding: _Padding));

            SettingsTile flashlightTile = new()
            {
                IconGlyph = "\uE754",
                Title = localizer.GetLocalizedString("/Flashlight/Title"),
            };

            SettingsTileItem flashlightTileItem = new()
            {
                Title = localizer.GetLocalizedString("/Flashlight/Welcome"),
                ShowTopRectangle = false,
            };
            flashlightTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.OnlyTextContent,
            });

            flashlightTile.Items.Add(flashlightTileItem);

            _tiles.Add(CreateSettingTile(flashlightTile, HandleSettingTileElementClick, TileType.Basic, padding: _Padding));
        }

        private void HandleSettingTileElementClick(ActionIds actionId, List<string> arguments, SettingTileContentDefinition contentDefinition)
        {
            switch (actionId)
            {
                
                case ActionIds.SwitchToggled:
                    
                    break;
                case ActionIds.FullButtonElementClicked:
                    ButtonClick(contentDefinition.ClickId);
                    break;
                case ActionIds.ComboBoxSelectionChanged:
                    SettingsManager.Instance.SetValue("COMPONENTS", "nowUsed", arguments[0]);
                    break;

            }
        }

        private async void ButtonClick(string targetId)
        {
            switch (targetId)
            {
                case "PSEUDOCONSOLEOPEN":
                    await ((App)Application.Current).SafeCreateNewWindow<ViewWindow>();
                    break;
            }
        }

    }
}
