using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace client.Converters
{
    // true -> visible (IsVisible = true)
    // false -> hidden (IsVisible = false)
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            return isVisible; // повертаємо bool, а не Visibility
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // інверсія: true -> сховати, false -> показати
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = !(value is bool b && b);
            return isVisible; // теж bool
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
