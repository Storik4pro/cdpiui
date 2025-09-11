using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.ViewModels;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.CreateConfigHelper
{
    public class ReportFileToEditModel
    {
        public string DisplayName { get; set; }
        public string PackName { get; set; }
        public string Directory { get; set; }
    }

    public sealed partial class RecentGoodCheckSelectionsContentDialog : ContentDialog
    {
        public ICommand SelectConfigCommand { get; }

        public string SelectedReport { get; private set; }
        public SelectResult SelectedResult { get; private set; } = SelectResult.Nothing;

        private ObservableCollection<ReportFileToEditModel> ReportModels = new();

        public RecentGoodCheckSelectionsContentDialog()
        {
            InitializeComponent();
            this.DataContext = this;

            SelectConfigCommand = new RelayCommand(p => ConfigSelected((Tuple<string, string>)p));
            ConfigsListView.ItemsSource = ReportModels;

            InitDialog();
        }

        private void InitDialog()
        {
            string localAppData = StateHelper.GetDataDirectory();
            string targetFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, StateHelper.Instance.FindKeyByValue("GoodCheck"), "Reports");

            ReportModels.Clear();
            GetReportFiles(targetFolder);

            Logger.Instance.CreateDebugLog(nameof(RecentGoodCheckSelectionsContentDialog), ReportModels.Count.ToString());

            if (ReportModels.Count == 0)
            {
                ReportErrorTextBlock.Visibility = Visibility.Visible;
                ConfigsScrollViewer.Visibility = Visibility.Collapsed;
            }
            else
            {
                ReportErrorTextBlock.Visibility= Visibility.Collapsed;
                ConfigsScrollViewer.Visibility= Visibility.Visible;
            }
        }

        private void GetReportFiles(string directoryPath, bool searchRecursively = false)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return;

            if (!Directory.Exists(directoryPath))
                return; 

            var option = searchRecursively ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var files = Directory.EnumerateFiles(directoryPath, "*.xml", option);

            foreach (var filePath in files)
            {
                DateTime creationTime;
                try
                {
                    creationTime = File.GetCreationTime(filePath);
                }
                catch
                {
                    creationTime = File.GetLastWriteTime(filePath);
                }

                var displayName = $"Report {creationTime.ToString("HH:mm dd.MM.yy")}";

                double sizeKb = 0;
                try
                {
                    var fi = new FileInfo(filePath);
                    sizeKb = fi.Length / 1024.0;
                }
                catch
                {
                    sizeKb = 0;
                }

                string packName = $"{sizeKb:F0} KB";

                ReportModels.Add(new ReportFileToEditModel
                {
                    DisplayName = displayName,
                    PackName = packName,
                    Directory = filePath
                });
            }
        }

        private void ConfigSelected(Tuple<string, string> tuple)
        {
            SelectedReport = tuple.Item1;
            SelectedResult = SelectResult.Selected;
            this.Hide();
        }


    }
}
