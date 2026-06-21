using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using ABI.Windows.UI;
using System.Drawing;
using System.Windows.Media;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Documents;
using WinUI3Localizer;
using CDPI_UI.Controls.Universal;

namespace CDPI_UI.Controls.Dialogs.Universal
{
    internal class ErrorContentDialog
    {
        private string GetLocalizedString(string locId, string defaultString)
        {
            ILocalizer localizer = Localizer.Get();
            string result = localizer.GetLocalizedString(locId);
            return string.IsNullOrEmpty(result) ? defaultString : result;
        }

        private StackPanel ErrorDetailsPanel;
        private HyperlinkButton MoreButton;

        public async Task ShowErrorDialogAsync(string content, string errorDetails, XamlRoot xamlRoot)
        {
            var dialog = new ContentDialog()
            {
                Title = GetLocalizedString("UnexpectedError", "Unexpected Error"),
                XamlRoot = xamlRoot,
                PrimaryButtonText = "OK",
                DefaultButton = ContentDialogButton.Primary
            };
            var scrollView = new ScrollView();

            var stackPanel = new StackPanel();
            scrollView.Content = stackPanel;
            var contentTextBlock = new TextBlock()
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap
            };

            stackPanel.Children.Add(contentTextBlock);

            MoreButton = new HyperlinkButton()
            {
                Content = GetLocalizedString("ViewMore", "More"),
                Margin = new Thickness(0, 10, 0, 0),
                Padding= new Thickness(0)
            };

            stackPanel.Children.Add(MoreButton);

            ErrorDetailsPanel = new StackPanel()
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0),
                Spacing = 10
            };

            Paragraph paragraph = new Paragraph();

            var errorDetailsTextBlock = new RichTextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                
            };
            Run run = new Run();

            errorDetailsTextBlock.IsTextSelectionEnabled = true;
            run.Text = errorDetails;

            paragraph.Inlines.Add(run);
            errorDetailsTextBlock.Blocks.Add(paragraph);

            ErrorDetailsPanel.Children.Add(errorDetailsTextBlock);

            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush();
            transparent.Opacity = 0;

            FontIcon icon = new FontIcon();
            icon.Glyph = "\uE8C8";
            icon.Margin = new Thickness(0, 0, 5, 0);
            icon.FontSize = 16;

            TextBlock textBlock1 = new TextBlock();
            textBlock1.Text = GetLocalizedString("CopyErrorText", "Copy error text");
            textBlock1.Style = (Style)Application.Current.Resources["BodyTextBlockStyle"];

            StackPanel stackPanel1 = new StackPanel();

            stackPanel1.Orientation = Orientation.Horizontal;
            stackPanel1.Children.Add(icon);
            stackPanel1.Children.Add(textBlock1);



            var copyButton = new CopyButton()
            {
                Content = stackPanel1,
            };
            
            copyButton.Click += (sender, e) =>
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(errorDetails);
                Clipboard.SetContent(dataPackage);
            };

            ErrorDetailsPanel.Children.Add(copyButton);

            stackPanel.Children.Add(ErrorDetailsPanel);

            MoreButton.Click += ViewMoreHandler;

            dialog.Content = scrollView;

            await dialog.ShowAsync();
        }

        private void ViewMoreHandler(object sender, RoutedEventArgs e)
        {
            ErrorDetailsPanel.Visibility = ErrorDetailsPanel.Visibility == Visibility.Collapsed
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            MoreButton.Content = ErrorDetailsPanel.Visibility == Visibility.Collapsed ? GetLocalizedString("ViewMore", "More") : GetLocalizedString("ViewLess", "Less");
        }
    }
}
