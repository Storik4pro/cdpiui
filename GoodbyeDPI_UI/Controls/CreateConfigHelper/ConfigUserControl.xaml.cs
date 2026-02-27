using CDPI_UI.Controls.Store;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using RomanNumerals.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.CreateConfigHelper
{
    public enum OpenFileDirectoryTypes
    {
        OpenBinFilesDirectory,
        OpenSiteListFilesDirectory,
    }

    public sealed partial class ConfigUserControl : UserControl
    {
        #region Commands

        // OPEN FOLDER 

        public static readonly DependencyProperty OpenFileDirectoryCommandProperty =
            DependencyProperty.Register(
                nameof(OpenFileDirectoryCommand),
                typeof(ICommand),
                typeof(ConfigUserControl),
                new PropertyMetadata(null)
            );

        public ICommand OpenFileDirectoryCommand
        {
            get => (ICommand)GetValue(OpenFileDirectoryCommandProperty);
            set => SetValue(OpenFileDirectoryCommandProperty, value);
        }

        // EDIT 

        public static readonly DependencyProperty EditCommandProperty =
            DependencyProperty.Register(
                nameof(EditCommand),
                typeof(ICommand),
                typeof(ConfigUserControl),
                new PropertyMetadata(null)
            );

        public ICommand EditCommand
        {
            get => (ICommand)GetValue(EditCommandProperty);
            set => SetValue(EditCommandProperty, value);
        }

        // REMOVE

        public static readonly DependencyProperty RemoveCommandProperty =
            DependencyProperty.Register(
                nameof(RemoveCommand),
                typeof(ICommand),
                typeof(ConfigUserControl),
                new PropertyMetadata(null)
            );

        public ICommand RemoveCommand
        {
            get => (ICommand)GetValue(RemoveCommandProperty);
            set => SetValue(RemoveCommandProperty, value);
        }

        // RENAME

        public static readonly DependencyProperty RenameCommandProperty =
            DependencyProperty.Register(
                nameof(RenameCommand),
                typeof(ICommand),
                typeof(ConfigUserControl),
                new PropertyMetadata(null)
            );

        public ICommand RenameCommand
        {
            get => (ICommand)GetValue(RenameCommandProperty);
            set => SetValue(RenameCommandProperty, value);
        }

        #endregion

        #region Properties
        public string Guid
        {
            get { return (string)GetValue(GuidProperty); }
            set { SetValue(GuidProperty, value); }
        }

        public static readonly DependencyProperty GuidProperty =
            DependencyProperty.Register(
                nameof(Guid), typeof(string), typeof(ConfigUserControl), new PropertyMetadata(string.Empty)
            );

        public string TargetComponentId
        {
            get { return (string)GetValue(TargetComponentIdProperty); }
            set { 
                SetValue(TargetComponentIdProperty, value);
                ComponentNameTextBlock.Text = StateHelper.Instance.ComponentIdPairs.TryGetOrDefault(TargetComponentId);
            }
        }

        public static readonly DependencyProperty TargetComponentIdProperty =
            DependencyProperty.Register(
                nameof(TargetComponentId), typeof(string), typeof(ConfigUserControl), new PropertyMetadata(string.Empty)
            );
        public string DisplayName
        {
            get { return (string)GetValue(DisplayNameProperty); }
            set { 
                SetValue(DisplayNameProperty, value);
                RenameTextBox.Text = DisplayName;
            }
        }

        public static readonly DependencyProperty DisplayNameProperty =
            DependencyProperty.Register(
                nameof(DisplayName), typeof(string), typeof(ConfigUserControl), new PropertyMetadata(string.Empty)
            );

        public List<string> UsedSiteLists
        {
            get { return (List<string>)GetValue(UsedSiteListsProperty); }
            set { 
                SetValue(UsedSiteListsProperty, value);
                CreateSiteListInfo();
            }
        }

        public static readonly DependencyProperty UsedSiteListsProperty =
            DependencyProperty.Register(
                nameof(UsedSiteLists), typeof(List<string>), typeof(ConfigUserControl), new PropertyMetadata(new List<string>())
            );

        public List<string> ExcludedSiteLists
        {
            get { return (List<string>)GetValue(ExcludedSiteListsProperty); }
            set { 
                SetValue(ExcludedSiteListsProperty, value);
                CreateExcludedSiteListInfo();
            }
        }

        public static readonly DependencyProperty ExcludedSiteListsProperty =
            DependencyProperty.Register(
                nameof(ExcludedSiteLists), typeof(List<string>), typeof(ConfigUserControl), new PropertyMetadata(new List<string>())
            );

        public string LastEditTime
        {
            get { return (string)GetValue(LastEditTimeProperty); }
            set { 
                SetValue(LastEditTimeProperty, value);
                LastEditTextBlock.Text = string.Format(localizer.GetLocalizedString("LastEditTimeText"), LastEditTime);
            }
        }

        public static readonly DependencyProperty LastEditTimeProperty =
            DependencyProperty.Register(
                nameof(LastEditTime), typeof(string), typeof(ConfigUserControl), new PropertyMetadata(string.Empty)
            );
        #endregion

        private ILocalizer localizer = Localizer.Get();
        public ConfigUserControl()
        {
            InitializeComponent();
        }

        private void CreateSiteListInfo()
        {
            if (UsedSiteLists.Count > 1)
            {
                SitelistTextBlock.Text =
                    string.Format(localizer.GetLocalizedString("UsedSiteListsText"), UsedSiteLists[0], UsedSiteLists.Count - 1);
            }
            else if (UsedSiteLists.Count == 1)
            {
                SitelistTextBlock.Text =
                    string.Format(localizer.GetLocalizedString("UsedSiteListText"), UsedSiteLists[0]);
            }
            else
            {
                SitelistTextBlock.Text = localizer.GetLocalizedString("NoUsedSiteListText");
            }
        }
        private void CreateExcludedSiteListInfo()
        {
            if (ExcludedSiteLists.Count > 1)
            {
                ExcludedSitelistTextBlock.Text =
                    string.Format(localizer.GetLocalizedString("ExcludedSiteListsText"), ExcludedSiteLists[0], ExcludedSiteLists.Count - 1);
            }
            else if (ExcludedSiteLists.Count == 1)
            {
                ExcludedSitelistTextBlock.Text =
                    string.Format(localizer.GetLocalizedString("ExcludedSiteListText"), ExcludedSiteLists[0]);
            }
            else
            {
                ExcludedSitelistTextBlock.Text = localizer.GetLocalizedString("NoExcludedSiteListText");
            }
        }

        private void OpenDirectory(OpenFileDirectoryTypes operationType)
        {
            Tuple<string, OpenFileDirectoryTypes> parameter = Tuple.Create(Guid, operationType);

            if (OpenFileDirectoryCommand != null && OpenFileDirectoryCommand.CanExecute(parameter))
            {
                OpenFileDirectoryCommand.Execute(parameter);
            }
        }

        private void OpenListsConfigDirectory_Click(object sender, RoutedEventArgs e)
        {
            OpenDirectory(OpenFileDirectoryTypes.OpenSiteListFilesDirectory);
        }

        private void OpenBinsConfigDirectory_Click(object sender, RoutedEventArgs e)
        {
            OpenDirectory(OpenFileDirectoryTypes.OpenBinFilesDirectory);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Tuple<string> parameter = Tuple.Create(Guid);

            if (EditCommand != null && EditCommand.CanExecute(parameter))
            {
                EditCommand.Execute(parameter);
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            Tuple<string> parameter = Tuple.Create(Guid);

            if (RemoveCommand != null && RemoveCommand.CanExecute(parameter))
            {
                RemoveCommand.Execute(parameter);
            }
        }

        private void ApplyRenameButton_Click(object sender, RoutedEventArgs e)
        {
            RenameFlyout.Hide();

            Tuple<string, string> parameter = Tuple.Create(Guid, RenameTextBox.Text);

            if (RenameCommand != null && RenameCommand.CanExecute(parameter))
            {
                RenameCommand.Execute(parameter);
            }
        }

        private void RenameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RenameTextBox.Text))
            {
                ApplyRenameButton.IsEnabled = true;
            }
            else
            {
                ApplyRenameButton.IsEnabled = false;
            }
        }

        private void Flyout_Closed(object sender, object e)
        {
            RenameTextBox.Text = DisplayName;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenDirectory(OpenFileDirectoryTypes.OpenSiteListFilesDirectory);
        }
    }
}
