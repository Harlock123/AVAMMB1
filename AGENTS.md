# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing, installing and the
command-line switches; `docs/ARCHITECTURE.md` explains the engine, renderer, audio and save formats in depth,
`docs/CONTENT_FORMAT.md` the game data, and `docs/modding/MODDING.md` mod packs. This file covers how to work on
the code and the rules that keep it working.

## What this is

AVAM&M (repository AVAMMB1), a first-person, grid-based, turn-based party RPG in the style of the 1986 computer
RPGs, in C# on .NET 10 and Avalonia 11.3. All game content is original JSON data; the art and audio are openly
licensed (see `ASSETS_LICENSES.md`). Built for Windows, macOS and Linux, x64 and ARM64, as single-file
executables, installers, disk images, AppImages and Flatpaks.

## Commands

```bash
dotnet build AVAMMB1.sln                        # needs the .NET 10 SDK (there is no global.json)
dotnet test AVAMMB1.sln                         # Core tests only; no window opens
dotnet run --project src/AVAMMB1.App            # play
dotnet run --project src/AVAMMB1.App -- --screenshot docs/screenshots   # README pictures, rendered headlessly
dotnet run --project src/AVAMMB1.App -- --smoke-test                    # headless self-test; exit code 0 on success
UPDATE_LANG_TEMPLATE=1 dotnet test AVAMMB1.sln --filter LocalizationTests   # refresh Assets/Lang/template.json
tools/make-save-fixture.sh [git-ref]            # write the save fixture for a release
./build-all.sh                                  # publish every RID into dist/ (RIDS="linux-x64" to limit)
```

Other switches: `--mute`, `--content <dir>` (load JSON from a folder), `--mods <dir>` and
`--check-content`. Setting `AVAMMB1_DATA_DIR` moves the whole data folder.

`Directory.Build.props` sets `net10.0`, nullable, `TreatWarningsAsErrors`, embedded PDBs, the game `Version`
and `AvaloniaVersion` for every project: a warning fails the build.

## Layout

- `src/AVAMMB1.Core/`: all rules and state, no UI, audio or dialogs.
  - `Content/` loads and cross-validates the JSON (`ContentDatabase`, embedded or directory sources, mod packs).
  - `Rules/Rulebook.cs` (bonuses, AC, to-hit, XP, spell power), `Dice/` (`DiceExpression`, `IRandomSource`).
  - `Session/GameSession*.cs` (movement, events, combat, quests, travel, hirelings, puzzles, new game plus)
    over `GameState`; `TownServices`, `QuestJournal`, `Chronicle`, `Tips`.
  - `Combat/CombatEngine.cs`, `Magic/SpellCaster.cs`, `Items/`, `Characters/`, `World/` (`GameMap`,
    `Depths` levels, `MapDraft`, `ViewDescriber`).
  - `Persistence/`: source-generated JSON (`GameJsonContext`), `SaveGameService`, `SaveMigrations`,
    `GameSettings` and `UserDataPaths`, `HallOfFame`.
  - `Info/`: `Loc` (translations), `ReleaseNotes` (reads `CHANGELOG.md`), `UpdateCheck`.
- `src/AVAMMB1.App/`: the Avalonia app (executable `AVAMMB1`), MVVM with CommunityToolkit.Mvvm and DI.
  - `Rendering/SceneRenderer.cs` is a CPU ray caster; `Controls/` has `SceneView`, `MapView` and the map editor.
  - `ViewModels/` and `Views/` are the screens and overlays; `ViewLocator` maps them with a `switch`.
  - `Services/`: OpenAL audio, SDL gamepads, launch options, `HeadlessRunner`, mods and the update check.
- `tests/AVAMMB1.Tests/`: xUnit 2 tests against Core, including scripted playthroughs and
  `Balance/BalanceSimulator`. `Fixtures/Saves/` holds a real save from every release.
- `Assets/`: `Data/` (game JSON and maps, embedded into Core), `Lang/`, `Graphics/`, `Audio/`, `Icons/`.
- `packaging/`: Windows Inno Setup, macOS `Info.plist`, Linux AppImage and Flatpak scripts.
- `tools/save-fixture/` and `tools/make-save-fixture.sh`: build a release's own code to write its fixture.

## Rules that keep it working

- **Core stays pure.** Nothing in `AVAMMB1.Core` may reference Avalonia, audio or OS dialogs; it is marked
  AOT-compatible, and all tests run against it.
- **Randomness goes through `IRandomSource`**, so tests can script rolls with `ScriptedRandom`. The app registers
  one `DefaultRandomSource`. Only `World/Depths` uses `new Random(seed)`, so a seed always gives the same level.
- **Content is validated.** `ContentDatabase` cross-checks every reference, and tests check every map is
  connected and every event reachable. Change content in `Assets/Data/` and follow `docs/CONTENT_FORMAT.md`.
- **Saves must keep loading.** A change to `GameState` or the save format needs a step in `SaveMigrations` and
  a new format number. Each release needs a fixture in `Fixtures/Saves/` (from `tools/make-save-fixture.sh`)
  and notes in `CHANGELOG.md` for the current `Version`; tests fail otherwise.
- **Text is translatable.** Use `Loc.T`/`Loc.F` in code and `{l:T '...'}` in XAML, then refresh
  `Assets/Lang/template.json` with the command above; `LocalizationTests` checks the language files.
- **Input.** Keys are bound by Avalonia `Key` name in `GameSettings.KeyBindings`; gamepad buttons go through
  `GamepadMapping` in Core, which is unit tested.
- **Player data.** Saves, settings, the Hall of Fame and mods live in `AVAMMB1` under `%APPDATA%` on Windows,
  `~/Library/Application Support` on macOS and `$XDG_DATA_HOME` (or `~/.local/share`) on Linux. Tests use
  their own temporary folders.
- **No trimming.** Avalonia, DI and the toolkit use reflection; publishing uses ReadyToRun instead.
- **Line endings.** `.editorconfig` asks for LF, UTF-8 and four-space indents in C# (two in JSON, XAML and
  project files), with file-scoped namespaces.

## Making a change

- Add or update tests with every rules change; `dotnet test` must pass with no warnings.
- After a balance change, check the balance simulator and boss tests still pass.
- If the change shows on screen, regenerate the screenshots with `--screenshot` and look at them.
- Update `README.md`, `CHANGELOG.md` and the docs to match.

## CI and releases

`.github/workflows/release.yml` runs on pushes to `main`, `v*` tags and pull requests. It builds with warnings
as errors and runs the tests, then publishes all six RIDs, each on its own OS runner, smoke-tests the native
builds, and builds and runs the installer, disk image, AppImage and Flatpaks. Only a `v*` tag creates a GitHub
release, with generated notes and every package attached.
