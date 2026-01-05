using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.FileProperties;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.ComponentSettings;

public enum TextFileOpenModes
{
    FollowSystem,
    UserChoose
}

public sealed partial class EditSitelistAskApplicationContentDialog : ContentDialog
{
    private ILocalizer localizer = Localizer.Get();
    public string FilePath { get; set; } = string.Empty;
    public bool IsSuccess { get; private set; } = false;
    public EditSitelistAskApplicationContentDialog()
    {
        InitializeComponent();

        this.Opened += EditSitelistAskApplicationContentDialog_Opened;
    }

    private async void EditSitelistAskApplicationContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        string appPath = SettingsManager.Instance.GetValue<string>("FILEOPENACTIONS", "applicationPath");
        Debug.WriteLine(appPath);
        if (!string.IsNullOrEmpty(appPath) && File.Exists(appPath))
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(appPath);
            var iconThumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 32);
            var bi = new BitmapImage();
            bi.SetSource(iconThumbnail);
            PreviousAppIcon.Source = bi;
            PreviousChoiceApplication.Text = Utils.FirstCharToUpper(Path.GetFileNameWithoutExtension(appPath));
        }
        else
        {
            PreviousChoiceButton.Visibility = Visibility.Collapsed;
        }

        DoNotAskAgainCheckBox.IsChecked = SettingsManager.Instance.GetValueOrDefault<bool>("FILEOPENACTIONS", "doNotRemindAgain", defaultValue:true);
    }

    private void SelectAppManuallyButton_Click(object sender, RoutedEventArgs e)
    {
        string appPath;
        using OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Title = localizer.GetLocalizedString("FileDialogChooseExecutable");
        openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        openFileDialog.FilterIndex = 4;

        openFileDialog.Filter = $"{localizer.GetLocalizedString("FileDialogChooseExecutableFilter")} (*.exe)|*.exe";
        openFileDialog.RestoreDirectory = true;

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            appPath = openFileDialog.FileName;
            SettingsManager.Instance.SetValue("FILEOPENACTIONS", "applicationPath", appPath);
            SettingsManager.Instance.SetValue("FILEOPENACTIONS", "mode", (int)TextFileOpenModes.UserChoose);
            if (!string.IsNullOrEmpty(FilePath))
                Utils.RunApp(appPath, $"\"{FilePath}\"");
            IsSuccess = true;
            this.Hide();
        }
        else
        {
            return;
        }
    }

    private void FollowSystemButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("FILEOPENACTIONS", "mode", (int)TextFileOpenModes.FollowSystem);
        if (!string.IsNullOrEmpty(FilePath))
            Utils.OpenFileInDefaultApp(FilePath);
        IsSuccess = true;
        this.Hide();
    }

    private void PreviousChoiceButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("FILEOPENACTIONS", "mode", (int)TextFileOpenModes.UserChoose);
        string appPath = SettingsManager.Instance.GetValue<string>("FILEOPENACTIONS", "applicationPath");
        if (!string.IsNullOrEmpty(FilePath))
            Utils.RunApp(appPath, $"\"{FilePath}\"");
        IsSuccess = true;
        this.Hide();
    }

    private void DoNotAskAgainCheckBox_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("FILEOPENACTIONS", "doNotRemindAgain", DoNotAskAgainCheckBox.IsChecked);
    }
}
