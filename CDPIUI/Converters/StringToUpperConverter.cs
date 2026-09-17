using Microsoft.UI.Xaml.Data;
using System;

namespace CDPIUI.Converters
{
    public class StringToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
                return s.ToUpper();
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
