using CDPI_UI.Helper;
using CDPI_UI.Helper.CreateConfigHelper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.ComponentSettings
{
    public sealed partial class ConnectTelegramProxyContentDialog : ContentDialog
    {
        private static string ComponentId = StateHelper.Instance.FindKeyByValue("TgWsProxy");

        public ConnectTelegramProxyContentDialog()
        {
            InitializeComponent();

            Init();
        }

        private string GetUrl()
        {
            return $"tg://proxy?server={IpValue.DisplayText}&port={PortValue.DisplayText}&secret={KeyValue.DisplayText}";
        }

        private void Init()
        {
            var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
            var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");

            if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
                return;

            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

            string startupString = componentHelper.GetConfigHelper().GetStartupParameters(savedFile, savedPackId);

            ObservableCollection<GraphicDesignerSettingItemModel> lst = [];
            GraphicDesignerHelper.LoadTgWsProxyDesignerConfig(lst, []);
            GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(lst, [], startupString, exclusive: false, model: GraphicDesignerHelper.TgWsProxyDesignerConfig);

            foreach (var item in lst)
            {
                if (!item.IsChecked && item.Type == "string") continue;
                switch (item.DisplayName)
                {
                    case "--port":
                        PortValue.DisplayText = item.Value;
                        break;
                    case "--host":
                        IpValue.DisplayText = item.Value;
                        break;
                    case "--secret":
                        KeyValue.DisplayText = item.Value;
                        break;
                }
            }

            ConnectUrl.Text = GetUrl();
        }

        private void ConnectUrl_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            UrlOpenHelper.LaunchTelegramProxyUrl(IpValue.DisplayText, PortValue.DisplayText, KeyValue.DisplayText);
        }
    }
}
