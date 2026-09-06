using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.CompilerServices;
using WinUIMenuBar = Microsoft.UI.Xaml.Controls.MenuBar;

namespace CDPIUI.Controls.MenuManagement
{
    public static class MenuBarHost
    {
        private static readonly ConditionalWeakTable<WinUIMenuBar, MenuBarController> Controllers = new();

        public static Frame GetFrame(DependencyObject element) =>
            (Frame)element.GetValue(FrameProperty);

        public static void SetFrame(DependencyObject element, Frame value) =>
            element.SetValue(FrameProperty, value);

        public static readonly DependencyProperty FrameProperty = DependencyProperty.RegisterAttached(
            "Frame",
            typeof(Frame),
            typeof(MenuBarHost),
            new PropertyMetadata(null, OnFrameChanged));

        public static MenuBarController GetController(DependencyObject element) =>
            element is WinUIMenuBar menuBar && Controllers.TryGetValue(menuBar, out var controller)
                ? controller
                : null;

        private static void OnFrameChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is not WinUIMenuBar menuBar)
                return;

            if (Controllers.TryGetValue(menuBar, out var oldController))
            {
                oldController.Dispose();
                Controllers.Remove(menuBar);
            }

            if (args.NewValue is Frame frame)
                Controllers.Add(menuBar, new MenuBarController(menuBar, frame));
        }
    }
}
