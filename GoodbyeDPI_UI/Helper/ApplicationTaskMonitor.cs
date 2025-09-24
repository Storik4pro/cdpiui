using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    public class ApplicationTaskMonitor
    {
        private static ApplicationTaskMonitor _instance;
        private static readonly object _lock = new object();

        public static ApplicationTaskMonitor Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ApplicationTaskMonitor();
                    return _instance;
                }
            }
        }

        private ApplicationTaskMonitor()
        {

        }

        public static bool IsComponentProcessRunned()
        {
            return ProcessManager.Instance.processState;
        }

        public static bool IsGoodCheckRunned()
        {
            return GoodCheckProcessHelper.Instance.IsRunned();
        }

        public static bool IsStoreWorking()
        {
            return StoreHelper.Instance.IsNowUpdatesChecked || !string.IsNullOrEmpty(StoreHelper.Instance.GetCurrentQueueOperationId());
        }
    }
}
