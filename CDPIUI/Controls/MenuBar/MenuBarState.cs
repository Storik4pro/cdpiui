using Microsoft.UI.Xaml;

namespace CDPIUI.Controls.MenuManagement
{
    public static class MenuBarState
    {
        public static DataTemplate GetTemplate(DependencyObject element) =>
            (DataTemplate)element.GetValue(TemplateProperty);

        public static void SetTemplate(DependencyObject element, DataTemplate value) =>
            element.SetValue(TemplateProperty, value);

        public static readonly DependencyProperty TemplateProperty = DependencyProperty.RegisterAttached(
            "Template",
            typeof(DataTemplate),
            typeof(MenuBarState),
            new PropertyMetadata(null));
    }
}
