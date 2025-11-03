using CDPI_UI.Helper;
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
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Store;

public sealed partial class StoreAskForSubItemInstallUserControl : UserControl
{


    public StoreAskForSubItemInstallUserControl()
    {
        InitializeComponent();

    }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set
        {
            SetValue(StoreIdProperty, value);

            if (DatabaseHelper.Instance.IsItemInstalled(StoreId) || StoreHelper.Instance.GetOperationIdFromItemId(StoreId) != null)
            {
                SetStatus(true);
                return;
            }
        }
    }

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(StoreAskForSubItemInstallUserControl), new PropertyMetadata(string.Empty)
        );

    public ImageSource CardImageSource
    {
        get { return (ImageSource)GetValue(CardImageSourceProperty); }
        set { SetValue(CardImageSourceProperty, value); }
    }

    public static readonly DependencyProperty CardImageSourceProperty =
        DependencyProperty.Register(
            "ImageSource", typeof(ImageSource), typeof(StoreAskForSubItemInstallUserControl), new PropertyMetadata(null)
        );

    public string CardTitle
    {
        get { return (string)GetValue(CardTitleProperty); }
        set { SetValue(CardTitleProperty, value); }
    }

    public static readonly DependencyProperty CardTitleProperty =
        DependencyProperty.Register(
            "Title", typeof(string), typeof(StoreAskForSubItemInstallUserControl), new PropertyMetadata(string.Empty)
        );

    public string CardCategory
    {
        get { return (string)GetValue(CardCategoryProperty); }
        set { SetValue(CardCategoryProperty, value); }
    }

    public static readonly DependencyProperty CardCategoryProperty =
        DependencyProperty.Register(
            nameof(CardCategory), typeof(string), typeof(StoreAskForSubItemInstallUserControl), new PropertyMetadata(string.Empty)
        );

    public string Developer
    {
        get { return (string)GetValue(DeveloperProperty); }
        set { SetValue(DeveloperProperty, value); }
    }

    public static readonly DependencyProperty DeveloperProperty =
        DependencyProperty.Register(
            nameof(Developer), typeof(string), typeof(StoreAskForSubItemInstallUserControl), new PropertyMetadata(string.Empty)
        );
    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set { SetValue(DescriptionProperty, value); }
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(StoreAskForSubItemInstallUserControl), new PropertyMetadata(string.Empty)
        );

    private void SetStatus(bool isInstalled)
    {
        StatusFontIcon.Glyph = isInstalled ? "\uE930" : "\uEBD3";
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (DatabaseHelper.Instance.IsItemInstalled(StoreId) || StoreHelper.Instance.GetOperationIdFromItemId(StoreId) != null)
        {
            SetStatus(true);
            return;
        }

        RemoveHandlers();
        ConnectHandlers();

        StoreHelper.Instance.AddItemToQueue(StoreId, string.Empty);
    }

    private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
    private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
    private Action<string> _itemActionsStoppedHandler;
    private Action<string> _itemRemovedHandler;

    private void ConnectHandlers()
    {
        _itemDownloadProgressChangedHandler = (data) =>
        {
            string operationId = data.Item1;
            double progress = data.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                return;

            StatusProgressBar.Visibility = Visibility.Visible;
            StatusProgressBar.IsIndeterminate = false;
            StatusProgressBar.Value = progress;
        };
        StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;
        _itemDownloadStageChangedHandler = (data) =>
        {
            string operationId = data.Item1;
            string stage = data.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                return;

            StatusProgressBar.Visibility = Visibility.Visible;

            switch (stage)
            {
                case "Downloading":
                    StatusProgressBar.IsIndeterminate = false;
                    break;
                default:
                    StatusProgressBar.IsIndeterminate = true;
                    break;
            }
        };
        StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

        _itemActionsStoppedHandler = (id) =>
        {
            RemoveHandlers();
            SetStatus(true);
        };
        StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;

        _itemRemovedHandler = (data) =>
        {
            if (data != StoreId)
                return;
            RemoveHandlers();
            SetStatus(false);
        };
        StoreHelper.Instance.ItemRemoved += _itemRemovedHandler;
    }

    private void RemoveHandlers()
    {
        StatusProgressBar.Visibility = Visibility.Collapsed;
        if (_itemDownloadStageChangedHandler != null)
            StoreHelper.Instance.ItemDownloadStageChanged -= _itemDownloadStageChangedHandler;

        if (_itemDownloadProgressChangedHandler != null)
            StoreHelper.Instance.ItemDownloadProgressChanged -= _itemDownloadProgressChangedHandler;

        if (_itemActionsStoppedHandler != null)
            StoreHelper.Instance.ItemActionsStopped -= _itemActionsStoppedHandler;

        if (_itemRemovedHandler != null)
            StoreHelper.Instance.ItemRemoved -= _itemRemovedHandler;
    }
}
