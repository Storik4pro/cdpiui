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

namespace GoodbyeDPI_UI.Helper
{
    public class SettingsManager
    {
        private readonly string _filePath;
        private XDocument _xDocument;

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
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            _filePath = Path.Combine(appPath, "data", "settings", "settings.xml");

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
            else
            {
                return (T)(object)default(T);
            }
        }
        public T GetValue<T>(string group, string key)
        {
            var settingElement = _xDocument.Root
                .Elements("Group")
                .FirstOrDefault(g => g.Attribute("Name")?.Value == group)?
                .Elements("Setting")
                .FirstOrDefault(s => s.Attribute("Key")?.Value == key);

            if (settingElement == null) {
                var defaultValue = GetDefaultValue<T>();

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
                return (T)(object)value;

            throw new Exception($"Type mismatch or unsupported type for setting '{key}' in group '{group}'.");
        }
        public T GetValue<T>(IEnumerable<string> groupPath, string key)
        {
            XElement current = _xDocument.Root;

            foreach (var grp in groupPath)
            {
                current = current
                    .Elements("Group")
                    .FirstOrDefault(g => (string)g.Attribute("Name") == grp);
                if (current == null)
                {
                    var defaultValue = GetDefaultValue<T>();
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
                return defaultValue;
            }

            string value = (string)settingElement.Attribute("Value");
            string type = (string)settingElement.Attribute("Type");

            if (typeof(T) == typeof(int) && type == "int" && int.TryParse(value, out var iv)) return (T)(object)iv;
            if (typeof(T) == typeof(double) && type == "double" && double.TryParse(value, out var dv)) return (T)(object)dv;
            if (typeof(T) == typeof(bool) && type == "bool" && bool.TryParse(value, out var bv)) return (T)(object)bv;
            if (typeof(T) == typeof(string) && type == "string") return (T)(object)value;

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
            else
            {
                throw new Exception($"Unsupported value type. {typeof(T)}. Value is {value}" );
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
                    throw new Exception($"Unsupported value type: {typeof(T)}. Value is {value}");
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
        }

    }
}
