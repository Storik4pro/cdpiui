using CDPI_UI.Extensions;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigHelper;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WinUI3Localizer;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.CreateConfigHelper
{
    public enum SaveConfigKitAsModes
    {
        None,
        SaveForDistribute,
        SaveForMe,
    }

    public class SaveConfigKitDialogResultModel
    {
        public SaveConfigKitAsModes Mode { get; set; }
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public string Developer { get; set; }
        public string HexAccentColor { get; set; }
        public Uri ImageSourceUri { get; set; }
    }

    public sealed partial class SaveConfigKitAsContentDialog : ContentDialog
    {
        private string DisplayNameProperty = string.Empty;
        public string DisplayName { 
            get
            {
                return DisplayNameProperty;
            }
            set
            {
                DisplayNameProperty = value;
                if (!string.IsNullOrEmpty(value))
                {
                    DisplayNameTextBox.Text = value;
                    EasyModeDisplayNameTextBox.Text = value;
                }
            }
        }
        private readonly ILocalizer localizer = Localizer.Get();

        public SaveConfigKitDialogResultModel Result { get; private set; } = new();

        public Uri ImageSourceUri { get; private set; } = new Uri("ms-appx:///Assets/Store/empty.png");
        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            private set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(
                nameof(ImageSource), typeof(ImageSource), typeof(SaveConfigKitAsContentDialog), new PropertyMetadata(new BitmapImage(new Uri("ms-appx:///Assets/Store/empty.png")))
            );

        public SaveConfigKitAsContentDialog()
        {
            InitializeComponent();

            colorPicker.Color = UIHelper.HexToColorConverter("#FF932B");
            SaveButton.IsEnabled = CheckIsSaveAvailable();
            SelectorBar1.SelectedItem = SaveForMeSelectorBarItem;

            SetDefaultValues();

            SaveButton.IsEnabled = CheckIsSaveAvailable();
        }

        private void SetDefaultValues()
        {
            IdTextBox.Text = SettingsManager.Instance.GetValueOrDefault("CONFIGKIT", "lastUsedId", defaultValue: Utils.GenerateNewId());
            DisplayNameTextBox.Text = string.IsNullOrEmpty(DisplayName) ? SettingsManager.Instance.GetValueOrDefault<string>("CONFIGKIT", "lastUsedDisplayName", defaultValue:string.Empty) : DisplayName;
            EasyModeDisplayNameTextBox.Text = DisplayNameTextBox.Text;
            VersionTextBox.Text = "0.0.0";
            DeveloperTextBox.Text = SettingsManager.Instance.GetValueOrDefault("CONFIGKIT", "lastUsedDevName", defaultValue: Environment.UserName);
        }

        private void SelectorBar1_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if ((string)SelectorBar1.SelectedItem.Tag == "SaveForMe")
            {
                EasyModeGrid.Visibility = Visibility.Visible;
                DistributeGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                EasyModeGrid.Visibility = Visibility.Collapsed;
                DistributeGrid.Visibility = Visibility.Visible;
            }
            SaveButton.IsEnabled = CheckIsSaveAvailable();
        }

        private void GenerateNewId_Click(object sender, RoutedEventArgs e)
        {
            IdTextBox.Text = Utils.GenerateNewId();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result.Mode = SaveConfigKitAsModes.None;
            this.Hide();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(IdTextBox.Text)) SettingsManager.Instance.SetValue("CONFIGKIT", "lastUsedId", IdTextBox.Text);
            if (!string.IsNullOrEmpty(DisplayNameTextBox.Text)) SettingsManager.Instance.SetValue("CONFIGKIT", "lastUsedDisplayName", DisplayNameTextBox.Text);
            if (!string.IsNullOrEmpty(DeveloperTextBox.Text)) SettingsManager.Instance.SetValue("CONFIGKIT", "lastUsedDevName", DeveloperTextBox.Text);

            Result.Mode = (string)SelectorBar1.SelectedItem?.Tag == "SaveForDistribute" ? SaveConfigKitAsModes.SaveForDistribute : SaveConfigKitAsModes.SaveForMe;
            Result.Id = IdTextBox.Text;
            Result.DisplayName = (string)SelectorBar1.SelectedItem?.Tag == "SaveForDistribute" ? DisplayNameTextBox.Text : EasyModeDisplayNameTextBox.Text;
            Result.Version = VersionTextBox.Text;
            Result.Developer = DeveloperTextBox.Text;
            Result.HexAccentColor = colorPicker.Color.ColorToHex();
            Result.ImageSourceUri = ImageSourceUri;

            this.Hide();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Add help
        }

        private void EditImageButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = localizer.GetLocalizedString("ChooseAnImage");
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                openFileDialog.FilterIndex = 4;

                openFileDialog.Filter = $"PNG (*.png)|*.png";
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
            ImageSourceUri = new Uri(filePath);
            ImageSource = new BitmapImage(ImageSourceUri);
        }       

        private bool CheckIsSaveAvailable()
        {
            if ((string)SelectorBar1.SelectedItem?.Tag == "SaveForDistribute")
            {
                return
                    Utils.IsIdCorrect(IdTextBox.Text) &&
                    !string.IsNullOrEmpty(DisplayNameTextBox.Text) &&
                    Utils.IsVersionCorrect(VersionTextBox.Text) &&
                    !string.IsNullOrEmpty(DeveloperTextBox.Text);
            }
            else
            {
                return !string.IsNullOrWhiteSpace(EasyModeDisplayNameTextBox.Text);
            }
        }

        private void DistributeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = CheckIsSaveAvailable();
        }

        private void EasyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = CheckIsSaveAvailable();
        }
    }
}
