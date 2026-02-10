using CDPI_UI.Extensions;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.Store.Settings;
using CDPI_UI.Views.Store.Settings.Memory;
using CommunityToolkit.WinUI.Controls;
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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Store.Settings
{
    

    public enum MemoryUsageCategories
    {
        Application,
        Settings,
        Logs,
        Temp,
        StoreItems,
        StoreCache,
    }

    public class MemoryViewItemModel : IComparable
    {
        public MemoryUsageCategories Category { get; set; }

        public string DisplayName { get; set; }
        public string DisplayDescription { get; set; }
        public string DisplayMemoryUsageText { get; set; }
        public string IconGlyph { get; set; }
        public double MaxValue { get; set; }
        public double CurrentValue { get; set; }

        public bool IsClickEnabled { get; set; }

        public int CompareTo(object o)
        {
            MemoryViewItemModel a = this;
            MemoryViewItemModel b = (MemoryViewItemModel)o;
            if (a.CurrentValue > b.CurrentValue) return -1;
            else if (a.CurrentValue < b.CurrentValue) return 1;
            else return 0;
        }

    }

    public sealed partial class MemoryDiskUsageViewUserControl : UserControl
    {
        private ILocalizer localizer = Localizer.Get();

        private ObservableCollection<MemoryViewItemModel> MemoryViewItemModels { get; set; } = [];

        private Dictionary<MemoryUsageCategories, string> CategoryGlyphPairs = new()
        {
            { MemoryUsageCategories.Application, "\uE74C" },
            { MemoryUsageCategories.Settings, "\uE713" },
            { MemoryUsageCategories.Logs, "\uEBE8" },
            { MemoryUsageCategories.Temp, "\uE74D" },
            { MemoryUsageCategories.StoreItems, "\uE71D" },
            { MemoryUsageCategories.StoreCache, "\uE719" },
        };

        private Dictionary<MemoryUsageCategories, Type> CategoryPageTypePairs = new()
        {
            { MemoryUsageCategories.Application, typeof(MemoryViewApplicationFilesDetailsPage) },
            { MemoryUsageCategories.Settings, typeof(MemoryViewSettingsDetailsPage) },
            { MemoryUsageCategories.Logs, typeof(MemoryViewLogsDetailsPage) },
            { MemoryUsageCategories.Temp, typeof(MemoryViewApplicationFilesDetailsPage) },
            { MemoryUsageCategories.StoreItems, typeof(MemoryViewInstalledItemsDetailsPage) },
            { MemoryUsageCategories.StoreCache, typeof(MemoryViewStoreCachePage) },
        };

        public MemoryDiskUsageViewUserControl()
        {
            InitializeComponent();

            MemoryCategoriesListView.ItemsSource = MemoryViewItemModels;

            
        }

        public string DiskLetter
        {
            get { return (string)GetValue(DiskLetterProperty); }
            set { 
                SetValue(DiskLetterProperty, value);
                Init();
            }
        }

        public static readonly DependencyProperty DiskLetterProperty =
            DependencyProperty.Register(
                nameof(DiskLetter), typeof(string), typeof(MemoryDiskUsageViewUserControl), new PropertyMetadata(string.Empty)
            );

        public async void Init()
        {
            MemoryViewItemModels.Clear();
            string appDir = StateHelper.Instance.workDirectory;
            string dataDir = StateHelper.GetDataDirectory();

            DriveInfo appDirDriveInfo = new(appDir);
            DriveInfo dataDirDriveInfo = new(dataDir);

            DriveInfo usedDriveInfo;
            string usedDir;

            long appDirSize;

            if (Path.Combine(appDir) == Path.Combine(dataDir) && appDirDriveInfo.VolumeLabel == DiskLetter)
            {
                appDirSize = await Utils.GetDirectorySize(appDir);
                usedDriveInfo = appDirDriveInfo;
                usedDir = appDir;
            }
            else if (appDirDriveInfo.VolumeLabel == dataDirDriveInfo.VolumeLabel && appDirDriveInfo.VolumeLabel == DiskLetter)
            {
                appDirSize = await Utils.GetDirectorySize(appDir) + await Utils.GetDirectorySize(dataDir);
                usedDriveInfo = appDirDriveInfo;
                usedDir = dataDir;
                CreateTileForMemoryUsageCategory(await Utils.GetDirectorySize(appDir), appDirSize, MemoryUsageCategories.Application);
            }
            else if (appDirDriveInfo.VolumeLabel == DiskLetter)
            {
                appDirSize = await Utils.GetDirectorySize(appDir);
                usedDriveInfo = appDirDriveInfo;
                usedDir = appDir;
            }
            else if (dataDirDriveInfo.VolumeLabel == DiskLetter)
            {
                appDirSize = await Utils.GetDirectorySize(dataDir);
                usedDriveInfo = dataDirDriveInfo;
                usedDir = dataDir;
            }
            else
            {
                return;
            }

            long totalFreeMemory = usedDriveInfo.AvailableFreeSpace;

            DiskNameTextBlock.Text = string.Format(
                localizer.GetLocalizedString("MemoryDiskName"),
                string.IsNullOrEmpty(usedDriveInfo.VolumeLabel) ? localizer.GetLocalizedString("LocalDrive") : usedDriveInfo.VolumeLabel, 
                usedDriveInfo.Name);

            Logger.Instance.CreateInfoLog(nameof(MemoryDiskUsageViewUserControl), $"Disk is {usedDriveInfo.Name}, {string.IsNullOrEmpty(usedDriveInfo.Name)}");

            CreateTotalStatistic(appDirSize, totalFreeMemory);

            await CreateTilesForCategories(usedDir, appDirSize);

            MemoryViewItemModels.Sort();
        }

        public void CreateTotalStatistic(long dirSize, long totalFreeMemory)
        {
            long totalBytesSize = (long)totalFreeMemory + dirSize;

            MainProgressBar.Maximum = totalBytesSize;
            MainProgressBar.Value = dirSize;

            UsedMemoryTextBlock.Text = string.Format(localizer.GetLocalizedString("UsedMemory"), Utils.FormatSize(dirSize));
            FreeMemoryTextBlock.Text = string.Format(localizer.GetLocalizedString("FreeMemory"), Utils.FormatSize((long)totalFreeMemory));
        }

        private async Task CreateTilesForCategories(string directory, long totalSize)
        {
            long parentMem = 0;
            long otherSize = totalSize;
            DirectoryInfo dirInfo = new DirectoryInfo(directory);
            foreach (var dir in dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                string relpath = Path.GetRelativePath(directory, dir.FullName);
                long size = await Utils.GetDirectorySize(dir.FullName);

                otherSize -= size;

                if (relpath.StartsWith("Settings"))
                {
                    CreateTileForMemoryUsageCategory(size, totalSize, MemoryUsageCategories.Settings);
                    
                }
                else if (relpath.StartsWith("Logs"))
                {
                    CreateTileForMemoryUsageCategory(size, totalSize, MemoryUsageCategories.Logs);
                }
                else if (relpath.StartsWith("Store"))
                {
                    foreach (var storeDir in dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        string st_relpath = Path.GetRelativePath(dir.FullName, storeDir.FullName);
                        long st_size = await Utils.GetDirectorySize(storeDir.FullName);
                        if (st_relpath.StartsWith("Items"))
                        {
                            CreateTileForMemoryUsageCategory(st_size, totalSize, MemoryUsageCategories.StoreItems);
                        }
                        else if (st_relpath.StartsWith("Cache"))
                        {
                            CreateTileForMemoryUsageCategory(st_size, totalSize, MemoryUsageCategories.StoreCache);
                        }
                        else
                        {
                            parentMem += st_size;
                        }
                    }
                }
                else if (relpath.StartsWith("TempFiles/"))
                {
                    CreateTileForMemoryUsageCategory(size, totalSize, MemoryUsageCategories.Temp);
                }
                else
                {
                    parentMem += size;
                }
            }

            CreateTileForMemoryUsageCategory(parentMem + otherSize, totalSize, MemoryUsageCategories.Application);
        }

        private void CreateTileForMemoryUsageCategory(long size, long totalSize, MemoryUsageCategories category)
        {
            if (size == 0) return;
            MemoryViewItemModels.Add(new MemoryViewItemModel
            {
                Category = category,
                IsClickEnabled = true,

                DisplayName = localizer.GetLocalizedString($"MemoryUsageCategory{category}DisplayName"),
                DisplayDescription = localizer.GetLocalizedString($"MemoryUsageCategory{category}Description"),
                DisplayMemoryUsageText = $"{Utils.FormatSize(size)}/{Utils.FormatSize(totalSize)}",
                IconGlyph = CategoryGlyphPairs.GetValueOrDefault(category),

                CurrentValue = size,
                MaxValue = totalSize
            });
        }



        private async void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard settingsCard)
            {
                if (settingsCard.Tag is MemoryUsageCategories category)
                {
                    var window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
                    window.NavigateSubPage(CategoryPageTypePairs.GetValueOrDefault(category), Utils.FormatSize((long)MemoryViewItemModels.FirstOrDefault(x => x.Category == category)?.CurrentValue), new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                }
            }
        }
    }
}
