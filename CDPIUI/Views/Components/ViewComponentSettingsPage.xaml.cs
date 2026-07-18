using CDPI_UI.Controls.Dialogs.ComponentSettings;
using CDPI_UI.Controls.MainPage;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using CDPI_UI.Views.CreateConfigUtil;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xaml;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;
using static CDPI_UI.Helper.Static.UIHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Components
{
    public class ComponentPageNavigationModel
    {
        public string Id { get; set; }
        public Action<ComponentPageNavigationModel> GoBackSignal { get; set; }
    }
    public sealed partial class ViewComponentSettingsPage : Page
    {
        private string ComponentId = string.Empty;

        public Dictionary<string, Type> ComponentSettingsPageTypePairs = new()
        {
            { "CSTYFL050", typeof(TgWsProxyComponentPage) }
        };

        public ICommand ShowComponentSettingsClickCommand { get; }

        private bool IsAnimated = false;

        private ILocalizer localizer = Localizer.Get();

        public ViewComponentSettingsPage()
        {
            InitializeComponent();
            PageContentFrame.IsNavigationStackEnabled = false;

            ShowComponentSettingsClickCommand = new RelayCommand(p => NavigateBackWithParameter());
        }

        private void NavigateBackWithParameter()
        {
            if (IsAnimated)
            {
                var anim = ConnectedAnimationService.GetForCurrentView()
                        .PrepareToAnimate("BackwardConnectedAnimation", ComponentTileUserControl);

                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
                {
                    anim.Configuration = new BasicConnectedAnimationConfiguration();
                }
            }

            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void NavigateBack(ComponentPageNavigationModel model)
        {
            model.GoBackSignal -= NavigateBack;
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            IsAnimated = anim?.TryStart(ComponentTileUserControl) ?? false;

            if (e.Parameter is string id && !string.IsNullOrEmpty(id))
            {
                if (ComponentSettingsPageTypePairs.TryGetValue(id, out Type type))
                {
                    ComponentPageNavigationModel model = new()
                    {
                        Id = id,
                    };
                    model.GoBackSignal += NavigateBack;
                    PageContentFrame.Navigate(type, model);
                }
                else
                {
                    PageContentFrame.Navigate(typeof(DefaultComponentSettingsPage), e.Parameter);
                }

                ComponentId = id;
            }

            DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(ComponentId);
            string componentName = databaseStoreItem != null ? databaseStoreItem.ShortName : ComponentId;

            ComponentTileUserControl.StoreId = ComponentId;
            ComponentTileUserControl.CardTitle = componentName;

            ComponentTileUserControl.CardImageSource = new BitmapImage(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(databaseStoreItem?.IconPath)));
            ComponentTileUserControl.CardBackgroundColor = databaseStoreItem?.BackgroudColor ?? "#000000";
        }
    }
}
