using CDPIUI.Default;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic
{
    internal class WindowOpenHelper
    {
        public static async Task OpenAsync(NameValueCollection parameters)
        {
            var windowName = parameters["windowName"];

            if (string.IsNullOrWhiteSpace(windowName)) return;

            if (windowName == "MainWindow") windowName = "ModernMainWindow";
            if (windowName == "LegacyMainWindow") windowName = "MainWindow";

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
            windowName = windowName == "ModernMainWindow" ? "Main" : windowName;
            var windowPrefix = windowName.EndsWith("Window", StringComparison.Ordinal)
                ? windowName[..^"Window".Length]
                : windowName;

            var fullName = $"CDPIUI.Views.{windowPrefix}.{pageName}";
            Debug.WriteLine(fullName);

            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName, false))
                .FirstOrDefault(t => t != null)
                ?? null;
        }
    }
}
