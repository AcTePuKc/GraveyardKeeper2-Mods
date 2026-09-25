# Date Time and Counter

A configurable HUD day counter and optional in-game clock for **Graveyard Keeper 2**. It uses the game's own HUD font and frame styling, follows HUD visibility, and reads the in-game clock rather than the computer's real-world time.

## Features

- Show the day, in-game time, or both; the two displays can be enabled independently.
- Choose 12-hour or 24-hour time and a minute interval of 1, 5, 10, 15, 30, or 60 minutes.
- Attach the display below the location label, or detach it and choose its screen position.
- In detached mode, adjust the text size and optionally hide the HUD-style background.
- Reposition the detached display by dragging it with the left mouse button. Press **Esc** or right-click to finish and return to the Mods settings.
- Hides with the HUD and during cinematics; it is not shown in the main menu.
- Uses the active game font, including when the game language changes.
- Supports the game's available languages and falls back to English when a locale is not present.
- Does not modify save files.

## Requirements

- BepInEx 5.4.23.x for Graveyard Keeper 2.
- GK2 Mod Framework 0.1.12 or newer.

## Installation

Extract the release archive into the game's installation directory, preserving the included folder structure. The plugin should end up at:

```text
BepInEx/plugins/GK2.DayCounter/GK2.DayCounter.dll
```

Open **Mods → Date Time and Counter → Settings** in-game to configure it. The clock is off by default; the day counter is on by default.

## Settings overview

When **Attach to location label** is enabled, the display follows the location label and cannot be moved independently. Turn it off to use a fixed HUD position. Enable **Reposition widget**, then drag the panel with the left mouse button. Press **Esc** or right-click to finish; the game returns to the Mods settings and saves the position.

The detached background can be switched off with **Show background**. The day and clock options are independent, so the widget can show only the date, only the time, or both.

## Adding a locale

Locale files live in the plugin's `Locales` folder. Add a JSON file named with the locale key, for example `xx.json`:

```json
{"day":"Day {0}","moveHint":"Esc: finish moving"}
```

`day` is formatted with the current day number. `moveHint` is shown while repositioning. If a language does not match its locale filename directly, add an alias to `Locales/language-map.json`:

```json
{"aliases":[{"language":"game-language-id","locale":"xx"}]}
```

The locale name in the alias must match the JSON filename without `.json`. No DLL change is needed to add locale text or aliases.

## Building from source

The project targets .NET Standard 2.1 and references the game's managed assemblies, BepInEx, and GK2 Mod Framework from the local game installation. With those dependencies installed, build with:

```powershell
dotnet build GK2.DayCounter.csproj -c Release
```

The build output is written to `bin/Release/netstandard2.1/` and includes the `Locales` directory.

## Support

When reporting a problem, include the game version, BepInEx and GK2 Mod Framework versions, a screenshot, and the relevant part of `BepInEx/LogOutput.log`.
