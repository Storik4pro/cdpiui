using CDPI_UI.Controls.CreateConfigHelper;
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

namespace CDPI_UI.Controls.ComponentSettings;

public sealed partial class TgWsDataCenterUserControl : UserControl
{
    #region Commands

    // EDIT

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(
            nameof(EditCommand),
            typeof(ICommand),
            typeof(TgWsDataCenterUserControl),
            new PropertyMetadata(null)
        );

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    // REMOVE

    public static readonly DependencyProperty RemoveCommandProperty =
        DependencyProperty.Register(
            nameof(RemoveCommand),
            typeof(ICommand),
            typeof(TgWsDataCenterUserControl),
            new PropertyMetadata(null)
        );

    public ICommand RemoveCommand
    {
        get => (ICommand)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    #endregion

    public TgWsDataCenterUserControl()
    {
        InitializeComponent();
    }

    public string Number
    {
        get { return (string)GetValue(NumberProperty); }
        set { SetValue(NumberProperty, value); }
    }

    public static readonly DependencyProperty NumberProperty =
        DependencyProperty.Register(
            nameof(Number), typeof(string), typeof(TgWsDataCenterUserControl), new PropertyMetadata(string.Empty)
        );

    public string Ip
    {
        get { return (string)GetValue(IpProperty); }
        set { SetValue(IpProperty, value); }
    }

    public static readonly DependencyProperty IpProperty =
        DependencyProperty.Register(
            nameof(Ip), typeof(string), typeof(TgWsDataCenterUserControl), new PropertyMetadata(string.Empty)
        );

    public string Guid
    {
        get { return (string)GetValue(GuidProperty); }
        set { SetValue(GuidProperty, value); }
    }

    public static readonly DependencyProperty GuidProperty =
        DependencyProperty.Register(
            nameof(Guid), typeof(string), typeof(TgWsDataCenterUserControl), new PropertyMetadata(string.Empty)
        );

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        string parameter = Guid;

        if (RemoveCommand != null && RemoveCommand.CanExecute(parameter))
        {
            RemoveCommand.Execute(parameter);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        string parameter = Guid;

        if (EditCommand != null && EditCommand.CanExecute(parameter))
        {
            EditCommand.Execute(parameter);
        }
    }
}
