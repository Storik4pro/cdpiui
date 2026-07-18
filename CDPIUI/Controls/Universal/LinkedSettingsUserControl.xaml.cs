using CDPI_UI.ViewModels;
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
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Universal;

public sealed partial class LinkedSettingsUserControl : UserControl
{
    public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.Register(
                nameof(ClickCommand),
                typeof(ICommand),
                typeof(LinkedSettingsUserControl),
                new PropertyMetadata(null)
            );

    public ICommand ClickCommand
    {
        get => (ICommand)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public LinkedSettingsUserControl()
    {
        InitializeComponent();
    }

    public ObservableCollection<SettingLinkModel> SettingModelsList
    {
        get { return (ObservableCollection<SettingLinkModel>)GetValue(SettingModelsListProperty); }
        set { SetValue(SettingModelsListProperty, value); }
    }

    public static readonly DependencyProperty SettingModelsListProperty =
        DependencyProperty.Register(
            nameof(SettingModelsList), typeof(ObservableCollection<SettingLinkModel>), typeof(LinkedSettingsUserControl), new PropertyMetadata(new ObservableCollection<SettingLinkModel>())
        );

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        object tag = ((HyperlinkButton)sender).Tag;

        if (ClickCommand != null && ClickCommand.CanExecute(tag))
        {
            ClickCommand.Execute(tag);
            return;
        }
        
    }
}
