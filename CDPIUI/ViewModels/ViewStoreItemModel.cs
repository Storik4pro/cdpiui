using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace CDPIUI.ViewModels
{
    public class ViewStoreItemModel
    {
        public string StoreId { get; set; }
        public string Name { get; set; }
        public string Developer { get; set; }
        public string ColorHEX { get; set; }
        public Brush ColorBrush
        {
            get => UIHelper.HexToSolidColorBrushConverter(ColorHEX);
        }
        
        public ImageSource ImageSource { get; set; }

        public static ViewStoreItemModel ConvertFromRepoItemModel(RepoItemModel repoItemModel)
        {
            return new()
            {
                StoreId = repoItemModel.store_id,
                Name = StoreHelper.Instance.GetLocalizedStoreItemName(repoItemModel.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                Developer = repoItemModel.developer,
                ColorHEX = repoItemModel.background,
                ImageSource = new BitmapImage(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(repoItemModel.icon)))
            };
        }
    }
}
