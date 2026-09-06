using CDPIUI.Controls.Default;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Views.Store.Settings.Memory;

public sealed partial class MemoryViewPreviousVersionsPage : TemplatePage
{
    private readonly ILocalizer localizer = Localizer.Get();
    internal static string PreviousVersionsDirectory => Path.Combine(Directories.CurrentDirectory, ".old");
    private string RollbackPath => Path.Combine(PreviousVersionsDirectory, "GDPIUI-Rollback.exe");

    public MemoryViewPreviousVersionsPage()
    {
        InitializeComponent();
        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = NavGrid;
        BreadcrumbBar.ItemsSource = new BreadcrumbBarModel[]
        {
            new() { DisplayName = localizer.GetLocalizedString("Settings"), Tag = typeof(SettingsPage) },
            new() { DisplayName = localizer.GetLocalizedString("MemoryUsage"), Tag = typeof(MemoryViewPage) },
            new() { DisplayName = localizer.GetLocalizedString("MemoryUsageCategoryPreviousVersionsDisplayName"), Tag = GetType() }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshSize();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (SettingsPage.MemoryNavigationSupportedPages.Contains(e.SourcePageType))
            PrepareToConnectedBackwardAnimate(NavGrid);
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = (BreadcrumbBarModel)args.Item;
        Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    private async void RefreshSize()
    {
        try
        {
            MemoryTextBlock.Text = UnitsParser.FormatSize(await FileSystemService.GetDirectorySize(PreviousVersionsDirectory, Logger.Instance));
            CleanupDirButton.IsEnabled = Directory.Exists(PreviousVersionsDirectory);
            RestoreButton.IsEnabled = File.Exists(RollbackPath);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteFlyout.Hide();
        ErrorStackPanel.Visibility = Visibility.Collapsed;
        CleanupDirButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;
        try
        {
            if (Directory.Exists(PreviousVersionsDirectory))
                await Task.Run(() => Directory.Delete(PreviousVersionsDirectory, recursive: true));
        }
        catch (Exception ex) { ShowError(ex); }
        finally { RefreshSize(); }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorStackPanel.Visibility = Visibility.Collapsed;
        try
        {
            if (!File.Exists(RollbackPath)) throw new FileNotFoundException(null, RollbackPath);
            using var process = Process.Start(new ProcessStartInfo(RollbackPath)
            {
                WorkingDirectory = PreviousVersionsDirectory,
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowError(Exception exception)
    {
        ErrorTextBlock.Text = exception.Message;
        ErrorStackPanel.Visibility = Visibility.Visible;
        Logger.Instance.CreateWarningLog(nameof(MemoryViewPreviousVersionsPage), exception.ToString());
    }
}
