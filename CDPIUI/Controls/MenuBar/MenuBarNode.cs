using Microsoft.UI.Xaml;

namespace CDPIUI.Controls.MenuManagement
{
    public enum MenuBarItemLifetime
    {
        Default,
        Weak,
        Strong,
    }

    public static class MenuBarNode
    {
        public static string GetId(DependencyObject element) =>
            (string)element.GetValue(IdProperty);

        public static void SetId(DependencyObject element, string value) =>
            element.SetValue(IdProperty, value);

        public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
            "Id",
            typeof(string),
            typeof(MenuBarNode),
            new PropertyMetadata(null));

        public static MenuBarItemLifetime GetLifetime(DependencyObject element) =>
            (MenuBarItemLifetime)element.GetValue(LifetimeProperty);

        public static void SetLifetime(DependencyObject element, MenuBarItemLifetime value) =>
            element.SetValue(LifetimeProperty, value);

        public static readonly DependencyProperty LifetimeProperty = DependencyProperty.RegisterAttached(
            "Lifetime",
            typeof(MenuBarItemLifetime),
            typeof(MenuBarNode),
            new PropertyMetadata(MenuBarItemLifetime.Default));

        public static int GetOrder(DependencyObject element) =>
            (int)element.GetValue(OrderProperty);

        public static void SetOrder(DependencyObject element, int value) =>
            element.SetValue(OrderProperty, value);

        public static readonly DependencyProperty OrderProperty = DependencyProperty.RegisterAttached(
            "Order",
            typeof(int),
            typeof(MenuBarNode),
            new PropertyMetadata(int.MaxValue));
    }
}
