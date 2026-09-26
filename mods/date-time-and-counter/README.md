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

Open **Mods → Date Time and Counter → Settings** in-game to configure it. The day counter and clock are on by default; the default time format is 24-hour with 5-minute intervals.

## Settings overview

When **Attach to location label** is enabled, the display follows the location label and cannot be moved independently. Turn it off to use a fixed HUD position. Enable **Reposition widget**, then drag the panel with the left mouse button. Press **Esc** or right-click to finish; the game returns to the Mods settings and saves the position.

The detached background can be switched off with **Show background**. The day and clock options are independent, so the widget can show only the date, only the time, or both.

## Adding a locale

Locale files live in the plugin's `Locales` folder. Each file contains the HUD text (`day` and `moveHint`) plus the mod name, description, and settings labels/descriptions shown by GK2 Mod Framework. The build also copies these catalogs to `GK2.Framework/Localization/com.actepukc.gk2.datetimeandcounter/`; when packaging a release, preserve that path under `BepInEx/plugins/` so the Framework can load the settings translations. The Framework localization API is provided by GK2 Mod Framework 0.1.12 or newer.

Add a JSON file named with the locale key, for example `xx.json`:

```json
{"day":"Day {0}","moveHint":"Esc: finish moving","mod.name":"Date Time and Counter","settings.show_day_counter.name":"Show day counter"}
```

`day` is formatted with the current day number. `moveHint` is shown while repositioning. Framework setting translations use keys such as `settings.show_day_counter.name` and `settings.show_day_counter.description`; English is used when a translation is missing. The Framework reads the game language and checks its exact locale file, then a neutral language (for example `pt_br` → `pt`), then English. Use normalized filenames (`pt_br`, `uk_ua`, `vi`, `zh_cn`, `zh_tw`) in the Framework localization folder. If a game language does not match the HUD locale filename directly, add an alias to `Locales/language-map.json`:

```json
{"aliases":[{"language":"game-language-id","locale":"xx"}]}
```

The locale name in the alias must match the JSON filename without `.json`. Adding translations for existing settings does not require a DLL change; adding a new setting requires a code change for its localization keys and English fallback.

## Building from source

The project targets .NET Standard 2.1 and references the game's managed assemblies, BepInEx, and GK2 Mod Framework from the local game installation. With those dependencies installed, build with:

```powershell
dotnet build GK2.DayCounter.csproj -c Release
```

The build output is written to `bin/Release/netstandard2.1/` and includes the HUD `Locales` directory and the Framework localization tree. Preserve both when assembling the release archive.

## Support

When reporting a problem, include the game version, BepInEx and GK2 Mod Framework versions, a screenshot, and the relevant part of `BepInEx/LogOutput.log`.
