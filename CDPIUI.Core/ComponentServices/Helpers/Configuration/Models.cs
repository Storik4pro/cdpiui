using CDPIUI.Shared.Models;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration
{
    public class AvailableVarValues
    {
        public string Comment { get; set; } = "";
        public string? VarName { get; set; }
        public int CurrentValueIndex { get; set; }
        public List<string>? Values { get; set; }
    }
    public class OldConfigItem
    {
        public string? custom_parameters { get; set; }
    }
    public partial class ConfigItem : INamedModel
    {
        public string? file_name;
        public string? packId;
        public string? meta { get; set; }
        public List<string>? target { get; set; }
        public string? name { get; set; }
        public string? not_converted_name;
        public Dictionary<string, bool>? jparams { get; set; }
        public List<string>? variables { get; set; }
        public Dictionary<string, string>? commaVars { get; set; }
        public List<AvailableVarValues>? availableCommaVarsValues { get; set; }
        public string? startup_string { get; set; }
        public ConfigMakerPresetMetadata? configMaker { get; set; }
        public List<string>? toggle_lists;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsLegacy { get; set; }

        public bool MarkAsRemoved = false;
    }

    /// <summary>
    /// Optional editor metadata. Runtime configuration remains in the legacy ConfigItem fields;
    /// this section only preserves information required to reopen a preset in ConfigMaker.
    /// </summary>
    public class ConfigMakerPresetMetadata
    {
        public int schemaVersion { get; set; } = 1;
        public List<ConfigMakerVariableMetadata>? variables { get; set; }
        public List<ConfigMakerResourceMetadata>? resources { get; set; }
    }

    public class ConfigMakerVariableMetadata
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? kind { get; set; }
        public string? storageKind { get; set; }
        public string? value { get; set; }
        public string? description { get; set; }
        public List<string>? values { get; set; }
        public string? onValue { get; set; }
        public string? offValue { get; set; }
        public string? internalParameterName { get; set; }
        public bool isSwitchEnabled { get; set; }
    }

    public class ConfigMakerResourceMetadata
    {
        public string? alias { get; set; }
        public string? path { get; set; }
        public string? kind { get; set; }
        public bool isBuiltIn { get; set; }
    }

    public class VariableItem
    {
        public string? variable_name;
        public string? name;
        public bool value;
    }

    public class CommaVariableItem
    {
        public string? name;
        public string? comment;
        public string? value;
        public List<string> values = [];
    }

    public class SiteListItem
    {
        public string? Name;
        public string? Type;
        public string? FilePath;
        public List<string>? ApplyParams;
        public List<string>? PrettyApplyParams;
    }

    public class ConfigInitItem
    {
        public List<string>? toggleListAvailable { get; set; }
        public Dictionary<string, string>? localized_strings_directory { get; set; }
    }
}
