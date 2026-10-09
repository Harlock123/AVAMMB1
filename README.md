<p align="center"><img src="docs/logo.png" alt="AVAM&M logo" width="560"></p>

# AVAM&M

**Book One - Secrets of the Inner Vault.** A first-person, grid-based, turn-based party RPG in the
spirit of the classic 1986 computer role-playing games, rebuilt from scratch with **Avalonia UI** and
**.NET 10 (C#)**. Gather six adventurers, map the dark cellars under Brindlemoor, brave the Hollow
Crypt and unseal the Inner Vault.

> **Disclaimer:** AVAM&M is an unofficial, non-commercial, clean-room fan re-implementation inspired only
> by the *mechanics* of 1980s CRPGs. It is not affiliated with, endorsed by, or connected to any
> publisher or rights holder, and contains none of their code, data, maps, text, graphics, music or
> sounds. All trademarks belong to their owners. All game content is original; all art and audio is
> openly licensed (CC0) - see [ASSETS_LICENSES.md](ASSETS_LICENSES.md).

**[Download the latest release](https://github.com/Harlock123/AVAMMB1/releases/latest)** - Windows, Linux and macOS, x64 and ARM64.

Code identifiers, the solution and executables use the name **`AVAMMB1`** (`AVAMMB1.sln`,
`AVAMMB1.exe`, `AVAMMB1-1.0.0-linux-x64.tar.gz`, ...), because `&` is unsafe in paths.

## Features

- **Pseudo-3D first-person view** - a software ray caster with textured walls, floors and ceilings,
  sky, distance fog, darkness that needs light, billboard sprites (trees, fountains, chests, shop
  signs), thin walls with doors, locked doors and secret doors, plus solid terrain blocks.
- **Secret doors**: hidden doors look and block like walls until the party **searches** (F). The
  best searcher rolls (Intellect, Luck and a robber's thievery help); found doors appear as doors in
  the 3D view and in purple on the automap, and are remembered in save games.
- **Grid movement**: step forward/back, turn 90 degrees, strafe; keyboard *and* on-screen buttons.
  Steps and turns glide smoothly (about 0.15 s, eased) instead of snapping; pressing a key mid-move
  finishes it instantly, so fast input never lags. Can be switched off in Settings for classic movement.
- **Animated combat**: monsters breathe and bob while idle, lunge when they attack, flash and shake
  when hit and fade out when slain; party portraits flash red when hurt (Settings toggle).
- **Automap and minimap** that record only what the party has actually seen.
- **Party of up to six** with full character creation: 5 races, 6 classes (Knight, Paladin, Archer,
  Cleric, Sorcerer, Robber), 7 attributes rolled 3d6, sex, alignment, class requirements and
  class-specific paper-doll portraits. Quick Party option with six premade heroes.
- **Original-style rules**: HP/SP, armor class, to-hit and damage bonuses, multiple attacks,
  experience levels with gold-paid training, food consumed when resting, conditions (asleep, blinded,
  silenced, poisoned, diseased, paralyzed, unconscious, dead, stoned), trap disarming, saving throws.
- **Towns** with inns (rest, save, roster of up to 18 characters), temples (heal, cure, raise dead,
  donate), taverns (food, rumors), smithies and magic shops, and training grounds.
- **Six maps**: two towns, a wilderness, two dungeons and the final vault, linked by a quest chain
  with keys, quest items and flags that leads to a final boss and victory screen.
- **Turn-based combat**: initiative order, front/back ranks for both sides, melee and missile attacks,
  28 learnable spells across two schools plus item-only effects, potions, scrolls, wands, blocking,
  running, bribing; monster AI with special abilities, healing, fleeing and target selection;
  treasure, item drops and XP.
- **Inventory and equipment**: 9 equipment slots, 12-slot backpacks, class restrictions,
  two-handed weapons, give/drop/use, charges.
- **Data-driven content**: monsters, items, spells, races, classes, shops and maps are JSON files you
  can edit (see [docs/CONTENT_FORMAT.md](docs/CONTENT_FORMAT.md)); put a `Content` folder next to the
  executable or pass `--content DIR` to mod the game without rebuilding.
- **Save/load** (10 slots incl. quick-save, JSON) in the OS user-data directory; **settings** for
  music/effects volume, fullscreen, minimap and **rebindable keys**.
- **Music and sound effects** via OpenAL Soft (Silk.NET) + NVorbis, bundled for all six platforms;
  the game silently falls back to no audio if no device is available.
- **Single-file, self-contained executables** for Windows, Linux and macOS (x64 and ARM64).

## Screenshots

All screenshots are real frames rendered by the game itself (Avalonia headless + Skia) with
`AVAMMB1 --screenshot docs/screenshots`, driving the party through normal game commands.

| | |
|---|---|
| ![Title screen](docs/screenshots/01-title.png) Title screen | ![Character creation](docs/screenshots/02-character-creation.png) Character creation |
| ![Town](docs/screenshots/04-town.png) Brindlemoor (town) | ![Shop](docs/screenshots/05-shop.png) Smithy |
| ![Dungeon](docs/screenshots/08-dungeon.png) Dungeon exploration (lit by a spell) | ![Combat](docs/screenshots/09-combat.png) Combat |
| ![Inventory](docs/screenshots/10-inventory.png) Character sheet / inventory | ![Automap](docs/screenshots/11-automap-dungeon.png) Dungeon automap |
| ![Town automap](docs/screenshots/06-automap-town.png) Town automap | ![Spells](docs/screenshots/12-spells.png) Spellcasting |
| ![Intro](docs/screenshots/03-intro.png) Story dialog | ![Settings](docs/screenshots/13-settings.png) Settings and key bindings |
| ![Dungeon entrance](docs/screenshots/07-dungeon-entrance.png) Arriving in the cellars | ![Secret door](docs/screenshots/14-secret-door.png) A secret door found by searching (purple on the minimap) |

## Download and install

Download the archive for your platform from the **[latest release](https://github.com/Harlock123/AVAMMB1/releases/latest)**
(all releases: [https://github.com/Harlock123/AVAMMB1/releases](https://github.com/Harlock123/AVAMMB1/releases)), or build it yourself (below). Direct links for v1.1.0:

| Platform | Download | How to run |
|---|---|---|
| Windows x64 | [AVAMMB1-1.1.0-win-x64.zip](https://github.com/Harlock123/AVAMMB1/releases/download/v1.1.0/AVAMMB1-1.1.0-win-x64.zip) | Unzip, run `AVAMMB1.exe`. SmartScreen may warn about an unsigned app: *More info -> Run anyway*. |
| Windows ARM64 | [AVAMMB1-1.1.0-win-arm64.zip](https://github.com/Harlock123/AVAMMB1/releases/download/v1.1.0/AVAMMB1-1.1.0-win-arm64.zip) | Same as above. |
| Linux x64 | [AVAMMB1-1.1.0-linux-x64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.1.0/AVAMMB1-1.1.0-linux-x64.tar.gz) | `tar xzf AVAMMB1-*.tar.gz && ./AVAMMB1`. Needs an X11 session (XWayland works) and the usual desktop libraries (fontconfig, libX11/libICE/libSM). |
| Linux ARM64 | [AVAMMB1-1.1.0-linux-arm64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.1.0/AVAMMB1-1.1.0-linux-arm64.tar.gz) | Same as above. |
| macOS Apple Silicon | [AVAMMB1-1.1.0-osx-arm64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.1.0/AVAMMB1-1.1.0-osx-arm64.tar.gz) | Extract, then open `AVAMMB1.app`. The app is **not notarized**: right-click -> *Open* the first time, or run `xattr -dr com.apple.quarantine AVAMMB1.app`. |
| macOS Intel | [AVAMMB1-1.1.0-osx-x64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.1.0/AVAMMB1-1.1.0-osx-x64.tar.gz) | Same as above. |

**Gatekeeper / notarization:** release builds made on the macOS CI runner are ad-hoc signed
(`codesign -s -`), which Apple Silicon requires, but they are not signed with a Developer ID or
notarized (that needs a paid Apple developer account). To notarize your own build:
`codesign --deep --force --options runtime --sign "Developer ID Application: ..." AVAMMB1.app`, zip
it, `xcrun notarytool submit ... --wait`, then `xcrun stapler staple AVAMMB1.app`.

Save games and settings live in:

| OS | Location |
|---|---|
| Windows | `%APPDATA%\AVAMMB1` |
| macOS | `~/Library/Application Support/AVAMMB1` |
| Linux | `$XDG_DATA_HOME/AVAMMB1` (default `~/.local/share/AVAMMB1`) |

Set `AVAMMB1_DATA_DIR` to use a different folder.

## Controls

| Action | Keys (default, rebindable) | Mouse |
|---|---|---|
| Step forward / back | Up / W, Down / S | arrow buttons |
| Turn left / right | Left / A, Right / D | curved-arrow buttons |
| Strafe left / right | Q / E | side-arrow buttons |
| Use location (re-enter shop, read sign) | Space / Enter | *Use* |
| Search for secret doors | F | *Search* |
| Rest (8 hours, eats food) | R | *Rest* |
| Cast a spell | K | *Cast* |
| Party / inventory sheet | I / C, or 1-6 for a member | click a portrait |
| Automap | M | *Map* |
| Quick save / quick load | F5 / F9 | |
| Game menu (save, load, settings) | Esc | *Menu* |
| Fullscreen | F11 or Alt+Enter | Settings |
| **Combat**: Fight / Run / Bribe | F, R, B | buttons |
| **Combat**: Attack, Shoot, Cast, Use item, Block, Run | A, S, C, U, B, R | buttons |
| **Combat**: pick target / spell / ally | 1-9 | click the monster card |

## How to play

1. *New Game* -> roll attributes, pick race, sex, alignment, class and name, *Add Adventurer*
   (up to six), or press *Quick Party*. Then *Begin the Adventure*.
2. In Brindlemoor, buy food at the tavern, gear at the smithy, then read the notice board and visit
   Archivist Pell in the south-west.
3. The cellars under the old well house (east side) are dark: cast *Holy Light* / *Glowlight*, use a
   torch or a Scroll of Light. Put fighters in the first three slots - only they can melee.
4. Spend experience at the training grounds, rest to recover (it costs food), and save often at inns.

## Build from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download). No other tools are needed for
building; `ffmpeg`/`python3` were only used once to import the assets.

```bash
git clone https://github.com/Harlock123/AVAMMB1.git && cd AVAMMB1
dotnet build AVAMMB1.sln              # zero warnings (warnings are errors)
dotnet test AVAMMB1.sln               # unit tests
dotnet run --project src/AVAMMB1.App  # play
```

Useful command line switches: `--mute`, `--content <dir>` (load JSON content from a folder),
`--screenshot <dir>` (regenerate the screenshots headlessly), `--smoke-test` (headless self-test:
renders the UI, moves the party, initializes audio, exits 0 on success).

### Packaging all platforms

```bash
./build-all.sh                         # Linux/macOS (bash); RIDS="linux-x64 osx-arm64" to limit
pwsh ./build-all.ps1                   # Windows (PowerShell); -Rids win-x64,win-arm64
```

Each target is published with `PublishSingleFile=true`, `SelfContained=true`,
`IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true` and
`PublishReadyToRun=true` into `dist/<rid>/`, then packaged as `dist/AVAMMB1-<version>-<rid>.zip`
(Windows) or `.tar.gz` (Linux, and macOS with an `AVAMMB1.app` bundle).
**Trimming is intentionally disabled**: Avalonia, the DI container and the toolkit rely on
reflection that trimming can break; ReadyToRun is used instead for faster startup.

### Build verification status (v1.0.0)

Built on a Linux ARM64 (aarch64) machine with the .NET 10.0.400 SDK:

| RID | Produced by `build-all.sh` | Actually executed |
|---|---|---|
| linux-arm64 | yes | **yes** - extracted archive passes `--smoke-test` (UI render, movement, OpenAL init); all README screenshots were captured with the published binary; the windowed game was launched on a Wayland/XWayland desktop |
| linux-x64 | yes (cross-compiled) | no (no x86-64 environment available) |
| win-x64, win-arm64 | yes (cross-compiled, PE32+ GUI executables) | no |
| osx-x64, osx-arm64 | yes (cross-compiled, `.app` bundle; not code-signed when built on Linux) | no |

The GitHub Actions workflow builds every RID on GitHub's Windows, Ubuntu and macOS runners. For
v1.0.0 it passed and smoke-tested `win-x64`, `linux-x64` and `osx-arm64` on those runners, each
loading the bundled OpenAL library. `win-arm64`, `osx-x64` and the CI-built `linux-arm64` were built
but not executed. The published `linux-arm64` release archive was downloaded and passes
`--smoke-test` on the dev machine.

Gameplay verification: 69 unit/integration tests pass, including a scripted playthrough that walks
the party (with normal movement commands) through every map, fights all scripted battles and
reaches the victory event, and a balance check of a lightly trained starting party against the
first dungeon's boss. The UI flow title -> party creation -> town -> shop -> dungeon -> combat ->
inventory -> automap -> spells -> settings is exercised headlessly by `--screenshot`.

## Project structure

```
AVAMMB1.sln
src/
  AVAMMB1.Core/      Rules engine - no UI dependencies
    Dice/            dice expressions, random source abstraction
    Rules/           enums, Rulebook (bonuses, AC, to-hit, XP, spell power)
    Content/         JSON definitions, ContentDatabase (load + cross-validation), content sources
    World/           GameMap (thin walls + blocks), reachability
    Characters/      Character, CharacterFactory
    Items/           ItemInstance, Inventory
    Combat/          CombatEngine (initiative, ranks, monster AI), MonsterInstance
    Magic/           SpellCaster (spells and item use)
    Session/         GameState, GameSession (movement, events, encounters, rest), TownServices
    Persistence/     source-generated JSON, SaveGameService, GameSettings, UserDataPaths
  AVAMMB1.App/       Avalonia UI (MVVM with CommunityToolkit.Mvvm, DI)
    Rendering/       SceneRenderer (software ray caster), TextureCache
    Controls/        SceneView, MapView (automap), PixelImage
    ViewModels/ Views/  screens and overlays
    Services/        audio (OpenAL), launch options, headless screenshot/smoke runner
tests/AVAMMB1.Tests/ xUnit tests (dice, content, combat, progression, inventory, sessions, save/load)
Assets/              Data (JSON), Graphics, Audio, Icons
docs/                ARCHITECTURE.md, CONTENT_FORMAT.md, screenshots
build-all.sh, build-all.ps1, packaging/macos/Info.plist, .github/workflows/release.yml
```

More detail: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Roadmap

- Monsters visible in the 3D view (currently they appear in the combat window only)
- More towns and dungeons (the original scope had five towns and many more dungeon levels)
- Per-character gold (currently shared by the party), more spells and special items
- Spinners, darkness/anti-magic squares, more trap types
- Proper Developer ID signing / notarization and an installer for Windows
- Gamepad support, localization, UI scaling options

## License

- Code and original game data: **MIT** - see [LICENSE](LICENSE).
- Art and audio: **CC0 1.0** third-party assets - see [ASSETS_LICENSES.md](ASSETS_LICENSES.md).
- Third-party libraries keep their own licenses (Avalonia MIT, OpenAL Soft LGPL-2.0+, ...).

## Credits

See [CREDITS.md](CREDITS.md) (also shown in-game under *Credits*). Graphics from the Dungeon Crawl
Stone Soup tile set; music by Juhani Junkala, cynicmusic and yd; sound effects by Kenney, rubberduck
and artisticdude - all CC0. Thank you!
