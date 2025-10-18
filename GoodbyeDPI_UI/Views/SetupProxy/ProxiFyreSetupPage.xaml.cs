using CDPI_UI.Controls.Dialogs.ProxySetupUtil;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage.Pickers;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.SetupProxy
{
    

    public class ProxiFyreProxyGroup
    {
        public List<string> appNames { get; set; }
        public string socks5ProxyEndpoint { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public List<string> supportedProtocols { get; set; }
    }

    public class ProxiFyreSettings
    {
        public string logLevel { get; set; } = "Error";
        public List<ProxiFyreProxyGroup> proxies { get; set; } = [];
        public List<string> excludes { get; set; } = [];
    }

    public sealed partial class ProxiFyreSetupPage : Page
    {
        private List<ApplicationInfo> WhiteList = [];
        private List<ApplicationInfo> BlackList = [];

        private ILocalizer localizer = Localizer.Get();

        private const string AddOnId = "ASPEWK002";

        private enum ProxiFyreModes
        {
            TCP,
            UDP,
            Both
        }
        private class ProxiFyreModeElement
        {
            public string DisplayName { get; set; }
            public ProxiFyreModes Mode { get; set; }
        }

        private ObservableCollection<ProxiFyreModeElement> proxiFyreModes = [];

        public ProxiFyreSetupPage()
        {
            InitializeComponent();
            this.DataContext = this;



            proxiFyreModes.Add(new ProxiFyreModeElement()
            {
                DisplayName = "TCP",
                Mode = ProxiFyreModes.TCP,
            });
            proxiFyreModes.Add(new ProxiFyreModeElement()
            {
                DisplayName = "UDP",
                Mode= ProxiFyreModes.UDP,
            });
            proxiFyreModes.Add(new ProxiFyreModeElement()
            {
                DisplayName = "TCP+UDP",
                Mode = ProxiFyreModes.Both
            });
            ProxiFyreModeComboBox.ItemsSource = proxiFyreModes;
            ProxiFyreModeComboBox.SelectionChanged += ProxiFyreModeComboBox_SelectionChanged;
            SetDescription();
            GetSettings();
        }

        private void ProxiFyreModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            if (anim != null)
            {
                anim.TryStart(ActionButtonsGrid);
            }
        }

        private void GoBackButton_Click(object sender, RoutedEventArgs e)
        {
            var anim = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackwardConnectedAnimation", ActionButtonsGrid);

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                anim.Configuration = new DirectConnectedAnimationConfiguration();
            }
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void Navigate<T>() where T : Page
        {
            var anim = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", ActionButtonsGrid);

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                anim.Configuration = new DirectConnectedAnimationConfiguration();
            }

            Frame.Navigate(typeof(T), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open help link
        }

        private void GoForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (SaveSettings())
                Navigate<ProxySetupCompletePage>();
        }

        

        private string GetProxyIpString(string ip, string port)
        {
            if (IPAddress.TryParse(ip, out var address))
            {
                return address.AddressFamily switch
                {
                    System.Net.Sockets.AddressFamily.InterNetwork => $"{ip}:{port}",
                    System.Net.Sockets.AddressFamily.InterNetworkV6 => $"[{ip}]:{port}",
                    _ => "",
                };
            }
            return string.Empty;
        }

        private void GetSettings()
        {
            string dir = Path.Combine(StateHelper.GetDataDirectory(), StateHelper.StoreDirName, StateHelper.StoreItemsDirName, AddOnId);
            if (!Path.Exists(dir))
                return; // TODO: show error message
            string settingsFile = Path.Combine(dir, "app-config.json");

            if (!File.Exists(settingsFile))
            {
                ProxiFyreModeComboBox.SelectedItem = proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.Both);

                IPValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
                PortValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "port");
            }
            else
            {
                string readyToCompareIpAddr = GetProxyIpString(
                    SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress"), SettingsManager.Instance.GetValue<string>("PROXY", "port"));

                ProxiFyreSettings settings = Utils.LoadJson<ProxiFyreSettings>(settingsFile);
                ProxiFyreProxyGroup proxyGroup = settings.proxies.FirstOrDefault(x => x.socks5ProxyEndpoint == readyToCompareIpAddr);
                if (proxyGroup is null && settings.proxies.Count > 0)
                {
                    proxyGroup = settings.proxies.First();
                }

                foreach (string app in proxyGroup.appNames)
                {
                    WhiteList.Add(new()
                    {
                        DisplayName = Path.GetFileNameWithoutExtension(app),
                        FullPath = app,
                    });
                }

                if (proxyGroup.supportedProtocols.Contains("UDP") && proxyGroup.supportedProtocols.Contains("TCP"))
                {
                    ProxiFyreModeComboBox.SelectedItem = proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.Both);
                }
                else if (proxyGroup.supportedProtocols.Contains("UDP"))
                {
                    ProxiFyreModeComboBox.SelectedItem = proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.UDP);
                }
                else
                {
                    ProxiFyreModeComboBox.SelectedItem = proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.TCP);
                }

                foreach (string app in settings.excludes)
                {
                    BlackList.Add(new()
                    {
                        DisplayName = Path.GetFileNameWithoutExtension(app),
                        FullPath = app,
                    });
                }

                IPValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
                PortValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "port");

            }
            SetDescription();
        }

        private bool SaveSettings()
        {
            string dir = Path.Combine(StateHelper.GetDataDirectory(), StateHelper.StoreDirName, StateHelper.StoreItemsDirName, AddOnId);
            if (!Path.Exists(dir))
                return false; // TODO: show error message
            string settingsFile = Path.Combine(dir, "app-config.json");

            ProxiFyreSettings settings = new();
            

            if (!File.Exists(settingsFile))
            {

            }
            else
            {
                settings = Utils.LoadJson<ProxiFyreSettings>(settingsFile);
            }

            string readyToCompareIpAddr = GetProxyIpString(IPValue.Text, PortValue.Text);

            if (string.IsNullOrEmpty(readyToCompareIpAddr))
            {
                return false;
            }

            SettingsManager.Instance.SetValue("PROXY", "IPAddress", IPValue.Text);
            SettingsManager.Instance.SetValue("PROXY", "port", PortValue.Text);


            bool flag = false;
            if (settings.proxies != null)
            {
                foreach (var proxyGroup in settings.proxies)
                {
                    if (proxyGroup.socks5ProxyEndpoint == readyToCompareIpAddr)
                    {
                        proxyGroup.supportedProtocols = GetProtocolFromProxiFyreMode(((ProxiFyreModeElement)ProxiFyreModeComboBox.SelectedItem).Mode);
                        proxyGroup.appNames = GetAppNamesFromModel(WhiteList);
                        flag = true;
                    }
                }
            }
            if (!flag)
            {
                settings.proxies.Add(new()
                {
                    socks5ProxyEndpoint = readyToCompareIpAddr,
                    appNames = GetAppNamesFromModel(WhiteList),
                    supportedProtocols = GetProtocolFromProxiFyreMode(((ProxiFyreModeElement)ProxiFyreModeComboBox.SelectedItem).Mode)
                });
            }

            ProxiFyreSettings readyToSaveSettigs = new()
            {
                logLevel = settings.logLevel ?? "Error",
                proxies = settings.proxies,
                excludes = GetAppNamesFromModel(BlackList)
            };

            string jsonString = System.Text.Json.JsonSerializer.Serialize(readyToSaveSettigs);
            Logger.Instance.CreateDebugLog(nameof(ProxiFyreSetupPage), jsonString);
            File.WriteAllText(settingsFile, jsonString);

            SettingsManager.Instance.SetValue("PROXY", "proxyType", StateHelper.ProxySetupTypes.ProxiFyre.ToString());


            return true;
        }

        private List<string> GetAppNamesFromModel(List<ApplicationInfo> info)
        {
            List<string> apps = [];
            foreach (var app in info)
            {
                apps.Add(app.FullPath);
            }
            return apps;
        }

        private List<string> GetProtocolFromProxiFyreMode(ProxiFyreModes mode)
        {
            switch (mode)
            {
                case ProxiFyreModes.TCP:
                    return ["TCP"];
                case ProxiFyreModes.UDP:
                    return ["UDP"];
                default:
                    return ["TCP", "UDP"];
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            IPValue.Text = "127.0.0.1";
            PortValue.Text = "1080";
        }

        private void SetDescription()
        {
            string whiteListApps = string.Empty;

            int i = 0;
            foreach (var app in WhiteList)
            {
                if (i < 3)
                    if (WhiteList.Count <= 1 || i == 0)
                        whiteListApps += $"{app.DisplayName}";
                    else
                        whiteListApps += $", {app.DisplayName}";
                else
                {
                    whiteListApps += " " + string.Format(localizer.GetLocalizedString("AndMore"), WhiteList.Count - 3);
                    break;
                }
                i++;
            }

            string whiteListDescription = string.Format(localizer.GetLocalizedString("SelectedAppsSettingsCardDescription"), whiteListApps);
            if (WhiteList.Count > 0)
                ApplicationWhiteListCard.Description = whiteListDescription;
            else
                ApplicationWhiteListCard.Description = localizer.GetLocalizedString("ApplicationListIsEmpty");

            string blackListApps = string.Empty;

            int j = 0;
            foreach (var app in BlackList)
            {
                if (j < 3)
                    if (BlackList.Count <= 1 || j == 0)
                        blackListApps += $"{app.DisplayName}";
                    else
                        blackListApps += $", {app.DisplayName}";
                else
                {
                    blackListApps += " " + string.Format(localizer.GetLocalizedString("AndMore"), BlackList.Count - 3);
                    break;
                }
                j++;
            }

            string blackListDescription = string.Format(localizer.GetLocalizedString("SelectedAppsSettingsCardDescription"), blackListApps);
            if (BlackList.Count > 0)
                ApplicationBlackListCard.Description = blackListDescription;
            else
                ApplicationBlackListCard.Description = localizer.GetLocalizedString("ApplicationListIsEmpty");
        }

        private async void ApplicationBlackListCard_Click(object sender, RoutedEventArgs e)
        {
            SelectAppContentDialog dialog = new SelectAppContentDialog(BlackList);
            dialog.XamlRoot = this.XamlRoot;
            dialog.Title = string.Format(localizer.GetLocalizedString("EditApplicationListDialogTitle"), localizer.GetLocalizedString("ApplicationBlackList"));
            await dialog.ShowAsync();

            BlackList = dialog._applications.ToList();
            SetDescription();
        }

        private async void ApplicationWhiteListCard_Click(object sender, RoutedEventArgs e)
        {
            SelectAppContentDialog dialog = new SelectAppContentDialog(WhiteList);
            dialog.XamlRoot = this.XamlRoot;
            dialog.Title = string.Format(localizer.GetLocalizedString("EditApplicationListDialogTitle"), localizer.GetLocalizedString("ApplicationWhiteList"));
            await dialog.ShowAsync();

            WhiteList = dialog._applications.ToList();
            SetDescription();
        }
    }
}
