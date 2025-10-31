using CDPI_UI.Controls.Dialogs;
using CDPI_UI.Controls.Dialogs.CreateConfigHelper;
using CDPI_UI.ViewModels;
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
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    public sealed partial class CreateConfigDesignerUserControl : UserControl
    {
        public static readonly DependencyProperty ModeChangedCommandProperty =
            DependencyProperty.Register(
                nameof(ModeChangedCommand),
                typeof(ICommand),
                typeof(CreateConfigDesignerUserControl),
                new PropertyMetadata(null)
            );

        public ICommand ModeChangedCommand
        {
            get => (ICommand)GetValue(ModeChangedCommandProperty);
            set => SetValue(ModeChangedCommandProperty, value);
        }

        public static readonly DependencyProperty ModeChangedParameterProperty =
            DependencyProperty.Register(
                nameof(ModeChangedParameter),
                typeof(object),
                typeof(CreateConfigDesignerUserControl),
                new PropertyMetadata(null)
            );

        public object ModeChangedParameter
        {
            get => GetValue(ModeChangedParameterProperty);
            set => SetValue(ModeChangedParameterProperty, value);
        }

        public ICommand ViewFullCommand { get; }
        public ICommand EditModeCommand { get; }

        private ILocalizer localizer = Localizer.Get();

        private ObservableCollection<ByeDPIModeComboBoxItem> _badges = [];

        public CreateConfigDesignerUserControl()
        {
            InitializeComponent();

            ViewFullCommand = new RelayCommand(p => ViewFullArgs());
            EditModeCommand = new RelayCommand(p => EditByeDPIMode());

            BadgesListView.ItemsSource = _badges;

            AuditGroupChooseVisibility();
        }
        public string Guid
        {
            get { return (string)GetValue(GuidProperty); }
            set { SetValue(GuidProperty, value); }
        }

        public static readonly DependencyProperty GuidProperty =
            DependencyProperty.Register(
                nameof(Guid), typeof(string), typeof(CreateConfigDesignerUserControl), new PropertyMetadata(string.Empty)
            );

        public string Args
        {
            get { return (string)GetValue(ArgsProperty); }
            set { SetValue(ArgsProperty, value); }
        }

        public static readonly DependencyProperty ArgsProperty =
            DependencyProperty.Register(
                nameof(Args), typeof(string), typeof(CreateConfigDesignerUserControl), new PropertyMetadata(string.Empty)
            );
        public string SiteListName
        {
            get { return (string)GetValue(SiteListNameProperty); }
            set { SetValue(SiteListNameProperty, value); }
        }

        public static readonly DependencyProperty SiteListNameProperty =
            DependencyProperty.Register(
                nameof(SiteListName), typeof(string), typeof(CreateConfigDesignerUserControl), new PropertyMetadata(string.Empty)
            );

        public List<ChooseGroupModeContentDialog.ByeDPIGroupModes> Modes
        {
            get { return (List<ChooseGroupModeContentDialog.ByeDPIGroupModes>)GetValue(ModesProperty); }
            set
            {
                SetValue(ModesProperty, value);
                ShowBadges();
                
            }
        }

        public static readonly DependencyProperty ModesProperty =
            DependencyProperty.Register(
                nameof(Modes), typeof(List<ChooseGroupModeContentDialog.ByeDPIGroupModes>), typeof(CreateConfigDesignerUserControl), new PropertyMetadata(new List<ChooseGroupModeContentDialog.ByeDPIGroupModes>())
            );

        public bool ShowGroupModeChooser
        {
            get { return (bool)GetValue(ShowGroupModeChooserProperty); }
            set {
                SetValue(ShowGroupModeChooserProperty, value);
                AuditGroupChooseVisibility();
            }
        }
        public static readonly DependencyProperty ShowGroupModeChooserProperty =
            DependencyProperty.Register(
                nameof(ShowGroupModeChooser), typeof(bool), typeof(CreateConfigDesignerUserControl), new PropertyMetadata(false)
            );

        private void AuditGroupChooseVisibility()
        {
            Debug.WriteLine($"ShowGroupModeChooser set to {ShowGroupModeChooser}");
            ModeStackPanel.Visibility = ShowGroupModeChooser ? Visibility.Visible : Visibility.Collapsed;
            EditMode.Visibility = ShowGroupModeChooser ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowBadges()
        {
            _badges.Clear();
            foreach (var mode in Modes) 
            {
                _badges.Add(new()
                {
                    Mode = mode,
                    DisplayName = mode.ToString().ToUpper(),
                    DisplayTip = localizer.GetLocalizedString($"/Flashlight/{mode.ToString().ToUpper()}"),
                });
            }
        }

        private void ViewFullArgs()
        {
            ViewApplyArgsContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = localizer.GetLocalizedString("ViewArguments"),
                DialogTitle = string.Empty,
                SeparationTextVisible = Visibility.Collapsed,
                Args = [this.Args ?? string.Empty]

            };
            _ = dialog.ShowAsync();
        }

        private async void EditByeDPIMode()
        {
            ChooseGroupModeContentDialog dialog = new(Modes)
            {
                XamlRoot = this.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Modes = dialog.Result;
                NotifyModeChanged();
            }
        }

        private void NotifyModeChanged()
        {
            if (ModeChangedParameter == null)
            {
                ModeChangedParameter = Tuple.Create(Guid, Modes);
            }
            if (ModeChangedCommand != null && ModeChangedCommand.CanExecute(ModeChangedParameter))
            {
                ModeChangedCommand.Execute(ModeChangedParameter);
                return;
            }
        }
    }
}
