# 🎮 XGP‑Save‑Tools

A .NET port of [XGP-save-extractor](https://github.com/Z1ni/XGP-save-extractor) rebuilt with new features, allows you to **extract** or **replace** Xbox Game Pass (PC) save files, enabling easy transfer of game saves between Xbox Game Pass and Steam/Epic versions (on supported games).

This version includes:
- Generic handler (allows extraction of saves from ANY game as long as it does not require any extra encryption)
- Replace mode (helps you to transfer saves from steam/epic into Xbox Game Pass)
- Custom directories 
- Better menu navigation
---

## 🛠️ Requirements

- **.NET 6 runtime** 
- Windows 10/11 (UWP package layout located at `%LOCALAPPDATA%\Packages`)
---

## 📝 Extensibility

Most games will work using the generic handler.
> **Obs**: If a game is not registered on the `games.json`, it's extracted files names will have no extension suffixes.

Some games require unique handlers, those have to be implemented.

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
- **`handler`**: Built‑in save format handlers (`generic`,`1c1f`, `1cnf`, `starfield`, etc.)
- **`handler_args`**: Handler-specific configurations such as file extension (`{ "suffix": ".sav" }`)

If a game requires a new format handler, you must implement the `ISaveHandler` interface.

---

## 🚀 How to Use

### 📤 Extract Saves

1. Select your game from the available list, or enter a path to your `wgs` directory.
2. Select your user container ID.
3. Choose **Extract Files**.
4. The tool will display each `OutputName` and generate a ZIP file in the root directory.

![Extracting Saves](https://github.com/user-attachments/assets/e8806a1a-5002-45e1-b4cc-ddcc321689bd)


### 🔄 Replace a Save Entry

1. Select **Replace Entry**.
2. Choose the save slot to overwrite.
3. Provide the file path to your new save file.
4. Keep replacing slots until you're done, then select **Finish**
4. The tool automatically **backs up** the container before overwriting every selected file.

![Replacing Saves](https://github.com/user-attachments/assets/73054752-6f65-4f54-a0eb-f3f18e8c0472)


> **Caution**: Not all listed entries are save slots, some files contain crucial general information and can break the game if replaced.

---

## ⚙️ Build & Installation

You can grab the latest pre-built executable release at https://github.com/brodrigz/XgpSaveTools/releases/latest

### 🔨 Build from Source

If you prefer to build yourself, clone the repo and publish with .NET 6:

```bash
dotnet publish Xgpst_ConsoleApp/Xgpst_ConsoleApp.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  /p:PublishSingleFile=true \
  --output bin/Release/net6.0/publish/win-x64
```
---

## 🙌 Acknowledgments & Contributions

- Port inspired by [Z1ni’s Python XGP-save-extractor](https://github.com/Z1ni/XGP-save-extractor).
- [@snoozbuster](https://github.com/snoozbuster) for reverse engineering container format at https://github.com/goatfungus/NMSSaveEditor/issues/306.
- Contributions and pull requests are very welcome. Please submit issues or pull requests with your game’s package name, handler type, and relevant samples.

---

