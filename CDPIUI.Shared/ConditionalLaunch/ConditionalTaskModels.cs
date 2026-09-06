using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace CDPIUI.Shared.ConditionalLaunch
{
    public enum ConditionalTaskPriority
    {
        Low = -1,
        Default = 0,
        High = 1
    }

    public enum ConditionalTriggerType
    {
        HotKey,
        ProcessStarted,
        ProcessStopped
    }

    [Flags]
    public enum ConditionalHotKeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    public enum ConditionalActionType
    {
        ApplyPreset,
        StartComponent,
        StopComponent,
        RestartComponent,
        StartAutorunComponents,
        StopAllComponents,
        StopNetworkService,
        CheckApplicationUpdates,
        CheckStoreUpdates,
        RunCompatibilityCheck,
        RunBasicDiagnostics,
        RunStoreDiagnostics,
        OpenMainPage,
        OpenStorePage,
        OpenTool,
        OpenHelp,
        Wait,
        ShowNotification
    }

    public sealed class ConditionalParameter
    {
        [XmlAttribute("Name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("Value")]
        public string Value { get; set; } = string.Empty;
    }

    public abstract class ConditionalParameterizedModel
    {
        [XmlElement("Parameter")]
        public List<ConditionalParameter> Parameters { get; set; } = [];

        public string? GetParameter(string name)
        {
            return Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public void SetParameter(string name, string? value)
        {
            var parameter = Parameters.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(value))
            {
                if (parameter != null)
                    Parameters.Remove(parameter);
                return;
            }

            if (parameter == null)
            {
                Parameters.Add(new ConditionalParameter
                {
                    Name = name,
                    Value = value
                });
                return;
            }

            parameter.Value = value;
        }
    }

    public sealed class ConditionalTrigger : ConditionalParameterizedModel
    {
        [XmlAttribute("Type")]
        public ConditionalTriggerType Type { get; set; }

        [XmlAttribute("DelaySeconds")]
        public int DelaySeconds { get; set; } = 30;
    }

    public sealed class ConditionalAction : ConditionalParameterizedModel
    {
        [XmlAttribute("Type")]
        public ConditionalActionType Type { get; set; }
    }

    [XmlRoot("ConditionalTask")]
    public sealed class ConditionalTask
    {
        public const int CurrentVersion = 3;

        [XmlAttribute("Version")]
        public int Version { get; set; } = CurrentVersion;

        [XmlAttribute("Id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("D");

        [XmlElement("Name")]
        public string Name { get; set; } = "New conditional task";

        [XmlElement("Enabled")]
        public bool IsEnabled { get; set; } = true;

        [XmlElement("StopAfterError")]
        public bool StopAfterError { get; set; } = true;

        [XmlIgnore]
        public ConditionalTaskPriority Priority { get; set; } = ConditionalTaskPriority.Default;

        [XmlElement("Priority")]
        public int SerializedPriority
        {
            get => (int)Priority;
            set => Priority = (ConditionalTaskPriority)value;
        }

        [XmlArray("Triggers")]
        [XmlArrayItem("Trigger")]
        public List<ConditionalTrigger> Triggers { get; set; } = [];

        // Version 1 stored a single Trigger directly under ConditionalTask.
        // It is consumed during normalization and is never written by version 2.
        [XmlElement("Trigger")]
        public ConditionalTrigger? LegacyTrigger { get; set; }

        [XmlArray("Actions")]
        [XmlArrayItem("Action")]
        public List<ConditionalAction> Actions { get; set; } = [];

        [XmlIgnore]
        public string? FilePath { get; set; }
    }

    public static class ConditionalTaskFileService
    {
        public const string FileExtension = ".cdpitask";
        public const string DirectoryName = "ConditionalTasks";

        private static readonly XmlSerializer Serializer = new(typeof(ConditionalTask));

        public static string GetTasksDirectoryFromSettingsFile(string settingsFilePath)
        {
            var settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(settingsFilePath));
            var dataDirectory = settingsDirectory == null
                ? null
                : Directory.GetParent(settingsDirectory)?.FullName;

            if (string.IsNullOrWhiteSpace(dataDirectory))
                throw new InvalidOperationException("Cannot resolve the conditional tasks directory.");

            return Path.Combine(dataDirectory, DirectoryName);
        }

        public static IReadOnlyList<ConditionalTask> LoadDirectory(string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);

            var tasks = new List<ConditionalTask>();
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, $"*{FileExtension}"))
            {
                try
                {
                    tasks.Add(Load(filePath));
                }
                catch
                {
                    // Invalid task files are ignored by the runtime. The editor
                    // reports import/load errors to the user separately.
                }
            }

            return tasks;
        }

        public static ConditionalTask Load(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            if (Serializer.Deserialize(stream) is not ConditionalTask task)
                throw new InvalidDataException("The file does not contain a conditional task.");

            Normalize(task);
            Validate(task);
            task.FilePath = Path.GetFullPath(filePath);
            return task;
        }

        public static string Save(ConditionalTask task, string directoryPath, string? filePath = null)
        {
            Validate(task);
            task.Version = ConditionalTask.CurrentVersion;
            Directory.CreateDirectory(directoryPath);

            filePath ??= task.FilePath;
            if (string.IsNullOrWhiteSpace(filePath) ||
                !string.Equals(Path.GetExtension(filePath), FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                filePath = Path.Combine(directoryPath, $"{task.Id}{FileExtension}");
            }

            filePath = Path.GetFullPath(filePath);
            var targetDirectory = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!filePath.StartsWith(targetDirectory, StringComparison.OrdinalIgnoreCase))
                filePath = Path.Combine(directoryPath, $"{task.Id}{FileExtension}");

            using (var stream = File.Create(filePath))
                Serializer.Serialize(stream, task);

            task.FilePath = filePath;
            return filePath;
        }

        public static void Export(ConditionalTask task, string filePath)
        {
            Validate(task);
            task.Version = ConditionalTask.CurrentVersion;
            if (!string.Equals(Path.GetExtension(filePath), FileExtension, StringComparison.OrdinalIgnoreCase))
                filePath += FileExtension;

            using var stream = File.Create(filePath);
            Serializer.Serialize(stream, task);
        }

        public static void Validate(ConditionalTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (task.Version > ConditionalTask.CurrentVersion)
                throw new InvalidDataException("The conditional task file version is not supported.");
            if (!Guid.TryParse(task.Id, out _))
                throw new InvalidDataException("The conditional task identifier is invalid.");
            if (string.IsNullOrWhiteSpace(task.Name))
                throw new InvalidDataException("The conditional task name is required.");
            if ((int)task.Priority is < -1 or > 1)
                throw new InvalidDataException("The conditional task priority is invalid.");
            Normalize(task);
            if (task.Triggers.Count == 0)
                throw new InvalidDataException("At least one conditional task trigger is required.");
            if (task.Triggers.Any(trigger => trigger == null))
                throw new InvalidDataException("The conditional task contains an invalid trigger.");
            if (task.Triggers.Any(trigger => trigger.DelaySeconds is < 0 or > 86400))
                throw new InvalidDataException("The process trigger delay must be between 0 and 86400 seconds.");
            if (task.Actions == null || task.Actions.Count == 0)
                throw new InvalidDataException("At least one action is required.");
        }

        private static void Normalize(ConditionalTask task)
        {
            task.Triggers ??= [];
            if (task.Triggers.Count == 0 && task.LegacyTrigger != null)
                task.Triggers.Add(task.LegacyTrigger);
            task.LegacyTrigger = null;
        }
    }
}
