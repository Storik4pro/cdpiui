using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Communication;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.Views.Store.Settings.Memory
{
    public sealed partial class MemoryViewConditionalLaunchDetailsPage : TemplatePage
    {
        private readonly ILocalizer _localizer = Localizer.Get();
        private readonly string _tasksDirectory;
        private readonly ObservableCollection<BreadcrumbBarModel> _breadcrumbItems = [];

        public MemoryViewConditionalLaunchDetailsPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = NavGrid;
            BreadcrumbBar.ItemsSource = _breadcrumbItems;
            _tasksDirectory = ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
                SettingsManager.Instance.SettingsFilePath);

            CreateBreadcrumbBarNavigation();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefreshTaskInformation();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (SettingsPage.MemoryNavigationSupportedPages.Contains(e.SourcePageType))
                PrepareToConnectedBackwardAnimate(NavGrid);
        }

        private void CreateBreadcrumbBarNavigation()
        {
            _breadcrumbItems.Clear();
            _breadcrumbItems.Add(new()
            {
                DisplayName = Text("Settings"),
                Tag = typeof(SettingsPage)
            });
            _breadcrumbItems.Add(new()
            {
                DisplayName = Text("MemoryUsage"),
                Tag = typeof(MemoryViewPage)
            });
            _breadcrumbItems.Add(new()
            {
                DisplayName = Text("MemoryViewConditionalLaunchDetails"),
                Tag = GetType()
            });
        }

        private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var item = (BreadcrumbBarModel)args.Item;
            Frame.Navigate(
                item.Tag,
                null,
                new SlideNavigationTransitionInfo
                {
                    Effect = SlideNavigationTransitionEffect.FromLeft
                });
        }

        private void ManageConditionalTasksButton_Click(object sender, RoutedEventArgs e)
        {
            Commands.CommandsHandler.HandleCommand("cdpiui://Tools/ConditionalLaunch");
        }

        private async void DeleteAllTasksButton_Click(object sender, RoutedEventArgs e)
        {
            var files = GetTaskFiles();
            if (files.Length == 0)
                return;

            ContentDialog dialog = new()
            {
                Title = Text("MemoryConditionalTasksDeleteAllTitle"),
                Content = Text("MemoryConditionalTasksDeleteAllContent"),
                PrimaryButtonText = Text("CL_DeleteButtonText"),
                CloseButtonText = Text("CL_CancelButtonText"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            try
            {
                foreach (var file in files)
                {
                    try
                    {
                        ((App)Application.Current).CloseWindow<ConditionalTaskEditorWindow>(
                            ConditionalTaskEditorWindow.WindowIdPrefix +
                            ConditionalTaskFileService.Load(file).Id);
                    }
                    catch
                    {
                        // Invalid task files have no open properties window.
                    }

                    File.Delete(file);
                }

                _ = PipeHelper.SendConditionalTasksReloadPacket();
                foreach (var window in ((App)Application.Current).OpenWindows
                    .OfType<ConditionalLaunchWindow>())
                {
                    window.ReloadTasksFromStorage();
                }

                RefreshTaskInformation();
                ShowStatus(Text("MemoryConditionalTasksDeleted"), InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                RefreshTaskInformation();
                ShowStatus(
                    string.Format(Text("MemoryConditionalTasksDeleteErrorFormat"), ex.Message),
                    InfoBarSeverity.Error);
            }
        }

        private void RefreshTaskInformation()
        {
            var files = GetTaskFiles();
            var size = files.Sum(file =>
            {
                try
                {
                    return new FileInfo(file).Length;
                }
                catch
                {
                    return 0L;
                }
            });

            MemoryTextBlock.Text = UnitsParser.FormatSize(size);
            TaskCountTextBlock.Text = string.Format(
                Text("MemoryConditionalTasksCountFormat"),
                files.Length);
            DeleteAllTasksButton.IsEnabled = files.Length > 0;
        }

        private string[] GetTaskFiles()
        {
            if (!Directory.Exists(_tasksDirectory))
                return [];

            return Directory.GetFiles(
                _tasksDirectory,
                $"*{ConditionalTaskFileService.FileExtension}",
                SearchOption.TopDirectoryOnly);
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }

        private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);
    }
}
