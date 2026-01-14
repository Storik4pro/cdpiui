using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using CDPI_UI.Views.Components;
using CDPI_UI.Views.CreateConfigUtil;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPI_UI.Helper.Static.UIHelper;
using ToolTip = Microsoft.UI.Xaml.Controls.ToolTip;
using ToolTipService = Microsoft.UI.Xaml.Controls.ToolTipService;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.MainPage;

public enum ComponentState
{
    SetupRequired,
    Stopped,
    Runned,
    ExitedWithException,
}

public class ComboboxItem
{
    public string file_name { get; set; }
    public string packId { get; set; }
    public string name { get; set; }
    public string packName { get; set; }
}

public class ConfigSettingsItem
{
    public string DisplayName { get; set; }
    public string Id { get; set; }
    public bool Value { get; set; }
}
public class AvailableFeaturesItem
{
    public string DisplayName { get; set; }
    public AvailableComponentFeatures Id { get; set; }
    public Uri Image { get; set; }
}
public enum AvailableComponentFeatures
{
    SetupProxy,
    AutoSelectConfig,
    CreateConfig,
    ExploreNewConfigs,
    VisitForum
}

public sealed partial class ComponentTileUserControl : UserControl
{
    private ILocalizer localizer = Localizer.Get();

    public ICommand ViewOutputClickCommand { get; }
    public ICommand ViewSettingsButtonClickCommand { get; }
    public ICommand ToggleProcessButtonClickCommand { get; }

    private ObservableCollection<ComboboxItem> _comboboxItems = new();
    private ObservableCollection<ConfigSettingsItem> ConfigSettingsList = [];
    private ObservableCollection<AvailableFeaturesItem> AvailableFeaturesList = [];

    private Dictionary<AvailableComponentFeatures, string> AvailableComponentFeatureImages = new()
    {
        { AvailableComponentFeatures.SetupProxy, "ms-appx:///Assets/Icons/Proxy.ico" },
        { AvailableComponentFeatures.AutoSelectConfig, "ms-appx:///Assets/Icons/GoodCheck.png" },
        { AvailableComponentFeatures.CreateConfig, "ms-appx:///Assets/Icons/Edit.png" },
        { AvailableComponentFeatures.ExploreNewConfigs, "ms-appx:///Assets/Icons/Store.png" },
        { AvailableComponentFeatures.VisitForum, "ms-appx:///Assets/Icons/OpenInNewWindow.png" },
    };

    private Dictionary<string, List<AvailableComponentFeatures>> AvailableFeaturesForComponent = new()
    {
        { "CSZTBN012", [AvailableComponentFeatures.AutoSelectConfig, AvailableComponentFeatures.ExploreNewConfigs, AvailableComponentFeatures.VisitForum] },
        { "CSGIVS036", [AvailableComponentFeatures.CreateConfig, AvailableComponentFeatures.ExploreNewConfigs, AvailableComponentFeatures.AutoSelectConfig] },
        { "CSBIHA024", [AvailableComponentFeatures.SetupProxy, AvailableComponentFeatures.AutoSelectConfig, AvailableComponentFeatures.CreateConfig] },
        { "CSSIXC048", [AvailableComponentFeatures.SetupProxy, AvailableComponentFeatures.CreateConfig, AvailableComponentFeatures.ExploreNewConfigs] },
        { "CSNIG9025", [AvailableComponentFeatures.SetupProxy, AvailableComponentFeatures.CreateConfig] },
    };


    public ComponentTileUserControl()
    {
        InitializeComponent();

        ViewOutputIconButton.DisplayText = localizer.GetLocalizedString("ViewOutputIconButton");
        SettingsIconButton.DisplayText = localizer.GetLocalizedString("SettingsIconButton");

        ViewOutputClickCommand = new RelayCommand(p => ViewOutputButtonClick());
        ViewSettingsButtonClickCommand = new RelayCommand(p => ViewSettingsButtonClick());
        ToggleProcessButtonClickCommand = new RelayCommand(p => ToggleProcessButtonClick());

        TasksHelper.Instance.TaskStateUpdated += TaskUpdated;

        CheckVisualState();

        this.SizeChanged += (e, a) => CheckVisualState();

        ConfigChooseCombobox.ItemsSource = _comboboxItems;
        _comboboxItems.CollectionChanged += ComboboxItems_CollectionChanged;
        ConfigSettingsListView.ItemsSource = ConfigSettingsList;
        AdditionalFeaturesListView.ItemsSource = AvailableFeaturesList;
    }

    public void CheckVisualState()
    {
        if (ActualWidth < 400)
        {
            VisualStateManager.GoToState(this, "SmallVisualState", true);
        }
        else if (ActualWidth < 824)
        {
            VisualStateManager.GoToState(this, "MediumVisualState", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "BigVisualState", true);
        }
    }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set { 
            SetValue(StoreIdProperty, value); 
            Init();
        }
    }

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(ComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public string ImageUrl
    {
        get { return (string)GetValue(ImageUrlProperty); }
        set { 
            SetValue(ImageUrlProperty, value);
            MainImage.Source = new BitmapImage(new Uri(value));
        }
    }

    public static readonly DependencyProperty ImageUrlProperty =
        DependencyProperty.Register(
            nameof(ImageUrl), typeof(string), typeof(ComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    private void TaskUpdated(Tuple<string, bool> tuple)
    {
        if (tuple.Item1 != StoreId) return;
        

        PreferTaskStateActions();
    }

    private void Init()
    {
        TitleTextBlock.Text = StateHelper.Instance.ComponentIdPairs.GetValueOrDefault(StoreId, StoreId);

        if (DatabaseHelper.Instance.IsItemInstalled(StoreId))
        {
            PreferTaskStateActions();

            TileBackgroungBorder.Background = UIHelper.HexToSolidColorBrushConverter(DatabaseHelper.Instance.GetItemById(StoreId).BackgroudColor);

            bool isAddToAutorun = SettingsManager.Instance.GetValue<bool>(["CONFIGS", StoreId], "usedForAutorun");
            AutorunBadgeGrid.Visibility = isAddToAutorun ? Visibility.Visible : Visibility.Collapsed;
            CheckComponentState();

            LoadConfigItems();

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    StoreId);
            if (componentHelper is null) return;

            InitConfigSettings();

            componentHelper.ConfigListUpdated += LoadConfigItems;

            GetAvailableFeaturesForItem();
        }
    }

    private void InitConfigSettings()
    {
        ConfigSettingsLoadFailureTextBlock.Visibility = Visibility.Visible;
        ConfigSettingsList.Clear();
        var sel = ConfigChooseCombobox.SelectedItem as ComboboxItem;

        if (sel == null)
            return;

        ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    StoreId);
        if (componentHelper is null) return;

        List<VariableItem> variables = componentHelper.GetConfigHelper().GetVariables(sel.file_name, sel.packId);
        List<string> toggleLists = componentHelper.GetConfigHelper().GetToggleLists(sel.file_name, sel.packId);

        if (variables.Count > 0 || toggleLists.Count > 0)
        {
            foreach (var variable in variables)
            {
                ConfigSettingsList.Add(new()
                {
                    DisplayName = $"{componentHelper.GetConfigHelper().GetLocalizedConfigVarName(variable.name, sel.packId)}",
                    Id = variable.name,
                    Value = variable.value
                });
            }
        }
        if (ConfigSettingsList.Count > 0)
        {
            ConfigSettingsLoadFailureTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void GetAvailableFeaturesForItem()
    {
        AvailableFeaturesList.Clear();
        AvailableFeaturesForComponent.TryGetValue(StoreId, out var availableFeatures);
        if (availableFeatures == null) return;

        foreach (var feature in availableFeatures)
        {
            AvailableComponentFeatureImages.TryGetValue(feature, out var imageSource);
            if (imageSource == null) continue;

            AvailableFeaturesList.Add(new()
            {
                DisplayName = localizer.GetLocalizedString(feature.ToString()),
                Id = feature,
                Image = new Uri(imageSource)
            });
        }
    }

    private async Task<ProcessManager> GetProcessManager()
    {
        return (await TasksHelper.Instance.GetTaskFromId(StoreId))?.ProcessManager;
    }

    private async void PreferTaskStateActions()
    {
        bool isRunned = await TasksHelper.Instance.IsTaskRunned(StoreId);

        if (isRunned)
        {
            StateToggleButton.DisplayText = localizer.GetLocalizedString("Stop");
            ShowComponentState(ComponentState.Runned);
        }
        else
        {
            StateToggleButton.DisplayText = localizer.GetLocalizedString("Start");
            CheckComponentState();
        }

        StateToggleButton.Checked = isRunned;
    }

    private async void ToggleProcessButtonClick()
    {
        bool isRunned = await TasksHelper.Instance.IsTaskRunned(StoreId);

        if (!isRunned)
        {
            TasksHelper.Instance.CreateAndRunNewTask(StoreId);
        }
        else
        {
            await TasksHelper.Instance.StopTask(StoreId);
        }
    }

    private async void CheckComponentState()
    {
        ShowComponentState(await GetComponentState());
    }

    private async Task<ComponentState> GetComponentState()
    {
        ProcessManager processManager = await GetProcessManager();
        if (processManager == null) return ComponentState.SetupRequired;

        if (processManager.isErrorHappens)
        {
            return ComponentState.ExitedWithException;
        }
        else if (processManager.processState)
        {
            return ComponentState.Runned;
        }
        else
        {
            return ComponentState.Stopped;
        }
    }

    private void ShowComponentState(ComponentState componentState)
    {
        switch (componentState)
        {
            case ComponentState.SetupRequired:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorSolidNeutralBrush"];
                ForegroundStatusFontIcon.Glyph = "\uE713";
                ForegroundStatusFontIcon.FontSize = 22;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusSetupRequired"));
                break;
            case ComponentState.Stopped:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                ForegroundStatusFontIcon.Glyph = "\uE7E8";
                ForegroundStatusFontIcon.FontSize = 20;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusStopped"));
                break;
            case ComponentState.Runned:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                ForegroundStatusFontIcon.Glyph = "\uF13E";
                ForegroundStatusFontIcon.FontSize = 28;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusStarted"));
                break;
            case ComponentState.ExitedWithException:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];
                ForegroundStatusFontIcon.Glyph = "\uE7BA";
                ForegroundStatusFontIcon.FontSize = 18;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusExceptionHappens"));
                break;
        }
    }

    private void SetStatusToolTip(string text)
    {
        ToolTip toolTip = new();
        toolTip.Content = text;
        ToolTipService.SetToolTip(StatusGrid, toolTip);
    }

    private async void ViewOutputButtonClick()
    {
        var window = await ((App)Application.Current).UnsafeCreateNewWindow<ViewWindow>(id: StoreId);
        window?.SetId(StoreId);
    }

    private async void ViewSettingsButtonClick()
    {
        var window = await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>();
        window.NavView_Navigate(typeof(ViewComponentSettingsPage), StoreId, new DrillInNavigationTransitionInfo());
    }

    private void LoadConfigItems()
    {
        ComponentItemsLoaderHelper.Instance.Init(forse:false);

        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                StoreId);

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

    private void ToggleVisibility(bool visible)
    {
        if (!visible)
        {
            ContentStackPanel.Visibility = Visibility.Collapsed;
            ErrorStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ContentStackPanel.Visibility = Visibility.Visible;
            ErrorStackPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConfigChooseCombobox.SelectedItem is ComboboxItem sel)
        {
            string oldCfg = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configFile");
            string oldId = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configId");
            SettingsManager.Instance.SetValue<string>(["CONFIGS", StoreId], "configFile", sel.file_name);
            SettingsManager.Instance.SetValue<string>(["CONFIGS", StoreId], "configId", sel.packId);

            InitConfigSettings();

            if ((oldCfg != sel.file_name || oldId != sel.packId) && await TasksHelper.Instance.IsTaskRunned(StoreId)) await TasksHelper.Instance.RestartTask(StoreId);
        }
    }

    private void ApplySavedSelection()
    {
        var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configFile");
        var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configId");

        if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
            return;

        var match = _comboboxItems
            .FirstOrDefault(ci => ci.file_name == savedFile
                               && ci.packId == savedPackId);
        if (match != null)
            ConfigChooseCombobox.SelectedItem = match;
    }

    private void ComboboxItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ApplySavedSelection();
    }

    private async void ConfigSettingToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        bool value = (sender as ToggleSwitch).IsOn;
        string id = (string)(sender as ToggleSwitch).Tag;

        var item = ConfigSettingsList.FirstOrDefault((x) => x.Id == id);
        if (item == null) return;

        var sel = ConfigChooseCombobox.SelectedItem as ComboboxItem;
        if (sel == null) return;

        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(StoreId);
        componentHelper.GetConfigHelper().ChangeVariableValue(sel.file_name, sel.packId, id, value);

        if (await TasksHelper.Instance.IsTaskRunned(StoreId)) _ = TasksHelper.Instance.RestartTask(StoreId);
    }

    private async void AvailableFeaturesButton_Click(object sender, RoutedEventArgs e)
    {
        var action = (AvailableComponentFeatures)((Button)sender).Tag;
        switch (action)
        {
            case AvailableComponentFeatures.SetupProxy:
                _ = ((App)Application.Current).SafeCreateNewWindow<ProxySetupUtilWindow>();
                break;
            case AvailableComponentFeatures.AutoSelectConfig:
                CreateConfigUtilWindow gwindow = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
                gwindow.NavigateToPage<CreateViaGoodCheck>(StoreId);
                break;
            case AvailableComponentFeatures.CreateConfig:
                CreateConfigHelperWindow _window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
                _window.CreateNewConfigForComponentId(StoreId);
                break;
            case AvailableComponentFeatures.ExploreNewConfigs:
                StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
                window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C002CS", new SuppressNavigationTransitionInfo());
                break;
            case AvailableComponentFeatures.VisitForum:
                UrlOpenHelper.LaunchComponentForumUrl(StoreId);
                break;
        }
    }
}
