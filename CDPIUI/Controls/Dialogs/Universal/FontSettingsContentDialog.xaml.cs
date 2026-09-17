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
using System.Windows.Forms;
using CDPIUI.Core;
using Application = Microsoft.UI.Xaml.Application;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;


namespace CDPIUI.Controls.Dialogs;

public sealed partial class FontSettingsContentDialog : ContentDialog
{
    public FontFamily FontName { get; set; }
    public new double FontSize { get; set; }
    
    public FontSettingsContentDialog()
    {
        InitializeComponent();

        this.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        this.Hide();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        FontSize = (double)FontSizeComboBox.SelectedItem;
        FontName = FontChooseComboBox.SelectedItem as FontFamily;
    }
}
