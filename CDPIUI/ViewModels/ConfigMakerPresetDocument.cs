#nullable enable

using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.LScript;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CDPIUI.ViewModels;

public enum ConfigMakerVariableKind
{
    Text,
    Choice,
    Switch,
}

public enum ConfigMakerVariableStorageKind
{
    Direct,
    Expression,
    Conditional,
}

public enum ConfigMakerResourceKind
{
    SiteList,
    Library,
    Payload,
    Other,
}

public sealed class ConfigMakerVariableDefinition : INotifyPropertyChanged
{
    private string name = string.Empty;
    private ConfigMakerVariableKind kind;
    private ConfigMakerVariableStorageKind storageKind;
    private string value = string.Empty;
    private string description = string.Empty;
    private string onValue = string.Empty;
    private string offValue = string.Empty;
    private string internalParameterName = string.Empty;
    private bool isSwitchEnabled;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ObservableCollection<string> Values { get; } = [];

    public string Name { get => name; set => SetField(ref name, value ?? string.Empty); }
    public ConfigMakerVariableKind Kind { get => kind; set => SetField(ref kind, value); }
    public ConfigMakerVariableStorageKind StorageKind { get => storageKind; set => SetField(ref storageKind, value); }
    public string Value { get => value; set => SetField(ref this.value, value ?? string.Empty); }
    public string Description { get => description; set => SetField(ref description, value ?? string.Empty); }
    public string OnValue { get => onValue; set => SetField(ref onValue, value ?? string.Empty); }
    public string OffValue { get => offValue; set => SetField(ref offValue, value ?? string.Empty); }
    public string InternalParameterName { get => internalParameterName; set => SetField(ref internalParameterName, value ?? string.Empty); }
    public bool IsSwitchEnabled { get => isSwitchEnabled; set => SetField(ref isSwitchEnabled, value); }

    public string Reference => $"%{Name}%";
    public string DisplayValue => Kind == ConfigMakerVariableKind.Switch
        ? IsSwitchEnabled ? OnValue : OffValue
        : Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(Name))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reference)));
        }
        if (propertyName is nameof(Value) or nameof(OnValue) or nameof(OffValue) or
            nameof(IsSwitchEnabled) or nameof(Kind))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayValue)));
        }
    }
}

public sealed class ConfigMakerPresetResource : INotifyPropertyChanged
{
    private string alias = string.Empty;
    private string path = string.Empty;
    private ConfigMakerResourceKind kind;
    private bool isBuiltIn;

    public string Alias { get => alias; set => SetField(ref alias, value ?? string.Empty); }
    public string Path { get => path; set => SetField(ref path, value ?? string.Empty); }
    public ConfigMakerResourceKind Kind { get => kind; set => SetField(ref kind, value); }
    public bool IsBuiltIn { get => isBuiltIn; set => SetField(ref isBuiltIn, value); }
    public string Reference => $"preset://{Alias}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(Alias))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reference)));
        }
    }
}

public sealed class ConfigMakerPresetDocument : INotifyPropertyChanged
{
    private static readonly Regex VariableNameRegex = new(
        "^[A-Za-z][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private string componentId = string.Empty;
    private string name = string.Empty;
    private string commandText = string.Empty;
    private readonly HashSet<ConfigMakerVariableDefinition> subscribedVariables = [];
    private readonly HashSet<ConfigMakerPresetResource> subscribedResources = [];

    public ConfigMakerPresetDocument()
    {
        Variables.CollectionChanged += Variables_CollectionChanged;
        Resources.CollectionChanged += Resources_CollectionChanged;
    }

    public string? PackId { get; set; }
    public string? FileName { get; set; }
    public string? Meta { get; set; }
    public string? TargetVersion { get; set; }
    public string ComponentId { get => componentId; set => SetField(ref componentId, value ?? string.Empty); }
    public string Name { get => name; set => SetField(ref name, value ?? string.Empty); }
    public string CommandText { get => commandText; set => SetField(ref commandText, value ?? string.Empty); }
    public ObservableCollection<ConfigMakerVariableDefinition> Variables { get; } = [];
    public ObservableCollection<ConfigMakerPresetResource> Resources { get; } = [];
    public bool HasVariables => Variables.Count > 0;
    public bool HasResources => Resources.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ContentChanged;

    public static bool IsValidVariableName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && VariableNameRegex.IsMatch(value);

    public bool ContainsVariable(string name, string? exceptId = null) => Variables.Any(variable =>
        !string.Equals(variable.Id, exceptId, StringComparison.Ordinal) &&
        string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase));

    public ConfigItem ToConfigItem(string packId, string presetName)
    {
        Dictionary<string, bool> jparams = new(StringComparer.OrdinalIgnoreCase);
        List<string> variables = [];
        Dictionary<string, string> commaVariables = new(StringComparer.OrdinalIgnoreCase);
        List<AvailableVarValues> availableValues = [];

        foreach (ConfigMakerVariableDefinition variable in Variables)
        {
            if (variable.StorageKind == ConfigMakerVariableStorageKind.Conditional ||
                variable.Kind == ConfigMakerVariableKind.Switch)
            {
                string parameterName = string.IsNullOrWhiteSpace(variable.InternalParameterName)
                    ? CreateInternalParameterName(variable.Name, variable.Id)
                    : variable.InternalParameterName;
                jparams[parameterName] = variable.IsSwitchEnabled;
                variables.Add(
                    $"%{variable.Name}%=$LOCALCONDITION({parameterName}==true ? " +
                    $"{variable.OnValue} $SEPARATOR {variable.OffValue})");
                continue;
            }

            if (variable.StorageKind == ConfigMakerVariableStorageKind.Expression)
            {
                variables.Add($"%{variable.Name}%={variable.Value}");
                continue;
            }

            commaVariables[variable.Name] = variable.Value;
            if (variable.Kind == ConfigMakerVariableKind.Choice ||
                variable.Values.Count > 0 ||
                !string.IsNullOrWhiteSpace(variable.Description))
            {
                int selectedIndex = variable.Values.IndexOf(variable.Value);
                availableValues.Add(new AvailableVarValues
                {
                    VarName = variable.Name,
                    Comment = variable.Description,
                    CurrentValueIndex = selectedIndex,
                    Values = variable.Values.Distinct(StringComparer.Ordinal).ToList(),
                });
            }
        }

        ConfigMakerPresetMetadata? metadata = Variables.Count > 0 || Resources.Count > 0
            ? CreateMetadata()
            : null;

        return new ConfigItem
        {
            packId = packId,
            meta = string.IsNullOrWhiteSpace(Meta) ? null : Meta,
            not_converted_name = presetName,
            name = presetName,
            target = string.IsNullOrWhiteSpace(TargetVersion)
                ? [ComponentId]
                : [ComponentId, TargetVersion],
            jparams = jparams,
            variables = variables,
            commaVars = commaVariables.Count == 0 ? null : commaVariables,
            availableCommaVarsValues = availableValues.Count == 0 ? null : availableValues,
            startup_string = CommandText,
            configMaker = metadata,
        };
    }

    public static ConfigMakerPresetDocument FromConfigItem(ConfigItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ConfigMakerPresetDocument document = new()
        {
            ComponentId = item.target?.FirstOrDefault() ?? string.Empty,
            Name = item.not_converted_name ?? item.name ?? string.Empty,
            CommandText = item.startup_string ?? string.Empty,
            PackId = item.packId,
            FileName = item.file_name,
            Meta = item.meta,
            TargetVersion = item.target?.ElementAtOrDefault(1),
        };

        if (item.configMaker?.variables is { Count: > 0 })
        {
            foreach (ConfigMakerVariableMetadata metadata in item.configMaker.variables)
            {
                document.Variables.Add(FromMetadata(metadata));
            }
        }
        else
        {
            LoadLegacyVariables(document, item);
        }

        foreach (ConfigMakerResourceMetadata metadata in item.configMaker?.resources ?? [])
        {
            if (string.IsNullOrWhiteSpace(metadata.alias) || string.IsNullOrWhiteSpace(metadata.path))
            {
                continue;
            }
            document.Resources.Add(new ConfigMakerPresetResource
            {
                Alias = metadata.alias,
                Path = metadata.path,
                Kind = ParseResourceKind(metadata.kind),
                IsBuiltIn = metadata.isBuiltIn,
            });
        }
        document.CommandText = RestoreResourceReferences(
            document.CommandText,
            item.configMaker?.resources ?? []);
        RestoreVariableResourceReferences(
            document.Variables,
            item.configMaker?.resources ?? []);

        return document;
    }

    public string ReplaceVariableReference(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
        {
            return CommandText;
        }
        CommandText = Regex.Replace(
            CommandText,
            $"%{Regex.Escape(oldName)}%",
            $"%{newName}%",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return CommandText;
    }

    private ConfigMakerPresetMetadata CreateMetadata() => new()
    {
        schemaVersion = 1,
        variables = Variables.Count == 0 ? null : Variables.Select(variable => new ConfigMakerVariableMetadata
        {
            id = variable.Id,
            name = variable.Name,
            kind = variable.Kind.ToString(),
            storageKind = variable.StorageKind.ToString(),
            value = string.IsNullOrEmpty(variable.Value) ? null : variable.Value,
            description = string.IsNullOrEmpty(variable.Description) ? null : variable.Description,
            values = variable.Values.Count == 0 ? null : variable.Values.ToList(),
            onValue = string.IsNullOrEmpty(variable.OnValue) ? null : variable.OnValue,
            offValue = string.IsNullOrEmpty(variable.OffValue) ? null : variable.OffValue,
            internalParameterName = string.IsNullOrEmpty(variable.InternalParameterName)
                ? null
                : variable.InternalParameterName,
            isSwitchEnabled = variable.IsSwitchEnabled,
        }).ToList(),
        resources = Resources.Count == 0 ? null : Resources.Select(resource => new ConfigMakerResourceMetadata
        {
            alias = resource.Alias,
            path = resource.Path,
            kind = resource.Kind.ToString(),
            isBuiltIn = resource.IsBuiltIn,
        }).ToList(),
    };

    private static ConfigMakerVariableDefinition FromMetadata(ConfigMakerVariableMetadata metadata)
    {
        ConfigMakerVariableDefinition variable = new()
        {
            Id = string.IsNullOrWhiteSpace(metadata.id)
                ? Guid.NewGuid().ToString("N")
                : metadata.id,
            Name = metadata.name ?? string.Empty,
            Kind = Enum.TryParse(metadata.kind, ignoreCase: true, out ConfigMakerVariableKind kind)
                ? kind
                : ConfigMakerVariableKind.Text,
            StorageKind = Enum.TryParse(
                metadata.storageKind,
                ignoreCase: true,
                out ConfigMakerVariableStorageKind storageKind)
                ? storageKind
                : ConfigMakerVariableStorageKind.Direct,
            Value = metadata.value ?? string.Empty,
            Description = metadata.description ?? string.Empty,
            OnValue = metadata.onValue ?? string.Empty,
            OffValue = metadata.offValue ?? string.Empty,
            InternalParameterName = metadata.internalParameterName ?? string.Empty,
            IsSwitchEnabled = metadata.isSwitchEnabled,
        };
        foreach (string value in metadata.values ?? [])
        {
            variable.Values.Add(value);
        }
        return variable;
    }

    private static void LoadLegacyVariables(ConfigMakerPresetDocument document, ConfigItem item)
    {
        foreach ((string variableName, string value) in item.commaVars ?? [])
        {
            AvailableVarValues? values = item.availableCommaVarsValues?.FirstOrDefault(candidate =>
                string.Equals(candidate.VarName, variableName, StringComparison.OrdinalIgnoreCase));
            ConfigMakerVariableDefinition variable = new()
            {
                Name = variableName,
                Kind = values?.Values is { Count: > 0 }
                    ? ConfigMakerVariableKind.Choice
                    : ConfigMakerVariableKind.Text,
                StorageKind = ConfigMakerVariableStorageKind.Direct,
                Value = value,
                Description = values?.Comment ?? string.Empty,
            };
            foreach (string availableValue in values?.Values ?? [])
            {
                variable.Values.Add(availableValue);
            }
            document.Variables.Add(variable);
        }

        foreach (string expression in item.variables ?? [])
        {
            Tuple<string, string, string, string>? condition =
                LScriptLangHelper.GetNameOnOffValuesFromConditionString(expression);
            if (condition != null && !string.IsNullOrWhiteSpace(condition.Item2))
            {
                document.Variables.Add(new ConfigMakerVariableDefinition
                {
                    Name = condition.Item2,
                    Kind = ConfigMakerVariableKind.Switch,
                    StorageKind = ConfigMakerVariableStorageKind.Conditional,
                    InternalParameterName = condition.Item1,
                    OnValue = condition.Item3,
                    OffValue = condition.Item4,
                    IsSwitchEnabled = item.jparams?.GetValueOrDefault(condition.Item1) ?? false,
                });
                continue;
            }

            Match match = Regex.Match(expression, "^%(?<name>[A-Za-z0-9_]+)%=(?<value>.*)$");
            if (match.Success)
            {
                document.Variables.Add(new ConfigMakerVariableDefinition
                {
                    Name = match.Groups["name"].Value,
                    Kind = ConfigMakerVariableKind.Text,
                    StorageKind = ConfigMakerVariableStorageKind.Expression,
                    Value = match.Groups["value"].Value,
                });
            }
        }
    }

    private static string CreateInternalParameterName(string name, string id) =>
        $"{name}_var_{id[..Math.Min(8, id.Length)]}";

    private static ConfigMakerResourceKind ParseResourceKind(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out ConfigMakerResourceKind kind)
            ? kind
            : ConfigMakerResourceKind.Other;

    private static string RestoreResourceReferences(
        string commandText,
        IEnumerable<ConfigMakerResourceMetadata> resources) =>
        ConfigFileReferences.RestorePresetReferences(commandText, resources);

    private static void RestoreVariableResourceReferences(
        IEnumerable<ConfigMakerVariableDefinition> variables,
        IEnumerable<ConfigMakerResourceMetadata> resources)
    {
        ConfigMakerResourceMetadata[] resourceSnapshot = resources.ToArray();
        foreach (ConfigMakerVariableDefinition variable in variables)
        {
            variable.Value = RestoreResourceReferences(variable.Value, resourceSnapshot);
            variable.OnValue = RestoreResourceReferences(variable.OnValue, resourceSnapshot);
            variable.OffValue = RestoreResourceReferences(variable.OffValue, resourceSnapshot);
            for (int index = 0; index < variable.Values.Count; index++)
            {
                variable.Values[index] = RestoreResourceReferences(
                    variable.Values[index],
                    resourceSnapshot);
            }
        }
    }

    private void Variables_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateItemSubscriptions(
            e,
            Variable_PropertyChanged,
            subscribedVariables,
            Variables);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasVariables)));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Resources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateItemSubscriptions(
            e,
            Resource_PropertyChanged,
            subscribedResources,
            Resources);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasResources)));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void UpdateItemSubscriptions<T>(
        NotifyCollectionChangedEventArgs args,
        PropertyChangedEventHandler handler,
        ISet<T> subscribedItems,
        IEnumerable<T> currentItems)
        where T : INotifyPropertyChanged
    {
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (T item in subscribedItems)
            {
                item.PropertyChanged -= handler;
            }
            subscribedItems.Clear();
            foreach (T item in currentItems)
            {
                item.PropertyChanged += handler;
                subscribedItems.Add(item);
            }
            return;
        }
        foreach (T item in args.OldItems?.OfType<T>() ?? [])
        {
            item.PropertyChanged -= handler;
            subscribedItems.Remove(item);
        }
        foreach (T item in args.NewItems?.OfType<T>() ?? [])
        {
            item.PropertyChanged += handler;
            subscribedItems.Add(item);
        }
    }

    private void Variable_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ContentChanged?.Invoke(this, EventArgs.Empty);

    private void Resource_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ContentChanged?.Invoke(this, EventArgs.Empty);

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }
}
