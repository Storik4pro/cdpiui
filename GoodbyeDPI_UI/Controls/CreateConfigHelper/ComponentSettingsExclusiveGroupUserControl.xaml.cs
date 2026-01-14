using CDPI_UI.ViewModels;
using CDPI_UI.Views.CreateConfigHelper;
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
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.CreateConfigHelper
{
    public sealed partial class ComponentSettingsExclusiveGroupUserControl : UserControl
    {

        public static readonly DependencyProperty ToggledCommandProperty =
            DependencyProperty.Register(
                nameof(ToggledCommand),
                typeof(ICommand),
                typeof(ComponentSettingsExclusiveGroupUserControl),
                new PropertyMetadata(null)
            );

        public ICommand ToggledCommand
        {
            get => (ICommand)GetValue(ToggledCommandProperty);
            set => SetValue(ToggledCommandProperty, value);
        }

        public static readonly DependencyProperty ToggledParameterProperty =
            DependencyProperty.Register(
                nameof(ToggledParameter),
                typeof(object),
                typeof(ComponentSettingsExclusiveGroupUserControl),
                new PropertyMetadata(null)
            );

        public object ToggledParameter
        {
            get => GetValue(ToggledParameterProperty);
            set => SetValue(ToggledParameterProperty, value);
        }

        public static readonly DependencyProperty ValueChangedCommandProperty =
                DependencyProperty.Register(
                    nameof(ValueChangedCommand),
                    typeof(ICommand),
                    typeof(ComponentSettingsExclusiveGroupUserControl),
                    new PropertyMetadata(null)
                );

        public ICommand ValueChangedCommand
        {
            get => (ICommand)GetValue(ValueChangedCommandProperty);
            set => SetValue(ValueChangedCommandProperty, value);
        }

        public static readonly DependencyProperty ValueChangedParameterProperty =
            DependencyProperty.Register(
                nameof(ValueChangedParameter),
                typeof(object),
                typeof(ComponentSettingsExclusiveGroupUserControl),
                new PropertyMetadata(null)
            );

        public object ValueChangedParameter
        {
            get => GetValue(ValueChangedParameterProperty);
            set => SetValue(ValueChangedParameterProperty, value);
        }

        public static readonly DependencyProperty NowSelectedGuidChangedCommandProperty =
                DependencyProperty.Register(
                    nameof(NowSelectedGuidChangedCommand),
                    typeof(ICommand),
                    typeof(ComponentSettingsExclusiveGroupUserControl),
                    new PropertyMetadata(null)
                );

        public ICommand NowSelectedGuidChangedCommand
        {
            get => (ICommand)GetValue(NowSelectedGuidChangedCommandProperty);
            set => SetValue(NowSelectedGuidChangedCommandProperty, value);
        }

        public static readonly DependencyProperty NowSelectedGuidChangedParameterProperty =
            DependencyProperty.Register(
                nameof(NowSelectedGuidChangedParameter),
                typeof(object),
                typeof(ComponentSettingsExclusiveGroupUserControl),
                new PropertyMetadata(null)
            );

        public object NowSelectedGuidChangedParameter
        {
            get => GetValue(NowSelectedGuidChangedParameterProperty);
            set => SetValue(NowSelectedGuidChangedParameterProperty, value);
        }

        public ICommand GraphicDesignerTextValueChangedCommand { get; }
        public ICommand GraphicDesignerBoolValueToggledCommand { get; }

        private ILocalizer localizer = Localizer.Get();
        public ComponentSettingsExclusiveGroupUserControl()
        {
            InitializeComponent();

            GraphicDesignerTextValueChangedCommand = new RelayCommand(p => NotifyValueChanged((Tuple<string, string>)p));
            GraphicDesignerBoolValueToggledCommand = new RelayCommand(p => NotifyToggled((Tuple<string, bool>)p));

            AvailableVariants.CollectionChanged += AvailableVariants_CollectionChanged;
        }

        private void HandleCollectionChanged()
        {
            foreach (var item in AvailableVariants)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.PropertyChanged += Item_PropertyChanged;
            }
        }

        private void Item_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is GraphicDesignerSettingItemModel model)
            {
                if (model.Guid == SelectedItemGuid)
                {
                    SelectItem(model);
                }
            }
        }

        private void AvailableVariants_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HandleCollectionChanged();
        }

        public string Guid
        {
            get { return (string)GetValue(GuidProperty); }
            set { SetValue(GuidProperty, value); }
        }

        public static readonly DependencyProperty GuidProperty =
            DependencyProperty.Register(
                nameof(Guid), typeof(string), typeof(ComponentSettingsExclusiveGroupUserControl), new PropertyMetadata(string.Empty)
            );
        public string DisplayName
        {
            get { return (string)GetValue(DisplayNameProperty); }
            set { SetValue(DisplayNameProperty, value); }
        }

        public static readonly DependencyProperty DisplayNameProperty =
            DependencyProperty.Register(
                nameof(DisplayName), typeof(string), typeof(ComponentSettingsExclusiveGroupUserControl), new PropertyMetadata(string.Empty)
            );
        public string SelectedItemGuid
        {
            get { return (string)GetValue(SelectedItemGuidProperty); }
            set { 
                SetValue(SelectedItemGuidProperty, value);
                CheckSelectedItem();
            }
        }

        public static readonly DependencyProperty SelectedItemGuidProperty =
            DependencyProperty.Register(
                nameof(SelectedItemGuid), typeof(string), typeof(ComponentSettingsExclusiveGroupUserControl), new PropertyMetadata(string.Empty)
            );

        public ObservableCollection<GraphicDesignerSettingItemModel> AvailableVariants
        {
            get { return (ObservableCollection<GraphicDesignerSettingItemModel>)GetValue(AvailableVariantsProperty); }
            set {
                foreach (var item in AvailableVariants)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
                SetValue(AvailableVariantsProperty, value);
                UpdateSelectorBar();
                CheckSelectedItem();
                HandleCollectionChanged();
            }
        }

        public static readonly DependencyProperty AvailableVariantsProperty =
            DependencyProperty.Register(
                nameof(AvailableVariants), typeof(ObservableCollection<GraphicDesignerSettingItemModel>), typeof(ComponentSettingsExclusiveGroupUserControl), new PropertyMetadata(new ObservableCollection<GraphicDesignerSettingItemModel>() )
            );

        private void UpdateSelectorBar()
        {
            SelectorBar.Items.Clear();

            foreach (var item in AvailableVariants)
            {
                string locText = localizer.GetLocalizedString($"/GraphicDesignerDescriptions/{item.Value}");
                SelectorBar.Items.Add(new()
                {
                    Text = string.IsNullOrEmpty(locText) ? item.DisplayName : locText,
                    Tag = item.Guid
                });
            }
        }

        private void CheckSelectedItem()
        {
            SelectorBar.SelectedItem = SelectorBar.Items.FirstOrDefault(x => x.Tag.ToString() == SelectedItemGuid);
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            SelectedItemGuid = SelectorBar.SelectedItem.Tag.ToString();
            var item = AvailableVariants?.FirstOrDefault(x => x.Guid == SelectedItemGuid);
            if (item != null)
            {
                MainComponentSettingsUserControl.Visibility = Visibility.Visible;
                SelectItem(item);
            }
            else
            {
                MainComponentSettingsUserControl.Visibility = Visibility.Collapsed;
            }
            NotifyNowSelectedGuidChanged(Tuple.Create(Guid, SelectedItemGuid));
        }

        private void SelectItem(GraphicDesignerSettingItemModel itemModel)
        {
            MainComponentSettingsUserControl.Guid = itemModel.Guid;
            MainComponentSettingsUserControl.DisplayName = itemModel.DisplayName;
            MainComponentSettingsUserControl.Description = itemModel.Description;
            MainComponentSettingsUserControl.Type = itemModel.Type;
            MainComponentSettingsUserControl.TextValue = itemModel.Value;
            MainComponentSettingsUserControl.AvailableEnumValues = itemModel.AvailableEnumValues;
            MainComponentSettingsUserControl.EnableTextInput = itemModel.EnableTextInput;
            MainComponentSettingsUserControl.IsSettingChecked = itemModel.IsChecked;

        }

        private void NotifyToggled(Tuple<string, bool> t)
        {
            ToggledParameter ??= t;
            if (ToggledCommand != null && ToggledCommand.CanExecute(ToggledParameter))
            {
                ToggledCommand.Execute(ToggledParameter);
            }
            ToggledParameter = null;
        }

        private void NotifyValueChanged(Tuple<string, string> t)
        {
            ValueChangedParameter ??= t;
            if (ValueChangedCommand != null && ValueChangedCommand.CanExecute(ValueChangedParameter))
            {
                ValueChangedCommand.Execute(ValueChangedParameter);
            }
            ValueChangedParameter = null;
        }
        private void NotifyNowSelectedGuidChanged(Tuple<string, string> t)
        {
            NowSelectedGuidChangedParameter ??= t;
            if (NowSelectedGuidChangedCommand != null && NowSelectedGuidChangedCommand.CanExecute(NowSelectedGuidChangedParameter))
            {
                NowSelectedGuidChangedCommand.Execute(NowSelectedGuidChangedParameter);
            }
            NowSelectedGuidChangedParameter = null;
        }
    }
}
