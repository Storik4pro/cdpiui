using CDPI_UI.Controls.Dialogs;
using CDPI_UI.Helper.Items;
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
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    public sealed partial class VariableUserControl : UserControl
    {
        public static readonly DependencyProperty RemoveCommandProperty =
            DependencyProperty.Register(
                nameof(RemoveCommand),
                typeof(ICommand),
                typeof(VariableUserControl),
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
                typeof(VariableUserControl),
                new PropertyMetadata(null)
            );

        public object RemoveCommandParameter
        {
            get => GetValue(RemoveCommandParameterProperty);
            set => SetValue(RemoveCommandParameterProperty, value);
        }

        public static readonly DependencyProperty ValueChangedCommandProperty =
            DependencyProperty.Register(
                nameof(ValueChangedCommand),
                typeof(ICommand),
                typeof(VariableUserControl),
                new PropertyMetadata(null)
            );

        public ICommand ValueChangedCommand
        {
            get => (ICommand)GetValue(ValueChangedCommandProperty);
            set => SetValue(ValueChangedCommandProperty, value);
        }

        public static readonly DependencyProperty ValueChangedCommandParameterProperty =
            DependencyProperty.Register(
                nameof(ValueChangedCommandParameter),
                typeof(object),
                typeof(VariableUserControl),
                new PropertyMetadata(null)
            );

        public object ValueChangedCommandParameter
        {
            get => GetValue(ValueChangedCommandParameterProperty);
            set => SetValue(ValueChangedCommandParameterProperty, value);
        }

        public VariableUserControl()
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
                nameof(VarName), typeof(string), typeof(VariableUserControl), new PropertyMetadata(string.Empty)
            );

        public string VarValue
        {
            get { return (string)GetValue(VarValueProperty); }
            set { SetValue(VarValueProperty, value); }
        }

        public static readonly DependencyProperty VarValueProperty =
            DependencyProperty.Register(
                nameof(VarValue), typeof(string), typeof(VariableUserControl), new PropertyMetadata(string.Empty)
            );

        public AvailableVarValues AvailableVarValues
        {
            get { return (AvailableVarValues)GetValue(AvailableVarValuesProperty); }
            set { SetValue(AvailableVarValuesProperty, value); }
        }

        public static readonly DependencyProperty AvailableVarValuesProperty =
            DependencyProperty.Register(
                nameof(AvailableVarValues), typeof(AvailableVarValues), typeof(VariableUserControl), new PropertyMetadata(null)
            );

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EditConfigVarValueDialog dialog = new EditConfigVarValueDialog(varName:VarName, varValue:VarValue, varValues:AvailableVarValues) 
            { 
                XamlRoot = this.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                VarValue = dialog.VarValue;
                ValueChangedCommandParameter = Tuple.Create(VarName, VarValue);
                if (ValueChangedCommand != null && ValueChangedCommand.CanExecute(ValueChangedCommandParameter))
                {
                    ValueChangedCommand.Execute(ValueChangedCommandParameter);
                    return;
                }
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (RemoveCommand != null && RemoveCommand.CanExecute(RemoveCommandParameter))
            {
                RemoveCommand.Execute(RemoveCommandParameter);
                return;
            }
        }

        private void CloseFlyout_Click(object sender, RoutedEventArgs e)
        {
            RemoveFlyout.Hide();
        }
    }
}
