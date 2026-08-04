#nullable enable

using CDPIUI.Default;
using CDPIUI.Views.ConfigImportUtil;
using Microsoft.UI.Xaml;
using System.Collections.Specialized;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ConfigImportUtilWindow : TemplateWindow
{
    public static ConfigImportUtilWindow? Instance { get; private set; }

    private string targetStoreId = string.Empty;
    public string TargetStoreId
    {
        get => targetStoreId;
        set
        {
            targetStoreId = value ?? string.Empty;
            ContentFrame.Navigate(
                typeof(MainPage),
                new NameValueCollection { { "componentId", targetStoreId } });
        }
    }

    public ConfigImportUtilWindow()
    {
        InitializeComponent();

        ILocalizer localizer = Localizer.Get();
        WindowTitle = localizer.GetLocalizedString("ConfigImportUtilWindowTitle");
        IconUri = @"Assets/Icons/Import.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        DisableResizeFeature();

        Instance = this;
        MainFrame = ContentFrame;
        ContentFrame.Navigate(
            typeof(MainPage),
            new NameValueCollection { { "componentId", targetStoreId } });

        Closed += ConfigImportUtilWindow_Closed;
    }

    private void ConfigImportUtilWindow_Closed(object sender, WindowEventArgs args)
    {
        Instance = null;
        Closed -= ConfigImportUtilWindow_Closed;
    }
}
