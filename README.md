# 🎮 XGP‑Save‑Tools

A .NET port of [XGP-save-extractor](https://github.com/Z1ni/XGP-save-extractor), built as a **dotnet console app and library**. It allows you to **extract** or **replace** Xbox Game Pass (PC) save files, enabling easy transfer of game saves between Xbox Game Pass and Steam/Epic versions (on supported games).

---

## 🛠️ Requirements

- **.NET 6 SDK** or later
- Windows 10/11 (UWP package layout located at `%LOCALAPPDATA%\Packages`)
- A valid `games.json` configuration file with proper handler entries (see below)

---

## ✅ Supported Games

Game support for **save extraction** aligns exactly with [Z1ni's Python XGP-save-extractor](https://github.com/Z1ni/XGP-save-extractor).

**Legend:** ✅ Confirmed working | ❔ Unconfirmed | - Not available in store

| Game | Steam | Epic |
|------|-------|------|
| Arcade Paradise | ✅ | ❔ |
| Atomic Heart | ✅ | - |
| The Callisto Protocol | ✅ | - |
| Celeste | ❔ | ❔ |
| Chained Echoes | ❔ | ❔ |
| Chorus | ✅ | ❔ |
| Control | ❔ | ✅ |
| Coral Island | ✅ | - |
| Cricket 24 | ✅ | - |
| Final Fantasy XV | ✅ | - |
| Forza Horizon 5 | ✅ | - |
| Fuga: Melodies of Steel 2 | ❔ | ❔ |
| Hades | ❔ | ❔ |
| High on Life | ✅ | ❔ |
| Hi-Fi RUSH | ✅ | ❔ |
| Hypnospace Outlaw | ✅ | ❔ |
| Just Cause 4 | ❔ | ❔ |
| Lies of P | ✅ | - |
| Manor Lords | ✅ | ❔ |
| Monster Train | ✅ | - |
| Ninja Gaiden Sigma | ✅ | - |
| Octopath Traveller | ❔ | ❔ |
| Palworld | ✅ | - |
| Persona 5 Royal | ✅ | - |
| Persona 5 Tactica | ✅ | - |
| Railway Empire 2 | ❔ | ❔ |
| Remnant 2 | ✅ | ❔ |
| Remnant: From the Ashes | ❔ | ❔ |
| Solar Ash | ✅ | ❔ |
| SpiderHeck | ✅ | ❔ |
| Starfield | ✅ | - |
| State of Decay 2 | ❔ | ❔ |
| Totally Accurate Battle Simulator | ✅ | - |
| Wo Long: Fallen Dynasty | ❔ | - |
| Yakuza 0 | ✅ | - |

---

## 🚫 Incompatible Games

The following games use different save formats incompatible with Steam/Epic versions:

| Game | Issue |
|------|-------|
| A Plague Tale: Requiem | [#139](https://github.com/Z1ni/XGP-save-extractor/issues/139) |
| ARK: Survival Ascended | [#165](https://github.com/Z1ni/XGP-save-extractor/issues/165) |
| Chivalry 2 | [#39](https://github.com/Z1ni/XGP-save-extractor/issues/39) |
| Death's Door | [#79](https://github.com/Z1ni/XGP-save-extractor/issues/79) |
| Forza Horizon 4 | [#71](https://github.com/Z1ni/XGP-save-extractor/issues/71) |
| Like a Dragon Gaiden: The Man Who Erased His Name | [#66](https://github.com/Z1ni/XGP-save-extractor/issues/66) |
| Like a Dragon: Ishin! | [#180](https://github.com/Z1ni/XGP-save-extractor/issues/180) |
| Neon White | [#185](https://github.com/Z1ni/XGP-save-extractor/issues/185) |
| Persona 3 Reload | [#114](https://github.com/Z1ni/XGP-save-extractor/issues/114) |
| Tinykin | [#28](https://github.com/Z1ni/XGP-save-extractor/issues/28) |
| Yakuza: Like a Dragon | [#72](https://github.com/Z1ni/XGP-save-extractor/issues/72) |

> **Note**: For **save slot replacement**, only the **1c1f** handler has been tested. Additional testing and feedback are more than welcome.

---

## 📝 Extensibility

Configure supported games via a strongly-typed `games.json` file:

```jsonc
{
  "games": [
    {
      "name": "Atomic Heart",
      "package": "FocusHomeInteractiveSA.579645D26CFD_4hny5m903y3g0",
      "handler": "1c1f",
      "handler_args": { "suffix": ".sav" }
    },
    {
      "name": "Starfield",
      "package": "BethesdaSoftworks.Starfield_8wekyb3d8bbwe",
      "handler": "starfield"
    }
    // Add or tweak game entries here...
  ]
}
```

- **`package`**: Folder path within `%LOCALAPPDATA%\Packages`
- **`handler`**: Built‑in save format handlers (`1c1f`, `1cnf`, `starfield`, etc.)
- **`handler_args`**: Handler-specific configurations

If a game requires a new format handler, you must implement the `ISaveHandler` interface.

---

## 🚀 How to Use

### 📤 Extract Saves

1. Select your game from the available list.
2. Select the appropriate user container ID.
3. Choose **Extract Files**.
4. The tool will display each `OutputName` and generate a ZIP file in the root directory.

![Extracting Saves](https://github.com/user-attachments/assets/a235f0ff-c637-4a68-8606-783e43648f46)

### 🔄 Replace a Save Entry

1. Select **Replace Entry**.
2. Choose the save slot to overwrite.
3. Provide the file path to your new save file.
4. The tool automatically **backs up** the container before overwriting the selected file.

![Replacing Saves](https://github.com/user-attachments/assets/2e449287-32ad-4434-be6a-75eece1b9d12)

> **Caution**: Not all listed entries are save slots—some files contain crucial general information and can break the game if replaced.

---

## ⚙ Build & Installation

Currently, no binary release packages are available. You must clone the repository and build the executable yourself.

---

## 🙌 Acknowledgments & Contributions

- Port inspired by [Z1ni’s Python XGP-save-extractor](https://github.com/Z1ni/XGP-save-extractor).
- [@snoozbuster](https://github.com/snoozbuster) for reverse engineering container format at https://github.com/goatfungus/NMSSaveEditor/issues/306.
- Contributions and pull requests are very welcome. Please submit issues or pull requests with your game’s package name, handler type, and relevant samples.

---

