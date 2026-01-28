using CDPI_UI.Controls.Dialogs.Store;
using CDPI_UI.Helper;
using CDPI_UI.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;
using ImageSource = Microsoft.UI.Xaml.Media.ImageSource;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Store
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    
    public class VersionControlModel
    {
        public string DisplayName { get; set; }
        public ImageSource DisplayImage { get; set; }
        public SupportedVersionControls Id { get; set; }
    }

    public sealed partial class SettingsPage : Page
    {
        private ILocalizer localizer = Localizer.Get();

        private readonly ObservableCollection<VersionControlModel> VersionControlModels = [];
        public SettingsPage()
        {
            InitializeComponent();

            VersionControlTypeComboBox.ItemsSource = VersionControlModels;
            LoadVersionControlInfo();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            VersionControlTypeComboBox.SelectionChanged -= VersionControlTypeComboBox_SelectionChanged;
        }

        private void LoadVersionControlInfo()
        {
            VersionControlModels.Clear();

            BitmapImage githubImage = new BitmapImage(new Uri("ms-appx:///Assets/Icons/github.png"));
            VersionControlModels.Add(new VersionControlModel
            { 
                DisplayImage = githubImage,
                DisplayName = localizer.GetLocalizedString("VersionControlGitHub"),
                Id = SupportedVersionControls.GitHub
            });
            BitmapImage gitlabImage = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Gitlab.png"));
            VersionControlModels.Add(new VersionControlModel
            { 
                DisplayImage = gitlabImage,
                DisplayName = localizer.GetLocalizedString("VersionControlGitLab"),
                Id = SupportedVersionControls.GitLab
            });

            VersionControlTypeComboBox.SelectedItem = VersionControlModels.FirstOrDefault(x => x.Id.ToString() == SettingsManager.Instance.GetValueOrDefault("STORE", "versionControlType", defaultValue: "GitHub"));

            VersionControlTypeComboBox.SelectionChanged += VersionControlTypeComboBox_SelectionChanged;
        }

        private void VersionControlTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InfoStackPanel.Visibility = Visibility.Visible;
            SettingsManager.Instance.SetValue("STORE", "versionControlType", ((VersionControlModel)VersionControlTypeComboBox.SelectedItem).Id.ToString());
            StoreHelper.ClearRepoCache();
        }

        private async void ManualItemInstallation_Click(object sender, RoutedEventArgs e)
        {
            LocalItemInstallingWarningContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Secondary)
            {
                string filePath;
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Title = localizer.GetLocalizedString("SelectLocalPackToInstall");
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                    openFileDialog.Filter = $"{localizer.GetLocalizedString("SignedPack")} (*.cdpisignedpack)|*.cdpisignedpack|" +
                        $"{localizer.GetLocalizedString("ConfigPack")} (*.cdpiconfigpack)|*.cdpiconfigpack|" +
                        $"{localizer.GetLocalizedString("Patch")} (*.cdpipatch)|*.cdpipatch|" +
                        $"{localizer.GetLocalizedString("AllSupported")} (*.cdpiconfigpack, *.cdpipatch, *.cdpisignedpack)|*.cdpiconfigpack;*.cdpipatch;*.cdpisignedpack";
                    openFileDialog.FilterIndex = 4;
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        filePath = openFileDialog.FileName;
                    }
                    else
                    {
                        return;
                    }
                }

                if (Path.GetExtension(filePath) == ".cdpipatch")
                {
                    ApplicationUpdateHelper.Instance.InstallApplicationUpdateFromFile(filePath);
                }
                else
                {
                    var window = await ((App)Application.Current).SafeCreateNewWindow<StoreLocalItemInstallingDialog>();
                    window.SetPackFilePath(filePath);
                }
            }
        }

        private void MemoryManagentSettingsCard_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
