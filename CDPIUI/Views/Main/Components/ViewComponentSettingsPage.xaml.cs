using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Helper.AddOns.ConfigShare;
using CDPIUI.Messages;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
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

        private bool importingDrop;

        private void RootGrid_DragOver(object sender, DragEventArgs e)
        {
            if (importingDrop || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = localizer.GetLocalizedString("ComponentDropImportCaption");
            e.DragUIOverride.IsCaptionVisible = true;
            e.Handled = true;

            DragGrid.Visibility = Visibility.Visible;
        }

        private async void RootGrid_Drop(object sender, DragEventArgs e)
        {
            DragGrid.Visibility = Visibility.Collapsed;

            if (importingDrop || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            e.Handled = true;
            var deferral = e.GetDeferral();
            importingDrop = true;
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (!IsLoaded) return;
                if (items.Any(item => item is not StorageFile))
                    throw new InvalidOperationException(localizer.GetLocalizedString("ComponentDropFilesOnly"));
                var paths = items.Select(item => item.Path).ToArray();
                if (paths.Length == 0) return;
                var app = (App)Application.Current;
                if (paths.Length != 1 || !string.Equals(Path.GetExtension(paths[0]), ".cdpiconfig", StringComparison.OrdinalIgnoreCase))
                {
                    var importer = await app.UnsafeCreateNewWindow<ConfigImportUtilWindow>(activate: false, id: Guid.NewGuid().ToString());
                    importer.ImportFiles(paths, ComponentId);
                    App.ActivateWindow(importer);
                    return;
                }
                if (SettingsManager.Instance.GetValueOrDefault<bool>("CONFIGSHARE", "AlwaysAskIfDragNDropConfigAdded", defaultValue: true))
                {
                    var owner = app.OpenWindows.FirstOrDefault(window => window.Content?.XamlRoot == XamlRoot);
                    var dialog = await app.UnsafeCreateNewWindow<ConfigShareImportDialog>(activate: false, id: Guid.NewGuid().ToString());
                    dialog.ConfigureForDragDrop(owner);
                    App.ActivateWindow(dialog);
                    await dialog.SetFileAsync(paths[0]);
                }
                else
                {
                    var service = new ConfigShareService();
                    using var package = await service.ReadAsync(paths[0]);
                    await service.InstallAsync(package, package.Manifest.Name);
                    string targetId = ConfigShareService.GetInstalledComponentId(package.Config);
                    if (targetId != null)
                        ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(targetId)?.ReInitConfigs();
                }
            }
            catch (Exception exception)
            {
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ViewComponentSettingsPage), exception.ToString());
                if (IsLoaded)
                    await new ErrorContentDialog().ShowErrorDialogAsync(
                        localizer.GetLocalizedString("ConfigShareError"), ConfigShareUI.ErrorText(exception), XamlRoot);
            }
            finally
            {
                importingDrop = false;
                deferral.Complete();
            }
        }

        private void RootGrid_DragLeave(object sender, DragEventArgs e)
        {
            DragGrid.Visibility = Visibility.Collapsed;
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
                    PageContentFrame.Navigate(type, model);
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
