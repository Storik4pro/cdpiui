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
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs
{
    public sealed partial class EditConfigVarValueDialog : ContentDialog
    {
        public string VarValue { get; private set; } = string.Empty;

        public EditConfigVarValueDialog(string varName, string varValue, AvailableVarValues varValues)
        {
            InitializeComponent();
            this.Title = $"Edit variable %{varName}%";
            VarValueTextBox.Text = varValue;

            DescriptionTextBlock.Text = varValues?.Comment ?? "There is no description available for this variable.";

            if (varValues != null && varValues.Values.Count > 0)
            {
                VarValueComboBox.ItemsSource = varValues.Values;
                VarValueComboBox.SelectedItem = varValue;
                TemplateSelectorStackPanel.Visibility = Visibility.Visible;
            }
            else
            {
                TemplateSelectorStackPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void VarValueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedValue = VarValueComboBox.SelectedItem as string;
            if (selectedValue != null)
            {
                VarValueTextBox.Text = selectedValue;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            VarValue = VarValueTextBox.Text;
        }
    }
}
