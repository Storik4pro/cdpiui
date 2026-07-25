using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDPIUI.Shared.Extentions
{
    public static class StringExtention
    {
        public static string? SerializeTuples(this List<Tuple<string, string>> list) 
        {
            string result = list == null ? null : string.Join(';', list.Select(t => $"{t.Item1}:{t.Item2}"));
            return result;
        }

        public static List<Tuple<string, string>> DeserializeTuples(this string data)
        {
            return [.. data.Split(';', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => {
                           var parts = s.Split(':');
                           return Tuple.Create(parts[0], parts.Length > 1 ? parts[1] : "");
                       })];
        }

        public static string FirstCharToUpper(this string input) =>
        input switch
        {
            null => throw new ArgumentNullException(nameof(input)),
            "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
            _ => string.Concat(input[0].ToString().ToUpper(), input.AsSpan(1).ToString())
        };
    }
}
