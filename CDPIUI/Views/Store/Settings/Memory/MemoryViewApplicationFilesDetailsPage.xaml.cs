using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.System;
using CDPIUI.Helper.Parsers;

using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store.Settings.Memory
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MemoryViewApplicationFilesDetailsPage : TemplatePage
    {
        private ILocalizer localizer = Localizer.Get();
        public MemoryViewApplicationFilesDetailsPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = NavGrid;

            BreadcrumbBar.ItemsSource = BreadcrumbBarModels;

            CreateBreadcrumbBarNavigation();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string size)
            {
                MemoryTextBlock.Text = size;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            try
            {
                if (SettingsPage.MemoryNavigationSupportedPages.Contains(e.SourcePageType))
                {
                    PrepareToConnectedBackwardAnimate(NavGrid);
                }
            }
            catch { }
        }

        private ObservableCollection<BreadcrumbBarModel> BreadcrumbBarModels = [];
        public void CreateBreadcrumbBarNavigation()
        {
            BreadcrumbBarModels.Clear();
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("Settings"), Tag = typeof(SettingsPage)
            });
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("MemoryUsage"), Tag = typeof(MemoryViewPage)
            });
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("MemoryViewApplicationFilesDetails"), Tag = this.GetType()
            });

        }

        private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var item = (BreadcrumbBarModel)args.Item;
            Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void ViewAppDirButton_Click(object sender, RoutedEventArgs e)
        {
            ShellHelper.LookupDirectory(CDPIUI.Core.Data.Directories.CurrentDirectory);
        }
    }
}
