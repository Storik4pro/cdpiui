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
    public class ConfigItem : INamedModel
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
        public List<string>? toggle_lists;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsLegacy { get; set; }

        public bool MarkAsRemoved = false;
    }

    public class VariableItem
    {
        public string? variable_name;
        public string? name;
        public bool value;
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
