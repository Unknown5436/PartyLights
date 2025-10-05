# PartyLights - Windows Audio-Synced Smart Lighting Control

## Application Overview

**PartyLights** is a Windows desktop application that synchronizes your Magic Home LED strips, Philips Hue smart bulbs, and TP-Link Kasa smart bulbs with audio from your computer or Spotify. The application features a modern, minimalist dark mode UI with preset lighting modes and real-time audio analysis.

## Technology Stack

### Core Framework

- **Language**: C# with .NET 8.0
- **UI Framework**: WPF (Windows Presentation Foundation) with Material Design principles
- **Architecture**: MVVM (Model-View-ViewModel) pattern
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection

### Key Libraries

- **Audio Processing**: NAudio (v2.2.1) for system audio capture and analysis
- **HTTP Client**: HttpClient for API communications
- **JSON Processing**: System.Text.Json
- **UI Components**: ModernWpf for dark theme components
- **Logging**: Serilog for comprehensive logging
- **Configuration**: Microsoft.Extensions.Configuration

### Device Control Libraries

- **Philips Hue**: Q42.HueApi (v4.0.0) - Official .NET Hue API wrapper
- **TP-Link Kasa**: TPLinkSmartHome (v1.0.0) - Community .NET library
- **Magic Home**: Custom implementation using UDP/TCP protocols

## Application Architecture

### Core Modules

1. **AudioCaptureService** - Handles system audio capture and analysis
2. **SpotifyService** - Manages Spotify Web API integration
3. **DeviceManagerService** - Unified device discovery and control
4. **LightingEffectEngine** - Processes audio data and generates lighting commands
5. **PresetManagerService** - Manages lighting effect presets
6. **ConfigurationService** - Handles app settings and device configurations

### Project Structure

```
PartyLights/
├── PartyLights.Core/           # Core business logic
├── PartyLights.Services/       # Service implementations
├── PartyLights.Devices/        # Device-specific implementations
├── PartyLights.UI/             # WPF user interface
├── PartyLights.Audio/         # Audio processing modules
└── PartyLights.Tests/         # Unit tests
```

## User Interface Design

### Main Window Layout (Dark Mode)

- **Header**: Application title, connection status indicators
- **Left Panel**: Device management and configuration
- **Center Panel**: Audio visualization and effect preview
- **Right Panel**: Preset selection and effect controls
- **Bottom Panel**: Audio source selection and global controls

### Key UI Components

- **Device Grid**: Visual representation of all connected devices
- **Audio Visualizer**: Real-time spectrum analyzer and waveform display
- **Preset Carousel**: Horizontal scrolling preset selection
- **Settings Panel**: Collapsible advanced configuration options
- **Status Bar**: Real-time connection status and performance metrics

## Audio Processing Modules

### System Audio Capture

- **Technology**: WASAPI Loopback Capture via NAudio
- **Analysis**: Real-time FFT for frequency analysis
- **Features**: Beat detection, volume level monitoring, frequency band separation

### Spotify Integration

- **Authentication**: OAuth 2.0 with PKCE flow
- **API Endpoints**:
  - Currently Playing Track
  - Audio Features (danceability, energy, valence)
  - Track Analysis (tempo, key, loudness)
- **Real-time Updates**: WebSocket connection for live playback data

### Audio Analysis Pipeline

1. **Capture**: 44.1kHz, 16-bit audio stream
2. **Preprocessing**: Noise reduction and normalization
3. **FFT Analysis**: 1024-point FFT for frequency analysis
4. **Feature Extraction**: Beat detection, RMS calculation, spectral centroid
5. **Mapping**: Convert audio features to lighting parameters

## Device Control Implementation

### Philips Hue Integration

- **Bridge Discovery**: Automatic network scanning
- **Authentication**: Press bridge button + API key generation
- **Device Control**:
  - Hue Bridge: Group control for synchronized effects
  - Hue Sync Box: Video sync mode integration
  - Individual bulbs: Color, brightness, and effect control
- **API Endpoints**: `/api/{username}/lights`, `/api/{username}/groups`

### TP-Link Kasa Integration

- **Device Discovery**: UDP broadcast for device detection
- **Authentication**: Token-based authentication
- **Control Protocol**: Encrypted JSON over TCP
- **Features**: Color control, brightness, scheduling, scene management

### Magic Home Integration

- **Protocol**: Custom UDP/TCP protocol implementation
- **Device Discovery**: Network scanning for Magic Home controllers
- **Control Features**: RGB color control, brightness, preset effects
- **Real-time Updates**: Direct UDP communication for low latency

## Lighting Effect Presets

### Beat Synchronization Mode

- **Effect**: Lights pulse/flash in sync with detected beats
- **Parameters**: Beat sensitivity, flash duration, color cycling
- **Implementation**: Tempo detection + rhythmic color changes

### Frequency Visualization Mode

- **Effect**: Different colors for bass (red), mid (green), treble (blue)
- **Parameters**: Frequency band thresholds, color intensity
- **Implementation**: FFT analysis + frequency band mapping

### Volume Reactive Mode

- **Effect**: Brightness changes with volume levels
- **Parameters**: Volume sensitivity, brightness range
- **Implementation**: RMS calculation + brightness scaling

### Mood Lighting Mode

- **Effect**: Colors based on Spotify audio features (energy, valence)
- **Parameters**: Mood sensitivity, color palette selection
- **Implementation**: Spotify API + color temperature mapping

### Spectrum Analyzer Mode

- **Effect**: Real-time frequency spectrum visualization
- **Parameters**: Spectrum resolution, color gradient
- **Implementation**: FFT visualization + rainbow color mapping

### Party Mode

- **Effect**: Combination of beat sync + frequency visualization
- **Parameters**: Effect intensity, transition speed
- **Implementation**: Multi-effect blending algorithm

## Implementation Phases

### Phase 1: Foundation (Weeks 1-2)

- [x] Project setup and architecture
- [x] Basic WPF UI with dark theme
- [x] Device discovery and connection testing
- [x] Basic audio capture implementation

### Phase 2: Core Services (Weeks 3-4)

- [x] Audio analysis pipeline
- [x] Device control implementations
- [x] Configuration management
- [x] Basic preset system

### Phase 3: Audio Integration (Weeks 5-6)

- [x] Spotify Web API integration
- [x] Advanced audio analysis
- [x] Real-time effect processing
- [x] Performance optimization

### Phase 4: Advanced Features (Weeks 7-8)

- [x] Complete preset system
- [x] Advanced UI features
- [x] Error handling and recovery
- [x] Performance tuning

### Phase 5: Polish & Testing (Weeks 9-10)

- [x] UI/UX refinements
- [x] Comprehensive testing
- [x] Documentation
- [x] Deployment preparation

## Configuration Management

### Device Configuration

- **IP Address Management**: Manual entry with auto-discovery option
- **Device Naming**: Custom names for easy identification
- **Group Management**: Create device groups for synchronized effects
- **Backup/Restore**: Configuration export/import functionality

### Audio Settings

- **Source Selection**: System audio vs Spotify toggle
- **Sensitivity Controls**: Adjustable thresholds for all effects
- **Latency Settings**: Balance between responsiveness and stability
- **Audio Device Selection**: Choose specific audio output device

### Effect Customization

- **Preset Editor**: Modify existing presets or create new ones
- **Color Palettes**: Predefined color schemes for different moods
- **Timing Controls**: Effect duration and transition settings
- **Device Assignment**: Assign specific effects to device groups

## Performance Considerations

### Optimization Strategies

- **Async/Await**: Non-blocking device communications
- **Threading**: Separate threads for audio processing and UI updates
- **Caching**: Device state caching to reduce API calls
- **Rate Limiting**: Respect device API rate limits

### Latency Management

- **Target Latency**: <100ms from audio to light response
- **Buffering**: Minimal audio buffering for real-time response
- **Network Optimization**: UDP for Magic Home, optimized HTTP for others
- **Effect Smoothing**: Interpolation between effect changes

## Security & Privacy

### Data Handling

- **Local Storage**: All configurations stored locally
- **API Keys**: Encrypted storage of authentication tokens
- **Network Security**: HTTPS for all API communications
- **Privacy**: No audio data transmitted to external services

### Authentication Management

- **Spotify**: OAuth 2.0 with secure token refresh
- **Hue Bridge**: Secure API key generation and storage
- **TP-Link**: Encrypted credential storage
- **Magic Home**: No authentication required (local network only)

## Deployment & Distribution

### Installation Package

- **Installer**: WiX Toolset for professional Windows installer
- **Dependencies**: .NET 8.0 Runtime included
- **Prerequisites**: Windows 10/11, network connectivity
- **Auto-Updates**: Built-in update mechanism

### User Documentation

- **Quick Start Guide**: Device setup and first-time configuration
- **User Manual**: Complete feature documentation
- **Troubleshooting**: Common issues and solutions
- **Video Tutorials**: Setup and usage demonstrations

## Future Enhancements

### Planned Features

- **Mobile Companion App**: Remote control via smartphone
- **Voice Control**: Integration with Windows Speech Recognition
- **Scene Scheduling**: Time-based lighting automation
- **Third-party Integrations**: Support for additional smart home platforms
- **Machine Learning**: AI-powered effect optimization

### Extensibility

- **Plugin System**: Support for custom effect plugins
- **API Server**: REST API for external integrations
- **Web Interface**: Browser-based control interface
- **Multi-room Support**: Advanced room-based lighting control

---

This comprehensive plan provides a solid foundation for developing your PartyLights application. The modular architecture ensures maintainability, while the preset system offers immediate usability. The balance between performance and features can be adjusted through the UI settings, and the automated setup process minimizes configuration complexity.
