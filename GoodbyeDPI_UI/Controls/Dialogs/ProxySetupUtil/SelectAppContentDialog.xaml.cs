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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Forms;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.ProxySetupUtil;

public enum SelectAppContentDialogResult
{
    None,
    ClassicApp,
    UniversalApp
}

public class ApplicationInfo
{
    public string DisplayName { get; set; }
    public string FullPath { get; set; }
    public SelectAppContentDialogResult AppType { get; set; }
}

public sealed partial class SelectAppContentDialog : ContentDialog
{
    private enum SelectedAppType
    {
        None,
        ClassicApp,
        UniversalApp
    }
    public ICommand RemoveAppClickCommand { get; }
    public ObservableCollection<ApplicationInfo> _applications { get; private set; } = [];

    private ILocalizer localizer = Localizer.Get();
    public SelectAppContentDialog(List<ApplicationInfo> applications)
    {
        InitializeComponent();
        this.DataContext = this;
        RemoveAppClickCommand = new RelayCommand(p => RemoveApp((ApplicationInfo)p));

        AuditVisibility();

        SelectedApplicationsListView.ItemsSource = _applications;

        foreach (var app in applications)
            _applications.Add(app);
    }

    private SelectAppContentDialogResult GetResult()
    {
        if (ClassicAppStackPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(ClassicAppTextBox.Text))
        {
            return SelectAppContentDialogResult.ClassicApp;
        }
        else if (UniversalAppStackPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(UniversalAppTextBox.Text))
        {
            return SelectAppContentDialogResult.UniversalApp;
        }
        else
        {
            return SelectAppContentDialogResult.None;
        }
    }

    private void CheckAppType(SelectedAppType appType)
    {
        switch (appType)
        {
            case SelectedAppType.None:
                ClassicAppStackPanel.Visibility = Visibility.Visible;
                UniversalAppStackPanel.Visibility = Visibility.Visible;
                Separator.Visibility = Visibility.Visible;
                ApplicationPreviewGrid.Visibility = Visibility.Collapsed;
                AddApplicationButton.IsEnabled = false;
                break;
            case SelectedAppType.ClassicApp:
                ClassicAppStackPanel.Visibility = Visibility.Visible;
                UniversalAppStackPanel.Visibility = Visibility.Collapsed;
                Separator.Visibility = Visibility.Collapsed;
                ApplicationPreviewGrid.Visibility = Visibility.Visible;
                AddApplicationButton.IsEnabled = true;
                break;
            case SelectedAppType.UniversalApp:
                ClassicAppStackPanel.Visibility = Visibility.Collapsed;
                UniversalAppStackPanel.Visibility = Visibility.Visible;
                Separator.Visibility = Visibility.Collapsed;
                ApplicationPreviewGrid.Visibility = Visibility.Visible;
                AddApplicationButton.IsEnabled = true;
                break;
        }
    }

    private void AuditVisibility()
    {
        if (string.IsNullOrWhiteSpace(ClassicAppTextBox.Text) && string.IsNullOrWhiteSpace(UniversalAppTextBox.Text))
        {
            CheckAppType(SelectedAppType.None);
        }
        else if (!string.IsNullOrWhiteSpace(ClassicAppTextBox.Text) && string.IsNullOrWhiteSpace(UniversalAppTextBox.Text))
        {
            CheckAppType(SelectedAppType.ClassicApp);
        }
        else if (string.IsNullOrWhiteSpace(ClassicAppTextBox.Text) && !string.IsNullOrWhiteSpace(UniversalAppTextBox.Text))
        {
            CheckAppType(SelectedAppType.UniversalApp);
        }
        else
        {
            CheckAppType(SelectedAppType.None);
        }
    }

    private void ClassicAppTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        AuditVisibility();
        ApplicationPreviewTextBlock.Text = ClassicAppTextBox.Text;
    }

    private void UniversalAppTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        AuditVisibility();
        ApplicationPreviewTextBlock.Text = UniversalAppTextBox.Text;
    }

    private async void FolderChooseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<ProxySetupUtilWindow>());
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var windowsAppsPath = @"C:\Progra~1";
        Windows.Storage.StorageFolder startFolder = null;
        try
        {
            startFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(windowsAppsPath);
        }
        catch
        {

        }

        if (startFolder != null)
        {
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        }

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            UniversalAppTextBox.Text = folder.Path;
        }
    }

    private void FileChooseButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath = string.Empty;
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Title = localizer.GetLocalizedString("ChooseExeFile");
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Filter = "EXE files (*.exe)|*.exe";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
            }
            else
            {
                return;
            }
        }

        ClassicAppTextBox.Text = filePath;

    }

    private void RemoveApp(ApplicationInfo model)
    {
        _applications.Remove(model);
    }

    private void Clean()
    {
        ClassicAppTextBox.Text = string.Empty;
        UniversalAppTextBox.Text = string.Empty;
        ApplicationPreviewTextBlock.Text = string.Empty;
        CheckAppType(SelectedAppType.None);
    }

    private void AddApplicationButton_Click(object sender, RoutedEventArgs e)
    {
        _applications.Add(new()
        {
            AppType = GetResult(),
            DisplayName = Path.GetFileNameWithoutExtension(ApplicationPreviewTextBlock.Text),
            FullPath = ApplicationPreviewTextBlock.Text,
        });
        Debug.WriteLine(_applications.Count);
        Clean();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }
}
