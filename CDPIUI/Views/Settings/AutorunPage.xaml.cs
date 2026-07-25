using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Features;
using CDPIUI.Core.Static;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.ViewModels;
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AutorunPage : Page
{
    private ObservableCollection<BreadcrumbBarModel> BreadcrumbBarModels = [];
    private readonly ObservableCollection<ViewComponentModel> ComponentModels = new();

    private ILocalizer localizer = Localizer.Get();

    public AutorunPage()
    {
        InitializeComponent();

        BreadcrumbBar.ItemsSource = BreadcrumbBarModels;
        CreateBreadcrumbBarNavigation();

        ComponentSettingsExpander.ItemsSource = ComponentModels;


        AutorunToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun");
        CheckAutorun(SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun"));

        HideInTrayToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "hideToTrayOnStartup");

        SettingsManager.Instance.PropertyChanged += SettingsManager_PropertyChanged;
        SettingsManager.Instance.EnumPropertyChanged += SettingsManager_EnumPropertyChanged;

        StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
        StoreHelper.Instance.ItemRemoved += Instance_ItemRemoved;

        LoadComponents();
    }

    

    private void LoadComponents()
    {
        ComponentModels.Clear();
        try
        {
            UIHelper.LoadInstalledComponentsList(ComponentModels);

            if (ComponentModels.Count > 0)
            {
                NoComponentInstalledTextBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoComponentInstalledTextBlock.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ConfigTestWindow), $"Can't load components: {ex.Message}");
        }
    }

    private void CheckComponentsAutorunState()
    {
        foreach (var item in ComponentModels)
        {
            item.IsUsedForAutorun = SettingsManager.Instance.GetValue<bool>(["CONFIGS", item.StoreId], "usedForAutorun");
        }

        AutorunWarningInfoBar.IsOpen = !ComponentModels.Any(x => x.IsUsedForAutorun);
    }

    private void StoreHelper_ItemActionsStopped(string obj)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (DatabaseHelper.Instance.GetItemById(obj)?.Type == "component")
                LoadComponents();
        });
    }

    private void Instance_ItemRemoved(string obj)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LoadComponents();
        });
    }


    #region Basic

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
        if (anim != null)
        {
            anim.TryStart(NavGrid);
        }

        var backAnim = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackwardConnectedAnimation");
        if (backAnim != null)
        {
            backAnim.TryStart(NavGrid);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        SettingsManager.Instance.PropertyChanged -= SettingsManager_PropertyChanged;
        StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;
        StoreHelper.Instance.ItemRemoved -= Instance_ItemRemoved;
        SettingsManager.Instance.EnumPropertyChanged -= SettingsManager_EnumPropertyChanged;

        try
        {
            if (SettingsPage.MainSettingsNavigationSupportedPages.Contains(e.SourcePageType))
            {
                var animq = ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate("ForwardConnectedAnimation", NavGrid);

                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
                {
                    animq.Configuration = new BasicConnectedAnimationConfiguration();
                }
            }
        }
        catch { }
    }


    public void CreateBreadcrumbBarNavigation()
    {
        BreadcrumbBarModels.Clear();
        BreadcrumbBarModels.Add(new()
        {
            DisplayName = localizer.GetLocalizedString("Settings"),
            Tag = typeof(SettingsPage)
        });
        BreadcrumbBarModels.Add(new()
        {
            DisplayName = localizer.GetLocalizedString("Autorun"),
            Tag = this.GetType()
        });
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = (BreadcrumbBarModel)args.Item;
        Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    #endregion

    private void SettingsManager_PropertyChanged(string propertyName)
    {
        AutorunToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun");
        CheckAutorun(SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun"));
    }

    private void SettingsManager_EnumPropertyChanged(IEnumerable<string> property)
    {
        if (property.Any() && property.ElementAt(0) == "CONFIGS")
        {
            CheckComponentsAutorunState();
        }
    }

    private void AutorunToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun") != AutorunToggleSwitch.IsOn)
        {
            if (AutorunToggleSwitch.IsOn)
            {
                ApplicationAutorunManager.AddToAutorun();
            }
            else
            {
                ApplicationAutorunManager.RemoveFromAutorun();
            }
        }
    }

    private void CheckAutorun(bool value)
    {
        if (value)
        {
            AutorunWarningInfoBar.IsOpen = true;
            foreach (var id in HardcodedItemIds.ComponentIds.Values)
            {
                if (SettingsManager.Instance.GetValue<bool>(["CONFIGS", id], "usedForAutorun"))
                {
                    AutorunWarningInfoBar.IsOpen = false;
                    break;
                }
            }
        }
        else
        {
            AutorunWarningInfoBar.IsOpen = false;
        }
    }

    private void ComponentAutorunToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch @switch && @switch.Tag is string componentId)
        {
            if (@switch.IsOn != SettingsManager.Instance.GetValue<bool>(["CONFIGS", componentId], "usedForAutorun"))
            {
                SettingsManager.Instance.SetValue<bool>(["CONFIGS", componentId], "usedForAutorun", @switch.IsOn);

            }
        }
    }

    private async void OpenStoreHyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
    }

    private void HideInTrayToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue<bool>("APPEARANCE", "hideToTrayOnStartup", HideInTrayToggleSwitch.IsOn);
    }
}
