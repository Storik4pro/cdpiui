using System;
using System.Collections.Generic;
using System.Data;

namespace CDPIUI.Shared
{


    public static class SharedConstants
    {



        #region Store

        // Local

        public static string StoreRepo = "Storik4pro/CDPIUI-Store";
        public static string GitLabStoreRepo = "Storik4/CDPIUI-Store";

        public static string ApplicationStoreId = "CDPIUIAppSt";
        public static string LocalUserItemsId = "LocalUserStorage";

        public static string LocalUserItemSiteListsFolder = "List";
        public static string LocalUserItemBinsFolder = "Bin";
        public static string LocalUserItemLocFolder = "Loc";

        // Repository

        public static string ApplicationCheckUpdatesUrl = "https://github.com/Storik4pro/cdpiui";
        public static string ApplicationGitLabCheckUpdatesUrl = "https://gitlab.com/Storik4/CDPI-UI";

        // Database

        public static string DatabaseFileName = "storedata.db";

        #endregion

        public static string PipeName = "{203ABE1D-01A2-47C0-B33D-8C8F7934CF4F}_CDPIUI";

        public static string Schema = "cdpiui";

        public static bool IsPreview = true;
    }
}
