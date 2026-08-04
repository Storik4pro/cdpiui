using CDPIUI.Commands;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Universal
{
    public sealed partial class UtilityButtonControls : UserControl
    {
        private static ILocalizer localizer = Localizer.Get();

        public UtilityButtonControls()
        {
            InitializeComponent();
        }

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading), typeof(bool), typeof(UtilityButtonControls), new PropertyMetadata(false)
            );

        public string LoadingStateText
        {
            get { return (string)GetValue(LoadingStateTextProperty); }
            set { SetValue(LoadingStateTextProperty, value); }
        }

        public static readonly DependencyProperty LoadingStateTextProperty =
            DependencyProperty.Register(
                nameof(LoadingStateText), typeof(string), typeof(UtilityButtonControls), new PropertyMetadata(localizer.GetLocalizedString("WorkingOnItTextBlock"))
            );


        public string HelpUrl
        {
            get { return (string)GetValue(HelpUrlProperty); }
            set { SetValue(HelpUrlProperty, value); }
        }

        public static readonly DependencyProperty HelpUrlProperty =
            DependencyProperty.Register(
                nameof(HelpUrl), typeof(string), typeof(UtilityButtonControls), new PropertyMetadata(string.Empty)
            );

        public object Buttons
        {
            get { return (object)GetValue(ButtonContentProperty); }
            set { SetValue(ButtonContentProperty, value); }
        }

        public static readonly DependencyProperty ButtonContentProperty =
        DependencyProperty.Register(
            nameof(Buttons), typeof(object), typeof(UtilityButtonControls), new PropertyMetadata(default(object)));

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            CommandsHandler.HandleCommand($"cdpiui://Help/{HelpUrl}");
        }
    }
}
