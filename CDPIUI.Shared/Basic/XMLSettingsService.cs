using CDPIUI.Shared.Exceptions.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;

namespace CDPIUI.Shared.Basic
{
    /// <summary>
    /// XML Settings service
    /// </summary>
    public class XMLSettingsService
    {
        /// <summary>
        /// Path to settings file
        /// </summary>
        public string SettingsFilePath { get; init; }

        private XDocument? _xDocument;
        private bool _persistenceUnavailable;
        public bool HasUnrecoverableLoadError { get; private set; }
        public bool WasRecoveredFromBackup { get; private set; }
        public string? LoadError { get; private set; }
        public string RecoveryNoticePath => SettingsFilePath + ".recovery-notice";

        public Action<string>? PropertyChanged;
        public Action<IEnumerable<string>>? EnumPropertyChanged;

        public XMLSettingsService(string filepath)
        {
            SettingsFilePath = filepath;
            Reload();
        }

        private readonly object _reloadLock = new();
        /// <summary>
        /// Reload setting file. Any <see cref="Action"/> won't be called any way
        /// </summary>
        public void Reload()
        {
            lock (_reloadLock)
            {
                _persistenceUnavailable = false;
                try
                {
                    _xDocument = LoadDocument(SettingsFilePath);
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    _xDocument = new XDocument(new XElement("Settings"));
                    SaveSettings();
                }
                catch (Exception ex) when (IsReadFailure(ex))
                {
                    LoadError = ex.Message;
                    try
                    {
                        if (_xDocument == null)
                        {
                            _xDocument = LoadDocument(SettingsFilePath + ".bak");
                            WasRecoveredFromBackup = true;
                        }
                    }
                    catch (Exception backupError) when (IsReadFailure(backupError))
                    {
                        HasUnrecoverableLoadError = _xDocument == null;
                        _xDocument ??= new XDocument(new XElement("Settings"));
                    }

                    try
                    {
                        File.Copy(SettingsFilePath, SettingsFilePath + ".corrupt-" + Guid.NewGuid().ToString("N"));
                    }
                    catch (Exception copyError) when (IsReadFailure(copyError)) { }

                    if (HasUnrecoverableLoadError)
                    {
                        try { File.WriteAllText(RecoveryNoticePath, LoadError); }
                        catch (Exception noticeError) when (IsReadFailure(noticeError)) { }
                    }

                    SaveSettings(keepBackup: false);
                }
            }
        }

        private static bool IsReadFailure(Exception exception) =>
            exception is XmlException or IOException or UnauthorizedAccessException;

        private static XDocument LoadDocument(string path)
        {
            var document = XDocument.Load(path);
            if (document.Root?.Name != "Settings")
                throw new XmlException("The settings document must have a Settings root element.");
            return document;
        }

        private void SaveSettings(bool keepBackup = true)
        {
            if (_persistenceUnavailable) return;
            string temporaryPath = SettingsFilePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(SettingsFilePath))!);
                _xDocument!.Save(temporaryPath);
                if (File.Exists(SettingsFilePath))
                    File.Replace(temporaryPath, SettingsFilePath, keepBackup ? SettingsFilePath + ".bak" : null);
                else
                    File.Move(temporaryPath, SettingsFilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _persistenceUnavailable = true;
                Debug.WriteLine(ex);
            }
            finally
            {
                try { File.Delete(temporaryPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }

        /// <summary>
        /// Get value or default. Sets default value to settings file. Throwing no exception
        /// </summary>
        /// <typeparam name="T">Target value type</typeparam>
        /// <param name="group">Group. "SYSTEM", for example</param>
        /// <param name="key">Key. "sampleKey", for example</param>
        /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Value if key exsits, otherwise default</returns>
        public T? GetValueOrDefault<T>(string group, string key, XElement? xElement = null, T? defaultValue = default)
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
        /// <summary>
        /// Get value or hardcoded defailt. Sets default value to settings file. 
        /// Throwing <see cref="SettingNotFoundException"/> if <paramref name="raiseExceptionIfNotExits"/> setted as true. 
        /// </summary>
        /// <typeparam name="T">Target value type</typeparam>
        /// <param name="group">Group. "SYSTEM", for example</param>
        /// <param name="key">Key. "sampleKey", for example</param>
        /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
        /// <param name="raiseExceptionIfNotExits">Throw <see cref="SettingNotFoundException"/> if <paramref name="key"/> not found in file</param>
        /// <returns>Value if key exsits, otherwise default</returns>
        /// <exception cref="SettingTypeNotSupported"></exception>
        /// <exception cref="SettingNotFoundException"></exception>
        public T? GetValue<T>(string group, string key, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
        {
            xElement ??= _xDocument.Root;
            var settingElement = xElement
                .Elements("Group")
                .FirstOrDefault(g => g.Attribute("Name")?.Value == group)?
                .Elements("Setting")
                .FirstOrDefault(s => s.Attribute("Key")?.Value == key);

            if (settingElement == null)
            {
                if (raiseExceptionIfNotExits) throw new SettingNotFoundException("Value not exist");

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
                throw new SettingTypeNotSupported($"Type mismatch or unsupported type for setting '{key}' in group '{group}'.");
            }
        }
        /// <summary>
        /// Get value or hardcoded defailt. Sets default value to settings file. 
        /// Throwing <see cref="SettingNotFoundException"/> if <paramref name="raiseExceptionIfNotExits"/> setted as true. 
        /// </summary>
        /// <typeparam name="T">Target value type</typeparam>
        /// <param name="groupPath">Group path. "SYSTEM"/"DATA", for example</param>
        /// <param name="key">Key. "sampleKey", for example</param>
        /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
        /// <param name="raiseExceptionIfNotExits">Throw <see cref="SettingNotFoundException"/> if <paramref name="key"/> not found in file</param>
        /// <returns>Value if key exsits, otherwise default</returns>
        /// <exception cref="SettingTypeNotSupported"></exception>
        /// <exception cref="SettingNotFoundException"></exception>
        public T? GetValue<T>(IEnumerable<string> groupPath, string key, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
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
                    if (raiseExceptionIfNotExits) throw new SettingNotFoundException("Value not exist");
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
                if (raiseExceptionIfNotExits) throw new SettingNotFoundException("Value not exist");
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
                throw new SettingTypeNotSupported($"Type mismatch or unsupported type for setting '{key}' in group path '{string.Join("/", groupPath)}'.");
            }
        }

        /// <summary>
        /// Get key value pair.
        /// </summary>
        /// <typeparam name="T">Target value type</typeparam>
        /// <param name="groupPath"></param>
        /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
        /// <param name="raiseExceptionIfNotExits">Throw <see cref="SettingNotFoundException"/> if <paramref name="groupPath"/> not found in file</param>
        /// <returns>Dictionary if <paramref name="groupPath"/> exsits, otherwise empty dict</returns>
        /// <exception cref="SettingNotFoundException"></exception>
        public Dictionary<string, T?> GetKeyPair<T>(IEnumerable<string> groupPath, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
        {
            if (xElement == null) xElement = _xDocument.Root;
            XElement current = xElement;

            Dictionary<string, T?> dict = [];

            foreach (var grp in groupPath)
            {
                current = current
                    .Elements("Group")
                    .FirstOrDefault(g => (string)g.Attribute("Name") == grp);
                if (current == null)
                {
                    if (raiseExceptionIfNotExits) throw new SettingNotFoundException("Value not exist");
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


        /// <summary>
        /// Set value to settings file
        /// </summary>
        /// <typeparam name="T">Target value type</typeparam>
        /// <param name="group">Group. "SYSTEM", for example</param>
        /// <param name="key">Key. "sampleKey", for example</param>
        /// <param name="value">Value to set</param>
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

            SaveSettings();
            PropertyChanged?.Invoke(group);
        }

        /// <summary>
        /// Set value to settings file
        /// </summary>
        /// <typeparam name="T">Target value type</typeparam>
        /// <param name="groupPath">Group path. "SYSTEM"/"DATA", for example</param>
        /// <param name="key">Key. "sampleKey", for example</param>
        /// <param name="value">Value to set</param>
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

            SaveSettings();
            EnumPropertyChanged?.Invoke(groupPath);
        }

        private static T? GetValueFromString<T>(string? value, string? type)
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

        protected virtual T? GetDefaultValueForKey<T>(string group, string key)
        {
            return GetDefaultValue<T>();
        }
        protected virtual T? GetDefaultValueForKey<T>(IEnumerable<string> groupPath, string key)
        {
            return GetDefaultValue<T>();
        }

        protected T? GetDefaultValue<T>()
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)false;
            }
            else if (typeof(T) == typeof(string))
            {
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
    }
}
