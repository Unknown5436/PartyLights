using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PartyLights.Core.Models;

namespace PartyLights.UI.Converters;

/// <summary>
/// Converts device type to color
/// </summary>
public class DeviceTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DeviceType deviceType)
        {
            return deviceType switch
            {
                DeviceType.PhilipsHue => new SolidColorBrush(Color.FromRgb(0, 122, 204)), // Blue
                DeviceType.TpLink => new SolidColorBrush(Color.FromRgb(255, 152, 0)),     // Orange
                DeviceType.MagicHome => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))                    // Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(128, 128, 128));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to status color
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green
                : new SolidColorBrush(Color.FromRgb(244, 67, 54));  // Red
        }
        return new SolidColorBrush(Color.FromRgb(128, 128, 128));    // Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to status text
/// </summary>
public class BoolToStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected ? "Connected" : "Disconnected";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts audio source to display text
/// </summary>
public class AudioSourceToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AudioSource audioSource)
        {
            return audioSource switch
            {
                AudioSource.System => "System Audio",
                AudioSource.Spotify => "Spotify",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "System Audio" => AudioSource.System,
                "Spotify" => AudioSource.Spotify,
                _ => AudioSource.System
            };
        }
        return AudioSource.System;
    }
}
