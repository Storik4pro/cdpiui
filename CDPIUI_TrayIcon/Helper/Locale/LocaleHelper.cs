using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class LocaleHelper
    {
        public static Dictionary<string, string> English = new Dictionary<string, string>()
        {
            { "UpdateFailure", "Application update failed.\nClick or tap here to open log" },
            { "TrayHide", "Application is runned and minimized to tray now.\nClick or tap here to open main window" },
            { "Autorun", "Autorun" },
            { "AutorunERR", "Cannot add this application to autorun." },
            { "ProcRun", "Process is runned now.\nClick or tap here to open open pseudoconsole and view process output" },
            { "ProcStop", "Process is stopped now.\nClick or tap here to open open pseudoconsole and view process output" },
            { "ProcERR", "Cannot start process." },
            { "Exit", "Exit" },
            { "ShowMainWindow", "Maximize app" },
            { "UpdateAvailable", "New application version is available.\nClick or tap here to open download page" },
            { "Start", "Start" },
            { "Stop", "Stop" },
            { "Utils", "Utils" },
            { "Pseudoconsole", "Open pseudoconsole (View process output)" },
            { "MsiInstallerHelper", "MSI installer helper" },
            { "MsiInstallerHelperErr", "Cannot install application package {0}. Error code is: {1}" },
            { "ProxySetupAsk", "Component {0} requires additional configuration before it can be used.\nClick or tap here to configure it." },
            { "CompatibilityCheckAssistant", "Compatibility check assistant" },
            { "ConfigRequiredNewestVersionOfComponent", "One or several installed items required newest version of {0}.\nClick or tap here to check updates for {0}" }
        };

        public static Dictionary<string, string> Russian = new Dictionary<string, string>()
        {
            { "UpdateFailure", "Не удалось обновить приложение.\nНажмите или коснитесь здесь, чтобы покзать журнал" },
            { "TrayHide", "Приложение запущено и свернуто в системный лоток.\nНажмите или коснитесь здесь, чтобы показать интерфейс" },
            { "Autorun", "Автозапуск" },
            { "AutorunERR", "Не удалось добавить приложение в автозапуск." },
            { "ProcRun", "Процесс запущен.\nНажмите или коснитесь здесь, чтобы просмотреть вывод процесса" },
            { "ProcStop", "Процесс остановлен.\nНажмите или коснитесь здесь, чтобы просмотреть вывод процесса" },
            { "ProcERR", "Не удалось запустить процесс." },
            { "Exit", "Выход" },
            { "ShowMainWindow", "Восстановить" },
            { "UpdateAvailable", "Доступна новая версия приложения.\nНажмите или коснитесь здесь, чтобы узнать подробности" },
            { "Start", "Запустить" },
            { "Stop", "Остановить" },
            { "Utils", "Утилиты" },
            { "Pseudoconsole", "Открыть псевдоконсоль (Просмотр вывода процесса)" },
            { "MsiInstallerHelper", "Помощник настройки MSI" },
            { "MsiInstallerHelperErr", "Не удалось установить пакет приложения {0}. Код ошибки: {1}" },
            { "ProxySetupAsk", "Компонент {0} требует дополнительной настройки перед началом работы.\nНажмите или коснитесь здесь, чтобы настроить его." },
            { "CompatibilityCheckAssistant", "Помощник проверки совместимости" },
            { "ConfigRequiredNewestVersionOfComponent", "Один или несколько установленных элементов требуют более новую версию {0} для корректной работы.\nНажмите или коснитесь здесь, чтобы проверить наличие обновлений для {0}" }
        };

        public static string GetLocaleString(string key)
        {
            try
            {
                var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return culture switch
                {
                    "en" => English.ContainsKey(key) ? English[key] : key,
                    "ru" => Russian.ContainsKey(key) ? Russian[key] : (English.ContainsKey(key) ? English[key] : key),
                    _ => English.ContainsKey(key) ? English[key] : key,
                };
            }
            catch (Exception)
            {
                return key;
            }
        }
    }
}
