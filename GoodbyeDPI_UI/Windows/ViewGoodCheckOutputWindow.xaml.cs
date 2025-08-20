using GoodbyeDPI_UI.DataModel;
using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using GoodbyeDPI_UI.Views;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ViewGoodCheckOutputWindow : Window
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private IntPtr _hwnd;
    private WindowProc _newWndProc;
    private IntPtr _oldWndProc;

    public ViewGoodCheckOutputWindow()
    {
        InitializeComponent();
        InitializeWindow();
        WindowHelper.Instance.SetWindowSize(this, 800, 600);
        this.Closed += ViewGoodCheckOutputWindow_Closed;
        TrySetMicaBackdrop(true);

        ((App)Application.Current).OpenWindows.Add(this);

        

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        SetOperationPages();
        ConnectHandlers();


    }

    private void ConnectHandlers()
    {
        GoodCheckProcessHelper.Instance.OperationsListChanged += () =>
        {
            SetOperationPages();
        };
    }

    private void SetOperationPages()
    {
        List<GoodCheckOperationModel> operations = GoodCheckProcessHelper.Instance.GetCurrentOperations();

        foreach (GoodCheckOperationModel op in operations)
        {
            CreateTab(op.OperationId, op.SiteListName);
        }
    }

    private void ViewGoodCheckOutputWindow_Closed(object sender, WindowEventArgs args)
    {
        
        ((App)Application.Current).OpenWindows.Remove(this);
    }

    private void InitializeWindow()
    {
        _hwnd = WindowNative.GetWindowHandle(this);
        _newWndProc = new WindowProc(NewWindowProc);
        _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void ProcessControl_Click(object sender, RoutedEventArgs e)
    {

    }

    

    private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        
    }

    public void CreateTab(int id, string title = null)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => CreateTab(id, title));
            return;
        }
        var existing = GetTabById(id);
        if (existing != null)
        {
            HeaderTabView.SelectedItem = existing;
            return;
        }

        var tab = new TabViewItem
        {
            Tag = id,
            IsClosable = false
        };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var iconHolder = new ContentControl { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        var textBlock = new TextBlock { Text = title ?? $"Item {id}", VerticalAlignment = VerticalAlignment.Center };

        headerPanel.Children.Add(iconHolder);
        headerPanel.Children.Add(textBlock);

        tab.Header = headerPanel;

        HeaderTabView.TabItems.Add(tab);

        HeaderTabView.SelectedItem = tab;
        ShowContentForId(id);
    }

    private void ShowContentForId(int id)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => ShowContentForId(id));
            return;
        }

        ContentFrame.Navigate(typeof(ViewOutputPage), id, new SuppressNavigationTransitionInfo());
    }

    private void HeaderTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HeaderTabView.SelectedItem is TabViewItem tab && tab.Tag is int id)
        {
            ShowContentForId(id);
        }
    }

    private TabViewItem GetTabById(int id)
    {
        return HeaderTabView.TabItems.OfType<TabViewItem>().FirstOrDefault(t => t.Tag is int tid && tid == id);
    }

    public void SetTabTitle(int id, string title)
    {
        var tab = GetTabById(id);
        if (tab == null) return;

        if (tab.Header is StackPanel headerPanel)
        {
            var textBlock = headerPanel.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock != null) textBlock.Text = title;
        }
    }

    public void SetTabIconElement(int id, UIElement iconElement)
    {
        var tab = GetTabById(id);
        if (tab == null) return;

        if (tab.Header is StackPanel headerPanel)
        {
            var iconHolder = headerPanel.Children.OfType<ContentControl>().FirstOrDefault();
            if (iconHolder != null)
            {
                iconHolder.Content = iconElement;
            }
        }
    }

    public void SetTabToProgress(int id)
    {
        var pr = new ProgressRing
        {
            IsActive = true,
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center
        };
        SetTabIconElement(id, pr);
        SetTabTitle(id, "Loading...");
    }

    public void SetTabToError(int id, string title = "Error")
    {
        var cross = new TextBlock
        {
            Text = "✖",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };

        SetTabIconElement(id, cross);
        SetTabTitle(id, title);
    }

    public void ClearTabIcon(int id)
    {
        SetTabIconElement(id, null);
    }

    #region WINAPI

    private delegate IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private IntPtr NewWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            minMaxInfo.ptMinTrackSize.x = 484;
            minMaxInfo.ptMinTrackSize.y = 300;
            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private const int GWLP_WNDPROC = -4;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    bool TrySetMicaBackdrop(bool useMicaAlt)
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            micaBackdrop.Kind = useMicaAlt ? Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt : Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base;
            this.SystemBackdrop = micaBackdrop;

            return true;
        }

        return false;
    }
    #endregion

    private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}
