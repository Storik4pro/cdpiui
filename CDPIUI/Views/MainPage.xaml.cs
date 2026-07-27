using CDPIUI.Controls.MainPage;
using CDPIUI.ViewModels;
using CDPIUI.Views.Main.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Foundation.Metadata;
using WinUI3Localizer;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store;
using CDPIUI.Helper.LScript;
using CDPIUI.Controls.Default;
using CDPIUI.Helper;


namespace CDPIUI.Views
{
    
    public sealed partial class MainPage : TemplatePage
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

            IsBackwardAnimationToPageAvailable = true;
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
            StoredElement = null;
        }

        private void ShowComponentSettings(object p)
        {
            var item = MainListView.ContainerFromItem((ViewStoreItemModel)p);
            if (item is FrameworkElement fw)
            {
                StoredElement = fw;
                ElementToAnimateBackwardConnectedAnimation = StoredElement;
                PrepareToConnectedForwardAnimate(fw);
            }
            var nvc = new NameValueCollection
            {
                { "componentId", ((ViewStoreItemModel)p).StoreId }
            };
            Frame.Navigate(typeof(ViewComponentSettingsPage), nvc, new DrillInNavigationTransitionInfo());
        }

        private void MainListView_Loaded(object sender, RoutedEventArgs e)
        {
            TryAnimate();
            FlashlightTipsWidgetUserControl.ConnectHandlers();
        }

        
    }
}
