using CDPIUI.Default;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Helper.WindowHelper
{
    internal class WindowOpenHelper
    {
        public static async Task OpenAsync(NameValueCollection parameters)
        {
            var windowName = parameters["windowName"];

            if (string.IsNullOrWhiteSpace(windowName)) return;

            if (windowName == "MainWindow") windowName = "ModernMainWindow";
            if (windowName == "LegacyMainWindow") windowName = "MainWindow";

            if (windowName is "ModernMainWindow" or "MainWindow" &&
                (((App)Application.Current).OpenWindows.OfType<WelcomeWindow>().Any() ||
                 !Core.SettingsManager.Instance.GetValueOrDefault("WELCOMEWIZARD", "Shown", defaultValue: false)))
            {
                await ((App)Application.Current).OpenStartupWindowAsync();
                return;
            }

            var pageName = parameters["page"];
            var id = parameters["id"];

            var pageParameters = new NameValueCollection();
            foreach (string key in parameters.AllKeys)
            {
                if (key == null)
                    continue;

                if (key.Equals("windowName", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("page", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pageParameters[key] = parameters[key];
            }

            Window window;

            if (!string.IsNullOrWhiteSpace(id))
            {
                var type = GetWindowType(windowName);
                if (type == null) return;
                window = await ((App)Application.Current)
                    .UnsafeCreateNewWindow(type, id: id);
            }
            else
            {
                var type = GetWindowType(windowName);
                if (type == null) return;
                window = await ((App)Application.Current)
                    .SafeCreateNewWindow(type);
            }

            if (window is OfflineHelpWindow helpWindow &&
                parameters["helpUrl"] is string helpUrl &&
                !string.IsNullOrWhiteSpace(helpUrl))
            {
                helpWindow.NavigateToPage(helpUrl);
            }

            if (!string.IsNullOrWhiteSpace(pageName))
            {
                if (window is not TemplateWindow baseWindow) return;

                var pType = GetPageType(windowName, pageName);
                if (pType == null) return;
                baseWindow.NavigateToPageWithParameter(
                    pType,
                    pageParameters.Count > 0 ? pageParameters : null);
            }
        }

        private static Type GetWindowType(string windowName)
        {
            var fullName = $"CDPIUI.{windowName}";

            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName, false))
                .FirstOrDefault(t => t != null)
                ?? null;
        }

        private static Type GetPageType(string windowName, string pageName)
        {
            var isModernMainWindow = windowName == "ModernMainWindow";

            if (isModernMainWindow && pageName == "HomePage")
                return ModernMainWindow.GetMainPage();

            windowName = isModernMainWindow ? "Main" : windowName;
            var windowPrefix = windowName.EndsWith("Window", StringComparison.Ordinal)
                ? windowName[..^"Window".Length]
                : windowName;

            var fullNames = new List<string>
            {
                $"CDPIUI.Views.{windowPrefix}.{pageName}"
            };

            // Main navigation pages live both directly under CDPIUI.Views and
            // under CDPIUI.Views.Settings, not only under CDPIUI.Views.Main.
            if (isModernMainWindow)
                fullNames.Add($"CDPIUI.Views.{pageName}");

            if (windowName == "StoreWindow" && pageName == "HomePage")
                fullNames.Add($"CDPIUI.{pageName}");

            foreach (var fullName in fullNames)
            {
                Debug.WriteLine(fullName);

                var pageType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(fullName, false))
                    .FirstOrDefault(t => t != null);

                if (pageType != null)
                    return pageType;
            }

            return null;
        }
    }
}
