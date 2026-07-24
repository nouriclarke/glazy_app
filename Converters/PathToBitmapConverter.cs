using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace ASTEM_DB.Converters
{
    public class PathToBitmapConverter : IValueConverter
    {
        public static readonly PathToBitmapConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try { return new Bitmap(path); }
                catch { return null; }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class StringNotEmptyConverter : IValueConverter
    {
        public static readonly StringNotEmptyConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string s && !string.IsNullOrWhiteSpace(s);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}