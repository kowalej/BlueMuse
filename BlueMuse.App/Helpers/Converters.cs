using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace BlueMuse.Helpers
{
    public static class BooleanExtensions
    {
        public static string ToYesNoString(this bool value)
        {
            return value ? "Yes" : "No";
        }
    }

    public class GenericStringFormatter : IValueConverter
    {
        // This converts the value object to the string to display.
        // This will work with most simple types.
        public object Convert(object v, Type t,
            object p, string l)
        {
            // Retrieve the format string and use it to format the value.
            string formatString = p as string;
            if (!string.IsNullOrEmpty(formatString))
            {
                return string.Format(formatString, v);
            }

            // If the format string is null or empty, simply
            // call ToString() on the value.
            return v.ToString();
        }

        // No need to implement converting back on a one-way binding
        public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
    }

    public class YesNoValueFormatter : IValueConverter
    {
        // This converts the value object to the string to display.
        // This will work with most simple types.
        public object Convert(object v, Type t,
            object p, string l)
        {
            // Retrieve the format string and use it to format the value.
            string formatString = p as string;
            if (!string.IsNullOrEmpty(formatString))
            {
                return string.Format(formatString, ((bool)v).ToYesNoString());
            }

            // If the format string is null or empty, simply
            // call ToString() on the value.
            return v.ToString();
        }

        // No need to implement converting back on a one-way binding
        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class VisibleWhenZeroConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, string l) => Equals(0, (Int32)v) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, string l)
        {
            if (t == typeof(bool))
                return !(bool)v;
            else if (t == typeof(Visibility))
                return (bool)v == true ? Visibility.Collapsed : Visibility.Visible;
            else
                throw new InvalidOperationException("The target must be of type boolean or visibility.");
        }
        public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
    }
}
