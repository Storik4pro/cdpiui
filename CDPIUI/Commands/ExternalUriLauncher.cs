using CDPIUI.Helper;
using CDPIUI.ViewModels;
using System.Windows.Input;

namespace CDPIUI.Commands
{
    internal class ExternalUriLauncher
    {
        #region UI link handle
        private static ExternalUriLauncher _instance;
        private static readonly object _lock = new();
        public static ExternalUriLauncher Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ExternalUriLauncher();
                    return _instance;
                }
            }
        }

        public ICommand Command { get; }

        private ExternalUriLauncher()
        {
            Command = new RelayCommand(ExecuteCommand);
        }

        private void ExecuteCommand(object parameter)
        {
            if (parameter is string uri)
            {
                UrlOpenHelper.LaunchUrl(uri);
            }
        }
        #endregion
    }
}
