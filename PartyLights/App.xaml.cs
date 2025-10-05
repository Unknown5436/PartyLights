using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using PartyLights.Core.Interfaces;
using PartyLights.Services;
using PartyLights.Devices;
using PartyLights.Audio;
using PartyLights.UI;

namespace PartyLights;

/// <summary>
/// Main application entry point
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(Configuration)
            .CreateLogger();

        try
        {
            // Build the host
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .UseSerilog()
                .Build();

            // Start the host
            await _host.StartAsync();

            // Show the main window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            throw;
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during application shutdown");
        }
        finally
        {
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ConfigurationManagerService>();
        services.AddSingleton<ConfigurationUiService>();
        services.AddSingleton<ConfigurationValidationService>();
        services.AddSingleton<IDeviceManagerService, DeviceManagerService>();
        services.AddSingleton<IAudioCaptureService, EnhancedAudioCaptureService>();
        services.AddSingleton<ISpotifyService, SpotifyService>();
        services.AddSingleton<ILightingEffectService, LightingEffectService>();

        // Advanced device control services
        services.AddSingleton<IAdvancedDeviceControlService, AdvancedDeviceControlService>();
        services.AddSingleton<IDeviceSynchronizationService, DeviceSynchronizationService>();

        // Preset management services
        services.AddSingleton<PresetManagementService>();
        services.AddSingleton<PresetExecutionEngine>();
        services.AddSingleton<PresetUiService>();

        // Device controllers
        services.AddTransient<IDeviceController, HueDeviceController>();
        services.AddTransient<IDeviceController, TpLinkDeviceController>();
        services.AddTransient<IDeviceController, MagicHomeDeviceController>();

        // UI
        services.AddTransient<MainWindow>();
        services.AddTransient<MainViewModel>();

        // Hosted services
        services.AddHostedService<AudioProcessingService>();
    }
}
