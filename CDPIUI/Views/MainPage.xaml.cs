using CDPIUI.Controls.MainPage;
using CDPIUI.Default;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Static;
using CDPIUI.Helper.ViewModels;
using CDPIUI.ViewModels;
using CDPIUI.Views.Components;
using CDPIUI.Views.CreateConfigUtil;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Page = Microsoft.UI.Xaml.Controls.Page;
using UserControl = Microsoft.UI.Xaml.Controls.UserControl;


namespace CDPIUI.Views
{
    
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<ViewStoreItemModel> Components = [];
        
        private ILocalizer localizer = Localizer.Get();

        public ICommand ShowComponentSettingsClickCommand { get; }

        private FrameworkElement StoredElement;

        public MainPage()
        {
            this.InitializeComponent();
            this.DataContext = this;
            this.NavigationCacheMode = NavigationCacheMode.Enabled;

            MainListView.ItemsSource = Components;

            LoadComponents();
            ShowComponentSettingsClickCommand = new RelayCommand(p => ShowComponentSettings(p));

            StoreHelper.Instance.ItemActionsStopped += Instance_ItemActionsStopped;
            StoreHelper.Instance.ItemRemoved += Instance_ItemRemoved;
        }

        private void Instance_ItemRemoved(string obj)
        {
            LoadComponents();
        }

        private void Instance_ItemActionsStopped(string obj)
        {
            LoadComponents();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        private void LoadComponents()
        {
            Components.Clear();
            List<DatabaseStoreItem> installedComponents = DatabaseHelper.Instance.GetItemsByType("component");

            foreach (DatabaseStoreItem installedComponent in installedComponents)
            {
                Components.Add(new ViewStoreItemModel()
                {
                    StoreId = installedComponent.Id,
                    ImageSource = new BitmapImage(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(installedComponent.IconPath))),
                    Name = string.IsNullOrEmpty(installedComponent.ShortName) ? installedComponent.Id : installedComponent.ShortName,
                    ColorHEX = installedComponent.BackgroudColor
                });
            }

            if (Components.Count > 0)
            {
                ComponentTilePlaceholder.Visibility = Visibility.Collapsed;
                FlashlightContainer.Visibility = Visibility.Visible;
            }
            else
            {
                ComponentTilePlaceholder.Visibility = Visibility.Visible;
                FlashlightContainer.Visibility = Visibility.Collapsed;
            }

            
        }

        private void TryAnimate()
        {
            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackwardConnectedAnimation");
            if (StoredElement != null) anim?.TryStart(StoredElement);
            StoredElement = null;
        }

        private void ShowComponentSettings(object p)
        {
            var item = MainListView.ContainerFromItem((ViewStoreItemModel)p);
            if (item is FrameworkElement fw)
            {
                StoredElement = fw;
                var anim = ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate("ForwardConnectedAnimation", fw);

                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
                {
                    anim.Configuration = new BasicConnectedAnimationConfiguration();
                }
            }
            Frame.Navigate(typeof(ViewComponentSettingsPage), ((ViewStoreItemModel)p).StoreId, new DrillInNavigationTransitionInfo());
        }

        private void MainListView_Loaded(object sender, RoutedEventArgs e)
        {
            TryAnimate();
            FlashlightTipsWidgetUserControl.ConnectHandlers();
        }

        
    }
}
