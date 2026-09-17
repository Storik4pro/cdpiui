using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDPIUI.Shared.Extentions
{
    public static class DictionaryExtention
    {
        /// <summary>
        /// Get dictionary key by value. <br></br> 
        /// If value is <see cref="string"/>, 
        /// uses <see cref="string.Equals(string, string)"/>.<br></br>
        /// Otherwise uses default object comparier.
        /// </summary>
        public static KeyType GetKeyByValue<KeyType, ValueType>(this Dictionary<KeyType, ValueType> dicitionary, ValueType value) where ValueType : class 
        {
            if (value is string stval)
            {
                return dicitionary.FirstOrDefault(kvp => string.Equals(kvp.Value as string, value as string, StringComparison.Ordinal)).Key;
            }

            return dicitionary.FirstOrDefault(kvp => kvp.Value == value).Key;
        }

        public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
        {
            foreach (var element in source)
                if (!target.Contains(element))
                    target.Add(element);
        }
    }
}
