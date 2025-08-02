using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Static;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Application = Microsoft.UI.Xaml.Application;
using DependencyObject = Microsoft.UI.Xaml.DependencyObject;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;
using ItemsControl = Microsoft.UI.Xaml.Controls.ItemsControl;
using ItemsPanelTemplate = Microsoft.UI.Xaml.Controls.ItemsPanelTemplate;
using SizeChangedEventArgs = Microsoft.UI.Xaml.SizeChangedEventArgs;
using Thickness = Microsoft.UI.Xaml.Thickness;
using UIElement = Microsoft.UI.Xaml.UIElement;
using Visibility = Microsoft.UI.Xaml.Visibility;
using Window = Microsoft.UI.Xaml.Window;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class HomePage : Page
{
    private double _largeElementWidth;
    public double LargeElementWidth
    {
        get => _largeElementWidth;
        set
        {
            if (_largeElementWidth != value)
            {
                _largeElementWidth = value;
                OnPropertyChanged();
            }
        }
    }

    private double _smallElementWidth;
    public double SmallElementWidth
    {
        get => _smallElementWidth;
        set
        {
            if (_smallElementWidth != value)
            {
                _smallElementWidth = value;
                OnPropertyChanged();
            }
        }
    }

    private double _currentPageWidth;
    public double CurrentPageWidth
    {
        get => _currentPageWidth;
        set
        {
            if (_currentPageWidth != value)
            {
                _currentPageWidth = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private int availableColumns = 6;


    private readonly List<(ItemsControl Panel, UICategoryData Data)> _panels
        = new List<(ItemsControl, UICategoryData)>();

    private double LargeMinW, LargePrefW;

    private const int DefaultLargeColumns = 8;
    private const double MaxTotalWidth = 1860;

    private IEnumerable<UICategoryData> _lastCategories;

    public HomePage()
    {
        InitializeComponent();

        // SetupLayout();

        Helper.StoreHelper.Instance.StoreInternalErrorHappens += StoreHelper_ErrorHappens;
        Helper.StoreHelper.Instance.UpdatingDatabaseStarted += StoreHelper_UpdatingDatabaseStarted;
        UpdateStoreDatabase();

        StoreScrollViewer.SizeChanged += OnContainerSizeChanged;

        this.Loaded += HomePage_Loaded;

        LargeElementWidth = 100;
        this.DataContext = this;
        this.LayoutUpdated += HomePage_LayoutUpdated;

    }

    private bool _isFirstLayout = true;

    private void HomePage_LayoutUpdated(object sender, object e)
    {
        if (_isFirstLayout)
        {
            _isFirstLayout = !SetupElementsWidth();
        } 
        else
        {
            this.LayoutUpdated -= HomePage_LayoutUpdated;
        }
    }

    private void HomePage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        
        
    }

    private List<UICategoryData> CreateCategoriesList(List<Helper.StoreHelper.RepoCategory> values)
    {
        List<UICategoryData> categoryDatas = new List<UICategoryData>();

        foreach (Helper.StoreHelper.RepoCategory category in values)
        {
            ObservableCollection<UIElement> categoryItems = new ObservableCollection<UIElement>();
            foreach (Helper.StoreHelper.RepoCategoryItem repoCategoryItem in category.items)
            {
                if (category.type == "basic_category")
                {
                    categoryItems.Add(
                        CreateLargeButton(
                            storeId:repoCategoryItem.store_id,
                            imageSource:Helper.StoreHelper.Instance.ExecuteScript(repoCategoryItem.icon),
                            price:Helper.DatabaseHelper.Instance.IsItemInstalled(repoCategoryItem.store_id) ? "Установлено" : "Получить",
                            title:Helper.StoreHelper.Instance.GetLocalizedStoreItemName(repoCategoryItem.name, "RU"),
                            backgroundColor:repoCategoryItem.background
                        )
                    );
                }
                else if (category.type == "second_category")
                {
                    categoryItems.Add(
                        CreateSmallButton(
                            storeId:repoCategoryItem.store_id,
                            imageSource: Helper.StoreHelper.Instance.ExecuteScript(repoCategoryItem.icon),
                            price: Helper.DatabaseHelper.Instance.IsItemInstalled(repoCategoryItem.store_id) ? "Установлено" : "Получить",
                            title: Helper.StoreHelper.Instance.GetLocalizedStoreItemName(repoCategoryItem.name, "RU"),
                            developer: repoCategoryItem.developer,
                            backgroundColor:repoCategoryItem.background
                        )
                    );
                }
            }

            UICategoryData categoryData = new UICategoryData
            {
                StoreId = category.store_id,
                Type = category.type,
                Name = Helper.StoreHelper.Instance.GetLocalizedStoreItemName(category.name, "RU"),
                Items = categoryItems,
            };

            categoryDatas.Add(categoryData);
        }

        return categoryDatas;
    }

    private StoreItemLargeButton CreateLargeButton(string storeId, string imageSource, string price, string title, string backgroundColor)
    {
        StoreItemLargeButton storeItemLargeButton;

        string eImageSource = StoreHelper.Instance.ExecuteScript(imageSource);
        BitmapImage image = new BitmapImage(new Uri(eImageSource));

        Windows.UI.Color color = UIHelper.HexToColorConverter(backgroundColor);

        storeItemLargeButton = new StoreItemLargeButton
        {
            StoreId = storeId,
            CardTitle = title,
            CardImageSource = image,
            CardPrice = price,
            CardBackgroundColor = color,
        };

        storeItemLargeButton.Click += (storeId) =>
        {
            Logger.Instance.CreateDebugLog(nameof(HomePage), $"Call {storeId}");

            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", storeItemLargeButton.imageElement);

            StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage), storeId, new SuppressNavigationTransitionInfo());
            // (Window.Current.Content as Frame)?.Navigate(typeof(Views.Store.ItemViewPage), storeId);
        };

        return storeItemLargeButton;
    }

    private StoreItemSmallButton CreateSmallButton(string storeId, string imageSource, string price, string title, string developer, string backgroundColor)
    {
        StoreItemSmallButton storeItemSmallButton;

        string eImageSource = StoreHelper.Instance.ExecuteScript(imageSource);
        BitmapImage image = new BitmapImage(new Uri(eImageSource));

        SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(backgroundColor);

        storeItemSmallButton = new StoreItemSmallButton
        {
            StoreId = storeId,
            CardTitle = title,
            CardImageSource = image,
            CardPrice = price,
            CardDeveloper = developer,
            CardBackgroundBrush = solidColorBrush,
        };

        storeItemSmallButton.Click += (storeId) =>
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", storeItemSmallButton.imageElement);

            StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage), storeId, new SuppressNavigationTransitionInfo());
        };

        return storeItemSmallButton;
    }

    public void InitializeAndShow(IEnumerable<UICategoryData> categories)
    {
        _lastCategories = categories;
        LoadCategories(categories);
    }

    private void LoadCategories(IEnumerable<UICategoryData> categories)
    {
        _panels.Clear();
        StoreStackPanel.Children.Clear();

        double totalWidth = Math.Min(StoreScrollViewer.ActualWidth
                                      - StoreScrollViewer.Padding.Left
                                      - StoreScrollViewer.Padding.Right,
                                      MaxTotalWidth);

        var sampleLarge = new StoreItemLargeButton();
        LargeMinW = sampleLarge.MinWidth;
        LargePrefW = sampleLarge.PreferredWidth;

        foreach (var cat in categories)
        {
            var header = new StoreCategoryButton
            {
                Text = cat.Name,
                Margin = new Thickness(0, 0, 0, 10)
            };
            StoreStackPanel.Children.Add(header);

            var itemsControl = new ItemsControl
            {
                Margin = new Thickness(0, 0, 0, 35),
                ItemsSource = cat.Items,
                ItemsPanel = (ItemsPanelTemplate)this.Resources["DefaultItemsPanel"]
            };

            _panels.Add((itemsControl, cat));

            StoreStackPanel.Children.Add(itemsControl);
        }

        LargeElementWidth = CalcGrid(MaxTotalWidth);
        
        CurrentPageWidth = totalWidth;

        SmallElementWidth = LargeElementWidth * 2;

        SetupElementsWidth();
    }
    

    private void StoreHelper_UpdatingDatabaseStarted()
    {
        LoadingGrid.Visibility = Visibility.Visible;
        StoreScrollViewer.Visibility = Visibility.Collapsed;
    }

    private async void UpdateStoreDatabase()
    {
        bool result = await Helper.StoreHelper.Instance.LoadAllStoreDatabase();
        // bool result = true;
        if (result)
        {
            List<UICategoryData> categoryDatas = CreateCategoriesList(StoreHelper.Instance.FormattedStoreDatabase);
            InitializeAndShow(categoryDatas);

            ErrorScrollViewer.Visibility = Visibility.Collapsed;
            LoadingGrid.Visibility = Visibility.Collapsed;
            StoreScrollViewer.Visibility = Visibility.Visible;
        } 
        else
        {
            ErrorScrollViewer.Visibility = Visibility.Visible;
            LoadingGrid.Visibility = Visibility.Collapsed;
            StoreScrollViewer.Visibility = Visibility.Collapsed;
        }
    }

    private void StoreHelper_ErrorHappens(string obj)
    {
        ErrorScrollViewer.Visibility = Visibility.Visible;
        ErrorCodeText.Text = obj;
        LoadingGrid.Visibility = Visibility.Collapsed;
        StoreScrollViewer.Visibility = Visibility.Collapsed;
    }

    public static T FindDescendant<T>(DependencyObject element) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is T t)
                return t;

            var result = FindDescendant<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private double CalcGrid(double newWidth)
    {
        double largeElementWidth = 0;

        double totalWidth = Math.Min(
            newWidth - StoreScrollViewer.Padding.Left - StoreScrollViewer.Padding.Right,
            MaxTotalWidth);

        int availableColumnsFloor = (int)Math.Floor(totalWidth / (LargeMinW + 15 + 15));
        int availableColumnsRound = (int)Math.Round(totalWidth / (LargeMinW + 15 + 15));

        if (availableColumnsFloor == availableColumnsRound)
        {
            availableColumns = availableColumnsFloor;
        }
        else
        {
            availableColumns = availableColumnsRound;
        }

        double preCalcWidth = totalWidth / (availableColumns);

        if (preCalcWidth < LargeMinW && availableColumns >= 2 )
        {
            availableColumns--;
        } 
        else if (preCalcWidth > LargePrefW*1.5)
        {
            if (availableColumns % 2 == 0)
                availableColumns += 2;
            else
                availableColumns++;
        }

        if (availableColumns % 2 != 0 && availableColumns > 2)
        {
            availableColumns--;
        }

        largeElementWidth = totalWidth / (availableColumns);

        return largeElementWidth;
    }

    private bool SetupElementsWidth()
    {
        bool result = true;
        if (_panels.Count <= 0)
            result = false;

        foreach (var (control, cat) in _panels)
        {
            bool isLarge = cat.Type == "largeButtonCategory";
            int elementCount = cat.Items.Count;

            var panel = FindDescendant<ItemsWrapGrid>(control);
            if (panel != null && DataContext is HomePage vm)
            {
                panel.ItemWidth = isLarge ? vm.LargeElementWidth : vm.SmallElementWidth;
                if (!isLarge)
                {
                    if (vm.SmallElementWidth * elementCount > CurrentPageWidth)
                        panel.MaximumRowsOrColumns = 2;
                    else
                        panel.MaximumRowsOrColumns = 1;
                }
                else
                {
                    panel.MaximumRowsOrColumns = 1;
                }
            } 
            else
            {
                result = false;     
            }
        }
        return result;
    }

    private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        double totalWidth = Math.Min(
            e.NewSize.Width - StoreScrollViewer.Padding.Left - StoreScrollViewer.Padding.Right,
            MaxTotalWidth);
        LargeElementWidth = CalcGrid(e.NewSize.Width);

        CurrentPageWidth = totalWidth;

        SmallElementWidth = LargeElementWidth * 2;

        SetupElementsWidth();
    }
}
