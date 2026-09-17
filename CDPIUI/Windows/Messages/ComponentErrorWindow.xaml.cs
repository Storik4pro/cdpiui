using CDPIUI.Commands;
using CDPIUI.Core.Store.Database;
using CDPIUI.Default;
using CDPIUI.Shared.PrettyErrorConvertionService;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ComponentErrorWindow : TemplateWindow
{
    public ComponentErrorWindow()
    {
        InitializeComponent();
        WindowTitle = Localizer.Get().GetLocalizedString("ComponentFailureTitle");
        IconUri = @"Assets/favicon.ico";
        CustomTitleBarUserControl = TitleBarControl;
        DisableResizeFeature();
    }

    public void SetError(string componentId, ErrorModel error)
    {
        Id = componentId;
        var localizer = Localizer.Get();
        var item = DatabaseHelper.Instance.GetItemById(componentId);
        Heading.Text = localizer.GetLocalizedString("ComponentFailureTitle");
        Description.Text = string.Format(localizer.GetLocalizedString("ComponentFailureDescription"),
            item?.ShortName ?? item?.Name ?? componentId);
        ErrorCode.Text = error?.ErrorCode ?? "UNKNOWN_ERROR";
        ConsoleLink.Tag = $"cdpiui://Tools/Console/{Uri.EscapeDataString(componentId)}";
    }

    private void ConsoleLink_Click(object sender, RoutedEventArgs e) => Link_Click(sender, e);
    private void Link_Click(object sender, RoutedEventArgs e) =>
        CommandsHandler.HandleCommand((string)((HyperlinkButton)sender).Tag);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
