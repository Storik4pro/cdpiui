using CDPIUI.Shared;
using CDPIUI.Shared.Pipe.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace CDPIUI.Commands
{
    internal static class CommandUriMapper
    {
        private static readonly CommandRoute[] Routes =
        [
            CreateWindowRoute("Main", [], "MainWindow"),
            CreateWindowRoute("Main", ["Home"], "MainWindow", "HomePage"),
            CreateWindowRoute("Main", ["Utilities"], "MainWindow", "UtilsPage"),
            CreateWindowRoute("Main", ["Settings"], "MainWindow", "SettingsPage"),
            CreateWindowRoute(
                "Main",
                ["Settings", "Autorun"],
                "MainWindow",
                "Settings.AutorunPage"),
            CreateWindowRoute(
                "Main",
                ["Settings", "Personalization"],
                "MainWindow",
                "Settings.PersonalizePage"),
            CreateWindowRoute("Main", ["About"], "MainWindow", "AboutPage"),
            new(
                "Main",
                ["Updates"],
                _ => CreateShowWindowCommand(
                    "MainWindow",
                    "AboutPage",
                    new NameValueCollection
                    {
                        { "isUpdateRequested", bool.TrueString }
                    })),
            new(
                "Main",
                ["Components", "{componentId}"],
                parameters => CreateShowWindowCommand(
                    "MainWindow",
                    "Components.ViewComponentSettingsPage",
                    new NameValueCollection
                    {
                        { "componentId", parameters["componentId"] }
                    })),

            CreateWindowRoute("Store", [], "StoreWindow"),
            CreateWindowRoute("Store", ["Home"], "StoreWindow", "HomePage"),
            new(
                "Store",
                ["Catalog", "{itemId}"],
                parameters => CreateShowWindowCommand(
                    "StoreWindow",
                    "ItemViewPage",
                    new NameValueCollection
                    {
                        { "itemId", parameters["itemId"] },
                        { "setFocus", "ItemActionButton" }
                    })),
            new(
                "Store",
                ["Category", "{categoryId}"],
                parameters => CreateShowWindowCommand(
                    "StoreWindow",
                    "CategoryViewPage",
                    new NameValueCollection
                    {
                        { "categoryId", parameters["categoryId"] }
                    })),
            CreateWindowRoute("Store", ["Downloads"], "StoreWindow", "DownloadsPage"),
            new(
                "Store",
                ["Updates"],
                _ => CreateShowWindowCommand(
                    "StoreWindow",
                    "DownloadsPage",
                    new NameValueCollection
                    {
                        { "isUpdateRequested", bool.TrueString }
                    })),
            CreateWindowRoute("Store", ["Library"], "StoreWindow", "LibraryPage"),
            CreateWindowRoute("Store", ["Settings"], "StoreWindow", "SettingsPage"),
            CreateWindowRoute(
                "Store",
                ["Settings", "Memory"],
                "StoreWindow",
                "Settings.MemoryViewPage"),
            CreateWindowRoute(
                "Store",
                ["Settings", "Memory", "Application"],
                "StoreWindow",
                "Settings.Memory.MemoryViewApplicationFilesDetailsPage"),
            CreateWindowRoute(
                "Store",
                ["Settings", "Memory", "InstalledItems"],
                "StoreWindow",
                "Settings.Memory.MemoryViewInstalledItemsDetailsPage"),
            CreateWindowRoute(
                "Store",
                ["Settings", "Memory", "Logs"],
                "StoreWindow",
                "Settings.Memory.MemoryViewLogsDetailsPage"),
            CreateWindowRoute(
                "Store",
                ["Settings", "Memory", "Settings"],
                "StoreWindow",
                "Settings.Memory.MemoryViewSettingsDetailsPage"),
            CreateWindowRoute(
                "Store",
                ["Settings", "Memory", "StoreCache"],
                "StoreWindow",
                "Settings.Memory.MemoryViewStoreCachePage"),
            CreateWindowRoute(
                "Store",
                ["Settings", "Memory", "ConditionalLaunch"],
                "StoreWindow",
                "Settings.Memory.MemoryViewConditionalLaunchDetailsPage"),

            CreateWindowRoute("Tools", ["Console"], "ViewWindow"),
            CreateWindowRoute("Tools", ["ConditionalLaunch"], "ConditionalLaunchWindow"),
            CreateWindowRoute("Tools", ["AutoConfig"], "CreateConfigUtilWindow"),
            new(
                "Tools",
                ["AutoConfig", "{componentId}"],
                parameters => CreateShowWindowCommand(
                    "CreateConfigUtilWindow",
                    "MainPage",
                    new NameValueCollection
                    {
                        { "componentId", parameters["componentId"] }
                    })),
            CreateWindowRoute("Tools", ["ConfigEditor"], "CreateConfigHelperWindow"),
            CreateWindowRoute("Tools", ["ImportConfig"], "ConfigImportUtilWindow"),
            new(
                "Tools",
                ["ImportConfig", "{componentId}"],
                parameters => CreateShowWindowCommand(
                    "ConfigImportUtilWindow",
                    "MainPage",
                    new NameValueCollection
                    {
                        { "componentId", parameters["componentId"] }
                    })),
            new(
                "Tools",
                ["CreateConfig", "{componentId}"],
                parameters => CreateShowWindowCommand(
                    "CreateConfigHelperWindow",
                    "CreateNewConfigPage",
                    new NameValueCollection
                    {
                        { "type", "CFGCREATEBYID" },
                        { "componentId", parameters["componentId"] }
                    })),
            new(
                "Tools",
                ["EditConfig", "{kitId}"],
                parameters => CreateShowWindowCommand(
                    "CreateConfigHelperWindow",
                    "EditConfigKitPage",
                    new NameValueCollection
                    {
                        { "kitId", parameters["kitId"] }
                    })),
            CreateWindowRoute("Tools", ["Help"], "OfflineHelpWindow"),
            CreateWindowRoute("Tools", ["Proxy"], "ProxySetupUtilWindow"),
            CreateWindowRoute("Tools", ["Troubleshooting"], "TroubleshootingWindow"),
            new(
                "Tools",
                ["Troubleshooting", "BasicCheck"],
                _ => CreateShowWindowCommand(
                    "TroubleshootingWindow",
                    "WorkPage",
                    new NameValueCollection
                    {
                        { "action", "BeginBasicCheck" }
                    })),
            new(
                "Tools",
                ["Troubleshooting", "StoreCheck"],
                _ => CreateShowWindowCommand(
                    "TroubleshootingWindow",
                    "WorkPage",
                    new NameValueCollection
                    {
                        { "action", "BeginStoreRepoCheck" }
                    })),
            CreateWindowRoute("Tools", ["PresetTest"], "ConfigTestWindow"),
            CreateWindowRoute("Tools", ["Hosts"], "EditHostFileWindow"),

            CreateWindowRoute("Help", [], "OfflineHelpWindow"),
            new(
                "Help",
                ["{*helpUrl}"],
                parameters => CreateShowWindowCommand(
                    "OfflineHelpWindow",
                    pageParameters: new NameValueCollection
                    {
                        { "helpUrl", $"/{parameters["helpUrl"].Trim('/')}" }
                    })),

            new(
                "Actions",
                ["CheckForUpdates"],
                _ => new UpdateMessageModel
                {
                    MessageType = UpdateMessageIds.CheckForUpdates
                }),
            new(
                "Actions",
                ["CompatibilityCheck"],
                _ => new CompatibilityCheckMessageModel
                {
                    MessageType = CompatibilityCheckMessageIds.Begin
                })
        ];

        public static IPipeMessage? ConvertBack(string commandUri)
        {
            if (!Uri.TryCreate(commandUri, UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals(SharedConstants.Schema, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var routesForHost = Routes.Where(route =>
                route.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase));

            foreach (var route in routesForHost)
            {
                if (route.TryMatch(uri, out var parameters))
                    return route.CreateCommand(parameters);
            }

            if (routesForHost.Any())
                return null;

            try
            {
                return PipeModelConvertor.ConvertBack(commandUri);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        private static CommandRoute CreateWindowRoute(
            string host,
            string[] path,
            string windowName,
            string? pageName = null)
        {
            return new CommandRoute(
                host,
                path,
                _ => CreateShowWindowCommand(windowName, pageName));
        }

        private static PresentationMessageModel CreateShowWindowCommand(
            string windowName,
            string? pageName = null,
            NameValueCollection? pageParameters = null)
        {
            var parameters = pageParameters ?? new NameValueCollection();
            parameters["windowName"] = windowName;

            if (!string.IsNullOrWhiteSpace(pageName))
                parameters["page"] = pageName;

            return new PresentationMessageModel
            {
                MessageType = PresentationMessageIds.ShowWindow,
                MessageData = parameters
            };
        }

        private sealed class CommandRoute(
            string host,
            string[] pathPattern,
            Func<IReadOnlyDictionary<string, string>, IPipeMessage> commandFactory)
        {
            public string Host { get; } = host;

            public IPipeMessage CreateCommand(IReadOnlyDictionary<string, string> parameters)
            {
                return commandFactory(parameters);
            }

            public bool TryMatch(Uri uri, out IReadOnlyDictionary<string, string> parameters)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                parameters = values;

                if (!Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
                    return false;

                var pathSegments = uri.AbsolutePath.Split(
                    ['/'],
                    StringSplitOptions.RemoveEmptyEntries);

                var hasCatchAll = pathPattern.Length > 0 &&
                    IsCatchAll(pathPattern[^1]);

                if ((!hasCatchAll && pathSegments.Length != pathPattern.Length) ||
                    (hasCatchAll && pathSegments.Length < pathPattern.Length))
                {
                    return false;
                }

                for (var index = 0; index < pathPattern.Length; index++)
                {
                    var patternSegment = pathPattern[index];

                    if (IsCatchAll(patternSegment))
                    {
                        var remainingSegments = new List<string>();

                        for (var pathIndex = index; pathIndex < pathSegments.Length; pathIndex++)
                        {
                            if (!TryDecodeSegment(pathSegments[pathIndex], out var remainingSegment))
                                return false;

                            remainingSegments.Add(remainingSegment);
                        }

                        var value = string.Join("/", remainingSegments);
                        if (string.IsNullOrWhiteSpace(value))
                            return false;

                        values[patternSegment[2..^1]] = value;
                        return true;
                    }

                    string actualSegment;

                    if (!TryDecodeSegment(pathSegments[index], out actualSegment))
                        return false;

                    if (patternSegment.StartsWith("{") && patternSegment.EndsWith("}"))
                    {
                        if (string.IsNullOrWhiteSpace(actualSegment))
                            return false;

                        values[patternSegment[1..^1]] = actualSegment;
                        continue;
                    }

                    if (!patternSegment.Equals(actualSegment, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }

            private static bool IsCatchAll(string patternSegment)
            {
                return patternSegment.StartsWith("{*") &&
                    patternSegment.EndsWith("}");
            }

            private static bool TryDecodeSegment(string segment, out string value)
            {
                try
                {
                    value = Uri.UnescapeDataString(segment);
                    return true;
                }
                catch (UriFormatException)
                {
                    value = string.Empty;
                    return false;
                }
            }
        }
    }
}
