using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using GoodbyeDPI_UI.Controls.Dialogs.Universal;
using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Static;
using Microsoft.UI;
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
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Controls.Store
{
    public sealed partial class StoreDownloadItemButton : UserControl
    {
        public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.Register(
                nameof(ClickCommand),
                typeof(ICommand),
                typeof(StoreDownloadItemButton),
                new PropertyMetadata(null)
            );

        public ICommand ClickCommand
        {
            get => (ICommand)GetValue(ClickCommandProperty);
            set => SetValue(ClickCommandProperty, value);
        }

        public static readonly DependencyProperty ClickCommandParameterProperty =
            DependencyProperty.Register(
                nameof(ClickCommandParameter),
                typeof(object),
                typeof(StoreDownloadItemButton),
                new PropertyMetadata(null)
            );

        public object ClickCommandParameter
        {
            get => GetValue(ClickCommandParameterProperty);
            set => SetValue(ClickCommandParameterProperty, value);
        }

        private MarkdownConfig _config;

        public MarkdownConfig MarkdownConfig
        {
            get => _config;
            set => _config = value;
        }

        public StoreDownloadItemButton()
        {
            InitializeComponent();
            _config = new MarkdownConfig();

            this.Loaded += StoreDownloadItemButton_Loaded;
        }

        private void StoreDownloadItemButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (DescriptionText.ActualHeight > 150)
            {
                ViewMoreButton.Visibility = Visibility.Visible;
            }
            DescriptionText.MaxHeight = 150;
        }

        public string StoreId
        {
            get { return (string)GetValue(StoreIdProperty); }
            set { 
                SetValue(StoreIdProperty, value);
                RemoveHandlers();
                ConnectHandlers();
            }
        }

        public static readonly DependencyProperty StoreIdProperty =
            DependencyProperty.Register(
                nameof(StoreId), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );

        public string OperationId
        {
            get { return (string)GetValue(OperationIdProperty); }
            set { SetValue(OperationIdProperty, value); }
        }

        public static readonly DependencyProperty OperationIdProperty =
            DependencyProperty.Register(
                nameof(OperationId), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );

        public string Developer
        {
            get { return (string)GetValue(DeveloperProperty); }
            set { SetValue(DeveloperProperty, value); }
        }

        public static readonly DependencyProperty DeveloperProperty =
            DependencyProperty.Register(
                nameof(Developer), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );

        public string Tooltip
        {
            get { return (string)GetValue(TooltipProperty); }
            set { SetValue(TooltipProperty, value); }
        }

        public static readonly DependencyProperty TooltipProperty =
            DependencyProperty.Register(
                nameof(Tooltip), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata("Cancel")
            );

        public string Category
        {
            get { return (string)GetValue(CategoryProperty); }
            set { SetValue(CategoryProperty, value); }
        }

        public static readonly DependencyProperty CategoryProperty =
            DependencyProperty.Register(
                nameof(Category), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );

        public string CurrentVersion
        {
            get { return (string)GetValue(CurrentVersionProperty); }
            set { SetValue(CurrentVersionProperty, value); }
        }

        public static readonly DependencyProperty CurrentVersionProperty =
            DependencyProperty.Register(
                nameof(CurrentVersion), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );

        public string ServerVersion
        {
            get { return (string)GetValue(ServerVersionProperty); }
            set { SetValue(ServerVersionProperty, value); }
        }

        public static readonly DependencyProperty ServerVersionProperty =
            DependencyProperty.Register(
                nameof(ServerVersion), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { 
                if (string.IsNullOrEmpty(value))
                    SetValue(DescriptionProperty, "Developer did not leave a comment for this release.");
                else 
                    SetValue(DescriptionProperty, value); 
            }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(
                nameof(Description), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
            );
        public bool IsUpdateLikeButton
        {
            get { return (bool)GetValue(IsUpdateLikeButtonProperty); }
            set { 
                SetValue(IsUpdateLikeButtonProperty, value);
                if (value)
                {
                    StatusStackPanel.Visibility = Visibility.Collapsed;
                    ProgressBar.Visibility = Visibility.Collapsed;
                    CancelButton.Visibility = Visibility.Collapsed;
                    DescriptionStackPanel.Visibility = Visibility.Visible;
                    UpdateGrid.Visibility = Visibility.Visible;
                    BigUpdateGrid.Visibility = Visibility.Visible;
                    VersionStackPanel.Visibility= Visibility.Visible;
                }
                else
                {
                    StatusStackPanel.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;

                    if (StoreHelper.Instance.GetCurrentQueueOperationId() == OperationId)
                        ProgressBar.Visibility = Visibility.Visible;

                    DescriptionStackPanel.Visibility = Visibility.Collapsed;
                    UpdateGrid.Visibility = Visibility.Collapsed;
                    BigUpdateGrid.Visibility = Visibility.Collapsed;
                    VersionStackPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        public static readonly DependencyProperty IsUpdateLikeButtonProperty =
            DependencyProperty.Register(
                nameof(IsUpdateLikeButton), typeof(bool), typeof(StoreDownloadItemButton), new PropertyMetadata(false)
            );

        public string ActionGlyph
        {
            get { return (string)GetValue(ActionGlyphProperty); }
            set { SetValue(ActionGlyphProperty, value); }
        }

        public static readonly DependencyProperty ActionGlyphProperty =
            DependencyProperty.Register(
                nameof(ActionGlyph), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata("\uE894")
            );

        public ImageSource CardImageSource
        {
            get { return (ImageSource)GetValue(CardImageSourceProperty); }
            set { SetValue(CardImageSourceProperty, value); }
        }

        public static readonly DependencyProperty CardImageSourceProperty =
            DependencyProperty.Register(
                nameof(CardImageSource), typeof(ImageSource), typeof(StoreDownloadItemButton), new PropertyMetadata(null)
            );

        public Brush CardBackgroundBrush
        {
            get { return (Brush)GetValue(CardBackgroundProperty); }
            set
            {
                SetValue(CardBackgroundProperty, value);
                Rectangle.Fill = value;
            }
        }

        public static readonly DependencyProperty CardBackgroundProperty =
            DependencyProperty.Register(
                nameof(CardBackgroundBrush),
                typeof(Brush),
                typeof(StoreDownloadItemButton),
                new PropertyMetadata(new SolidColorBrush(Colors.Transparent))
            );

        private void ConnectHandlers()
        {
            

            StoreHelper.Instance.ItemDownloadStageChanged += StoreHelper_ItemDownloadStageChanged;
            StoreHelper.Instance.ItemDownloadProgressChanged += StoreHelper_ItemDownloadProgressChanged;
            StoreHelper.Instance.ItemTimeRemainingChanged += StoreHelper_ItemTimeRemainingChanged;
            StoreHelper.Instance.ItemDownloadSpeedChanged += StoreHelper_ItemDownloadSpeedChanged;
            StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
            StoreHelper.Instance.ItemInstallingErrorHappens += StoreHelper_ItemInstallingErrorHappens;
        }


        private void RemoveHandlers()
        {
            StoreHelper.Instance.ItemDownloadStageChanged -= StoreHelper_ItemDownloadStageChanged;
            StoreHelper.Instance.ItemDownloadProgressChanged -= StoreHelper_ItemDownloadProgressChanged;
            StoreHelper.Instance.ItemTimeRemainingChanged -= StoreHelper_ItemTimeRemainingChanged;
            StoreHelper.Instance.ItemDownloadSpeedChanged -= StoreHelper_ItemDownloadSpeedChanged;
            StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;
        }

        private void StoreHelper_ItemInstallingErrorHappens(Tuple<string, string> obj)
        {
            string operationId = obj.Item1;
            string errorCode = obj.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                return;

            ActionGlyph = "\uE777";
            ErrorTextBlock.Text = errorCode;
            SpeedTextBlock.Text = "";
            TimeTextBlock.Text = "";
            BigStatusTextBlock.Text = "Произошла ошибка";

            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 100;

            Tooltip = "Retry";

            ProgressBar.Foreground = new SolidColorBrush((Color)Application.Current.Resources["SystemFillColorCritical"]);
            RemoveHandlers();
        }

        private void StoreHelper_ItemActionsStopped(string obj)
        {
            if (obj == StoreId)
                RemoveHandlers();
        }

        private void StoreHelper_ItemDownloadSpeedChanged(Tuple<string, double> obj)
        {
            string operationId = obj.Item1;
            double speed = obj.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                return;

            SpeedTextBlock.Text = $"{Utils.FormatSpeed(speed)}, ";
        }

        private void StoreHelper_ItemDownloadProgressChanged(Tuple<string, double> obj)
        {
            string operationId = obj.Item1;
            double progress = obj.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                return;

            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = progress;
        }

        private void StoreHelper_ItemTimeRemainingChanged(Tuple<string, TimeSpan> obj)
        {
            string operationId = obj.Item1;
            TimeSpan time = obj.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                return;

            TimeTextBlock.Text = Utils.ConvertMinutesToPrettyText(time.Minutes);
        }

        private void StoreHelper_ItemDownloadStageChanged(Tuple<string, string> obj)
        {
            string operationId = obj.Item1;
            string stage = obj.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                return;

            string stageHeaderText;

            ProgressBar.Visibility = Visibility.Visible;

            switch (stage)
            {
                case "GETR":
                    stageHeaderText = "Подготовка";
                    ProgressBar.IsIndeterminate = true;
                    break;
                case "END":
                    stageHeaderText = "Завершение";
                    ProgressBar.IsIndeterminate = true;
                    break;
                case "Downloading":
                    stageHeaderText = "Скачивание";
                    ProgressBar.IsIndeterminate = false;
                    break;
                case "Extracting":
                    stageHeaderText = "Установка";
                    ProgressBar.IsIndeterminate = true;
                    break;
                case "ErrorHappens":
                    stageHeaderText = "Произошла ошибка";
                    break;
                case "Completed":
                    stageHeaderText = "Завершение";
                    ProgressBar.IsIndeterminate = true;
                    break;
                case "CANC":
                    stageHeaderText = "Отмена";
                    ProgressBar.IsIndeterminate = true;
                    break;
                default:
                    stageHeaderText = "";
                    break;
            }

            BigStatusTextBlock.Text = stageHeaderText;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate("ForwardConnectedAnimation", PART_Image);

            StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage), StoreId, new SuppressNavigationTransitionInfo());
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!StoreHelper.Instance.RemoveItemFromQueue(StoreId))
            {
                StoreHelper.Instance.AddItemToQueue(StoreId, string.Empty);
                if (ClickCommand != null && ClickCommand.CanExecute(ClickCommandParameter))
                {
                    ClickCommand.Execute(ClickCommandParameter);
                    return;
                }
            }
            
        }

        private async void ViewMoreButton_Click(object sender, RoutedEventArgs e)
        {
            MarkdownTextViewContentDialog dialog = new MarkdownTextViewContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Title = "View more",
                Text = Description
            };

            await dialog.ShowAsync();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClickCommand != null && ClickCommand.CanExecute(ClickCommandParameter))
            {
                ClickCommand.Execute(ClickCommandParameter);
                return;
            }
        }
    }
}
