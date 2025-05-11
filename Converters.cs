using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace InvoiceBalanceRefresher.Converters
{
    /// <summary>
    /// Contains utility converters for the application
    /// </summary>
    public static class Converters
    {
        #region Font Weight Conversion

        /// <summary>
        /// Attempts to parse a string representation of a font weight into a FontWeight value
        /// </summary>
        /// <param name="weightString">String representation of a font weight</param>
        /// <param name="fontWeight">The resulting FontWeight if parsing succeeds</param>
        /// <returns>True if parsing succeeds, false otherwise</returns>
        public static bool TryParseFontWeight(string weightString, out FontWeight fontWeight)
        {
            fontWeight = FontWeights.Normal;

            try
            {
                // Handle common FontWeight predefined values
                switch (weightString)
                {
                    case "Thin": fontWeight = FontWeights.Thin; return true;
                    case "ExtraLight": fontWeight = FontWeights.ExtraLight; return true;
                    case "Light": fontWeight = FontWeights.Light; return true;
                    case "Normal": fontWeight = FontWeights.Normal; return true;
                    case "Medium": fontWeight = FontWeights.Medium; return true;
                    case "SemiBold": fontWeight = FontWeights.SemiBold; return true;
                    case "Bold": fontWeight = FontWeights.Bold; return true;
                    case "ExtraBold": fontWeight = FontWeights.ExtraBold; return true;
                    case "Black": fontWeight = FontWeights.Black; return true;
                    case "ExtraBlack": fontWeight = FontWeights.ExtraBlack; return true;
                    default:
                        // Try to parse as numeric weight
                        if (int.TryParse(weightString, out int numericWeight))
                        {
                            fontWeight = FontWeight.FromOpenTypeWeight(numericWeight);
                            return true;
                        }
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Visibility Converters

        /// <summary>
        /// Converts a boolean value to Visibility (true = Visible, false = Collapsed)
        /// </summary>
        public static Visibility BoolToVisibility(bool value)
        {
            return value ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a boolean value to Visibility (true = Collapsed, false = Visible)
        /// </summary>
        public static Visibility BoolToInverseVisibility(bool value)
        {
            return value ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        #region Color Converters

        /// <summary>
        /// Tries to parse a string to a Color
        /// </summary>
        public static bool TryParseColor(string colorString, out Color color)
        {
            color = Colors.Transparent;
            try
            {
                if (string.IsNullOrWhiteSpace(colorString))
                    return false;

                // Handle hex color format
                if (colorString.StartsWith("#"))
                {
                    var converter = new ColorConverter();
                    var convertedColor = converter.ConvertFrom(colorString);
                    if (convertedColor != null)
                    {
                        color = (Color)convertedColor;
                        return true;
                    }
                    return false;
                }

                // Handle named colors
                // Handle named colors
                var colorProperty = typeof(Colors).GetProperty(colorString);
                if (colorProperty != null && colorProperty.GetValue(null) is Color validColor)
                {
                    color = validColor;
                    return true;
                }


                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Enum Converters

        /// <summary>
        /// Converts between enum types that have the same underlying values
        /// </summary>
        public static TTarget ConvertEnum<TSource, TTarget>(TSource source)
            where TSource : Enum
            where TTarget : Enum
        {
            return (TTarget)Enum.ToObject(typeof(TTarget), Convert.ToInt32(source));
        }

        #endregion

        #region Numeric Converters

        /// <summary>
        /// Formats a decimal as a currency string
        /// </summary>
        public static string FormatCurrency(decimal value)
        {
            return value.ToString("C", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Attempts to parse a string to decimal, handling currency symbols
        /// </summary>
        public static bool TryParseCurrency(string value, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Remove currency symbols and other non-numeric characters except decimal point and negative sign
            string cleanValue = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray());
            return decimal.TryParse(cleanValue, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
        }

        #endregion

        #region String Formatting

        /// <summary>
        /// Formats field names by inserting spaces before capital letters
        /// </summary>
        public static string FormatFieldName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return string.Empty;

            // Insert spaces before capital letters in the middle of the string
            string result = fieldName[0].ToString();
            for (int i = 1; i < fieldName.Length; i++)
            {
                if (char.IsUpper(fieldName[i]))
                {
                    result += " ";
                }
                result += fieldName[i];
            }
            return result;
        }

        #endregion
    }

    #region IValueConverter Implementations for XAML

    /// <summary>
    /// Converts boolean values to Visibility (true = Visible, false = Collapsed)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool boolValue && boolValue) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }

    /// <summary>
    /// Converts boolean values to Visibility (true = Collapsed, false = Visible)
    /// </summary>
    public class BoolToInverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool boolValue && boolValue) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Converts a string to a formatted field name with spaces before capital letters
    /// </summary>
    public class FieldNameFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string fieldName ? Converters.FormatFieldName(fieldName) : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Formats a decimal value as currency
    /// </summary>
    public class CurrencyFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return Converters.FormatCurrency(decimalValue);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && Converters.TryParseCurrency(stringValue, out decimal result))
                return result;
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts between LogLevel enums (for handling different LogLevel enum types)
    /// </summary>
    public class LogLevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MainWindow.LogLevel mainWindowLogLevel)
                return (LogLevel)(int)mainWindowLogLevel;
            else if (value is LogLevel logLevel)
                return (MainWindow.LogLevel)(int)logLevel;
            
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(MainWindow.LogLevel) && value is LogLevel logLevel)
                return (MainWindow.LogLevel)(int)logLevel;
            else if (targetType == typeof(LogLevel) && value is MainWindow.LogLevel mainWindowLogLevel)
                return (LogLevel)(int)mainWindowLogLevel;
            
            return DependencyProperty.UnsetValue;
        }
    }
    #endregion
}
