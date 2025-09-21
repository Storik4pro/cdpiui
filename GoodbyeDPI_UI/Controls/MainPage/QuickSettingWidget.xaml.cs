using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    public sealed partial class QuickSettingWidget : UserControl
    {
        public QuickSettingWidget()
        {
            InitializeComponent();
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set
            {
                SetValue(TitleProperty, value);
                if (!string.IsNullOrEmpty(value))
                {
                    TitleTextBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    TitleTextBlock.Visibility = Visibility.Collapsed;
                }
            }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(QuickSettingWidget), new PropertyMetadata(string.Empty)
            );

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set
            {
                SetValue(DescriptionProperty, value);
                if (!string.IsNullOrEmpty(value))
                {
                    DescriptionTextBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    DescriptionTextBlock.Visibility = Visibility.Collapsed;
                }
            }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(
                nameof(Description), typeof(string), typeof(QuickSettingWidget), new PropertyMetadata(string.Empty)
            );

        public string IconGlyph
        {
            get { return (string)GetValue(IconGlyphProperty); }
            set
            {
                SetValue(IconGlyphProperty, value);
                if (!string.IsNullOrEmpty(value))
                {
                    FontIcon.Visibility = Visibility.Visible;
                }
                else
                {
                    FontIcon.Visibility = Visibility.Collapsed;
                }
            }
        }

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(
                nameof(IconGlyph), typeof(string), typeof(QuickSettingWidget), new PropertyMetadata(string.Empty)
            );

        public object InnerContent
        {
            get { return (object)GetValue(InnerContentProperty); }
            set { SetValue(InnerContentProperty, value); }
        }

        public static readonly DependencyProperty InnerContentProperty =
            DependencyProperty.Register(nameof(InnerContent), typeof(object), typeof(QuickSettingWidget), new PropertyMetadata(null));
    }
}
