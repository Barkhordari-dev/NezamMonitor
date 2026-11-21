using System;
using System.Globalization;
using System.Windows.Data;

namespace NezamMonitorPro.App.Converters;

/// <summary>
/// مقدار «ثبت نشده» در صفحه‌های دفترچه کمی محو نشان داده می‌شود تا با مقدار
/// واقعی قاطی نشود؛ بقیه مقدارها کاملاً واضح‌اند.
/// </summary>
public sealed class PlaceholderTextOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s == "ثبت نشده" ? 0.6 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
