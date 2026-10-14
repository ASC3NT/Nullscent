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
- **Skinning system (WIP)** — built-in Bars/Circles skins and a partial osu!mania legacy adapter use a shared pipeline. Compatibility and visual validation are incomplete; this system is not ready for production use.
- Per-column key bindings, configurable
- Settings overlay (toggled in-game)
- FPS counter toggle
- Screenshot capture
- 1000Hz fixed timestep for input precision

## What's still in progress

- Some mod effects are defined but not fully implemented (HD fade, FL masking, etc.)
- Skin support is actively being developed. It does not provide full osu!stable parity, and some legacy features and visual cases remain unsupported or unverified.
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

## Skinning status

The skinning architecture and built-in skins are still a work in progress and are **not considered finished or production-ready**. Native Nullscent skins and osu!mania legacy skins currently share a normalized model, but legacy compatibility is a limited subset, and visual/gameplay validation is ongoing. Beatmap skins, barlines, split stages, and some historical osu!stable behavior are not implemented. See [the skinning notes](Nullscent/Skinning.md) for the current architecture, supported subset, and known limits.
