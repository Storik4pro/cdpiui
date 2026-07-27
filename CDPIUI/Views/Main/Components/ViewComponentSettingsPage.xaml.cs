using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.MainPage;

using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigUtil;
using CDPIUI.Views.Main.Components;
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
using System.Collections.Specialized;
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
using static CDPIUI.Helper.UIHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Main.Components
{
    public class ComponentPageNavigationModel
    {
        public string Id { get; set; }
        public Action<ComponentPageNavigationModel> GoBackSignal { get; set; }
    }
    public sealed partial class ViewComponentSettingsPage : TemplatePage
    {
        private string ComponentId = string.Empty;

        public Dictionary<string, Type> ComponentSettingsPageTypePairs = new()
        {
            { "CSTYFL050", typeof(TgWsProxyComponentPage) }
        };

        public ICommand ShowComponentSettingsClickCommand { get; }

        private ILocalizer localizer = Localizer.Get();

        public ViewComponentSettingsPage()
        {
            InitializeComponent();
            PageContentFrame.IsNavigationStackEnabled = false;

            ShowComponentSettingsClickCommand = new RelayCommand(p => NavigateBackWithParameter());

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = ComponentTileUserControl;
        }

        private void NavigateBackWithParameter()
        {
            if (IsAnimated)
            {
                PrepareToConnectedBackwardAnimate(ComponentTileUserControl);
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

            if (Parameter != null)
            {
                var id = Parameter.Get("componentId");
                if (ComponentSettingsPageTypePairs.TryGetValue(id, out Type type))
                {
                    ComponentPageNavigationModel model = new()
                    {
                        Id = id,
                    };
                    model.GoBackSignal += NavigateBack;
                    PageContentFrame.Navigate(type, new NameValueCollection() { { "model", JSONConvertor.SerializeObject(model) } });
                }
                else
                {
                    PageContentFrame.Navigate(typeof(DefaultComponentSettingsPage), Parameter);
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
