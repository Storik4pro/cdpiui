using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using CDPI_UI.Views.CreateConfigHelper;
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
using System.Text.RegularExpressions;
using System.Windows.Input;
using Unidecode.NET;
using Windows.ApplicationModel.Chat;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.CreateConfigHelper
{
    public enum CreateConfigResult
    {
        Selected,
        Canceled,
        Nothing
    }
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
        private AskAutoFillMode _askAutoFillMode;
        private ConfigItem ConfigItem;

        public CreateConfigResult Result { get; private set; } = CreateConfigResult.Nothing;

        private ObservableCollection<FileSelectModel> Models = [];

        public SelectUsedFilesForConfigContentDialog(List<string> files, string configName, string configPath, ConfigItem configItem, AskAutoFillMode askAutoFillMode)
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += SelectUsedFilesForConfigContentDialog_Loaded;

            _files = files;
            _askAutoFillMode = askAutoFillMode;
            ConfigName = configName;
            ConfigPath = configPath;
            ConfigItem = configItem;

            ChangeFilePathCommand = new RelayCommand(p => ChangeFilePath((Tuple<string, string>)p));

            CreateModels();
            FilesListView.ItemsSource = Models;

            
        }

        private void SelectUsedFilesForConfigContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (_askAutoFillMode == AskAutoFillMode.Quiet)
            {
                Result = CreateConfigResult.Selected;
                AutoCorrectActions();
                this.Hide();
            }
        }

        private void CreateModels()
        {
            
            bool flag = true;

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;

            foreach (var file in _files)
            {
                string convDir;
                
                if (Path.GetExtension(file).Equals(".txt", StringComparison.CurrentCultureIgnoreCase))
                {
                    convDir = Path.Combine(
                        ConfigHelper.GetItemFolderFromPackId(StateHelper.LocalUserItemsId),
                        StateHelper.LocalUserItemSiteListsFolder,
                        $"{ConfigName.Unidecode().Replace(" ", "_")}_converted_{secondsSinceEpoch}");
                }
                else
                {
                    convDir = Path.Combine(
                        ConfigHelper.GetItemFolderFromPackId(StateHelper.LocalUserItemsId),
                        StateHelper.LocalUserItemBinsFolder,
                        $"{ConfigName.Unidecode().Replace(" ", "_")}_converted_{secondsSinceEpoch}");
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
                if (string.Equals(Path.GetFileNameWithoutExtension(filePath), "autohostlist", StringComparison.OrdinalIgnoreCase))
                {
                    return filePath;
                }
                if (ConfigItem != null && ConfigItem.packId != null && ConfigItem.jparams != null)
                {
                    string _filePath = Helper.LScript.LScriptLangHelper.ExecuteScriptUnsafe(
                        ConfigHelper.ReplaceVariables(
                            filePath, ConfigHelper.GetReadyToUseVariables(ConfigItem.packId, ConfigItem.variables, ConfigItem.jparams)),
                        callItemId: ConfigItem.packId);

                    if (Path.Exists(_filePath))
                        return _filePath;
                }
                if (File.Exists(filePath))
                {
                    return filePath;
                }
                Debug.WriteLine($"{Path.Combine(ConfigPath, filePath)}");
                if (File.Exists(Path.Combine(Path.GetDirectoryName(ConfigPath), filePath)))
                {
                    return Path.Combine(Path.GetDirectoryName(ConfigPath), filePath);
                }

                var items = DatabaseHelper.Instance.GetItemsByType("configlist");
                foreach (var item in items)
                {
                    string itemPath = item.Directory;
                    var files = Directory.EnumerateFiles(itemPath, $"*{Path.GetExtension(filePath)}", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (Path.GetFileName(filePath) == Path.GetFileName(file))
                            return file;
                    }
                }
                var components = DatabaseHelper.Instance.GetItemsByType("component");
                foreach (var item in components)
                {
                    string itemPath = item.Directory;
                    var files = Directory.EnumerateFiles(itemPath, $"*{Path.GetExtension(filePath)}", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (Path.GetFileName(filePath) == Path.GetFileName(file))
                            return file;
                    }
                }
                var addOns = DatabaseHelper.Instance.GetItemsByType("addon");
                foreach (var item in addOns)
                {
                    string itemPath = item.Directory;
                    var files = Directory.EnumerateFiles(itemPath, $"*{Path.GetExtension(filePath)}", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        
                        if (Path.GetFileName(filePath) == Path.GetFileName(file))
                            return file;
                    }
                }
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
            Result = CreateConfigResult.Selected;
            if (Models.Count != 0)
            {
                AutoCorrectActions();
            }
        }
        private ILocalizer localizer = Localizer.Get();
        private async void ShowDialog(string message)
        {
            var dlg = new MessageDialog(message, localizer.GetLocalizedString("AutoCorrectError"));
            InitializeWithWindow.Initialize(dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>()));
            await dlg.ShowAsync();
        }

        private void AutoCorrectActions()
        {
            foreach (var model in Models)
            {
                try
                {
                    string filepath = Regex.Replace(model.FilePath, @"%(?<name>[A-Za-z0-9_]+)%", "");
                    Logger.Instance.CreateDebugLog(nameof(SelectUsedFilesForConfigContentDialog), $"{filepath}");

                    if (!string.Equals(Path.GetFileNameWithoutExtension(model.AutoCorrectFilePath), "autohostlist", StringComparison.OrdinalIgnoreCase))
                        File.Copy(model.AutoCorrectFilePath, Path.Combine(model.ConvertDirectoryPath, Path.GetFileName(filepath)), true);

                    Files.Add(
                        model.FilePath,
                        "$GETCURRENTDIR()/" +
                        Path.Combine(
                            Utils.GetFolderNamesUpTo(
                                model.ConvertDirectoryPath, StateHelper.LocalUserItemsId), Path.GetFileName(filepath)
                                )
                        );

                    Logger.Instance.CreateDebugLog(nameof(SelectUsedFilesForConfigContentDialog), $"{model.FilePath}");

                }
                catch (Exception ex)
                {
                    ShowDialog("ERR_AUTOCORRECT_IO:\n" + ex.Message);
                    Result = CreateConfigResult.Canceled;
                    return;
                }
            }
        }
    }
}
