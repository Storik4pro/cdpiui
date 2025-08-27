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

namespace GoodbyeDPI_UI.DataModel
{
    internal class ErrorContentDialog
    {
        public async Task ShowErrorDialogAsync(string content, string errorDetails, XamlRoot xamlRoot)
        {
            var dialog = new ContentDialog()
            {
                Title = "Unexpected error",
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

            var detailsHyperlinkButton = new HyperlinkButton()
            {
                Content = "More",
                Margin = new Thickness(0, 10, 0, 0),
                Padding= new Thickness(0)
            };

            stackPanel.Children.Add(detailsHyperlinkButton);

            var errorDetailsPanel = new StackPanel()
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0)
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

            errorDetailsPanel.Children.Add(errorDetailsTextBlock);

            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush();
            transparent.Opacity = 0;

            FontIcon icon = new FontIcon();
            icon.Glyph = "\uE8C8";
            icon.Margin = new Thickness(0, 0, 5, 0);

            TextBlock textBlock1 = new TextBlock();
            textBlock1.Text = "Copy error text";

            StackPanel stackPanel1 = new StackPanel();

            stackPanel1.Orientation = Orientation.Horizontal;
            stackPanel1.Children.Add(icon);
            stackPanel1.Children.Add(textBlock1);



            var copyButton = new Button()
            {
                Content = stackPanel1,
                Background = transparent,
                BorderBrush = transparent,
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(0)
            };
            
            copyButton.Click += (sender, e) =>
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(errorDetails);
                Clipboard.SetContent(dataPackage);
            };

            errorDetailsPanel.Children.Add(copyButton);

            stackPanel.Children.Add(errorDetailsPanel);

            detailsHyperlinkButton.Click += (sender, e) =>
            {
                errorDetailsPanel.Visibility = errorDetailsPanel.Visibility == Visibility.Collapsed
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            dialog.Content = scrollView;

            await dialog.ShowAsync();
        }
    }
}
