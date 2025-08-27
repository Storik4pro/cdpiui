using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Items;
using GoodbyeDPI_UI.Helper.Static;
using GoodbyeDPI_UI.ViewModels;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Unidecode.NET;
using Windows.ApplicationModel.Chat;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Controls.Dialogs.CreateConfigHelper
{
    public class FileSelectModel
    {
        public string ConvertDirectoryPath { get; set; }
        public string FilePath { get; set; }
        public string AutoCorrectFilePath { get; set; }
    }
    public sealed partial class SelectUsedFilesForConfigContentDialog : ContentDialog
    {
        public ICommand ChangeFilePathCommand { get; }
        
        public Dictionary<string, string> Files = [];

        private List<string> _files;
        private string ConfigName;
        private string ConfigPath;

        private ObservableCollection<FileSelectModel> Models = [];

        public SelectUsedFilesForConfigContentDialog(List<string> files, string configName, string configPath)
        {
            InitializeComponent();
            this.DataContext = this;
            _files = files;
            ConfigName = configName;
            ConfigPath = configPath;

            ChangeFilePathCommand = new RelayCommand(p => ChangeFilePath((Tuple<string, string>)p));

            CreateModels();
            FilesListView.ItemsSource = Models;
        }

        private void CreateModels()
        {
            
            bool flag = true;

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;

            foreach (var file in _files)
            {
                if (File.Exists(file))
                {
                    continue;
                }
                string convDir;
                
                if (Path.GetExtension(file).Equals(".txt", StringComparison.CurrentCultureIgnoreCase))
                {
                    convDir = Path.Combine(
                        ConfigHelper.GetItemFolderFromPackId(StateHelper.LocalUserItemsId),
                        StateHelper.LocalUserItemSiteListsFolder,
                        $"{ConfigName.Unidecode()}_converted_{secondsSinceEpoch}");
                }
                else
                {
                    convDir = Path.Combine(
                        ConfigHelper.GetItemFolderFromPackId(StateHelper.LocalUserItemsId),
                        StateHelper.LocalUserItemBinsFolder,
                        $"{ConfigName.Unidecode()}_converted_{secondsSinceEpoch}");
                }
                if (!Directory.Exists(convDir))
                {
                    Directory.CreateDirectory(convDir);
                }
                string autoCorrectPath = FindAutoCorrectPath(file);
                Models.Add(new FileSelectModel()
                {
                    ConvertDirectoryPath = convDir,
                    FilePath = file,
                    AutoCorrectFilePath = autoCorrectPath
                });

                if (string.IsNullOrEmpty(autoCorrectPath))
                {
                    flag = false;
                }
            }

            IsPrimaryButtonEnabled = flag;

            
        }

        private string FindAutoCorrectPath(string filePath)
        {
            try
            {
                Debug.WriteLine($"{Path.Combine(ConfigPath, filePath)}");
                if (File.Exists(Path.Combine(Path.GetDirectoryName(ConfigPath), filePath)))
                {
                    return Path.Combine(Path.GetDirectoryName(ConfigPath), filePath);
                }
                // var items = DatabaseHelper.Instance.GetItemsByType("configlist");
                // TODO: search in store
            }
            catch
            {
                // pass
            }
            return string.Empty;
        }

        private void ChangeFilePath(Tuple<string, string> tuple)
        {
            var _m = Models.FirstOrDefault(m => m.FilePath == tuple.Item1);
            if (_m != null)
                Models.Remove(_m);

            _files.Remove(tuple.Item1);
            Files.Add(tuple.Item1, tuple.Item2);

            if (Models.Count == 0)
            {
                IsPrimaryButtonEnabled = true;
            }
        }

        private void RootDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Models.Count != 0)
            {
                foreach (var model in Models)
                {
                    try
                    {
                        File.Copy(model.AutoCorrectFilePath, Path.Combine(model.ConvertDirectoryPath, Path.GetFileName(model.FilePath)), true);
                        Files.Add(
                            model.FilePath,
                            "$GETCURRENTDIR()/" +
                            Path.Combine(
                                Utils.GetFolderNamesUpTo(
                                    model.ConvertDirectoryPath, StateHelper.LocalUserItemsId), Path.GetFileName(model.FilePath)
                                    )
                            );

                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            "ERR_AUTOCORRECT_IO:\n" + ex.Message, "Autocorrect Error",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
