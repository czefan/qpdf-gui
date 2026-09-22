using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace QpdfGui.App.Converters;

/// <summary>
/// 将绑定属性与 ConverterParameter 比较，相等则返回 true。
/// 当 RadioButton 选中 (true) 时，ConvertBack 将 ConverterParameter 返回给源属性。
/// </summary>
public class EqualityToBoolConverter : IValueConverter
{
    public static readonly EqualityToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null && parameter == null) return true;
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
        {
            return parameter;
        }
        return BindingOperations.DoNothing;
    }
}
