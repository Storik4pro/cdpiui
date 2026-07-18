using CDPI_UI.Helper;
using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
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
using static CDPI_UI.Helper.StoreHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.Store
{
    public sealed partial class AcceptLicenseContentDialog : ContentDialog
    {
        public List<License> Licenses = [];

        public AcceptLicenseContentDialog()
        {
            InitializeComponent();
            this.DataContext = this;


        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            HyperlinkButton _sender = sender as HyperlinkButton;
            string url = _sender.Tag.ToString();

            if (url.StartsWith("$OPENINBROWSER"))
            {
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(LScriptLangHelper.GetArgumentsFromScript(url)));
            }
            else
            {
                string _url = LScriptLangHelper.ExecuteScriptUnsafe(url);
                Utils.OpenFileInDefaultApp(_url);
            }
        }
    }
}
