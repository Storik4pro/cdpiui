using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    public sealed partial class GoodCheckSitelistStrategyChooserControl : UserControl
    {
        public Action<Tuple<string, string>> StrategyChanged;

        public string FilePath = string.Empty;
        private List<GoodCheckStrategiesList> _strategiesLists = new List<GoodCheckStrategiesList>();
        public List<GoodCheckStrategiesList> StrategiesLists
        {
            get => _strategiesLists;
            set
            {
                if (ReferenceEquals(_strategiesLists, value)) return;
                _strategiesLists = value ?? new List<GoodCheckStrategiesList>();
                SyncListToObservable();
            }
        }

        private readonly ObservableCollection<GoodCheckStrategiesList> _observable = new ObservableCollection<GoodCheckStrategiesList>();
        public GoodCheckSitelistStrategyChooserControl()
        {
            InitializeComponent();

            StrategyChooseCombobox.ItemsSource = _observable;

            StrategyChooseCombobox.SelectedIndex = 0;
        }

        public void RefreshFromList()
        {
            SyncListToObservable();
        }

        private void SyncListToObservable()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _observable.Clear();
                foreach (var item in _strategiesLists)
                    _observable.Add(item);

                if (_observable.Count == 0)
                    StrategyChooseCombobox.SelectedIndex = -1;
                else if (StrategyChooseCombobox.SelectedIndex < 0 || StrategyChooseCombobox.SelectedIndex >= _observable.Count)
                    StrategyChooseCombobox.SelectedIndex = 0;
            });

        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(GoodCheckSitelistStrategyChooserControl), new PropertyMetadata(string.Empty)
            );

        public string PackName
        {
            get { return (string)GetValue(PackNameProperty); }
            set { SetValue(PackNameProperty, value); }
        }

        public static readonly DependencyProperty PackNameProperty =
            DependencyProperty.Register(
                nameof(PackName), typeof(string), typeof(GoodCheckSitelistStrategyChooserControl), new PropertyMetadata(string.Empty)
            );

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Utils.OpenFileInDefaultApp(FilePath);
        }

        private void StrategyChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StrategyChanged?.Invoke(Tuple.Create(FilePath, StrategiesLists[StrategyChooseCombobox.SelectedIndex].FilePath));
        }
    }
}
