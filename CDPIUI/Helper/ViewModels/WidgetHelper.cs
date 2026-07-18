using CDPIUI.Controls.Dialogs.MainPage;
using CDPIUI.Default;
using CDPIUI.Helper.Static;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Helper.ViewModels
{
    public static class WidgetHelper
    {
        public class LaunchActionWithParameterModel<T>
        {
            public Type Target { get; set; }
            public object Parameter { get; set; }
        }

        public static Dictionary<WidgetType, Type> AllowedWidgetTypeObjectTypes = new()
        {
            { WidgetType.OpenWindow, typeof(LaunchActionWithParameterModel<TemplateWindow>) },
            { WidgetType.OpenDialog, typeof(LaunchActionWithParameterModel<ContentDialog>) },
            { WidgetType.LaunchUrl, typeof(string) },
            { WidgetType.NavigateToPage, typeof(string) },
        };

        public static WidgetViewModel CreateWidget(WidgetType type, object actionObject, string nameLocKey, string descriptionLocKey, Uri imageUri, bool asMonochrome = false)
        {
            ILocalizer localizer = Localizer.Get();
            string displayName = localizer.GetLocalizedString($"/Widgets/{nameLocKey}");
            string displayDescription = localizer.GetLocalizedString($"/Widgets/{descriptionLocKey}");



            return new WidgetViewModel()
            {
                Id = Guid.NewGuid(),
                Type = type,
                ActionObject = actionObject,

                Name = string.IsNullOrEmpty(displayName) ? nameLocKey : displayName,
                Description = string.IsNullOrEmpty(displayDescription) ? descriptionLocKey : displayDescription,
                UriImageSource = imageUri,
                ShowAsMonochrome = asMonochrome,

                ShowOpenInNewWindowBadge = type == WidgetType.LaunchUrl,

            };
        }

        public static ObservableCollection<WidgetViewModel> GetAllWidgets()
        {
            ObservableCollection<WidgetViewModel> widgets = [];

            widgets.Add(
                CreateWidget(
                    WidgetType.OpenWindow,
                    new LaunchActionWithParameterModel<TemplateWindow>() { Target = typeof(CreateConfigUtilWindow), Parameter = null },
                    "Autoselection",
                    "AutoselectionDescription",
                    UIHelper.GetUriFromString("ms-appx:///Assets/Icons/GoodCheck.ico"),
                    false));

            widgets.Add(
                CreateWidget(
                    WidgetType.OpenWindow, 
                    new LaunchActionWithParameterModel<TemplateWindow>() { Target = typeof(StoreWindow), Parameter = null }, 
                    "OpenStore", 
                    "OpenStoreDescription", 
                    UIHelper.GetUriFromString("ms-appx:///Assets/Icons/Store.png"),
                    true));

            widgets.Add(
                CreateWidget(
                    WidgetType.OpenDialog, 
                    new LaunchActionWithParameterModel<ContentDialog>() { Target = typeof(CommunityContentDialog), Parameter = null }, 
                    "AskCommunity",
                    "AskCommunityDescription", 
                    UIHelper.GetUriFromString("ms-appx:///Assets/Icons/telegram.png"),
                    true));

            widgets.Add(
                CreateWidget(
                    WidgetType.OpenWindow, 
                    new LaunchActionWithParameterModel<TemplateWindow>() { Target = typeof(OfflineHelpWindow), Parameter = null }, 
                    "OpenLocalHelp",
                    "OpenLocalHelpDescription", 
                    UIHelper.GetUriFromString("ms-appx:///Assets/Icons/help.ico"),
                    false));

            widgets.Add(
                CreateWidget(
                    WidgetType.LaunchUrl, 
                    UrlOpenHelper.ReportUrl, 
                    "ReportAProblem",
                    "ReportAProblemDescription", 
                    UIHelper.GetUriFromString("ms-appx:///Assets/Icons/github.png"),
                    true));

            widgets.Add(
                CreateWidget(
                    WidgetType.OpenWindow, 
                    new LaunchActionWithParameterModel<TemplateWindow>() { Target = typeof(TroubleshootingWindow), Parameter = null}, 
                    "RecoverApp",
                    "RecoverAppDescription", 
                    UIHelper.GetUriFromString("ms-appx:///Assets/Icons/Troubleshooting.ico"),
                    false));

            widgets.Add(
                CreateWidget(
                    WidgetType.OpenWindow, 
                    new LaunchActionWithParameterModel<TemplateWindow>() { Target = typeof(EditHostFileWindow), Parameter = null}, 
                    "ReplaceHostsFile",
                    "ReplaceHostsFileDescription", 
                    UIHelper.GetUriFromString("ms-appx:///Assets/Icons/EditHostsFile.ico"),
                    false));

            return widgets;
        }

        public static async void RunActions(WidgetType actionType, object actionObject, XamlRoot xamlRoot)
        {
            if (AllowedWidgetTypeObjectTypes.TryGetValue(actionType, out Type expectedType) && expectedType == actionObject.GetType())
            {
                switch(actionType)
                {
                    case WidgetType.OpenWindow:
                        if (((LaunchActionWithParameterModel<TemplateWindow>)actionObject).Parameter == null)
                            await ((App)Application.Current).SafeCreateNewWindow(((LaunchActionWithParameterModel<TemplateWindow>)actionObject).Target);
                        else
                            await ((App)Application.Current).UnsafeCreateNewWindow(
                                ((LaunchActionWithParameterModel<TemplateWindow>)actionObject).Target, 
                                id:(string)((LaunchActionWithParameterModel<TemplateWindow>)actionObject).Parameter);
                        break;
                    case WidgetType.OpenDialog:
                        var dialog = (ContentDialog)Activator.CreateInstance(((LaunchActionWithParameterModel<ContentDialog>)actionObject).Target);

                        dialog.XamlRoot = xamlRoot;

                        await dialog.ShowAsync();
                        break;

                    case WidgetType.LaunchUrl: 
                        UrlOpenHelper.LaunchUrl((string)actionObject);
                        break;
                    case WidgetType.NavigateToPage:
                        break;
                }
            }
        }
    }
}
