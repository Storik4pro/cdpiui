using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;
using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace CDPIUI.TrayIcon.ConditionalLaunch
{
    internal readonly record struct ConditionalActionResult(bool Success, string? Error = null)
    {
        public static ConditionalActionResult Ok() => new(true);
        public static ConditionalActionResult Failed(string error) => new(false, error);
    }

    internal static class ConditionalActionExecutor
    {
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<ConditionalActionResult>>
            PendingCoreActions = new(StringComparer.OrdinalIgnoreCase);

        public static async Task ExecuteTaskAsync(ConditionalTask task)
        {
            foreach (var action in task.Actions)
            {
                ConditionalActionResult result;
                try
                {
                    result = await ExecuteActionAsync(action, task.Name);
                }
                catch (Exception ex)
                {
                    result = ConditionalActionResult.Failed(ex.Message);
                }

                if (result.Success)
                    continue;

                Logger.Instance.CreateErrorLog(
                    nameof(ConditionalActionExecutor),
                    $"Task '{task.Name}', action '{action.Type}' failed: {result.Error}");

                if (task.StopAfterError)
                    break;
            }
        }

        public static void CompleteCoreAction(
            string? operationId,
            string? successValue,
            string? error)
        {
            if (string.IsNullOrWhiteSpace(operationId) ||
                !PendingCoreActions.TryRemove(operationId, out var completion))
            {
                return;
            }

            completion.TrySetResult(
                bool.TryParse(successValue, out var success) && success
                    ? ConditionalActionResult.Ok()
                    : ConditionalActionResult.Failed(error ?? "The Core action failed."));
        }

        private static async Task<ConditionalActionResult> ExecuteActionAsync(
            ConditionalAction action,
            string taskName)
        {
            switch (action.Type)
            {
                case ConditionalActionType.ApplyPreset:
                case ConditionalActionType.StartComponent:
                case ConditionalActionType.StartAutorunComponents:
                case ConditionalActionType.CheckStoreUpdates:
                case ConditionalActionType.CheckApplicationUpdates:
                    return await ExecuteCoreActionAsync(action);

                case ConditionalActionType.StopComponent:
                    {
                        var componentId = RequireParameter(action, "componentId");
                        if (await TasksHelper.Instance.GetTaskFromId(componentId) == null)
                            return ConditionalActionResult.Failed($"Component '{componentId}' was not found.");
                        await TasksHelper.Instance.StopTask(componentId);
                    }
                    return ConditionalActionResult.Ok();

                case ConditionalActionType.RestartComponent:
                    {
                        var componentId = RequireParameter(action, "componentId");
                        if (await TasksHelper.Instance.GetTaskFromId(componentId) == null)
                            return ConditionalActionResult.Failed($"Component '{componentId}' was not found.");
                        await TasksHelper.Instance.RestartTask(componentId);
                    }
                    return ConditionalActionResult.Ok();

                case ConditionalActionType.StopAllComponents:
                    await TasksHelper.Instance.StopAllTasks();
                    return ConditionalActionResult.Ok();

                case ConditionalActionType.StopNetworkService:
                    await TasksHelper.Instance.StopService();
                    return ConditionalActionResult.Ok();

                case ConditionalActionType.RunCompatibilityCheck:
                    return ResultFromSend(await PipeHelper.SendCompatibilityCheckPacket());

                case ConditionalActionType.RunBasicDiagnostics:
                    return ResultFromSend(await OpenWindowAsync(
                        "TroubleshootingWindow",
                        "WorkPage",
                        new() { { "action", "BeginBasicCheck" } }));

                case ConditionalActionType.RunStoreDiagnostics:
                    return ResultFromSend(await OpenWindowAsync(
                        "TroubleshootingWindow",
                        "WorkPage",
                        new() { { "action", "BeginStoreRepoCheck" } }));

                case ConditionalActionType.OpenMainPage:
                    return ResultFromSend(await OpenMainPageAsync(action));

                case ConditionalActionType.OpenStorePage:
                    return ResultFromSend(await OpenStorePageAsync(action));

                case ConditionalActionType.OpenTool:
                    return ResultFromSend(await OpenToolAsync(action));

                case ConditionalActionType.OpenHelp:
                    {
                        var helpUrl = action.GetParameter("helpUrl");
                        NameValueCollection data = [];
                        if (!string.IsNullOrWhiteSpace(helpUrl))
                            data["helpUrl"] = "/" + helpUrl.Trim('/');
                        return ResultFromSend(await PipeHelper.SendOpenWindowPacket(
                            "OfflineHelpWindow", data, openIfNotConnected: true));
                    }

                case ConditionalActionType.Wait:
                    {
                        var milliseconds = ParseInteger(action, "milliseconds", 0, 86_400_000);
                        await Task.Delay(milliseconds);
                        return ConditionalActionResult.Ok();
                    }

                case ConditionalActionType.ShowNotification:
                    NotifyHelper.ShowMessage(
                        string.Format(
                            LocaleHelper.GetLocaleString("ConditionalTaskNotificationTitle"),
                            taskName),
                        action.GetParameter("message") ?? string.Empty,
                        string.Empty);
                    return ConditionalActionResult.Ok();

                default:
                    return ConditionalActionResult.Failed(
                        $"Action '{action.Type}' is not supported.");
            }
        }

        private static async Task<ConditionalActionResult> ExecuteCoreActionAsync(
            ConditionalAction action)
        {
            var operationId = Guid.NewGuid().ToString("D");
            var completion = new TaskCompletionSource<ConditionalActionResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!PendingCoreActions.TryAdd(operationId, completion))
                return ConditionalActionResult.Failed("Cannot create an action operation.");

            try
            {
                if (!await PipeHelper.SendConditionalActionPacket(operationId, action))
                    return ConditionalActionResult.Failed("Cannot send the action to CDPIUI.Core.");

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                try
                {
                    return await completion.Task.WaitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    return ConditionalActionResult.Failed("CDPIUI.Core did not confirm the action in time.");
                }
            }
            finally
            {
                PendingCoreActions.TryRemove(operationId, out _);
            }
        }

        private static async Task<bool> OpenMainPageAsync(ConditionalAction action)
        {
            var target = RequireParameter(action, "target");
            return target switch
            {
                "Home" => await OpenWindowAsync("MainWindow", "HomePage"),
                "Utilities" => await OpenWindowAsync("MainWindow", "UtilsPage"),
                "Settings" => await OpenWindowAsync("MainWindow", "SettingsPage"),
                "AutorunSettings" => await OpenWindowAsync("MainWindow", "Settings.AutorunPage"),
                "Personalization" => await OpenWindowAsync("MainWindow", "Settings.PersonalizePage"),
                "About" => await OpenWindowAsync("MainWindow", "AboutPage"),
                "Updates" => await OpenWindowAsync(
                    "MainWindow", "AboutPage", new() { { "isUpdateRequested", bool.TrueString } }),
                "ComponentSettings" => await OpenWindowAsync(
                    "MainWindow",
                    "Components.ViewComponentSettingsPage",
                    new() { { "componentId", RequireParameter(action, "componentId") } }),
                _ => false
            };
        }

        private static async Task<bool> OpenStorePageAsync(ConditionalAction action)
        {
            var target = RequireParameter(action, "target");
            return target switch
            {
                "Home" => await OpenWindowAsync("StoreWindow", "HomePage"),
                "CatalogItem" => await OpenWindowAsync(
                    "StoreWindow", "ItemViewPage", new()
                    {
                        { "itemId", RequireParameter(action, "itemId") },
                        { "setFocus", "ItemActionButton" }
                    }),
                "Category" => await OpenWindowAsync(
                    "StoreWindow", "CategoryViewPage", new()
                    {
                        { "categoryId", RequireParameter(action, "categoryId") }
                    }),
                "Downloads" => await OpenWindowAsync("StoreWindow", "DownloadsPage"),
                "Updates" => await OpenWindowAsync(
                    "StoreWindow", "DownloadsPage", new() { { "isUpdateRequested", bool.TrueString } }),
                "Library" => await OpenWindowAsync("StoreWindow", "LibraryPage"),
                "Settings" => await OpenWindowAsync("StoreWindow", "SettingsPage"),
                "Memory" => await OpenWindowAsync("StoreWindow", "Settings.MemoryViewPage"),
                "MemoryApplication" => await OpenWindowAsync(
                    "StoreWindow", "Settings.Memory.MemoryViewApplicationFilesDetailsPage"),
                "MemoryInstalledItems" => await OpenWindowAsync(
                    "StoreWindow", "Settings.Memory.MemoryViewInstalledItemsDetailsPage"),
                "MemoryLogs" => await OpenWindowAsync(
                    "StoreWindow", "Settings.Memory.MemoryViewLogsDetailsPage"),
                "MemorySettings" => await OpenWindowAsync(
                    "StoreWindow", "Settings.Memory.MemoryViewSettingsDetailsPage"),
                "MemoryStoreCache" => await OpenWindowAsync(
                    "StoreWindow", "Settings.Memory.MemoryViewStoreCachePage"),
                _ => false
            };
        }

        private static async Task<bool> OpenToolAsync(ConditionalAction action)
        {
            var target = RequireParameter(action, "target");
            return target switch
            {
                "ComponentConsole" => await PipeHelper.SendOpenWindowPacket(
                    "ViewWindow",
                    new() { { "id", RequireParameter(action, "componentId") } },
                    openIfNotConnected: true),
                "AutoConfig" => await OpenWindowAsync(
                    "CreateConfigUtilWindow",
                    "MainPage",
                    OptionalParameter(action, "componentId")),
                "ConfigEditor" => await PipeHelper.SendOpenWindowPacket(
                    "CreateConfigHelperWindow", openIfNotConnected: true),
                "CreateConfig" => await OpenWindowAsync(
                    "CreateConfigHelperWindow",
                    "CreateNewConfigPage",
                    new()
                    {
                        { "type", "CFGCREATEBYID" },
                        { "componentId", RequireParameter(action, "componentId") }
                    }),
                "EditConfigPack" => await OpenWindowAsync(
                    "CreateConfigHelperWindow",
                    "EditConfigKitPage",
                    new() { { "kitId", RequireParameter(action, "kitId") } }),
                "ProxySetup" => await PipeHelper.SendOpenWindowPacket(
                    "ProxySetupUtilWindow", openIfNotConnected: true),
                "Troubleshooting" => await PipeHelper.SendOpenWindowPacket(
                    "TroubleshootingWindow", openIfNotConnected: true),
                "PresetTest" => await PipeHelper.SendOpenWindowPacket(
                    "ConfigTestWindow", openIfNotConnected: true),
                "HostsEditor" => await PipeHelper.SendOpenWindowPacket(
                    "EditHostFileWindow", openIfNotConnected: true),
                _ => false
            };
        }

        private static NameValueCollection OptionalParameter(
            ConditionalAction action,
            string name)
        {
            NameValueCollection result = [];
            var value = action.GetParameter(name);
            if (!string.IsNullOrWhiteSpace(value))
                result[name] = value;
            return result;
        }

        private static async Task<bool> OpenWindowAsync(
            string window,
            string page,
            NameValueCollection? parameters = null)
        {
            parameters ??= [];
            parameters["page"] = page;
            return await PipeHelper.SendOpenWindowPacket(
                window, parameters, openIfNotConnected: true);
        }

        private static ConditionalActionResult ResultFromSend(bool sent)
        {
            return sent
                ? ConditionalActionResult.Ok()
                : ConditionalActionResult.Failed("The action could not be sent.");
        }

        private static string RequireParameter(ConditionalAction action, string name)
        {
            var value = action.GetParameter(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Parameter '{name}' is required.");
            return value;
        }

        private static int ParseInteger(
            ConditionalAction action,
            string name,
            int minimum,
            int maximum)
        {
            if (!int.TryParse(RequireParameter(action, name), out var value) ||
                value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    $"Value must be between {minimum} and {maximum}.");
            }
            return value;
        }
    }
}
