using CDPI_UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPI_UI.Helper.UserExperience
{
    public class LinkedSettingsHelper
    {
        public enum Pages
        {
            TgWsProxyPage,

        }
        public LinkedSettingsHelper() { }

        private static List<SettingLinkModel> TgWsProxyLinks = new()
        {
            new() { DisplayName = "CreateNewConfig", Action = LinkedActions.CreateNewConfigForComponent },
            new() { DisplayName = "EditConfig", Action = LinkedActions.EditCurrentConfig },
            new() { DisplayName = "OpenProxyInTelegram", Action = LinkedActions.OpenProxyInTelegram },
        };

        public static void LoadLinksForPage(Pages page, ObservableCollection<SettingLinkModel> collection)
        {
            collection.Clear();

            switch (page)
            {
                case Pages.TgWsProxyPage:
                    AppendListContentToObservableCollection(TgWsProxyLinks, collection);
                    break;
            }
        }


        private static void AppendListContentToObservableCollection(List<SettingLinkModel> list, ObservableCollection<SettingLinkModel> collection)
        {
            ILocalizer localizer = Localizer.Get();
            foreach (SettingLinkModel item in list)
            {
                collection.Add(new()
                {
                    DisplayName = localizer.GetLocalizedString($"/SettingTiles/{item.DisplayName}"),
                    Action = item.Action,
                });
            }
        }


    }
}
