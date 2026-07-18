using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;
using CDPI_UI.Helper;
using WinUI3Localizer;
using CDPI_UI.Controls.Dialogs.Universal;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.EditHostsFile;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    private ILocalizer localizer = Localizer.Get();
    public MainPage()
    {
        InitializeComponent();

        this.Loaded += MainPage_Loaded;
        this.ActualThemeChanged += MainPage_ActualThemeChanged;

        SetState();
    }

    private void SetState()
    {
        bool isFileChanged = SettingsManager.Instance.GetValueOrDefault<bool>("HOSTS", "isFileEdited", defaultValue:false);
        bool isBackupAvailable = SettingsManager.Instance.GetValueOrDefault<bool>("HOSTS", "isBackupAvailable", defaultValue: false);

        StatusFontIcon.Glyph = isFileChanged ? "\uEC61" : "\uEB90";
        StatusTextBlock.Text = isFileChanged ? localizer.GetLocalizedString("HostsFileStatusEnabled") : localizer.GetLocalizedString("HostsFileStatusDisabled");

        ReplaceHostsFileSettingsCard.IsEnabled = !isFileChanged;
        ReplaceHostsFileSettingsCard.Description = isFileChanged ? localizer.GetLocalizedString("HostsFileAreAllreadyEdited") : "";

        RemoveHostsFileSettingsCard.IsEnabled = isFileChanged;
        RemoveHostsFileSettingsCard.Description = isFileChanged ? "" : localizer.GetLocalizedString("HostsFileNotEdited");

        RecoverHostsFileSettingsCard.IsEnabled = isBackupAvailable;
        RecoverHostsFileSettingsCard.Description = isBackupAvailable ? "" : localizer.GetLocalizedString("NoBackupAvailable");
    }

    private void SetLoadingMode(bool isLoading)
    {
        LoadingGrid.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = !isLoading;
        if (isLoading)
        {
            ReplaceHostsFileSettingsCard.IsEnabled = false;
            RemoveHostsFileSettingsCard.IsEnabled = false;
            RecoverHostsFileSettingsCard.IsEnabled = false;
        }
        else
        {
            SetState();
        }
    }

    private void MainPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateCriticalPointerOverColor();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCriticalPointerOverColor();
    }

    private void UpdateCriticalPointerOverColor()
    {
        var baseColor = (Color)((App)Application.Current).Resources["SystemFillColorCriticalBackground"];
        bool isDarkTheme = this.ActualTheme == ElementTheme.Dark;
        float blendFactor = isDarkTheme ? 0.04f : 0.95f;

        Color pointerOverColor = BlendWithWhite(baseColor, blendFactor);

        if (isDarkTheme) SettingsCardBorderBrushPointerOverSolidColorBrush.Color = pointerOverColor;
        else SettingsCardBorderBrushPointerOverSolidColorBrush.Color = (Color)((App)Application.Current).Resources["CardStrokeColorDefault"];
        SettingsCardBackgroundPointerOver.Color = pointerOverColor;
    }

    private Color BlendWithWhite(Color baseColor, float factor)
    {
        if (baseColor.A == 0) baseColor = Color.FromArgb(255, 232, 17, 35);

        byte r = (byte)Math.Clamp(baseColor.R + (255 - baseColor.R) * factor, 0, 255);
        byte g = (byte)Math.Clamp(baseColor.G + (255 - baseColor.G) * factor, 0, 255);
        byte b = (byte)Math.Clamp(baseColor.B + (255 - baseColor.B) * factor, 0, 255);

        return Color.FromArgb(255, r, g, b); 
    }

    private async void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var window = await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
        window.NavigateToPage("/Utils/EditHostsFileUtility");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var window = ((App)Application.Current).GetCurrentWindowFromType<EditHostFileWindow>();
        window?.Close();
    }

    private async void ReplaceHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        SetLoadingMode(true);
        string result = "ERR_FLASHLIGHT_BISY";
        try
        {
            result = ErrorsHelper.GetPrettyErrorCode("HOSTEDIT", await HostsFileEditHelper.AddDomains());
        }
        catch (Exception ex)
        {
            result = ErrorsHelper.GetPrettyErrorCode("HOSTEDIT", ex);
        }
        finally
        {
            if (result == "HOSTEDIT_SUCCESS (0x00000000)")
            {
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isFileEdited", true);
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isBackupAvailable", true);
            }
            else if (!result.StartsWith("HOSTEDIT_OPERATION_CANCELLED_BY_USER"))
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(localizer.GetLocalizedString("ErrorHappensWhenTryingAddDomainsToHostsFile"), result, this.XamlRoot);
            }

            SetLoadingMode(false);
        }
    }

    private async void RemoveHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        SetLoadingMode(true);
        string result = "ERR_FLASHLIGHT_BISY";
        try
        {
            result = ErrorsHelper.GetPrettyErrorCode("HOSTEDIT", await HostsFileEditHelper.RemoveDomains());
        }
        catch (Exception ex)
        {
            result = ErrorsHelper.GetPrettyErrorCode("HOSTEDIT", ex);
        }
        finally
        {
            if (result == "HOSTEDIT_SUCCESS (0x00000000)")
            {
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isFileEdited", false);
            }
            else if (!result.StartsWith("HOSTEDIT_OPERATION_CANCELLED_BY_USER"))
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(localizer.GetLocalizedString("ErrorHappensWhenTryingRemoveDomainsToHostsFile"), result, this.XamlRoot);
            }

            SetLoadingMode(false);
        }
    }

    private async void RecoverHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        SetLoadingMode(true);
        string result = "ERR_FLASHLIGHT_BISY";
        try
        {
            result = ErrorsHelper.GetPrettyErrorCode("HOSTEDIT", await HostsFileEditHelper.RestoreDomains());
        }
        catch (Exception ex)
        {
            result = ErrorsHelper.GetPrettyErrorCode("HOSTEDIT", ex);
        }
        finally
        {
            if (result == "HOSTEDIT_SUCCESS (0x00000000)")
            {
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isFileEdited", false);
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isBackupAvailable", false);
            }
            else if (!result.StartsWith("HOSTEDIT_OPERATION_CANCELLED_BY_USER"))
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(localizer.GetLocalizedString("ErrorHappensWhenTryingRecoverHostsFile"), result, this.XamlRoot);
            }

            SetLoadingMode(false);
        }
    }

    private void OpenHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        HostsFileEditHelper.EditHostsFile(this.XamlRoot);
        
    }
}
