# PartyLights - Advanced Audio-Synced Smart Lighting

A comprehensive Windows application that synchronizes smart lighting devices (Philips Hue, TP-Link Kasa, Magic Home) with audio playing on your computer or through Spotify. Features a modern dark mode UI with advanced lighting effects, real-time audio analysis, comprehensive testing, automated documentation, and production-ready deployment capabilities.

## 🎵 Features

### Advanced Audio Analysis
- **Real-time Audio Capture**: High-performance WASAPI loopback capture with adaptive buffering
- **Advanced FFT Analysis**: 1024-point FFT with Blackman-Harris windowing and spectral analysis
- **Sophisticated Beat Detection**: Multi-algorithm beat detection with confidence scoring
- **Comprehensive Frequency Analysis**: 12-band frequency spectrum with detailed band analysis
- **Advanced Spectral Features**: Spectral centroid, rolloff, bandwidth, contrast, flatness, and flux
- **MFCC Analysis**: Mel-frequency cepstral coefficients for audio fingerprinting
- **Mood Classification**: AI-powered mood detection (Happy, Sad, Excited, Calm, Angry, Neutral)
- **Genre Recognition**: Machine learning-based music genre detection
- **Tempo Estimation**: Advanced BPM calculation with rhythm analysis
- **Harmonic Analysis**: Fundamental frequency detection and harmonic content analysis
- **Dynamic Range Analysis**: Compression ratio, peak levels, and crest factor calculation
- **Audio Quality Metrics**: Signal-to-noise ratio, distortion detection, and clipping analysis

### Advanced Lighting Effects
- **🎵 Beat Sync**: Volume-based brightness with beat-triggered flash effects and tempo synchronization
- **🌈 Frequency Visualization**: Advanced RGB mapping from frequency bands with smooth transitions
- **😊 Mood Lighting**: AI-powered color selection based on detected mood and emotional analysis
- **📊 Spectrum Analyzer**: Real-time dominant frequency band visualization with HSV color mapping
- **🎉 Party Mode**: Dynamic color cycling with beat-driven effects and high-energy patterns
- **🔊 Volume Reactive**: Adaptive brightness changes based on audio volume with smoothing
- **🎨 Custom Patterns**: User-defined lighting patterns with parameter customization
- **🌊 Wave Effects**: Flowing wave patterns synchronized to audio rhythm
- **⚡ Strobe Effects**: Beat-synchronized strobe lighting with intensity control
- **🎭 Theater Mode**: Cinematic lighting effects for ambient entertainment
- **🌈 Rainbow Mode**: Continuous color cycling with customizable speed and intensity
- **🔥 Fire Effects**: Dynamic fire-like patterns with audio-reactive intensity

### Comprehensive Device Support
- **Philips Hue**: Advanced bridge discovery, OAuth authentication, full color/brightness/temperature control, scene management
- **TP-Link Kasa**: UDP discovery, encrypted JSON protocol, color temperature support, device grouping
- **Magic Home**: UDP discovery, binary protocol, 18 built-in effects, custom pattern support
- **Multi-Device Synchronization**: Coordinated lighting across all device types
- **Device Grouping**: Logical grouping of devices for synchronized effects
- **Advanced Device Management**: Performance monitoring, connection health, and error recovery

### Advanced User Interface
- **Modern Dark Theme**: Sophisticated dark mode UI with ModernWpfUI and custom styling
- **Real-time Visualizations**: Live audio analysis with spectrum analyzer, waveform, and beat visualizer
- **Advanced Device Management**: Discover, connect, group, and monitor multiple devices
- **Comprehensive Preset System**: Save, load, share, and manage lighting effect configurations
- **Advanced Settings Panel**: Granular control over sensitivity, latency, effect parameters, and performance
- **Responsive Design**: Adaptive UI that scales across different screen sizes and resolutions
- **Accessibility Features**: Full accessibility compliance with screen reader support
- **Theme Customization**: Multiple theme options with custom color schemes
- **Animation System**: Smooth transitions and easing functions for enhanced user experience

### Spotify Integration
- **OAuth 2.0 Authentication**: Secure Spotify account connection with PKCE flow
- **Real-time Playback**: Live playback state monitoring and control
- **Track Information**: Access to track metadata, audio features, and analysis
- **Playlist Management**: Browse and control Spotify playlists
- **Audio Features API**: Access to Spotify's audio analysis data
- **Device Control**: Control Spotify playback on connected devices
- **Search Integration**: Search tracks, artists, albums, and playlists
- **User Profile**: Access to user's listening history and preferences

### Performance & Optimization
- **Adaptive Performance**: Dynamic quality adjustment based on system resources
- **Resource Management**: Efficient memory and CPU usage with garbage collection optimization
- **Performance Monitoring**: Real-time performance metrics and profiling
- **Quality Settings**: Configurable performance vs. quality trade-offs
- **Background Processing**: Non-blocking audio processing and device communication
- **Caching System**: Intelligent caching for improved responsiveness

### Testing & Quality Assurance
- **Comprehensive Testing**: Unit, integration, UI, and performance testing
- **Test Automation**: Automated test execution and scheduling
- **Test Reporting**: Detailed test reports with analytics and insights
- **Code Coverage**: High test coverage with quality metrics
- **Continuous Integration**: Automated testing in CI/CD pipelines
- **Error Handling**: Robust error handling with recovery mechanisms

### Documentation & Deployment
- **Automated Documentation**: Generated user guides, API docs, and developer documentation
- **Deployment Automation**: Automated deployment pipelines and distribution
- **Installer Generation**: MSI, NSIS, Inno Setup, and portable installers
- **Distribution Management**: Multi-platform distribution packages
- **Version Control**: Automated versioning and release management
- **Production Ready**: Enterprise-grade deployment and monitoring capabilities

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
- **.NET 8.0**: Modern C# framework with latest features
- **WPF**: Windows Presentation Foundation with ModernWpfUI
- **MVVM**: Model-View-ViewModel pattern with CommunityToolkit.Mvvm
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **NAudio**: Advanced audio capture, processing, and FFT analysis
- **Spotify Web API**: OAuth 2.0 integration with PKCE flow
- **Q42.HueApi**: Philips Hue bridge communication
- **ModernWpfUI**: Modern UI components and theming
- **Serilog**: Structured logging with multiple sinks
- **System.Text.Json**: High-performance JSON serialization
- **HttpClient**: Modern HTTP client for API communication
- **xUnit**: Unit testing framework
- **Moq**: Mocking framework for testing
- **FluentAssertions**: Fluent assertion library

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

## 📋 Project Status

### ✅ Phase 1: Foundation (Completed)
- [x] Project setup and architecture
- [x] Basic WPF UI with dark theme
- [x] Device discovery and connection testing
- [x] Basic audio capture implementation

### ✅ Phase 2: Core Services (Completed)
- [x] Audio analysis pipeline
- [x] Device control implementations
- [x] Configuration management
- [x] Basic preset system

### ✅ Phase 3: Audio Integration (Completed)
- [x] Spotify Web API integration
- [x] Advanced audio analysis
- [x] Real-time effect processing
- [x] Performance optimization

### ✅ Phase 4: Advanced Features (Completed)
- [x] Complete preset system
- [x] Advanced UI features
- [x] Error handling and recovery
- [x] Performance tuning

### ✅ Phase 5: Polish & Testing (Completed)
- [x] UI/UX refinements
- [x] Comprehensive testing
- [x] Documentation
- [x] Deployment preparation

## 🎯 Current Status: **PRODUCTION READY**
The PartyLights application is now feature-complete with enterprise-grade capabilities including comprehensive testing, automated documentation, and production-ready deployment automation.

## 🎛️ Usage

### Basic Operation
1. **Launch the application**
2. **Discover devices** by clicking "Discover Devices"
3. **Connect to devices** by clicking the connect button on each device card
4. **Connect to Spotify** (optional) by clicking "Connect Spotify" and authorizing
5. **Select audio source** (System Audio or Spotify)
6. **Choose a lighting effect** from the comprehensive preset library
7. **Start synchronization** by clicking "Start Sync"
8. **Enjoy** your music with synchronized lighting!

### Advanced Settings
- **Audio Sensitivity**: Adjust beat detection sensitivity (0.1 - 2.0)
- **Processing Latency**: Set audio processing latency in milliseconds
- **Brightness Range**: Configure minimum and maximum brightness levels
- **Color Intensity**: Adjust color saturation and intensity
- **Performance Mode**: Choose between Quality, Balanced, or Performance modes
- **Effect Parameters**: Fine-tune individual effect parameters
- **Device Grouping**: Create logical groups for synchronized effects
- **Preset Management**: Create, edit, and share custom presets
- **Spotify Settings**: Configure Spotify integration and playback control
- **Theme Customization**: Customize UI themes and color schemes
- **Accessibility**: Configure accessibility features and screen reader support

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
- **NAudio**: Advanced audio capture, processing, and FFT analysis
- **Q42.HueApi**: Philips Hue bridge communication and control
- **ModernWpfUI**: Modern UI components and theming system
- **Serilog**: Structured logging with multiple output sinks
- **CommunityToolkit.Mvvm**: MVVM framework with source generators
- **System.Text.Json**: High-performance JSON serialization
- **HttpClient**: Modern HTTP client for API communication
- **xUnit**: Unit testing framework
- **Moq**: Mocking framework for unit testing
- **FluentAssertions**: Fluent assertion library for readable tests

## 📊 Advanced Audio Analysis Pipeline

The application features a sophisticated audio analysis pipeline with multiple specialized analyzers and advanced features:

### Core Analyzers
- **FrequencyAnalyzer**: FFT-based frequency domain analysis with Blackman-Harris windowing
- **BeatAnalyzer**: Advanced beat detection with volume history and confidence scoring
- **SpectralAnalyzer**: Comprehensive spectral features (bandwidth, contrast, flatness, flux)
- **RhythmAnalyzer**: Rhythm regularity and complexity analysis with tempo estimation
- **MoodAnalyzer**: AI-powered mood classification based on energy, valence, and arousal
- **HarmonicAnalyzer**: Fundamental frequency detection and harmonic content analysis
- **QualityAnalyzer**: Audio quality metrics including SNR, distortion, and clipping detection

### Advanced Features Extracted
- **Volume Analysis**: RMS, peak levels, and dynamic range
- **Frequency Analysis**: 12-band spectrum with detailed band analysis
- **Spectral Features**: Centroid, rolloff, bandwidth, contrast, flatness, flux
- **Beat Analysis**: Beat detection, intensity, confidence, and tempo estimation
- **Mood Classification**: Emotional analysis with confidence scoring
- **Genre Recognition**: Machine learning-based music genre detection
- **MFCC Analysis**: Mel-frequency cepstral coefficients for audio fingerprinting
- **Harmonic Analysis**: Fundamental frequency and harmonic content
- **Quality Metrics**: Signal-to-noise ratio, distortion, and clipping analysis
- **Temporal Features**: Attack, decay, sustain, and release characteristics

## 🎨 Advanced Lighting Effects

### Core Effects
- **Beat Sync**: Volume-based brightness control with beat-triggered flash effects
- **Frequency Visualization**: Advanced RGB mapping from frequency bands with smooth transitions
- **Mood Lighting**: AI-powered color selection based on detected mood and emotional analysis
- **Spectrum Analyzer**: Real-time dominant frequency band visualization with HSV color mapping
- **Party Mode**: Dynamic color cycling with beat-driven effects and high-energy patterns
- **Volume Reactive**: Adaptive brightness changes based on audio volume with smoothing

### Advanced Effects
- **Custom Patterns**: User-defined lighting patterns with parameter customization
- **Wave Effects**: Flowing wave patterns synchronized to audio rhythm
- **Strobe Effects**: Beat-synchronized strobe lighting with intensity control
- **Theater Mode**: Cinematic lighting effects for ambient entertainment
- **Rainbow Mode**: Continuous color cycling with customizable speed and intensity
- **Fire Effects**: Dynamic fire-like patterns with audio-reactive intensity
- **Pulse Effects**: Rhythmic pulsing synchronized to beat detection
- **Gradient Effects**: Smooth color gradients with audio-reactive transitions

## 🔌 Advanced Device Protocols

### Philips Hue
- **Discovery**: HTTP-based bridge discovery with automatic network scanning
- **Authentication**: OAuth 2.0 with bridge button press + API key generation
- **Control**: REST API with JSON commands and real-time updates
- **Features**: Full color, brightness, temperature, effects, scenes, and groups
- **Advanced**: Scene management, group control, and performance monitoring

### TP-Link Kasa
- **Discovery**: UDP broadcast on port 9999 with encrypted responses
- **Protocol**: Encrypted JSON over TCP with device-specific encryption
- **Control**: Device-specific commands with error handling and retry logic
- **Features**: Color, brightness, temperature, device grouping, and scheduling
- **Advanced**: Multi-device synchronization and performance optimization

### Magic Home
- **Discovery**: UDP broadcast on port 48899 with device enumeration
- **Protocol**: Binary commands over TCP port 5577 with checksum validation
- **Control**: Byte-array commands with built-in effect support
- **Features**: Color, brightness, 18 built-in effects, and custom patterns
- **Advanced**: Effect synchronization and real-time control

## 🐛 Troubleshooting

### Common Issues

**Audio not detected**
- Ensure Windows audio is playing
- Check audio device permissions
- Verify WASAPI loopback capture is available
- Test with different audio sources
- Check audio driver compatibility

**Devices not discovered**
- Ensure devices are on the same network
- Check firewall settings
- Verify device power and connectivity
- Try manual IP address configuration
- Restart network services

**Connection failures**
- Check IP addresses are correct
- Verify network connectivity
- Ensure device APIs are accessible
- Check device authentication status
- Verify device firmware compatibility

**Spotify integration issues**
- Re-authenticate Spotify account
- Check internet connection
- Verify Spotify app is running
- Check API permissions
- Update Spotify application

**Performance issues**
- Adjust performance settings
- Close unnecessary applications
- Check system resources
- Update graphics drivers
- Restart the application

### Advanced Troubleshooting

**Log Analysis**
- Enable debug logging in settings
- Check log files for detailed error information
- Use built-in diagnostic tools
- Monitor performance metrics

**Network Diagnostics**
- Test device connectivity with ping
- Check port accessibility
- Verify multicast support
- Test with different network configurations

**System Requirements**
- Ensure Windows 10/11 compatibility
- Check .NET 8.0 runtime installation
- Verify audio device compatibility
- Monitor system resource usage

### Logging
The application uses Serilog for comprehensive structured logging. Logs are written to:
- Console output with color coding
- File logs with rotation and compression
- Windows Event Log integration
- Performance metrics logging
- User activity tracking

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **NAudio** - Advanced audio processing and FFT analysis
- **Q42.HueApi** - Philips Hue bridge communication and control
- **ModernWpfUI** - Modern UI components and theming system
- **CommunityToolkit.Mvvm** - MVVM framework with source generators
- **Spotify Web API** - Music streaming and audio analysis integration
- **Serilog** - Structured logging and performance monitoring
- **System.Text.Json** - High-performance JSON serialization
- **xUnit** - Comprehensive testing framework

## 📞 Support

For support, please open an issue on GitHub or contact the development team.

---

**PartyLights** - Bringing your music to life through intelligent lighting! 🎵💡