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
using GoodbyeDPI_UI.Helper;
using Application = Microsoft.UI.Xaml.Application;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;
using GoodbyeDPI_UI.Helper.Static;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Controls.Dialogs;

public sealed partial class FontSettingsContentDialog : ContentDialog
{
    public string FontName { get; set; }
    public new int FontSize { get; set; }
    
    public FontSettingsContentDialog()
    {
        InitializeComponent();

        this.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;

        List<string> monoFonts = new List<string>
        {
            "Consolas",
            "Courier New",
            "Lucida Console",
            "Cascadia Code",
            "Cascadia Mono"
        };

        List<string> fontSize = new()
        {
            "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "36", "48", "72"
        };

        FontChooseComboBox.ItemsSource = monoFonts;
        FontChooseComboBox.SelectedItem = SettingsManager.Instance.GetValue<string>("PSEUDOCONSOLE", "fontFamily");
        FontSizeComboBox.ItemsSource = fontSize;
        FontSizeComboBox.SelectedValue = SettingsManager.Instance.GetValue<double>("PSEUDOCONSOLE", "fontSize").ToString();
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        this.Hide();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        bool result = int.TryParse(FontSizeComboBox.SelectedItem.ToString(), out int size);
        FontName = FontChooseComboBox.SelectedItem.ToString();
        FontSize = (int)SettingsManager.Instance.GetValue<double>("PSEUDOCONSOLE", "fontSize");

        if (!result)
        {
            WarningText.Visibility = Visibility.Visible;
            return;
        }

        
        FontSize = size;
    }

    private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool result = int.TryParse(FontSizeComboBox.SelectedItem.ToString(), out int size);

        if (!result)
        {
            WarningText.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
            return;
        }
        IsPrimaryButtonEnabled = true;
        WarningText.Visibility = Visibility.Collapsed;
    }
}
