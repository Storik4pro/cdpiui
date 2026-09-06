using CDPIUI.Core;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPIUI.Controls.Dialogs.CreateConfigHelper.ChooseGroupModeContentDialog;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Dialogs.CreateConfigHelper;

public class ByeDPIModeComboBoxItem
{
    public ByeDPIGroupModes Mode;
    public string DisplayName;
    public string DisplayTip;
}

public sealed partial class ChooseGroupModeContentDialog : ContentDialog
{
    public enum ByeDPIGroupModes
    {
        Torst,
        Redirect,
        SslErr,
        None
    }

    private ObservableCollection<ByeDPIModeComboBoxItem> _modes = [];

    private ILocalizer localizer = Localizer.Get();

    public List<ByeDPIGroupModes> Result { get; private set; } = [];

    public ChooseGroupModeContentDialog(List<ByeDPIGroupModes> modes)
    {
        InitializeComponent();

        ModeTokenView.ItemsSource = _modes;

        _modes.Add(new()
        {
            Mode = ByeDPIGroupModes.Torst,
            DisplayName = "TORST",
            DisplayTip = localizer.GetLocalizedString("/Flashlight/TORST")
        });
        _modes.Add(new()
        {
            Mode = ByeDPIGroupModes.Redirect,
            DisplayName = "REDIRECT",
            DisplayTip = localizer.GetLocalizedString("/Flashlight/REDIRECT")
        });
        _modes.Add(new()
        {
            Mode = ByeDPIGroupModes.SslErr,
            DisplayName = "SSLERR",
            DisplayTip = localizer.GetLocalizedString("/Flashlight/SSLERR")
        });
        _modes.Add(new()
        {
            Mode = ByeDPIGroupModes.None,
            DisplayName = "NONE",
            DisplayTip = localizer.GetLocalizedString("/Flashlight/NONE")
        });

        foreach (var mode in modes)
        {
            ModeTokenView.SelectRange(new ItemIndexRange(_modes.IndexOf(_modes.FirstOrDefault(x=> x.Mode == mode)), 1));
        }

        

        this.Closing += ChooseGroupModeContentDialog_Closing;
    }

    private void ChooseGroupModeContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        this.Closing -= ChooseGroupModeContentDialog_Closing;

        foreach (var item in ModeTokenView.SelectedItems)
        {
            Result.Add(((ByeDPIModeComboBoxItem)item).Mode);
        }
    }
}
