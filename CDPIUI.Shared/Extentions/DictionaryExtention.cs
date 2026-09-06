using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDPIUI.Shared.Extentions
{
    public static class DictionaryExtention
    {
        public static KeyType GetKeyByValue<KeyType, ValueType>(this Dictionary<KeyType, ValueType> dicitionary, ValueType value) where ValueType : class 
        {
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
