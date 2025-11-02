using CDPI_UI.Controls.Dialogs.CreateConfigHelper;
using CDPI_UI.Helper;
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.CreateConfigHelper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public class DragItem(string args, string siteListName, string directory, bool showGroupModeChooser, List<ChooseGroupModeContentDialog.ByeDPIGroupModes> modes, Guid guid) : INotifyPropertyChanged
    {
        public string Args { get; set; } = args;
        public string SiteListName { get; set; } = siteListName;
        public string Directory { get; set; } = directory;
        public Guid Id { get; } = guid;

        private bool showGroupModeChooser = showGroupModeChooser;
        public bool ShowGroupModeChooser {
            get => showGroupModeChooser; 
            set => SetField(ref showGroupModeChooser, value); 
        } 

        public List<ChooseGroupModeContentDialog.ByeDPIGroupModes> Modes { get; set; } = modes;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public override string ToString() => Args;
    }

    public sealed partial class GoodCheckConfigVisualEditorPage : Page
    {
        public ObservableCollection<DragItem> LeftItems { get; } = new ObservableCollection<DragItem>();
        public ObservableCollection<DragItem> RightItems { get; } = new ObservableCollection<DragItem>();

        private object _parameter = null;

        private DragItem _draggedItem;
        private string _dragSource;

        public string ComponentId { get; private set; } = string.Empty;

        public ICommand ChangeMode { get; }

        public GoodCheckConfigVisualEditorPage()
        {
            InitializeComponent();
            DataContext = this;

            LeftListView.ItemsSource = LeftItems;
            RightListView.ItemsSource = RightItems;

            RightItems.CollectionChanged += RightItems_CollectionChanged;

            ChangeMode = new RelayCommand(p => ChangeModeInModel((Tuple<string, List<ChooseGroupModeContentDialog.ByeDPIGroupModes>>)p));

            this.Loaded += GoodCheckConfigVisualEditorPage_Loaded;
            CheckIsContinuationPossible();
        }

        private void RightItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            CheckIsContinuationPossible();
        }

        private void GoodCheckConfigVisualEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_parameter is Tuple<string, List<DragItem>> items) 
            {
                foreach (var item in items.Item2)
                {
                    LeftItems.Add(item);
                }
            }
            this.Loaded -= GoodCheckConfigVisualEditorPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Tuple<string, List<DragItem>> items)
            {
                ComponentId = items.Item1;
                _parameter = items;

                if (ComponentId == StateHelper.Instance.FindKeyByValue("GoodbyeDPI"))
                {
                    WarningGrid.Visibility = Visibility.Visible;
                }
            }
        }

        public void CalcWidth()
        {
            LeftGrid.MaxWidth = RootPage.ActualWidth - 220;
        }

        private void RootPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CalcWidth();
        }

        private void LeftListView_Loaded(object sender, RoutedEventArgs e)
        {
            CalcWidth();
            LeftGrid.Width = (RootPage.ActualWidth - 40) / 2;
        }
        private void LeftListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            _draggedItem = e.Items[0] as DragItem;
            _dragSource = "Left";

            if (_draggedItem != null)
            {
                try
                {
                    e.Data.SetText(_draggedItem.Args.ToString());
                    e.Data.Properties.Title = _draggedItem.Id.ToString();
                    e.Data.RequestedOperation = DataPackageOperation.Copy;
                }
                catch
                {
                    _draggedItem = null;
                    _dragSource = null;
                }
            }
        }

        private void RightListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            _draggedItem = e.Items[0] as DragItem;
            _dragSource = "Right";

            if (_draggedItem != null)
            {
                try
                {
                    e.Data.SetText(_draggedItem.Args.ToString());
                    e.Data.Properties.Title = _draggedItem.Id.ToString();
                    e.Data.RequestedOperation = DataPackageOperation.Move;
                }
                catch
                {
                    _draggedItem = null;
                    _dragSource = null;
                }
            }
        }

        private void RightListView_DragOver(object sender, DragEventArgs e)
        {
            if (_dragSource == "Left")
            {
                if (ComponentId == StateHelper.Instance.FindKeyByValue("GoodbyeDPI") && RightItems.Count >= 1)
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                    return;
                }
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            else if (_dragSource == "Right")
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private void LeftListView_DragOver(object sender, DragEventArgs e)
        {
            if (_dragSource == "Right")
                e.AcceptedOperation = DataPackageOperation.Move;
            else
                e.AcceptedOperation = DataPackageOperation.None;
        }

        private void RightListView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string idText = e.DataView.Properties.Title;

                    if (ComponentId == StateHelper.Instance.FindKeyByValue("GoodbyeDPI") && RightItems.Count >= 1)
                    {
                        return;
                    }

                    if (Guid.TryParse(idText, out Guid parsedId) && _dragSource == "Left")
                    {
                        var sourceLeft = LeftItems.FirstOrDefault(x => x.Id == parsedId);
                        if (sourceLeft != null && RightItems.FirstOrDefault(x=> x.Id == parsedId) == null)
                        {
                            DragItem item = new(sourceLeft.Args,
                                sourceLeft.SiteListName,
                                sourceLeft.Directory,
                                StateHelper.Instance.FindKeyByValue("ByeDPI") == ComponentId,
                                [ChooseGroupModeContentDialog.ByeDPIGroupModes.None],
                                parsedId);
                            RightItems.Add(item);
                            CheckRightItemsSort();
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                _draggedItem = null;
                _dragSource = null;
            }
        }

        private void RightListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            CheckRightItemsSort();
        }

        private void CheckRightItemsSort()
        {
            if (StateHelper.Instance.FindKeyByValue("ByeDPI") != ComponentId) return;
            if (RightItems.Count > 0)
            {
                var firstItem = RightItems.First();
                firstItem.ShowGroupModeChooser = false;
                foreach (var item in RightItems)
                {
                    if (item != firstItem && item.ShowGroupModeChooser == false)
                        item.ShowGroupModeChooser = true;
                }
            }
        }

        private void LeftListView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string idText = e.DataView.Properties.Title;

                    if (Guid.TryParse(idText, out Guid parsedId) && _dragSource == "Right")
                    {
                        var toRemove = RightItems.FirstOrDefault(x => x.Id == parsedId);
                        if (toRemove != null)
                        {
                            RightItems.Remove(toRemove);
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                _draggedItem = null;
                _dragSource = null;
            }
        }

        private void ChangeModeInModel(Tuple<string, List<ChooseGroupModeContentDialog.ByeDPIGroupModes>> tuple)
        {
            var targetItem = RightItems.FirstOrDefault(x => x.Id.ToString() == tuple.Item1);
            if (targetItem != null)
            {
                targetItem.Modes = tuple.Item2;
            }
        }

        private void GoBackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private void GoForwardButton_Click(object sender, RoutedEventArgs e)
        {
            string startupString = string.Empty;

            if (StateHelper.Instance.FindKeyByValue("Zapret") == ComponentId)
            {
                foreach (var item in RightItems)
                {
                    string _str = "";
                    if (startupString.Length != 0) _str = "--new ";

                    _str += $"--hostlist=\"{Path.Combine(item.Directory, item.SiteListName)}\" {item.Args} ";
                    startupString += _str;
                }
            }
            else if (StateHelper.Instance.FindKeyByValue("ByeDPI") == ComponentId)
            {
                foreach (var item in RightItems)
                {
                    string _str = "";
                    if (startupString.Length != 0)
                    {
                        _str = $"-A{ConvertModesToString(item.Modes)} ";
                    }

                    _str += $"-H=\"{Path.Combine(item.Directory, item.SiteListName)}\" {Utils.ReplaseIp(item.Args)} ";
                    startupString += _str;
                }
            }
            else
            {
                foreach (var item in RightItems)
                {
                    string _str = "";

                    _str += $"--blacklist=\"{Path.Combine(item.Directory, item.SiteListName)}\" {Utils.ReplaseIp(item.Args)} ";
                    startupString += _str;
                }
            }
            Debug.WriteLine(startupString);

            Frame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGSTRING", startupString, ComponentId), new DrillInNavigationTransitionInfo());
        }

        private string ConvertModesToString(List<ChooseGroupModeContentDialog.ByeDPIGroupModes> modes)
        {
            string finalString = string.Empty;
            foreach (var mode in modes)
            {
                switch (mode)
                {
                    case ChooseGroupModeContentDialog.ByeDPIGroupModes.Torst:
                        finalString += "t";
                        break;
                    case ChooseGroupModeContentDialog.ByeDPIGroupModes.Redirect:
                        finalString += "r";
                        break;
                    case ChooseGroupModeContentDialog.ByeDPIGroupModes.SslErr:
                        finalString += "s";
                        break;
                    case ChooseGroupModeContentDialog.ByeDPIGroupModes.None:
                        finalString += "n";
                        break;
                }
                finalString += ",";
            }
            Debug.WriteLine(finalString.Length);
            return finalString.Length > 0 ? finalString.Remove(finalString.Length-1) : finalString;
        }

        private void CheckIsContinuationPossible()
        {
            if (RightItems.Count > 0)
            {
                GoForwardButton.IsEnabled = true;
            }
            else
            {
                GoForwardButton.IsEnabled = false;
            }
        }
    }
}
