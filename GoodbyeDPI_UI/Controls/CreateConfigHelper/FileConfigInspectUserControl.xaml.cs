using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigHelper;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Forms;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using UserControl = Microsoft.UI.Xaml.Controls.UserControl;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls
{
    public sealed partial class FileConfigInspectUserControl : UserControl
    {
        public static readonly DependencyProperty ChangeFilePathCommandProperty =
            DependencyProperty.Register(
                nameof(ChangeFilePathCommand),
                typeof(ICommand),
                typeof(FileConfigInspectUserControl),
                new PropertyMetadata(null)
            );

        public ICommand ChangeFilePathCommand
        {
            get => (ICommand)GetValue(ChangeFilePathCommandProperty);
            set => SetValue(ChangeFilePathCommandProperty, value);
        }

        public static readonly DependencyProperty ChangeFilePathCommandParameterProperty =
            DependencyProperty.Register(
                nameof(ChangeFilePathCommandParameter),
                typeof(object),
                typeof(FileConfigInspectUserControl),
                new PropertyMetadata(null)
            );

        public object ChangeFilePathCommandParameter
        {
            get => GetValue(ChangeFilePathCommandParameterProperty);
            set => SetValue(ChangeFilePathCommandParameterProperty, value);
        }
        public FileConfigInspectUserControl()
        {
            InitializeComponent();
        }

        public string ConvertFolderPath
        {
            get { return (string)GetValue(ConvertFolderPathProperty); }
            set { SetValue(ConvertFolderPathProperty, value); }
        }

        public static readonly DependencyProperty ConvertFolderPathProperty =
            DependencyProperty.Register(
                nameof(ConvertFolderPath), typeof(string), typeof(FileConfigInspectUserControl), new PropertyMetadata(string.Empty)
            );

        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(
                nameof(FilePath), typeof(string), typeof(FileConfigInspectUserControl), new PropertyMetadata(string.Empty)
            );

        public string AutoCorrectFilePath
        {
            get { return (string)GetValue(AutoCorrectFilePathProperty); }
            set { 
                SetValue(AutoCorrectFilePathProperty, value);
                AutoCorrectStackPanel.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
                ApplyAutoCorrectButton.IsEnabled = !string.IsNullOrEmpty(value);
            }
        }

        public static readonly DependencyProperty AutoCorrectFilePathProperty =
            DependencyProperty.Register(
                nameof(AutoCorrectFilePath), typeof(string), typeof(FileConfigInspectUserControl), new PropertyMetadata(string.Empty)
            );

        private void Hyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            Utils.OpenFileInDefaultApp(AutoCorrectFilePath);
        }

        private void ApplyAutoCorrectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                File.Copy(AutoCorrectFilePath, Path.Combine(ConvertFolderPath, Path.GetFileName(FilePath)), true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "ERR_AUTOCORRECT_IO:\n" + ex.Message, "Autocorrect Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            string lastSegment = Utils.GetFolderNamesUpTo(ConvertFolderPath, StateHelper.LocalUserItemsId);

            ChangeFilePathCommandParameter = Tuple.Create(FilePath, $"$GETCURRENTDIR()/{lastSegment}/{Path.GetFileName(FilePath)}");
            if (ChangeFilePathCommand != null && ChangeFilePathCommand.CanExecute(ChangeFilePathCommandParameter))
            {
                ChangeFilePathCommand.Execute(ChangeFilePathCommandParameter);
                return;
            }
        }

        private void SelectNewFile_Click(object sender, RoutedEventArgs e)
        {
            using (OpenFileDialog openFileDialog = new())
            {
                openFileDialog.Title = "Choose file";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                openFileDialog.FilterIndex = 0;

                openFileDialog.Filter = Path.GetExtension(FilePath) == ".txt" ? "TXT files (*.txt)|*.txt" :
                    Path.GetExtension(FilePath) == ".txt" ? "BIN files (*.bin)|*.bin" : "All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = false;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.Copy(openFileDialog.FileName, Path.Combine(ConvertFolderPath, Path.GetFileName(FilePath)), true);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            "ERR_FILESELECT_IO:\n" + ex.Message, "Autocorrect Error",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return;
                    }
                    string lastSegment = Utils.GetFolderNamesUpTo(ConvertFolderPath, StateHelper.LocalUserItemsId);

                    ChangeFilePathCommandParameter = Tuple.Create(FilePath, $"$GETCURRENTDIR()/{lastSegment}/{Path.GetFileName(openFileDialog.FileName)}");
                    if (ChangeFilePathCommand != null && ChangeFilePathCommand.CanExecute(ChangeFilePathCommandParameter))
                    {
                        ChangeFilePathCommand.Execute(ChangeFilePathCommandParameter);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }
    }
}
