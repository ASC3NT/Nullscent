# Nullscent

Personal osu!mania client built from scratch in C# with MonoGame. Inspired by McOsu and osu!lazer. It reads your local osu!mania beatmaps, lets you pick a song, and plays it. That's the scope — no online, no editor, no other game modes.

Still early in development but fully playable. The core gameplay loop is solid.

---

## What works

- Drag and drop `.osz` files directly into the window to import songs
- Drag and drop `.osk` skin files to apply a skin
- Song select screen
- Full mania gameplay — scrolling notes, long notes, scroll velocity support
- Judgment system with hit error bar
- Score, accuracy, combo tracking
- Pause and fail states
- Results screen after a map ends
- Mod selection: AT, EZ, NF, HT, HR, DT, HD, FL, RD, MR, 1K/4K/7K
- **Partial skin support** — `skin.ini` parsing and custom assets are working, but there are still quite a few visual bugs and unfinished parts.
- Per-column key bindings, configurable
- Settings overlay (toggled in-game)
- FPS counter toggle
- Screenshot capture
- 1000Hz fixed timestep for input precision

## What's still in progress

- Some mod effects are defined but not fully implemented (HD fade, FL masking, etc.)
- Skin system is functional but not complete — fallback rendering is still common
- Settings are being refactored
- Anything beyond local singleplayer is out of scope for now

---

## Stack

- C# / .NET 8 / MonoGame 3.8.4 (WindowsDX)
- Audio: BASS + BASSmix (`bass.dll` / `bassmix.dll` bundled)
- Font rendering: FontStashSharp + bundled `.otf`
- Windows only

## Running it

You need Visual Studio 2022+ and the .NET 8 SDK. Clone the repo, open `Nullscent.slnx`, build and run.

Drop a `Songs` folder with your `.osz` files (or drag them into the window) and they'll be picked up automatically.

> Work in progress.
