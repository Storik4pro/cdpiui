using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper.Settings
{
    // ChatGPT -> piece of shit. 

    public class IniSettingsHelper
    {
        private readonly string _filePath;
        private readonly FileIniDataParser _parser;
        private IniData _data;
        private readonly object _lock = new object();

        public Encoding FileEncoding { get; }

        public IniSettingsHelper(string filePath, Encoding encoding = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            FileEncoding = encoding ?? Encoding.UTF8;

            _parser = new FileIniDataParser();
            _parser.Parser.Configuration.KeyValueAssigmentChar = '=';
            _parser.Parser.Configuration.AssigmentSpacer = string.Empty;
            _parser.Parser.Configuration.CommentString = "//";
            Reload();
        }
        public void SetValue<T>(string section, string key, T value)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_lock)
            {
                if (!_data.Sections.ContainsSection(section))
                    _data.Sections.AddSection(section);

                string strValue = value.ToString();
                _data[section][key] = strValue ?? string.Empty;
            }
        }

        private bool TryGetString(string section, string key, out string value)
        {
            lock (_lock)
            {
                value = null;
                if (!_data.Sections.ContainsSection(section)) return false;
                var sectionData = _data[section];
                if (!sectionData.ContainsKey(key)) return false;
                value = sectionData[key];
                return true;
            }
        }

        public bool TryGetValue<T>(string section, string key, out T value)
        {
            value = default;

            if (!TryGetString(section, key, out var raw)) return false;
            if (raw == null) return false;

            var targetType = typeof(T);
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                object converted;

                if (underlying.IsEnum)
                {
                    converted = Enum.Parse(underlying, raw, ignoreCase: true);
                }
                else if (underlying == typeof(bool))
                {
                    var s = raw.Trim();

                    converted = bool.Parse(s);
                }
                else if (underlying == typeof(Guid))
                {
                    converted = Guid.Parse(raw);
                }
                else if (underlying == typeof(TimeSpan))
                {
                    converted = TimeSpan.Parse(raw);
                }
                else
                {
                    try
                    {
                        converted = Convert.ChangeType(raw, underlying);
                    }
                    catch
                    {
                        var conv = TypeDescriptor.GetConverter(underlying);
                        if (conv != null && conv.CanConvertFrom(typeof(string)))
                        {
                            converted = conv.ConvertFromInvariantString(raw);
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                value = (T)converted!;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        public T GetValue<T>(string section, string key, T defaultValue)
        {
            if (TryGetValue<T>(section, key, out var val)) return val!;
            return defaultValue;
        }

        public T GetValue<T>(string section, string key)
        {
            if (TryGetValue<T>(section, key, out var val)) return val!;
            throw new KeyNotFoundException($"Section '{section}' or key '{key}' not found or cannot convert to {typeof(T).Name}.");
        }

        public void Save()
        {
            lock (_lock)
            {
                using (var sw = new StreamWriter(_filePath, false, FileEncoding))
                {
                    _parser.WriteData(sw, _data);
                }

                Reload();
            }
        }

        public void Reload()
        {
            lock (_lock)
            {
                if (File.Exists(_filePath))
                {
                    _data = _parser.ReadFile(_filePath);
                }
                else
                {
                    throw new FileNotFoundException("INI file not found.");
                }
            }
        }


        public void Dispose()
        {
            // pass
        }
    }
}
