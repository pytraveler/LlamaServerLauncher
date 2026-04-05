# LlamaCppLauncher

A Windows desktop application for launching and managing [llama.cpp](https://github.com/ggerganov/llama.cpp) server instances with an intuitive graphical interface.

## Features

### Server Configuration
- **Executable Path** - Select the `llama-server.exe` binary
- **Model Selection** - Choose a specific model file (.gguf) or set a models directory
- **Network Settings** - Configure host address (default: 127.0.0.1) and port (default: 8080)

### Model Parameters
- Context size (`-c`)
- Number of threads (`-t`)
- GPU layers (`-ngl`)
- Temperature
- Max tokens (`-n`)
- Batch size (`-b`)
- Top-K sampling (`--top-k`)
- Top-P sampling (`--top-p`)
- Repeat penalty (`--repeat-penalty`)

### Advanced Options
- Flash Attention toggle (`-fa`)
- WebUI enable/disable (`--webui`)
- Embedding mode (`--embedding`)
- Slots management (`--slots`)
- Metrics endpoint (`--metrics`)
- API key authentication (`--api-key`)
- Custom command-line arguments

### Logging & Monitoring
- Configurable log file output
- Verbose logging mode (`-v`)
- Real-time log viewer in the application
- Server status display with process ID

### Profile Management
- Save multiple configuration profiles locally
- Load saved profiles instantly
- Delete unwanted profiles
- Export profiles to JSON format
- Import profiles from JSON files

## Requirements

- Windows OS
- .NET 8.0 Runtime (included in self-contained build)
- [llama.cpp](https://github.com/ggerganov/llama.cpp/releases) server binary (`llama-server.exe`)

## Installation

1. Download the latest release from the releases page
2. Extract the archive to your desired location
3. Run `LlamaServerLauncher.exe`

Alternatively, build from source:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Usage

1. Click "Browse" next to **Executable** and select your `llama-server.exe`
2. Click "Browse" next to **Model** and select your model file (.gguf)
3. Configure additional parameters as needed
4. Click **Start Server** to launch llama-server
5. Monitor logs in the **Log Output** section

### Managing Profiles

To save current settings as a profile:
1. Enter a name in the profile dropdown/input field
2. Click **Save Profile**

To load a saved profile:
1. Select the profile from the dropdown
2. Click **Load Profile**

To export/import configurations:
- Use **Export Profile** to save as JSON
- Use **Import Profile** to load from JSON

## Architecture

- **Framework**: WPF (.NET 8.0)
- **Pattern**: MVVM (Model-View-ViewModel)
- **Build**: Self-contained single-file executable

### Project Structure
```
LlamaServerLauncher/
├── Models/           # Data models and command-line building
├── ViewModels/       # MVVM view models
├── Services/         # Business logic services
├── Converters/       # XAML value converters
└── App.xaml          # Application entry point
```

## License

MIT License - See LICENSE file for details.