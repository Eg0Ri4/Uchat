using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Globalization;

namespace client.Converters
{
    public class BoolToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isIncoming)
                return isIncoming ? HorizontalAlignment.Left : HorizontalAlignment.Right;

            return HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static readonly BoolToAlignmentConverter Instance = new();
    }
}
