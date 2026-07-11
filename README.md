# 🎮 XGP‑Save‑Tools 🎮

A .NET CLI for extracting and replacing Xbox Game Pass PC saves, based on [XGP-save-extractor](https://github.com/Z1ni/XGP-save-extractor). It understands the WGS container format and supports game-specific conversion where storefront save formats differ.

- Generic WGS extraction and direct-entry replacement
- Game-specific export/import operations
- Automatic backups and rollback for multi-file replacements
- Custom WGS directories
---

## Requirements

- **.NET 6 runtime** 
- Windows 10/11 (UWP package layout located at `%LOCALAPPDATA%\Packages`)
---

## How to Use

### Extract Saves

1. Select your game from the available list, or enter a path to your `wgs` directory.
2. Select your user container ID.
3. Choose **Extract Files**.
4. The tool will display each `OutputName` and generate a ZIP file in the root directory.

![Extracting Saves](https://github.com/user-attachments/assets/e8806a1a-5002-45e1-b4cc-ddcc321689bd)


### Replace a Save Entry

1. Select **Replace Entry**.
2. Choose the directly mapped WGS entry to overwrite.
3. Provide the file path to your new save file.
4. Review and confirm the generated import plan.
5. The tool automatically creates a complete WGS backup before committing the replacement.

![Replacing Saves](https://github.com/user-attachments/assets/73054752-6f65-4f54-a0eb-f3f18e8c0472)


> **Caution**: Not all listed entries are save slots, some files contain crucial general information and can break the game if replaced.

### Build & Installation

You can grab the latest pre-built executable release at https://github.com/brodrigz/XgpSaveTools/releases/latest

### Build from Source

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

## Implementing new handlers

Register every game in [`games.json`](XgpSaveTools/games.json) using its `%LOCALAPPDATA%\Packages` folder name. Unregistered games fall back to `generic`. Start with a built-in mapping handler when the storefronts use the same file format:

| Handler | Default behavior |
|---|---|
| `generic` | Exposes every WGS entry using its stored filename |
| `1c1f` | Exposes the first file in each container, named after the container |
| `1cnf` | Exposes every file from the first container |
| `1cnf-folder` | Exposes every container as a folder and its entries as files |

```json
{
  "name": "Example Game",
  "package": "Publisher.ExampleGame_abc123",
  "handler": "1c1f",
  "handler_args": { "suffix": ".sav" }
}
```

These handlers automatically receive **Extract Files**, **Replace Entry**, and **Delete Entry** operations. For different naming only, implement `ISaveHandler`, add it to `SaveHandlerFactory`, and return direct `ContainerEntry` mappings. Mark a transformed legacy handler with `IExportOnlySaveHandler` so temporary export files cannot be offered as replacement targets.

### Custom operations

Use `IGameSaveHandler` when a game needs encryption, runtime input, validation, or an atomic multi-file import. A handler advertises only the operations it safely supports:

```csharp
public sealed class ExampleHandler : IGameSaveHandler
{
    public const string HandlerId = "example";
    public string Id => HandlerId;

    public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context) =>
        new IGameSaveOperation[] { new ExportOperation() };

    private sealed class ExportOperation : IGameSaveOperation
    {
        public OperationDefinition Definition { get; } = new(
            "export", "Export", "Convert this save.", OperationKind.Export);

        public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
            new OperationParameter[]
            {
                new TextParameter("account-id", "Target account ID"),
                new ChoiceParameter("slot", "Source slot",
                    context.Containers.Select(x => new ChoiceOption(x.Name, x.Name)).ToList())
            };

        public Task<OperationPlan> PrepareAsync(
            GameSaveContext context, OperationArguments arguments,
            ITempWorkspace workspace, CancellationToken cancellationToken)
        {
            arguments.Validate(GetParameters(context));
            var slot = context.Containers.Single(x =>
                x.Name == arguments.GetRequiredString("slot"));
            var output = workspace.GetPath("save.dat");

            var source = slot.Files.Single(x => x.Name == "save.dat");
            File.Copy(source.Path, output); // Transform here if needed.

            return Task.FromResult<OperationPlan>(new ExportPlan(
                new[] { new ExportArtifact("save.dat", output) }, "example.zip"));
        }
    }
}
```

`GetParameters` defines console prompts without calling `Console`: use `TextParameter`, `FileParameter`, `DirectoryParameter`, `ChoiceParameter`, or `BooleanParameter`. Read validated values from `OperationArguments`.

Return an `ExportPlan` for ZIP output. For replacement, prepare every file in `ITempWorkspace` and return one `ImportPlan` containing `PlannedReplacement` or `PlannedDeletion` entries. The central executor validates targets, creates a WGS backup, commits the complete plan, and rolls back failures.

Finally, set `"handler": "example"` in `games.json` and add `if (game.Handler == ExampleHandler.HandlerId) return new ExampleHandler();` to `GameSaveHandlerFactory.Get`. See [`DoomDarkAgesHandler.cs`](XgpSaveTools/SaveHandlers/Impl/DoomDarkAges/DoomDarkAgesHandler.cs) for a bidirectional example and [`XgpSaveTools/Operations`](XgpSaveTools/Operations) for the relevant contracts.

---

## 🙌 Acknowledgments & Contributions

- Port inspired by [Z1ni’s Python XGP-save-extractor](https://github.com/Z1ni/XGP-save-extractor).
- [@snoozbuster](https://github.com/snoozbuster) for reverse engineering container format at https://github.com/goatfungus/NMSSaveEditor/issues/306.
- [@mi5hmash](https://github.com/mi5hmash/idSaveDataResigner) for documenting the idTech 7/8 Steam save encryption scheme used by DOOM.
- [id Software's DOOM 3 BFG source release](https://github.com/id-Software/DOOM-3-BFG/blob/master/neo/idlib/hashing/MD5.cpp) for the reference `MD5_BlockChecksum` implementation.
- Contributions and pull requests are very welcome. Please submit issues or pull requests with your game’s package name, handler type, and relevant samples.

---

