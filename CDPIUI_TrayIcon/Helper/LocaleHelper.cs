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
        };

        public static Dictionary<string, string> Russian = new Dictionary<string, string>()
        {
            { "UpdateFailure", "Не удалось обновить приложение.\nНажмите или коснитесь здесь, чтобы открыть лог" },
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
