using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoodbyeDPI_UI.ViewModels;

namespace GoodbyeDPI_UI.Helper
{
    internal class StateHelper
    {
        private static StateHelper _instance;
        private static readonly object _lock = new object();

        public static StateHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new StateHelper();
                    return _instance;
                }
            }
        }

        public string workDirectory = Directory.GetCurrentDirectory();

        // Components

        public ComponentSettings goodbyedpiSettings = new("goodbyedpi");
        public ComponentSettings zapretSettings = new("zapret");
        public ComponentSettings byedpiSettings = new("byedpi");
        public ComponentSettings spoofdpiSettings = new("spoofdpi");

        public bool isCheckedComponentsUpdateComplete = false;
        public string lastComponentsUpdateError = "";

        private StateHelper()
        {

        }

    }
}
