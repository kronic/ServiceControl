﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ServiceControl.Config.Xaml.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public bool IsHidden { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = false;
            if (value is bool)
            {
                flag = (bool)value;
            }
            return flag ^ Invert ? Visibility.Visible : (IsHidden ? Visibility.Hidden : Visibility.Collapsed);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var result = false;

            if (value is Visibility)
            {
                result = (Visibility)value == Visibility.Visible;
            }

            return result ^ Invert;
        }
    }
}