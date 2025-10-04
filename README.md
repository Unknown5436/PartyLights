# PartyLights - Audio-Synced Smart Lighting

A Windows application that synchronizes smart lighting devices (Philips Hue, TP-Link Kasa, Magic Home) with audio playing on your computer or through Spotify. Features a modern dark mode UI with various lighting effects including beat detection, frequency visualization, mood-based colors, and more.

## 🎵 Features

### Audio Analysis
- **Real-time Audio Capture**: WASAPI loopback capture for system audio
- **Advanced FFT Analysis**: 1024-point FFT with Blackman-Harris windowing
- **Beat Detection**: Sophisticated beat detection with volume history analysis
- **Frequency Analysis**: 12-band frequency spectrum analysis
- **Mood Classification**: Automatic mood detection (Happy, Sad, Excited, Calm, Angry, Neutral)
- **Genre Recognition**: Music genre detection (Electronic, Rock, Jazz, Pop, etc.)
- **Tempo Estimation**: BPM calculation with median interval smoothing

### Lighting Effects
- **🎵 Beat Sync**: Volume-based brightness with beat-triggered flash effects
- **🌈 Frequency Visualization**: RGB mapping from frequency bands
- **😊 Mood Lighting**: Color selection based on detected mood
- **📊 Spectrum Analyzer**: Dominant frequency band visualization
- **🎉 Party Mode**: Dynamic color cycling with beat-driven effects
- **🔊 Volume Reactive**: Brightness changes based on audio volume

### Device Support
- **Philips Hue**: Bridge discovery, user registration, full color/brightness control
- **TP-Link Kasa**: UDP discovery, encrypted protocol, color temperature support
- **Magic Home**: UDP discovery, binary protocol, 18 built-in effects

### User Interface
- **Modern Dark Theme**: Minimalist dark mode UI using ModernWpfUI
- **Real-time Visualization**: Live audio analysis display
- **Device Management**: Discover, connect, and manage multiple devices
- **Preset System**: Save and load lighting effect configurations
- **Settings Panel**: Adjustable sensitivity, latency, and effect parameters

## 🏗️ Architecture

### Project Structure
```
PartyLights/
├── PartyLights/                 # Main WPF application
├── PartyLights.Core/            # Core models and interfaces
├── PartyLights.Services/        # Business logic services
├── PartyLights.Devices/         # Device-specific controllers
├── PartyLights.Audio/           # Audio processing modules
├── PartyLights.UI/              # WPF user interface
└── PartyLights.Tests/           # Unit and integration tests
```

### Technology Stack
- **.NET 8.0**: Modern C# framework
- **WPF**: Windows Presentation Foundation for UI
- **MVVM**: Model-View-ViewModel pattern
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **NAudio**: Audio capture and processing
- **ModernWpfUI**: Modern UI components
- **Serilog**: Structured logging
- **Q42.HueApi**: Philips Hue integration

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- Smart lighting devices (Philips Hue, TP-Link Kasa, or Magic Home)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/partylights.git
   cd partylights
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

4. **Run the application**
   ```bash
   dotnet run --project PartyLights
   ```

### Device Setup

#### Philips Hue
1. Ensure your Hue Bridge is connected to your network
2. Press the bridge button when prompted during device discovery
3. The app will automatically register and discover your lights

#### TP-Link Kasa
1. Ensure your TP-Link devices are connected to your network
2. Use the Kasa app to set up devices initially
3. The app will discover devices automatically

#### Magic Home
1. Ensure your Magic Home controller is connected to WiFi
2. The app will discover devices using UDP broadcast

## 📋 Current Progress

### ✅ Phase 1: Foundation (Completed)
- [x] Project setup and architecture
- [x] Basic WPF UI with dark theme
- [x] Device discovery and connection testing
- [x] Basic audio capture implementation

### ✅ Phase 2: Core Services (In Progress)
- [x] Audio analysis pipeline
- [ ] Device control implementations
- [ ] Configuration management
- [ ] Basic preset system

### 🔄 Upcoming Phases
- **Phase 3**: Spotify integration and advanced audio analysis
- **Phase 4**: Complete preset system and advanced UI features
- **Phase 5**: Testing, documentation, and deployment

## 🎛️ Usage

### Basic Operation
1. **Launch the application**
2. **Discover devices** by clicking "Discover Devices"
3. **Connect to devices** by clicking the connect button on each device card
4. **Select audio source** (System Audio or Spotify)
5. **Choose a lighting effect** from the preset buttons
6. **Start synchronization** by clicking "Start Sync"

### Advanced Settings
- **Sensitivity**: Adjust beat detection sensitivity (0.1 - 2.0)
- **Latency**: Set audio processing latency in milliseconds
- **Brightness Range**: Configure minimum and maximum brightness levels
- **Color Intensity**: Adjust color saturation and intensity

## 🔧 Development

### Building from Source
```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run tests
dotnet test

# Create a release build
dotnet publish --configuration Release --runtime win-x64 --self-contained true
```

### Project Dependencies
- **NAudio**: Audio capture and FFT processing
- **Q42.HueApi**: Philips Hue bridge communication
- **ModernWpfUI**: Modern UI components
- **Serilog**: Structured logging
- **CommunityToolkit.Mvvm**: MVVM framework

## 📊 Audio Analysis Pipeline

The application features a sophisticated audio analysis pipeline with multiple specialized analyzers:

### Analyzers
- **FrequencyAnalyzer**: FFT-based frequency domain analysis
- **BeatAnalyzer**: Advanced beat detection with volume history
- **SpectralAnalyzer**: Spectral features (bandwidth, contrast, flatness)
- **RhythmAnalyzer**: Rhythm regularity and complexity analysis
- **MoodAnalyzer**: Mood classification based on energy, valence, and arousal

### Features Extracted
- Volume (RMS)
- Frequency bands (12 bands)
- Spectral centroid, rolloff, flux
- Beat detection and intensity
- Tempo estimation (BPM)
- Mood classification
- Genre recognition
- Zero-crossing rate
- Spectral bandwidth and contrast

## 🎨 Lighting Effects

### Beat Sync
- Volume-based brightness control
- Beat-triggered flash effects
- Tempo-responsive timing

### Frequency Visualization
- RGB mapping from frequency bands
- Low frequencies → Red
- Mid frequencies → Green
- High frequencies → Blue

### Mood Lighting
- Automatic color selection based on detected mood
- Brightness adjustment based on arousal level
- Smooth color transitions

### Spectrum Analyzer
- Dominant frequency band visualization
- HSV color space mapping
- Spectral flux-based brightness

### Party Mode
- Dynamic color cycling
- Beat-driven color changes
- High-energy effects

## 🔌 Device Protocols

### Philips Hue
- **Discovery**: HTTP-based bridge discovery
- **Authentication**: Bridge button press + API key generation
- **Control**: REST API with JSON commands
- **Features**: Full color, brightness, temperature, effects

### TP-Link Kasa
- **Discovery**: UDP broadcast on port 9999
- **Protocol**: Encrypted JSON over TCP
- **Control**: Device-specific commands
- **Features**: Color, brightness, temperature

### Magic Home
- **Discovery**: UDP broadcast on port 48899
- **Protocol**: Binary commands over TCP port 5577
- **Control**: Byte-array commands
- **Features**: Color, brightness, 18 built-in effects

## 🐛 Troubleshooting

### Common Issues

**Audio not detected**
- Ensure Windows audio is playing
- Check audio device permissions
- Verify WASAPI loopback capture is available

**Devices not discovered**
- Ensure devices are on the same network
- Check firewall settings
- Verify device power and connectivity

**Connection failures**
- Check IP addresses are correct
- Verify network connectivity
- Ensure device APIs are accessible

### Logging
The application uses Serilog for structured logging. Logs are written to:
- Console output
- File logs (if configured)
- Windows Event Log (if configured)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **NAudio** - Audio processing library
- **Q42.HueApi** - Philips Hue integration
- **ModernWpfUI** - Modern UI components
- **CommunityToolkit.Mvvm** - MVVM framework

## 📞 Support

For support, please open an issue on GitHub or contact the development team.

---

**PartyLights** - Bringing your music to life through intelligent lighting! 🎵💡