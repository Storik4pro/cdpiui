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
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views.CreateConfigHelper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public class DragItem
    {
        public string Args { get; set; }
        public string SiteListName { get; set; }
        public string Directory { get; set; }
        public Guid Id { get; }

        public DragItem(string args, string siteListName, string directory)
        {
            Args = args;
            Id = Guid.NewGuid();
            SiteListName = siteListName;
            Directory = directory;
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

        public GoodCheckConfigVisualEditorPage()
        {
            InitializeComponent();
            DataContext = this;

            LeftListView.ItemsSource = LeftItems;
            RightListView.ItemsSource = RightItems;

            this.Loaded += GoodCheckConfigVisualEditorPage_Loaded;
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
                    e.Data.SetText(_draggedItem.Id.ToString());
                    e.Data.Properties.Title = _draggedItem.Args;
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
                    e.Data.SetText(_draggedItem.Id.ToString());
                    e.Data.Properties.Title = _draggedItem.Args;
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
                e.AcceptedOperation = DataPackageOperation.Copy;
            else if (_dragSource == "Right")
                e.AcceptedOperation = DataPackageOperation.Move;
            else
                e.AcceptedOperation = DataPackageOperation.None;
        }

        private void LeftListView_DragOver(object sender, DragEventArgs e)
        {
            if (_dragSource == "Right")
                e.AcceptedOperation = DataPackageOperation.Move;
            else
                e.AcceptedOperation = DataPackageOperation.None;
        }

        private async void RightListView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string idText = await e.DataView.GetTextAsync();

                    if (Guid.TryParse(idText, out Guid parsedId) && _dragSource == "Left")
                    {
                        var sourceLeft = LeftItems.FirstOrDefault(x => x.Id == parsedId);
                        if (sourceLeft != null)
                        {
                            RightItems.Add(new DragItem(sourceLeft.Args, sourceLeft.SiteListName, sourceLeft.Directory));
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

        private async void LeftListView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string idText = await e.DataView.GetTextAsync();

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

        private void GoBackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private void GoForwardButton_Click(object sender, RoutedEventArgs e)
        {
            string startupString = string.Empty;

            foreach (var item in RightItems)
            {
                string _str = "";
                if (startupString.Length != 0) _str = "--new ";

                _str += $"--hostlist={Path.Combine(item.Directory, item.SiteListName)} {item.Args} ";
                startupString += _str;
            }


            Frame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGSTRING", startupString, ComponentId), new DrillInNavigationTransitionInfo());
        }
    }
}
