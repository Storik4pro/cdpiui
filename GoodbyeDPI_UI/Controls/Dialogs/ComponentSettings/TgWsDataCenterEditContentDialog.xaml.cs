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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.ComponentSettings;

public sealed partial class TgWsDataCenterEditContentDialog : ContentDialog
{
    private string number = string.Empty;
    public string Number
    {
        get
        {
            return number;
        }
        set
        {
            number = value;
            if (NumberTextBox.Text != Number) NumberTextBox.Text = Number;
        }
    }

    private string ip = string.Empty;
    public string Ip
    { 
        get
        {
            return ip;
        }
        set
        {
            ip = value;
            if (IpTextBox.Text != Ip) IpTextBox.Text = Ip;
        }
    }

    public bool Result { get; private set; }

    public TgWsDataCenterEditContentDialog()
    {
        InitializeComponent();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Number = NumberTextBox.Text;
        Ip = IpTextBox.Text;
        Result = true;
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = false;

    }
}
