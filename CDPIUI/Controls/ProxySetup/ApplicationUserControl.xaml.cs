using CDPI_UI.Controls.Dialogs.ProxySetupUtil;
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

    public sealed partial class ApplicationUserControl : UserControl
    {
        public static readonly DependencyProperty RemoveCommandProperty =
            DependencyProperty.Register(
                nameof(RemoveCommand),
                typeof(ICommand),
                typeof(ApplicationUserControl),
                new PropertyMetadata(null)
            );

        public ICommand RemoveCommand
        {
            get => (ICommand)GetValue(RemoveCommandProperty);
            set => SetValue(RemoveCommandProperty, value);
        }

        public static readonly DependencyProperty RemoveCommandParameterProperty =
            DependencyProperty.Register(
                nameof(RemoveCommandParameter),
                typeof(object),
                typeof(ApplicationUserControl),
                new PropertyMetadata(null)
            );

        public object RemoveCommandParameter
        {
            get => GetValue(RemoveCommandParameterProperty);
            set => SetValue(RemoveCommandParameterProperty, value);
        }

        public ApplicationUserControl()
        {
            InitializeComponent();
        }

        public string DisplayName
        {
            get { return (string)GetValue(DisplayNameProperty); }
            set { SetValue(DisplayNameProperty, value); }
        }

        public static readonly DependencyProperty DisplayNameProperty =
            DependencyProperty.Register(
                nameof(DisplayName), typeof(string), typeof(ApplicationUserControl), new PropertyMetadata(string.Empty)
            );

        public string FullPath
        {
            get { return (string)GetValue(FullPathProperty); }
            set { SetValue(FullPathProperty, value); }
        }

        public static readonly DependencyProperty FullPathProperty =
            DependencyProperty.Register(
                nameof(FullPath), typeof(string), typeof(ApplicationUserControl), new PropertyMetadata(string.Empty)
            );

        public SelectAppContentDialogResult ApplicationType
        {
            get { return (SelectAppContentDialogResult)GetValue(ApplicationTypeProperty); }
            set {
                SetValue(ApplicationTypeProperty, value); 
                switch (value)
                {
                    case SelectAppContentDialogResult.None:
                        break;
                    case SelectAppContentDialogResult.UniversalApp:
                        break;
                    case SelectAppContentDialogResult.ClassicApp:
                        break;
                }
            }
        }

        public static readonly DependencyProperty ApplicationTypeProperty =
            DependencyProperty.Register(
                nameof(ApplicationType), typeof(string), typeof(ApplicationUserControl), new PropertyMetadata(SelectAppContentDialogResult.None)
            );

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (RemoveCommand != null && RemoveCommand.CanExecute(RemoveCommandParameter))
            {
                RemoveCommand.Execute(RemoveCommandParameter);
                return;
            }
        }
    }
}
