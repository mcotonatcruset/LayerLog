# LayerLog

A Windows desktop viewer for your 3D-printing models, with a history that remembers exactly how each figure was printed.

Tuning a slicer until a figure comes out right takes a lot of trial and error, and that knowledge gets lost between changing profiles and loose gcode files on SD cards. LayerLog reads the gcode already on your cards and keeps, for each one, the settings used and what you changed from the base profile.

## Features

- **3D viewer:** browse a model folder and inspect STL files in a full-window scene. The folder and the last figure are remembered.
- **Automatic history:** plug in an SD card and new gcode is imported on its own. Cards are told apart by volume serial, and the same file on two cards counts once. Nothing on the card is ever modified.
- **What you changed:** process and filament settings are compared with the base Creality Print profile, so only your changes are shown.
- **Prints:** one gcode can have many prints, each with a rating, a note and a photo.
- **Compare:** put two gcode side by side and see only the differences.
- **Send to Creality Print:** open one figure or a group in the slicer.

3MF files can be listed and sent to Creality Print, but not previewed yet.

## Run it

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Importing needs gcode from Creality Print 7.x.

```bash
dotnet run --project src/LayerLog.App
```

```bash
dotnet test
```

Data lives in `%LOCALAPPDATA%\LayerLog`. The first run opens `Documents\Impresora3D` if it exists, and asks for a folder otherwise.

## How it is built

| Project | Responsibility |
|---|---|
| `src/LayerLog.Core` | Everything testable, with no UI: gcode and STL parsing, profile inheritance, settings diffs, the SQLite repository and the importer. |
| `src/LayerLog.App` | WPF and MVVM (CommunityToolkit.Mvvm), HelixToolkit.Wpf.SharpDX for 3D, and SD card detection. |
| `tests/LayerLog.Core.Tests` | xUnit tests over fixtures trimmed from real gcode. |

A few decisions worth a look:

- **Config comes from the gcode itself.** Creality Print writes about 600 settings at the end of each file, so history needs no integration with the slicer.
- **Each entry stores its own comparison.** Profiles change over time and between versions, so results are frozen at import. If the exact profile version is not on disk, the nearest one is used and the entry says so.
- **Interrupted imports are safe.** Card locations are replaced only after a full pass, so pulling a card out mid-import loses nothing.
- **Large models stay smooth.** A 1 million triangle STL renders at 240 FPS on an RTX 5060 Ti. Files are parsed off the UI thread.

`docs/superpowers` holds the design document and the implementation plan.
