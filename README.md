# Nullscent - osu!mania Practice Client

A complete osu!mania practice client built with MonoGame and BASS.NET, inspired by osu!lazer and McOsu.

## Features

✅ **Complete Beatmap Support**
- Full .osu file parser (osu!mania mode 3)
- 1K-18K key count support
- Long notes (LN) with head/body/tail rendering
- Scroll velocity (SV) changes
- Timing points and BPM changes

✅ **Precise Gameplay**
- Sample-accurate audio timing via BASS.NET
- 1000Hz input polling for minimal latency
- osu!lazer exact hit windows (OD-based)
- ScoreV2 scoring system with combo multiplier
- Health drain system (optional)

✅ **Practice Features**
- Rate changing (0.5x - 1.5x) with pitch preservation
- Retry/quick restart
- Pause menu with rate adjustment
- No online submission (offline practice only)

✅ **UI & Customization**
- Modern song select with search
- Real-time results screen
- Fully customizable keybinds (1K-18K)
- Settings screen (scroll speed, offset, volume, etc.)
- Skin system with skin.ini support

✅ **Audio**
- Master, music, and hitsound volume control
- Global audio offset calibration
- Custom hitsound support per beatmap

## Requirements

- **OS:** Windows 10/11 (64-bit)
- **.NET:** .NET 8 Runtime
- **Audio:** BASS.DLL (included via ManagedBass NuGet)

## Quick Start

1. **Install .NET 8 Runtime** (if not already installed)
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0

2. **Prepare Songs Folder**
   ```
   Nullscent/
   ├── Nullscent.exe
   ├── Songs/
   │   ├── [Song 1]/
   │   │   ├── audio.mp3
   │   │   ├── beatmap.osu
   │   │   └── ...
   │   ├── [Song 2]/
   │   └── ...
   └── settings.json
   ```

3. **Run the Game**
   - Launch `Nullscent.exe`
   - The game will scan the `Songs/` folder on startup
   - Use arrow keys to navigate, Enter to play

## Controls

### Song Select
- **↑/↓**: Navigate beatmap sets
- **←/→**: Switch difficulties
- **Enter**: Start beatmap
- **Type**: Search beatmaps
- **Backspace**: Clear search
- **Escape**: Exit

### Gameplay
- **Keys**: Configurable per key count (default 4K: D, F, J, K)
- **Escape**: Pause
- **Alt+F4**: Exit game

### Pause Menu
- **↑/↓**: Navigate options
- **←/→**: Change rate (when "Change Rate" is selected)
- **Enter**: Confirm selection
- **Escape**: Resume

### Results Screen
- **←/→**: Select Retry/Back
- **Enter**: Confirm
- **Escape**: Back to song select

## Configuration

Settings are stored in `settings.json` (auto-created on first run):

```json
{
  "SongsDirectory": "./Songs/",
  "SkinDirectory": "./Skin/",
  "ScrollSpeed": 20,
  "GlobalAudioOffset": 0.0,
  "MasterVolume": 1.0,
  "MusicVolume": 0.8,
  "HitsoundVolume": 0.6,
  "HealthDrainEnabled": false,
  "WindowWidth": 1280,
  "WindowHeight": 720,
  "Fullscreen": false
}
```

### Key Settings

- **ScrollSpeed**: 1-40 (higher = faster notes)
- **GlobalAudioOffset**: Audio sync calibration in milliseconds
  - Positive = audio early (notes appear later)
  - Negative = audio late (notes appear earlier)
- **HealthDrainEnabled**: false = practice mode (can't fail)

## Keybinds

Default keybinds match osu!mania:

| Key Count | Default Keys |
|-----------|-------------|
| 1K | Space |
| 2K | F, J |
| 3K | F, Space, J |
| 4K | D, F, J, K |
| 5K | D, F, Space, J, K |
| 6K | S, D, F, J, K, L |
| 7K | S, D, F, Space, J, K, L |
| 8K+ | Extended layout |

To customize keybinds:
1. Press F1 in song select (or create SettingsScreen)
2. Select "Edit Keybinds"
3. Choose key count with Tab
4. Press any key to bind to selected column

## Skin System

Skins are loaded from `./Skin/` directory.

### Basic Skin Structure
```
Skin/
├── skin.ini
├── mania-note-1.png
├── mania-note-2.png
├── mania-note-hold-head.png
├── mania-note-hold-body.png
├── mania-note-hold-tail.png
├── mania-key-1.png
├── mania-key-1D.png (pressed state)
└── ...
```

### skin.ini Format

```ini
[General]
Name: My Skin
Author: YourName
Version: 1.0

[Mania]
ColumnWidth: 50,50,50,50
HitPosition: 402
LightingN: 1

[Mania4K]
NoteImage0: custom-note-0.png
NoteImage1: custom-note-1.png
KeyImage0: custom-key-0.png
KeyImage0D: custom-key-0-down.png
```

If skin files are missing, the game uses fallback colored rectangles (fully playable).

## Hit Windows (OD-based)

Identical to osu!lazer:

| Judgement | Window (OD 8) |
|-----------|---------------|
| MAX (320) | ±16ms |
| 300 | ±40ms |
| 200 | ±73ms |
| 100 | ±103ms |
| 50 | ±127ms |
| MISS | >188ms |

Formula: `window = baseWindow - (3 × OD)`

## Building from Source

1. **Prerequisites**
   - Visual Studio 2022 (or Visual Studio Code + .NET SDK)
   - .NET 8 SDK

2. **Clone & Build**
   ```bash
   git clone <repository-url>
   cd Nullscent
   dotnet restore
   dotnet build
   ```

3. **Run**
   ```bash
   dotnet run --project Nullscent/Nullscent.csproj
   ```

## Project Structure

```
Nullscent/
├── Audio/           # BASS.NET audio engine
├── Beatmap/         # .osu parser and data models
├── Config/          # Settings and configuration
├── Core/            # Game state and input management
├── Gameplay/        # Gameplay logic (judging, scoring, columns)
├── Skin/            # Skin system and config parser
└── UI/              # All UI screens (song select, results, etc.)
```

## Troubleshooting

### No beatmaps showing up
- Ensure `.osu` files are in `Songs/` subdirectories
- Check that beatmaps are **osu!mania mode** (Mode: 3 in .osu file)
- Look at console output for parsing errors

### Audio not playing
- Verify audio file exists in beatmap folder
- Check `AudioFilename` in .osu matches actual file
- Ensure BASS.DLL is present (should be in output folder)

### Notes feel off-sync
- Adjust `GlobalAudioOffset` in settings.json
- Positive values if you're hitting late
- Negative values if you're hitting early
- Adjust in 5ms increments

### Performance issues
- Disable VSync in settings.json
- Lower `ScrollSpeed` if rendering is slow
- Close background applications

## Credits

- **Inspired by:** osu!lazer (ppy) & McOsu (McKay)
- **Frameworks:** MonoGame, BASS.NET
- **Beatmap Format:** osu!stable/osu!lazer

## License

This is a practice client for personal use. It does not connect to osu! servers and does not submit scores.

---

**Note:** This client is for practice purposes only. For ranked play, use the official osu! client.
