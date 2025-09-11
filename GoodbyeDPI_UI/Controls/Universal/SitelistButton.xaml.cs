using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI;

public sealed partial class SitelistButton : UserControl
{
    public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.Register(
                nameof(ClickCommand),
                typeof(ICommand),
                typeof(SitelistButton),
                new PropertyMetadata(null)
            );

    public ICommand ClickCommand
    {
        get => (ICommand)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public static readonly DependencyProperty ClickCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ClickCommandParameter),
            typeof(object),
            typeof(SitelistButton),
            new PropertyMetadata(null)
        );

    public object ClickCommandParameter
    {
        get => GetValue(ClickCommandParameterProperty);
        set => SetValue(ClickCommandParameterProperty, value);
    }

    public Action Click;

    public SitelistButton()
    {
        InitializeComponent();
    }

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(SitelistButton), new PropertyMetadata(string.Empty)
        );

    public string PackName
    {
        get { return (string)GetValue(PackNameProperty); }
        set { SetValue(PackNameProperty, value); }
    }

    public static readonly DependencyProperty PackNameProperty =
        DependencyProperty.Register(
            nameof(PackName), typeof(string), typeof(SitelistButton), new PropertyMetadata(string.Empty)
        );
    public string Directory
    {
        get { return (string)GetValue(DirectoryProperty); }
        set { SetValue(DirectoryProperty, value); }
    }

    public static readonly DependencyProperty DirectoryProperty =
        DependencyProperty.Register(
            nameof(Directory), typeof(string), typeof(SitelistButton), new PropertyMetadata(string.Empty)
        );

    public string PackId
    {
        get { return (string)GetValue(PackIdProperty); }
        set { SetValue(PackIdProperty, value); }
    }

    public static readonly DependencyProperty PackIdProperty =
        DependencyProperty.Register(
            nameof(PackId), typeof(string), typeof(SitelistButton), new PropertyMetadata(string.Empty)
        );

    public bool IsLogoVisible
    {
        get { return (bool)GetValue(IsLogoVisibleProperty); }
        set { 
            SetValue(IsLogoVisibleProperty, value); 
            LogoFontIcon.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public static readonly DependencyProperty IsLogoVisibleProperty =
        DependencyProperty.Register(
            nameof(IsLogoVisible), typeof(bool), typeof(SitelistButton), new PropertyMetadata(true)
        );

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Click?.Invoke();
        ClickCommandParameter = Tuple.Create(Directory, PackId);
        if (ClickCommand != null && ClickCommand.CanExecute(ClickCommandParameter))
        {
            ClickCommand.Execute(ClickCommandParameter);
            return;
        }
    }
}
