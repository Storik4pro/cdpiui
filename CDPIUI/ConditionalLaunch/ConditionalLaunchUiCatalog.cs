#nullable enable

using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.ConditionalLaunch;
using System;
using System.Collections.Generic;
using System.Linq;
using FormsKeys = System.Windows.Forms.Keys;
using WinUI3Localizer;

namespace CDPIUI.ConditionalLaunch
{
    internal static class ConditionalLaunchUiCatalog
    {
        public static List<ChoiceItem<ConditionalTaskPriority>> CreatePriorities(ILocalizer localizer) =>
        [
            new($"{Text(localizer, "CL_PriorityHigh")} (1)", ConditionalTaskPriority.High),
            new($"{Text(localizer, "CL_PriorityDefault")} (0)", ConditionalTaskPriority.Default),
            new($"{Text(localizer, "CL_PriorityLow")} (-1)", ConditionalTaskPriority.Low)
        ];

        public static List<ChoiceItem<ConditionalTriggerType>> CreateTriggerTypes(ILocalizer localizer) =>
        [
            new(Text(localizer, "CL_TriggerHotKey"), ConditionalTriggerType.HotKey),
            new(Text(localizer, "CL_TriggerProcessStarted"), ConditionalTriggerType.ProcessStarted),
            new(Text(localizer, "CL_TriggerProcessStopped"), ConditionalTriggerType.ProcessStopped)
        ];

        public static List<ChoiceItem<FormsKeys>> CreateHotKeyChoices()
        {
            List<ChoiceItem<FormsKeys>> result = [];
            for (var key = FormsKeys.A; key <= FormsKeys.Z; key++)
                result.Add(new(key.ToString(), key));
            for (var key = FormsKeys.D0; key <= FormsKeys.D9; key++)
                result.Add(new(((int)key - (int)FormsKeys.D0).ToString(), key));
            for (var key = FormsKeys.F1; key <= FormsKeys.F24; key++)
                result.Add(new(key.ToString(), key));

            result.AddRange(
            [
                new("Space", FormsKeys.Space),
                new("Insert", FormsKeys.Insert),
                new("Delete", FormsKeys.Delete),
                new("Home", FormsKeys.Home),
                new("End", FormsKeys.End),
                new("Page Up", FormsKeys.PageUp),
                new("Page Down", FormsKeys.PageDown),
                new("Left", FormsKeys.Left),
                new("Right", FormsKeys.Right),
                new("Up", FormsKeys.Up),
                new("Down", FormsKeys.Down)
            ]);
            return result;
        }

        public static List<ConditionalActionDefinition> CreateActionDefinitions(ILocalizer localizer)
        {
            var components = Text(localizer, "CL_CategoryComponents");
            var maintenance = Text(localizer, "CL_CategoryMaintenance");
            var application = Text(localizer, "CL_CategoryInterface");
            var workflow = Text(localizer, "CL_CategoryWorkflow");
            var componentId = new ConditionalParameterDefinition(
                "componentId",
                Text(localizer, "CL_ParameterComponentId"),
                UseInstalledComponentSelector: true);

            var definitions = new List<ConditionalActionDefinition>
            {
                new(ConditionalActionType.ApplyPreset, components, Text(localizer, "CL_ActionApplyPreset"),
                [
                    componentId,
                    new("packId", Text(localizer, "CL_ParameterPresetPackId")),
                    new("fileName", Text(localizer, "CL_ParameterPresetFileName"))
                ]),
                new(ConditionalActionType.StartComponent, components, Text(localizer, "CL_ActionStartComponent"), [componentId]),
                new(ConditionalActionType.StopComponent, components, Text(localizer, "CL_ActionStopComponent"), [componentId]),
                new(ConditionalActionType.RestartComponent, components, Text(localizer, "CL_ActionRestartComponent"), [componentId]),
                new(ConditionalActionType.StartAutorunComponents, components, Text(localizer, "CL_ActionStartAutorunComponents"), []),
                new(ConditionalActionType.StopAllComponents, components, Text(localizer, "CL_ActionStopAllComponents"), []),
                new(ConditionalActionType.StopNetworkService, components, Text(localizer, "CL_ActionStopNetworkService"), []),

                new(ConditionalActionType.CheckApplicationUpdates, maintenance, Text(localizer, "CL_ActionCheckApplicationUpdates"), []),
                new(ConditionalActionType.CheckStoreUpdates, maintenance, Text(localizer, "CL_ActionCheckStoreUpdates"), []),
                new(ConditionalActionType.RunCompatibilityCheck, maintenance, Text(localizer, "CL_ActionRunCompatibilityCheck"), []),
                new(ConditionalActionType.RunBasicDiagnostics, maintenance, Text(localizer, "CL_ActionRunBasicDiagnostics"), []),
                new(ConditionalActionType.RunStoreDiagnostics, maintenance, Text(localizer, "CL_ActionRunStoreDiagnostics"), []),

                new(ConditionalActionType.OpenMainPage, application, Text(localizer, "CL_ActionOpenMainPage"),
                [
                    new("target", Text(localizer, "CL_ParameterPage"), Choices:
                    [
                        Choice(localizer, "CL_ChoiceHome", "Home"),
                        Choice(localizer, "CL_ChoiceUtilities", "Utilities"),
                        Choice(localizer, "CL_ChoiceSettings", "Settings"),
                        Choice(localizer, "CL_ChoiceAutorunSettings", "AutorunSettings"),
                        Choice(localizer, "CL_ChoicePersonalization", "Personalization"),
                        Choice(localizer, "CL_ChoiceAbout", "About"),
                        Choice(localizer, "CL_ChoiceApplicationUpdates", "Updates"),
                        Choice(localizer, "CL_ChoiceComponentSettings", "ComponentSettings")
                    ]),
                    new("componentId", Text(localizer, "CL_ParameterComponentIdForSettings"), Required: false,
                        RequiredForTargets: ["ComponentSettings"],
                        VisibleForTargets: ["ComponentSettings"],
                        UseInstalledComponentSelector: true)
                ]),
                new(ConditionalActionType.OpenStorePage, application, Text(localizer, "CL_ActionOpenStorePage"),
                [
                    new("target", Text(localizer, "CL_ParameterPage"), Choices:
                    [
                        Choice(localizer, "CL_ChoiceHome", "Home"),
                        Choice(localizer, "CL_ChoiceCatalogItem", "CatalogItem"),
                        Choice(localizer, "CL_ChoiceCategory", "Category"),
                        Choice(localizer, "CL_ChoiceDownloads", "Downloads"),
                        Choice(localizer, "CL_ChoiceUpdates", "Updates"),
                        Choice(localizer, "CL_ChoiceLibrary", "Library"),
                        Choice(localizer, "CL_ChoiceSettings", "Settings"),
                        Choice(localizer, "CL_ChoiceSettingsMemory", "Memory"),
                        Choice(localizer, "CL_ChoiceMemoryApplication", "MemoryApplication"),
                        Choice(localizer, "CL_ChoiceMemoryInstalledItems", "MemoryInstalledItems"),
                        Choice(localizer, "CL_ChoiceMemoryLogs", "MemoryLogs"),
                        Choice(localizer, "CL_ChoiceMemorySettings", "MemorySettings"),
                        Choice(localizer, "CL_ChoiceMemoryStoreCache", "MemoryStoreCache")
                    ]),
                    new("itemId", Text(localizer, "CL_ParameterItemId"), Required: false,
                        RequiredForTargets: ["CatalogItem"], VisibleForTargets: ["CatalogItem"]),
                    new("categoryId", Text(localizer, "CL_ParameterCategoryId"), Required: false,
                        RequiredForTargets: ["Category"], VisibleForTargets: ["Category"])
                ]),
                new(ConditionalActionType.OpenTool, application, Text(localizer, "CL_ActionOpenTool"),
                [
                    new("target", Text(localizer, "CL_ParameterTool"), Choices:
                    [
                        Choice(localizer, "CL_ChoiceComponentConsole", "ComponentConsole"),
                        Choice(localizer, "CL_ChoiceAutoConfig", "AutoConfig"),
                        Choice(localizer, "CL_ChoiceConfigEditor", "ConfigEditor"),
                        Choice(localizer, "CL_ChoiceCreateConfig", "CreateConfig"),
                        Choice(localizer, "CL_ChoiceEditConfigPack", "EditConfigPack"),
                        Choice(localizer, "CL_ChoiceProxySetup", "ProxySetup"),
                        Choice(localizer, "CL_ChoiceTroubleshooting", "Troubleshooting"),
                        Choice(localizer, "CL_ChoicePresetTest", "PresetTest"),
                        Choice(localizer, "CL_ChoiceHostsEditor", "HostsEditor")
                    ]),
                    new("componentId", Text(localizer, "CL_ParameterComponentIdForTool"), Required: false,
                        RequiredForTargets: ["ComponentConsole", "CreateConfig"],
                        VisibleForTargets: ["ComponentConsole", "AutoConfig", "CreateConfig"],
                        UseInstalledComponentSelector: true),
                    new("kitId", Text(localizer, "CL_ParameterKitId"), Required: false,
                        RequiredForTargets: ["EditConfigPack"], VisibleForTargets: ["EditConfigPack"],
                        UseInstalledConfigKitSelector: true)
                ]),
                new(ConditionalActionType.OpenHelp, application, Text(localizer, "CL_ActionOpenHelp"),
                [new("helpUrl", Text(localizer, "CL_ParameterHelpPath"), Required: false, DefaultValue: "GettingStarted/")]),

                new(ConditionalActionType.Wait, workflow, Text(localizer, "CL_ActionWait"),
                [new("milliseconds", Text(localizer, "CL_ParameterMilliseconds"), IsNumber: true, Minimum: 0, Maximum: 86400000, DefaultValue: "5000")]),
                new(ConditionalActionType.ShowNotification, workflow, Text(localizer, "CL_ActionShowNotification"),
                [new("message", Text(localizer, "CL_ParameterMessage"))])
            };

            var categoryOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [components] = 0,
                [maintenance] = 1,
                [application] = 2,
                [workflow] = 3
            };
            return definitions
                .OrderBy(definition => categoryOrder[definition.Category])
                .ThenBy(definition => definition.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static ConditionalTask CreateNewTask(ILocalizer localizer) => new()
        {
            Name = Text(localizer, "CL_NewTaskDefaultName"),
            Triggers =
            [
                new ConditionalTrigger
                {
                    Type = ConditionalTriggerType.HotKey,
                    DelaySeconds = 0,
                    Parameters =
                    [
                        new() { Name = "modifiers", Value = ConditionalHotKeyModifiers.Control.ToString() },
                        new() { Name = "key", Value = FormsKeys.F1.ToString() }
                    ]
                }
            ]
        };

        public static ConditionalTask CloneTask(ConditionalTask task) => new()
        {
            Version = task.Version,
            Id = task.Id,
            Name = task.Name,
            IsEnabled = task.IsEnabled,
            StopAfterError = task.StopAfterError,
            Priority = task.Priority,
            FilePath = task.FilePath,
            Triggers = task.Triggers.Select(trigger => new ConditionalTrigger
            {
                Type = trigger.Type,
                DelaySeconds = trigger.DelaySeconds,
                Parameters = trigger.Parameters
                    .Select(parameter => new ConditionalParameter { Name = parameter.Name, Value = parameter.Value })
                    .ToList()
            }).ToList(),
            Actions = task.Actions.Select(action => new ConditionalAction
            {
                Type = action.Type,
                Parameters = action.Parameters
                    .Select(parameter => new ConditionalParameter { Name = parameter.Name, Value = parameter.Value })
                    .ToList()
            }).ToList()
        };

        public static ConditionalTaskListItem CreateTaskListItem(
            ConditionalTask task,
            ILocalizer localizer)
        {
            var priority = task.Priority switch
            {
                ConditionalTaskPriority.High => Text(localizer, "CL_PriorityHigh"),
                ConditionalTaskPriority.Low => Text(localizer, "CL_PriorityLow"),
                _ => Text(localizer, "CL_PriorityDefault")
            };
            var status = Text(localizer, task.IsEnabled ? "CL_StatusEnabled" : "CL_StatusDisabled");
            var triggerSummary = string.Join(
                $" {Text(localizer, "CL_OrSeparator")} ",
                task.Triggers.Select(trigger => FormatTrigger(trigger, localizer)));
            return new(task, task.Name, status, priority, triggerSummary);
        }

        public static ConditionalActionListItem CreateActionListItem(
            ConditionalAction action,
            int order,
            ILocalizer localizer,
            IReadOnlyList<ConditionalActionDefinition>? definitions = null)
        {
            definitions ??= CreateActionDefinitions(localizer);
            var definition = definitions.First(item => item.Type == action.Type);
            if (action.Type == ConditionalActionType.ApplyPreset)
                return new(action, order, definition.Name, CreateApplyPresetSummary(action, localizer));

            var values = definition.Parameters.Select(parameterDefinition =>
            {
                var value = action.GetParameter(parameterDefinition.Name);
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : $"{parameterDefinition.Label}: {FormatParameterValue(parameterDefinition, value)}";
            }).Where(value => value != null).Cast<string>().ToList();
            var summary = values.Count == 0
                ? definition.Category
                : string.Join(" • ", values);
            return new(action, order, definition.Name, summary);
        }

        private static string CreateApplyPresetSummary(
            ConditionalAction action,
            ILocalizer localizer)
        {
            var componentId = action.GetParameter("componentId");
            var packId = action.GetParameter("packId");
            var fileName = action.GetParameter("fileName");
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(componentId))
            {
                values.Add($"{Text(localizer, "CL_ParameterComponent")}: " +
                    ResolveStoreItemName(componentId));
            }
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                values.Add($"{Text(localizer, "CL_ParameterPreset")}: " +
                    ResolvePresetName(componentId, packId, fileName));
            }
            if (!string.IsNullOrWhiteSpace(packId))
            {
                values.Add($"{Text(localizer, "CL_ParameterPresetPackId")}: " +
                    ResolveStoreItemName(packId));
            }
            return string.Join(" • ", values);
        }

        private static string FormatParameterValue(
            ConditionalParameterDefinition definition,
            string value)
        {
            var choice = definition.Choices.FirstOrDefault(item =>
                string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase));
            if (choice != null)
                return choice.Name;
            if (definition.UseInstalledComponentSelector ||
                definition.UseInstalledConfigKitSelector)
            {
                return ResolveStoreItemName(value);
            }
            return value;
        }

        private static string ResolveStoreItemName(string id)
        {
            try
            {
                return DatabaseHelper.Instance.GetItemById(id)?.ShortName ?? id;
            }
            catch
            {
                return id;
            }
        }

        private static string ResolvePresetName(
            string? componentId,
            string? packId,
            string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(componentId))
                    return fileName;
                ComponentItemsLoaderHelper.Instance.Init();
                var config = ComponentItemsLoaderHelper.Instance
                    .GetComponentHelperFromId(componentId)?.GetConfigHelper();
                var preset = config?.GetConfigItems().FirstOrDefault(item =>
                    string.Equals(item.packId, packId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.file_name, fileName, StringComparison.OrdinalIgnoreCase));
                return preset?.name ?? preset?.not_converted_name ?? fileName;
            }
            catch
            {
                return fileName;
            }
        }

        public static string FormatTrigger(ConditionalTrigger trigger, ILocalizer localizer)
        {
            if (trigger.Type == ConditionalTriggerType.HotKey)
            {
                var modifiers = (trigger.GetParameter("modifiers") ?? string.Empty)
                    .Replace("Control", "Ctrl", StringComparison.OrdinalIgnoreCase)
                    .Replace("Windows", "Win", StringComparison.OrdinalIgnoreCase)
                    .Replace(", ", " + ", StringComparison.Ordinal);
                if (string.Equals(modifiers, nameof(ConditionalHotKeyModifiers.None), StringComparison.OrdinalIgnoreCase))
                    modifiers = string.Empty;
                var key = trigger.GetParameter("key") ?? string.Empty;
                return string.IsNullOrWhiteSpace(modifiers) ? key : $"{modifiers} + {key}";
            }

            var triggerName = Text(localizer, trigger.Type == ConditionalTriggerType.ProcessStarted
                ? "CL_TriggerProcessStarted"
                : "CL_TriggerProcessStopped");
            var processName = trigger.GetParameter("processName") ?? string.Empty;
            return string.Format(
                Text(localizer, "CL_ProcessTriggerSummaryFormat"),
                triggerName,
                processName,
                trigger.DelaySeconds);
        }

        private static ConditionalParameterChoice Choice(
            ILocalizer localizer,
            string resourceKey,
            string value) => new(Text(localizer, resourceKey), value);

        private static string Text(ILocalizer localizer, string resourceKey) =>
            localizer.GetLocalizedString(resourceKey);
    }

    internal sealed record ChoiceItem<T>(string Name, T Value);

    internal sealed class ConditionalTaskListItem(
        ConditionalTask task,
        string name,
        string status,
        string priorityLabel,
        string triggerSummary)
    {
        public ConditionalTask Task { get; } = task;
        public string Name { get; } = name;
        public string Status { get; } = status;
        public string PriorityLabel { get; } = priorityLabel;
        public string TriggerSummary { get; } = triggerSummary;
    }

    internal sealed class ConditionalActionListItem(
        ConditionalAction action,
        int order,
        string name,
        string summary)
    {
        public ConditionalAction Action { get; set; } = action;
        public int Order { get; set; } = order;
        public string Name { get; set; } = name;
        public string Summary { get; set; } = summary;
    }

    internal sealed record ConditionalTriggerListItem(
        string Type,
        string Details,
        string Status);

    internal sealed record ConditionalActionDefinition(
        ConditionalActionType Type,
        string Category,
        string Name,
        IReadOnlyList<ConditionalParameterDefinition> Parameters)
    {
        public string DisplayName => $"{Category}  •  {Name}";
    }

    internal sealed record ConditionalParameterChoice(string Name, string Value);

    internal sealed record ConditionalParameterDefinition(
        string Name,
        string Label,
        bool Required = true,
        bool IsNumber = false,
        double Minimum = 0,
        double Maximum = 0,
        string? DefaultValue = null,
        IReadOnlyList<ConditionalParameterChoice>? Choices = null,
        IReadOnlyList<string>? RequiredForTargets = null,
        IReadOnlyList<string>? VisibleForTargets = null,
        bool UseInstalledComponentSelector = false,
        bool UseInstalledConfigKitSelector = false)
    {
        public IReadOnlyList<ConditionalParameterChoice> Choices { get; init; } =
            Choices ?? Array.Empty<ConditionalParameterChoice>();

        public bool IsRequired(IReadOnlyDictionary<string, string> values)
        {
            if (Required)
                return true;
            if (RequiredForTargets == null ||
                !values.TryGetValue("target", out var target))
            {
                return false;
            }
            return RequiredForTargets.Contains(target, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsVisible(IReadOnlyDictionary<string, string> values)
        {
            if (VisibleForTargets == null)
                return true;
            return values.TryGetValue("target", out var target) &&
                VisibleForTargets.Contains(target, StringComparer.OrdinalIgnoreCase);
        }
    }
}
