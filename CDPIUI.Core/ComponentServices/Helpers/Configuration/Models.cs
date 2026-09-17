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
        [Newtonsoft.Json.JsonIgnore]
        [global::System.Text.Json.Serialization.JsonIgnore]
        public string? file_name;

        [Newtonsoft.Json.JsonIgnore]
        [global::System.Text.Json.Serialization.JsonIgnore]
        public string? packId;

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? meta { get; set; }
        public List<string>? target { get; set; }
        public string? name { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [global::System.Text.Json.Serialization.JsonIgnore]
        public string? not_converted_name;
        public Dictionary<string, bool>? jparams { get; set; }
        public List<string>? variables { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? commaVars { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<AvailableVarValues>? availableCommaVarsValues { get; set; }
        public string? startup_string { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public ConfigMakerPresetMetadata? configMaker { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [global::System.Text.Json.Serialization.JsonIgnore]
        public List<string>? toggle_lists;

        [Newtonsoft.Json.JsonIgnore]
        [global::System.Text.Json.Serialization.JsonIgnore]
        public bool IsLegacy { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [global::System.Text.Json.Serialization.JsonIgnore]
        public bool MarkAsRemoved = false;
    }

    /// <summary>
    /// Optional editor metadata. Runtime configuration remains in the legacy ConfigItem fields;
    /// this section only preserves information required to reopen a preset in ConfigMaker.
    /// </summary>
    public class ConfigMakerPresetMetadata
    {
        public int schemaVersion { get; set; } = 1;

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<ConfigMakerVariableMetadata>? variables { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<ConfigMakerResourceMetadata>? resources { get; set; }
    }

    public class ConfigMakerVariableMetadata
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? kind { get; set; }
        public string? storageKind { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? value { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? description { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? values { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? onValue { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? offValue { get; set; }

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? internalParameterName { get; set; }

        [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public bool isSwitchEnabled { get; set; }
    }

    public class ConfigMakerResourceMetadata
    {
        public string? alias { get; set; }
        public string? path { get; set; }
        public string? kind { get; set; }

        [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
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
