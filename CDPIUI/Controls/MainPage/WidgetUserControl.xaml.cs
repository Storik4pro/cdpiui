using CDPIUI.Core;
using CDPIUI.Helper.ViewModels;
using CDPIUI.ViewModels;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.MainPage;

public sealed partial class WidgetUserControl : UserControl
{
    private ObservableCollection<WidgetViewModel> Widgets = [];

    private ILocalizer localizer = Localizer.Get();

    public WidgetUserControl()
    {
        InitializeComponent();

        WidgetsItemsRepeater.ItemsSource = Widgets;

        WidgetToggleButton.IsChecked = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "showWidgetsPanel");

        LoadWidgets();
        CheckWidgetState();
    }

    private void LoadWidgets()
    {
        Widgets.Clear();
        foreach (WidgetViewModel widget in WidgetHelper.GetAllWidgets())
        {
            Widgets.Add(widget);
        }
    }

    private void CheckWidgetState()
    {
        if (WidgetToggleButton.IsChecked == true)
        {
            WidgetStateTextBlock.Text = localizer.GetLocalizedString("HideWidgets");
            FallbackIcon.Glyph = "\uE70E";
            AnimatedIcon.SetState(WidgetAnimatedIcon, "NormalOn");
        }
        else
        {
            WidgetStateTextBlock.Text = localizer.GetLocalizedString("ShowWidgets");
            FallbackIcon.Glyph = "\uE70D";

            AnimatedIcon.SetState(WidgetAnimatedIcon, "NormalOff");
        }
    }

    private void WidgetToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("APPEARANCE", "showWidgetsPanel", WidgetToggleButton.IsChecked);
        CheckWidgetState();
    }

    private void Widget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Guid guid)
        {
            var el = Widgets.FirstOrDefault(x => x.Id == guid);
            if (el != null)
                WidgetHelper.RunActions(el.Type, el.ActionObject, this.XamlRoot);
        }
    }
}
