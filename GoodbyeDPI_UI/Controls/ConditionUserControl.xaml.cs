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

namespace GoodbyeDPI_UI
{
    public sealed partial class ConditionUserControl : UserControl
    {
        public static readonly DependencyProperty RemoveCommandProperty =
            DependencyProperty.Register(
                nameof(RemoveCommand),
                typeof(ICommand),
                typeof(ConditionUserControl),
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
                typeof(ConditionUserControl),
                new PropertyMetadata(null)
            );

        public object RemoveCommandParameter
        {
            get => GetValue(RemoveCommandParameterProperty);
            set => SetValue(RemoveCommandParameterProperty, value);
        }

        public ConditionUserControl()
        {
            InitializeComponent();
        }

        public string VarName
        {
            get { return (string)GetValue(VarNameProperty); }
            set { SetValue(VarNameProperty, value); }
        }

        public static readonly DependencyProperty VarNameProperty =
            DependencyProperty.Register(
                nameof(VarName), typeof(string), typeof(ConditionUserControl), new PropertyMetadata(string.Empty)
            );

        public string OnValue
        {
            get { return (string)GetValue(OnValueProperty); }
            set { SetValue(OnValueProperty, value); }
        }

        public static readonly DependencyProperty OnValueProperty =
            DependencyProperty.Register(
                nameof(OnValue), typeof(string), typeof(ConditionUserControl), new PropertyMetadata(string.Empty)
            );

        public string OffValue
        {
            get { return (string)GetValue(OffValueProperty); }
            set { SetValue(OffValueProperty, value); }
        }

        public static readonly DependencyProperty OffValueProperty =
            DependencyProperty.Register(
                nameof(OffValue), typeof(string), typeof(ConditionUserControl), new PropertyMetadata(string.Empty)
            );

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(
                nameof(Description), typeof(string), typeof(ConditionUserControl), new PropertyMetadata(string.Empty)
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
