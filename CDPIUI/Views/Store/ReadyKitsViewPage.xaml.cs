using CDPIUI.Controls.Default;
using CDPIUI.Controls.Store;
using CDPIUI.Core.Store;
using CDPIUI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.Views.Store
{
    public sealed partial class ReadyKitsViewPage : TemplatePage
    {
        private readonly ILocalizer _localizer = Localizer.Get();

        public ObservableCollection<StoreViewBundleItem> Bundles { get; } = [];

        public ReadyKitsViewPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = PageTitleTextBlock;
            PageTitleTextBlock.Text = _localizer.GetLocalizedString("ReadyKitsPageTitle");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            bool loaded = await StoreHelper.Instance.LoadAllStoreDatabase(forseSync: false);
            if (!loaded)
            {
                ShowError(_localizer.GetLocalizedString("ReadyKitsLoadError"));
                return;
            }

            Bundles.Clear();
            foreach (StoreViewBundleItem bundle in StoreHelper.Instance.ReadyKits
                .OrderByDescending(kit => kit.IsRecommended)
                .Select(ReadyKitViewModelFactory.Create))
            {
                Bundles.Add(bundle);
            }

            if (Bundles.Count == 0)
            {
                ShowError(_localizer.GetLocalizedString("ReadyKitsEmptyMessage"));
                return;
            }

            LoadingGrid.Visibility = Visibility.Collapsed;
            ErrorGrid.Visibility = Visibility.Collapsed;
            StoreScrollViewer.Visibility = Visibility.Visible;
        }

        private void ReadyKitButton_Click(StoreReadyKitButton button)
        {
            PrepareToConnectedForwardAnimate(button.ImageElement);
            StoreWindow.Instance.NavigateSubPage(
                typeof(ReadyKitViewPage),
                new NameValueCollection { { "kitId", button.KitId } },
                new SuppressNavigationTransitionInfo());
        }

        private void ShowError(string message)
        {
            LoadingGrid.Visibility = Visibility.Collapsed;
            StoreScrollViewer.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = message;
            ErrorGrid.Visibility = Visibility.Visible;
        }
    }
}
