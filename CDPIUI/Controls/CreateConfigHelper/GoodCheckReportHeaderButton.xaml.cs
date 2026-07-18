using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
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
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    public sealed partial class GoodCheckReportHeaderButton : UserControl
    {
        public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.Register(
                nameof(ClickCommand),
                typeof(ICommand),
                typeof(GoodCheckReportHeaderButton),
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
                typeof(GoodCheckReportHeaderButton),
                new PropertyMetadata(null)
            );

        public object ClickCommandParameter
        {
            get => GetValue(ClickCommandParameterProperty);
            set => SetValue(ClickCommandParameterProperty, value);
        }

        public GoodCheckReportHeaderButton()
        {
            InitializeComponent();
        }

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(
                nameof(Header), typeof(string), typeof(GoodCheckReportHeaderButton), new PropertyMetadata(string.Empty)
            );
        public string SubHeader
        {
            get { return (string)GetValue(SubHeaderProperty); }
            set { SetValue(SubHeaderProperty, value); }
        }

        public static readonly DependencyProperty SubHeaderProperty =
            DependencyProperty.Register(
                nameof(SubHeader), typeof(string), typeof(GoodCheckReportHeaderButton), new PropertyMetadata(string.Empty)
            );
        public string SuccessCount
        {
            get { return (string)GetValue(SuccessCountProperty); }
            set { SetValue(SuccessCountProperty, value); }
        }

        public static readonly DependencyProperty SuccessCountProperty =
            DependencyProperty.Register(
                nameof(SuccessCount), typeof(string), typeof(GoodCheckReportHeaderButton), new PropertyMetadata("0")
            );
        public string FailureCount
        {
            get { return (string)GetValue(FailureCountProperty); }
            set { SetValue(FailureCountProperty, value); }
        }

        public static readonly DependencyProperty FailureCountProperty =
            DependencyProperty.Register(
                nameof(FailureCount), typeof(string), typeof(GoodCheckReportHeaderButton), new PropertyMetadata("0")
            );
        public string FlagsCount
        {
            get { return (string)GetValue(FlagsCountProperty); }
            set { SetValue(FlagsCountProperty, value); }
        }

        public static readonly DependencyProperty FlagsCountProperty =
            DependencyProperty.Register(
                nameof(FlagsCount), typeof(string), typeof(GoodCheckReportHeaderButton), new PropertyMetadata("0")
            );

        public bool IsOpened
        {
            get { return (bool)GetValue(IsOpenedCountProperty); }
            set { 
                SetValue(IsOpenedCountProperty, value);
                ChevronFontIcon.Glyph = value ? "\uE70E" : "\uE70D";
            }
        }

        public static readonly DependencyProperty IsOpenedCountProperty =
            DependencyProperty.Register(
                nameof(IsOpened), typeof(bool), typeof(GoodCheckReportHeaderButton), new PropertyMetadata(false)
            );
        public int? Id
        {
            get { return (int?)GetValue(IdProperty); }
            set { SetValue(IdProperty, value); }
        }

        public static readonly DependencyProperty IdProperty =
            DependencyProperty.Register(
                nameof(Id), typeof(int?), typeof(GoodCheckReportHeaderButton), new PropertyMetadata(null)
            );

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ClickCommandParameter == null)
            {
                ClickCommandParameter = this;
            }
            if (ClickCommand != null && ClickCommand.CanExecute(ClickCommandParameter))
            {
                ClickCommand.Execute(ClickCommandParameter);
                return;
            }
        }
    }
}
