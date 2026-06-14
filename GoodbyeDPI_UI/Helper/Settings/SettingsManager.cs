using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;

namespace CDPI_UI.Helper
{
    public class SettingsManager
    {
        private readonly string _filePath;
        private XDocument _xDocument;

        public Action<string> PropertyChanged;
        public Action<IEnumerable<string>> EnumPropertyChanged;

        private static SettingsManager _instance;
        private static readonly object _lock = new object();

        public static SettingsManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new SettingsManager();
                    return _instance;
                }
            }
        }
        private SettingsManager()
        {
            string appPath = StateHelper.GetDataDirectory();
            _filePath = Path.Combine(appPath, StateHelper.SettingsDir, "Settings.xml");

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

            if (File.Exists(_filePath))
            {
                _xDocument = XDocument.Load(_filePath);
            }
            else
            {
                _xDocument = new XDocument(new XElement("Settings"));
                _xDocument.Save(_filePath);
            }
        }

        private T GetDefaultValue<T>()
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)false;
            } 
            else if (typeof(T) == typeof(string)) {
                return (T)(object)"NaN";
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)0;
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)10.0;
            }
            else if (typeof(T) == typeof(List<string>))
            {
                return (T)(object)new List<string>();
            }
            else
            {
                return (T)(object)default(T);
            }
        }
        public T GetValueOrDefault<T>(string group, string key, XElement xElement = null, T defaultValue = default)
        {
            if (xElement == null) xElement = _xDocument.Root;
            var settingElement = xElement
                .Elements("Group")
                .FirstOrDefault(g => g.Attribute("Name")?.Value == group)?
                .Elements("Setting")
                .FirstOrDefault(s => s.Attribute("Key")?.Value == key);

            if (settingElement == null)
            {
                SetValue(group, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group '{group}' not found.");
                return defaultValue;
            }
            else return GetValue<T>(group, key, xElement);
        }
        public T GetValue<T>(string group, string key, XElement xElement = null, bool raiseExceptionIfNotExits = false)
        {
            if (xElement == null) xElement = _xDocument.Root;
            var settingElement = xElement
                .Elements("Group")
                .FirstOrDefault(g => g.Attribute("Name")?.Value == group)?
                .Elements("Setting")
                .FirstOrDefault(s => s.Attribute("Key")?.Value == key);

            if (settingElement == null) {
                if (raiseExceptionIfNotExits) throw new Exception("Value not exist");

                var defaultValue = GetDefaultValueForKey<T>(group, key);

                SetValue(group, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group '{group}' not found.");
                return defaultValue;
            }
            string value = settingElement.Attribute("Value")?.Value;
            string type = settingElement.Attribute("Type")?.Value;

            try
            {
                return GetValueFromString<T>(value, type);
            }
            catch
            {
                throw new Exception($"Type mismatch or unsupported type for setting '{key}' in group '{group}'.");
            }
        }
        public T GetValue<T>(IEnumerable<string> groupPath, string key, XElement xElement = null, bool raiseExceptionIfNotExits = false)
        {
            if (xElement == null) xElement = _xDocument.Root;
            XElement current = xElement;

            foreach (var grp in groupPath)
            {
                current = current
                    .Elements("Group")
                    .FirstOrDefault(g => (string)g.Attribute("Name") == grp);
                if (current == null)
                {
                    if (raiseExceptionIfNotExits) throw new Exception("Value not exist");
                    var defaultValue = GetDefaultValueForKey<T>(groupPath, key);
                    SetValue(groupPath, key, defaultValue);
                    Debug.WriteLine($"Group path '{string.Join("/", groupPath)}' not found.");
                    return defaultValue;
                }
            }

            var settingElement = current
                .Elements("Setting")
                .FirstOrDefault(s => (string)s.Attribute("Key") == key);

            if (settingElement == null)
            {
                if (raiseExceptionIfNotExits) throw new Exception("Value not exist");
                var defaultValue = GetDefaultValue<T>();
                SetValue(groupPath, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group path '{string.Join("/", groupPath)}' not found.");
                return defaultValue;
            }

            string value = (string)settingElement.Attribute("Value");
            string type = (string)settingElement.Attribute("Type");

            try
            {
                return GetValueFromString<T>(value, type);
            }
            catch
            {
                throw new Exception($"Type mismatch or unsupported type for setting '{key}' in group path '{string.Join("/", groupPath)}'.");
            }
        }

        public Dictionary<string, T> GetKeyPair<T>(IEnumerable<string> groupPath, XElement xElement = null, bool raiseExceptionIfNotExits = false)
        {
            if (xElement == null) xElement = _xDocument.Root;
            XElement current = xElement;

            Dictionary<string, T> dict = [];

            foreach (var grp in groupPath)
            {
                current = current
                    .Elements("Group")
                    .FirstOrDefault(g => (string)g.Attribute("Name") == grp);
                if (current == null)
                {
                    if (raiseExceptionIfNotExits) throw new Exception("Value not exist");
                    var defaultValue = GetDefaultValueForKey<T>(groupPath, "$$~");
                    SetValue(groupPath, "$$~", defaultValue);
                    Debug.WriteLine($"Group path '{string.Join("/", groupPath)}' not found.");
                    return dict;
                }
            }
            
            if (current != null)
            {
                foreach (var setting in current.Elements("Setting"))
                {
                    var result = GetValueFromString<T>((string)setting.Attribute("Value"), (string)setting.Attribute("Type"));
                    dict.Add((string)setting.Attribute("Key"), result);
                }
            }
            return dict;
        }

        private static T GetValueFromString<T>(string value, string type)
        {
            if (typeof(T) == typeof(int) && type == "int" && int.TryParse(value, out var iv)) return (T)(object)iv;
            if (typeof(T) == typeof(double) && type == "double" && double.TryParse(value, out var dv)) return (T)(object)dv;
            if (typeof(T) == typeof(bool) && type == "bool" && bool.TryParse(value, out var bv)) return (T)(object)bv;
            if (typeof(T) == typeof(string) && type == "string") return (T)(object)value;

            if (typeof(T) == typeof(List<string>) && type == nameof(List<string>)) return string.IsNullOrEmpty(value) ? (T)(object)new List<string>() : (T)(object)value.Split(';').ToList();

            if (typeof(T) == typeof(DateTime) && type == nameof(DateTime))
                return (T)(object)DateTime.Parse((string)value);

            throw new Exception();
        }

        public void SetValue<T>(string group, string key, T value)
        {
            string type;
            string valueString;

            if (value is int)
            {
                type = "int";
                valueString = value.ToString();
            }
            else if (value is double)
            {
                type = "double";
                valueString = value.ToString();
            }
            else if (value is bool)
            {
                type = "bool";
                valueString = value.ToString().ToLower();
            }
            else if (value is string)
            {
                type = "string";
                valueString = value as string;
            }
            else if (value is DateTime)
            {
                type = nameof(DateTime);
                valueString = value.ToString();
            }
            else if (typeof(T) == typeof(List<string>))
            {
                type = nameof(List<string>);
                valueString = string.Join(";", (List<string>)(object)value);

                Debug.WriteLine(valueString);
            }
            else
            {
                type = nameof(T);
                valueString = value.ToString();
            }

            var groupElement = _xDocument.Root
                .Elements("Group")
                .FirstOrDefault(g => g.Attribute("Name")?.Value == group);

            if (groupElement == null)
            {
                groupElement = new XElement("Group", new XAttribute("Name", group));
                _xDocument.Root.Add(groupElement);
            }

            var settingElement = groupElement
                .Elements("Setting")
                .FirstOrDefault(s => s.Attribute("Key")?.Value == key);

            if (settingElement != null)
            {
                settingElement.SetAttributeValue("Value", valueString);
                settingElement.SetAttributeValue("Type", type);
            }
            else
            {
                groupElement.Add(new XElement("Setting",
                    new XAttribute("Key", key),
                    new XAttribute("Type", type),
                    new XAttribute("Value", valueString)));
            }

            _xDocument.Save(_filePath);
            PropertyChanged?.Invoke(group);
        }
        public void SetValue<T>(IEnumerable<string> groupPath, string key, T value)
        {
            string type;
            string valueString;

            switch (value)
            {
                case int i:
                    type = "int";
                    valueString = i.ToString();
                    break;
                case double d:
                    type = "double";
                    valueString = d.ToString();
                    break;
                case bool b:
                    type = "bool";
                    valueString = b.ToString().ToLower();
                    break;
                case string s:
                    type = "string";
                    valueString = s;
                    break;
                case List<string> lst:
                    type = nameof(List<string>);
                    valueString = string.Join(";", lst);
                    break;
                default:
                    type = nameof(T);
                    valueString = value.ToString();
                    break;
            }

            XElement current = _xDocument.Root;
            foreach (var grp in groupPath)
            {
                var next = current
                    .Elements("Group")
                    .FirstOrDefault(g => (string)g.Attribute("Name") == grp);

                if (next == null)
                {
                    next = new XElement("Group", new XAttribute("Name", grp));
                    current.Add(next);
                }

                current = next;
            }

            var setting = current
                .Elements("Setting")
                .FirstOrDefault(s => (string)s.Attribute("Key") == key);

            if (setting != null)
            {
                setting.SetAttributeValue("Type", type);
                setting.SetAttributeValue("Value", valueString);
            }
            else
            {
                current.Add(new XElement("Setting",
                    new XAttribute("Key", key),
                    new XAttribute("Type", type),
                    new XAttribute("Value", valueString)));
            }

            _xDocument.Save(_filePath);
            EnumPropertyChanged?.Invoke(groupPath);
        }

        private T GetDefaultValueForKey<T>(string group, string key)
        {
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateHelper.TemplateDir, StateHelper.TemplateSettingsDir, "Settings.xml");

            if (!File.Exists(templatePath)) return GetDefaultValue<T>();
            try
            {
                XDocument temp_xDocument = XDocument.Load(templatePath);
                return GetValue<T>(group, key, temp_xDocument.Root, raiseExceptionIfNotExits:true);
            }
            catch
            {
                return GetDefaultValue<T>();
            }
        }
        private T GetDefaultValueForKey<T>(IEnumerable<string> groupPath, string key)
        {
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateHelper.TemplateDir, StateHelper.TemplateSettingsDir, "Settings.xml");

            if (!File.Exists(templatePath)) return GetDefaultValue<T>();
            try
            {
                XDocument temp_xDocument = XDocument.Load(templatePath);
                return GetValue<T>(groupPath, key, temp_xDocument.Root, raiseExceptionIfNotExits: true);
            }
            catch
            {
                return GetDefaultValue<T>();
            }
        }

    }
}
