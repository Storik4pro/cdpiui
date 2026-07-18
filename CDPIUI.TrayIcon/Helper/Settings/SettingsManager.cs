using CDPIUI_TrayIcon.Helper;
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

namespace CDPIUI_TrayIcon.Helper
{
    public class SettingsManager
    {
        private string _filePath;
        private XDocument? _xDocument;

        public Action<string>? PropertyChanged;
        public Action<IEnumerable<string>>? EnumPropertyChanged;

        private static SettingsManager? _instance;
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
            _filePath = Utils.GetSettingsFile();

            _ = Directory.CreateDirectory(Path.GetDirectoryName(_filePath)?? string.Empty);

            Reload();
        }

        private object _reloadLock = new object();

        public void Reload()
        {
            lock (_reloadLock)
            {
                _filePath = Utils.GetSettingsFile();
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
        }

        private T? GetDefaultValue<T>()
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)true;
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
                return (T)(object)0.0;
            }
            else
            {
                if (default(T) is null) return default;
                else return (T)(object)default(T);
            }
        }
        public T GetValue<T>(string group, string key, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
        {
            if (_xDocument is null) return GetDefaultValue<T>()!;

            xElement ??= _xDocument.Root;
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

            if (typeof(T) == typeof(int) && type == "int" && int.TryParse(value, out int intValue))
                return (T)(object)intValue;

            if (typeof(T) == typeof(double) && type == "double" && double.TryParse(value, out double doubleValue))
                return (T)(object)doubleValue;

            if (typeof(T) == typeof(bool) && type == "bool" && bool.TryParse(value, out bool boolValue))
                return (T)(object)boolValue;

            if (typeof(T) == typeof(string) && type == "string")
                return (T)(object)value!;

            if (typeof(T) == typeof(DateTime) && type == nameof(DateTime))
                return (T)(object)DateTime.Parse(value!);

            throw new Exception($"Type mismatch or unsupported type for setting '{key}' in group '{group}'.");
        }
        public T GetValue<T>(IEnumerable<string> groupPath, string key, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
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
                var defaultValue = GetDefaultValue<T>();
                SetValue(groupPath, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group path '{string.Join("/", groupPath)}' not found.");
                return defaultValue!;
            }

            string value = (string)settingElement.Attribute("Value");
            string type = (string)settingElement.Attribute("Type");

            if (typeof(T) == typeof(int) && type == "int" && int.TryParse(value, out var iv)) return (T)(object)iv;
            if (typeof(T) == typeof(double) && type == "double" && double.TryParse(value, out var dv)) return (T)(object)dv;
            if (typeof(T) == typeof(bool) && type == "bool" && bool.TryParse(value, out var bv)) return (T)(object)bv;
            if (typeof(T) == typeof(string) && type == "string") return (T)(object)value!;

            if (typeof(T) == typeof(DateTime) && type == nameof(DateTime))
                return (T)(object)DateTime.Parse((string)value!);

            throw new Exception($"Type mismatch or unsupported type for setting '{key}' in group path '{string.Join("/", groupPath)}'.");
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
                    new XAttribute("Value", valueString!)));
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
                    new XAttribute("Value", valueString!)));
            }

            _xDocument.Save(_filePath);
            EnumPropertyChanged?.Invoke(groupPath);
        }

        private T GetDefaultValueForKey<T>(string group, string key)
        {
            return GetDefaultValue<T>()!;
        }
        private T GetDefaultValueForKey<T>(IEnumerable<string> groupPath, string key)
        {
            return GetDefaultValue<T>()!;
        }

    }
}
