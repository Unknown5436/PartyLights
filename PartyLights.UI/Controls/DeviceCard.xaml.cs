using System.Windows;
using System.Windows.Controls;
using PartyLights.Core.Models;

namespace PartyLights.UI.Controls;

/// <summary>
/// User control for displaying device information
/// </summary>
public partial class DeviceCard : UserControl
{
    public static readonly DependencyProperty DeviceProperty =
        DependencyProperty.Register(nameof(Device), typeof(SmartDevice), typeof(DeviceCard));

    public SmartDevice Device
    {
        get => (SmartDevice)GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    public DeviceCard()
    {
        InitializeComponent();
    }
}
