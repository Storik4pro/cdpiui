using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
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
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.CreateConfigHelper
{
    public enum NavigationState
    {
        None,
        LoadFileFromPath,
        
    }
    public class GoodCheckReportHeaderModel
    {
        public int Id { get; set; }
        public string Directory { get; set; }
        public string FileName { get; set; }
        public string SuccessCount { get; set; }
        public string FailureCount { get; set; }
        public string FlagsCount { get; set; }
        public List<StrategyModel> Strategies { get; set; }
    }

    public sealed partial class ViewGoodCheckReportPage : Page
    {
        public ICommand HeaderClickCommand { get; }

        public string ComponentId { get; private set; }

        private ObservableCollection<GoodCheckReportHeaderModel> HeaderModels = [];

        private bool ErrorHappens = false;

        GoodCheckReportHeaderButton _storeditem;
        public ViewGoodCheckReportPage()
        {
            InitializeComponent();
            this.DataContext = this;
            this.NavigationCacheMode = NavigationCacheMode.Enabled;

            HeadersListView.ItemsSource = HeaderModels;

            HeaderClickCommand = new RelayCommand(p => OpenHeader((GoodCheckReportHeaderButton)p));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Tuple<NavigationState, string> fTuple)
            {
                var (navState, filePath) = fTuple;
                if (navState == NavigationState.LoadFileFromPath)
                {
                    LoadHeadersFromFile(filePath);
                }
            }
            else if (e.Parameter is List<StrategyModel> strategyModels) 
            {
                int flags = 0;
                foreach (var strategyModel in strategyModels)
                {
                    if (strategyModel.Flag) flags++;
                }
                if (_storeditem != null)
                {
                    _storeditem.FlagsCount = flags.ToString();
                    var m = HeaderModels.FirstOrDefault(b => b.Id == _storeditem.Id);
                    if (m != null)
                    {
                        m.Strategies = strategyModels;
                        m.FlagsCount = flags.ToString();
                    }
                }
            }

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackwardConnectedAnimation");
            if (anim != null)
            {
                anim.TryStart(_storeditem);
                _storeditem = null;
            }
        }

        private void LoadHeadersFromFile(string fileName)
        {
            ErrorHappens = false;
            HeaderModels.Clear();

            var (groups, componentId) = BasicGoodCheckHelper.LoadGroupsFromFile(fileName);
            ComponentId = componentId;

            if (groups == null)
            {
                // TODO: open error dialog
                return;
            }
            int ids = 0;
            foreach (var group in groups)
            {
                int successStrategies = 0;
                int failureStrategies = 0;
                int flagCount = 0;
                foreach (var strategy in group.Strategies)
                {
                    bool sResult = int.TryParse(strategy.Success, out var success);
                    bool aResult = float.TryParse(strategy.All, out var all);

                    if (!sResult || !aResult) 
                    {
                        ErrorHappens = true;
                        continue;
                    }

                    if (all != 0 && (success / (all / 100)) >= 65)
                    {
                        successStrategies++;
                    }
                    else
                    {
                        failureStrategies++;
                    }

                    if (strategy.Flag) 
                        flagCount++;

                }
                string dir = Path.GetDirectoryName(group.FullPath);
                HeaderModels.Add(new() 
                { 
                    Id = ids,
                    Directory = string.IsNullOrEmpty(dir)? group.FullPath : dir,
                    FileName = group.Name,
                    SuccessCount = successStrategies.ToString(),
                    FailureCount = failureStrategies.ToString(),
                    FlagsCount = flagCount.ToString(),
                    Strategies = group.Strategies,
                });
                ids++;
            }

        }

        private void OpenHeader(GoodCheckReportHeaderButton button)
        {
            _storeditem = button;

            List<StrategyModel> strategies = [];

            if (button.Id != null)
            {
                strategies = HeaderModels.FirstOrDefault(b => b.Id == button.Id).Strategies;
            }

            var anim = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", button);
            anim.Configuration = new DirectConnectedAnimationConfiguration();
            Frame.Navigate(typeof(ViewGoodCheckSiteListReportPage), Tuple.Create(button, strategies, ComponentId), new SuppressNavigationTransitionInfo());
        }

        private void Hyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            TipGrid.Visibility = Visibility.Collapsed;
        }

        private void GoForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (HeaderModels.FirstOrDefault(m => m.FlagsCount == "0") != null)
            {
                GoForwardButtonTeachingTip.IsOpen = true;
                return;
            }

            Frame.Navigate(typeof(GoodCheckConfigVisualEditorPage), Tuple.Create(ComponentId, CreateDragList()), new DrillInNavigationTransitionInfo());
        }

        private void GoForwardButtonTeachingTip_ActionButtonClick(TeachingTip sender, object args)
        {
            
            Frame.Navigate(typeof(GoodCheckConfigVisualEditorPage), Tuple.Create(ComponentId, CreateDragList()), new DrillInNavigationTransitionInfo());
        }

        private List<DragItem> CreateDragList()
        {
            List<DragItem> list = [];
            foreach (var item in HeaderModels)
            {
                string sitelistName = item.FileName;
                foreach (var strategy in item.Strategies)
                {
                    if (strategy.Flag)
                    {
                        list.Add(new(Utils.ReplaseIp(strategy.Strategy), sitelistName, item.Directory, false, [], Guid.NewGuid()));
                    }
                }
            }
            return list;
        }
    }
}
