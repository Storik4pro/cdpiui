using CDPIUI.Controls.Dialogs.Q;
using CDPIUI.Core;
using CDPIUI.Core.Static;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.LScript;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.StartScreen;
using WinRT.Interop;
using WinUI3Localizer;
using CDPIUI.Controls.Default;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public class ComponentTileModel
{
    public string Id;
    public string ImageSource;
    public double Width;
    public double Height;
}

public sealed partial class ModernMainPage : TemplatePage, INotifyPropertyChanged
{
    public ObservableCollection<ComponentTileModel> TileModels = [];

    private double ElementWidthProperty = 210;
    public double ElementWidth {
        get
        {
            return ElementWidthProperty;
        }
        set
        {
            SetField(ref ElementWidthProperty, value);
        }
    }
    public double ElementHeight { get; set; } = 250;
    public double Spacing { get; set; } = 0;

    private ILocalizer localizer = Localizer.Get();
    public ModernMainPage()
    {
        InitializeComponent();
        this.DataContext = this;

        StoreHelper.Instance.ItemActionsStopped += Instance_ItemActionsStopped;
        StoreHelper.Instance.ItemRemoved += Instance_ItemRemoved;

        this.Loaded += ModernMainPage_Loaded;

        DateTime today = DateTime.Today;
        Random rnd = new Random();
        if ((today.Month == 4 && today.Day == 1) || rnd.Next(1, 10000) == 180)
        {
            QHyperlinkButton.Visibility = Visibility.Visible;
        }
        else
        {
            QHyperlinkButton.Visibility = Visibility.Collapsed;
        }
    }

    private void Instance_ItemRemoved(string obj)
    {
        CreateTiles();
    }

    private void Instance_ItemActionsStopped(string obj)
    {
        CreateTiles();
    }

    private void CreateTiles()
    {
        TileModels.Clear();
        List<DatabaseStoreItem> installedComponents = DatabaseHelper.Instance.GetItemsByType("component");
        CalcWidth(SettingsManager.Instance.GetValue<int>("APPEARANCE", "mainGridColumnsCount"), installedComponents.Count);
        foreach (DatabaseStoreItem installedComponent in installedComponents)
        {
            TileModels.Add(new ComponentTileModel()
            {
                Id = installedComponent.Id,
                ImageSource = LScriptLangHelper.ExecuteScript(installedComponent.IconPath),
                Width = ElementWidth,
                Height = ElementHeight,
            });
        }
        AuditMarkup();
    }

    private void ModernMainPage_Loaded(object sender, RoutedEventArgs e)
    {
        MainGridView.ItemsSource = TileModels;
        CreateTiles();
        
    }

    private int MaxColumns()
    {
        double pageWidth = ContentGrid.ActualWidth;

        return (int)(pageWidth / 210);
    }

    private void CalcWidth(int columns, int elCount)
    {
        double pageWidth = ContentGrid.ActualWidth;
        
        if (columns < 0)
        {
            if (elCount == 1)
            {
                columns = 1;
            }
            else if (elCount == 2)
            {
                columns = 2;
            }
            else
            {
                columns = 4;
            }
        }
        columns = Math.Min(columns, MaxColumns());
        ElementWidth = (pageWidth + 10) / columns;
        Spacing = columns != 1 ? 10 : 0;
    }

    private void AuditMarkup()
    {
        if (TileModels.Count == 0)
        {
            ComponentTilePlaceholder.Visibility = Visibility.Visible;
            ComponentTilesScrollContainer.Visibility = Visibility.Collapsed;
        }
        else
        {
            ComponentTilePlaceholder.Visibility = Visibility.Collapsed;
            ComponentTilesScrollContainer.Visibility = Visibility.Visible;
        }
    }

    

    private async void QHyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        QContentDialog q = new()
        {
            XamlRoot = this.XamlRoot,
        };
        await q.ShowAsync();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        List<DatabaseStoreItem> installedComponents = DatabaseHelper.Instance.GetItemsByType("component");
        CalcWidth(SettingsManager.Instance.GetValue<int>("APPEARANCE", "mainGridColumnsCount"), installedComponents.Count);
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
