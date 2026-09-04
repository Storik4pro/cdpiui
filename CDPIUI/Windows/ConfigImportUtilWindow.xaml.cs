using CDPIUI.Default;
using CDPIUI.Views.ConfigImportUtil;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.Specialized;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ConfigImportUtilWindow : TemplateWindow
{
    private string targetStoreId = string.Empty;
    public string TargetStoreId
    {
        get => targetStoreId;
        set
        {
            targetStoreId = value ?? string.Empty;
            ContentFrame.Navigate(
                typeof(MainPage),
                new NameValueCollection { { "componentId", targetStoreId } },
                new SuppressNavigationTransitionInfo());
        }
    }

    public void ImportFiles(string[] paths, string componentId)
    {
        TargetStoreId = componentId;
        ((MainPage)ContentFrame.Content).QueueDroppedFiles(paths);
    }

    public ConfigImportUtilWindow()
    {
        InitializeComponent();

        ILocalizer localizer = Localizer.Get();
        WindowTitle = localizer.GetLocalizedString("ConfigImportUtilWindowTitle");
        IconUri = @"Assets/Icons/Import.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        DisableResizeFeature();

        MainFrame = ContentFrame;

        ContentFrame.Navigate(
            typeof(MainPage),
            new NameValueCollection { { "componentId", targetStoreId } },
            new SuppressNavigationTransitionInfo());
    }
}
