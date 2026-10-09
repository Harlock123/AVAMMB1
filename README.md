<p align="center"><img src="docs/logo.png" alt="AVAM&M logo" width="560"></p>

# AVAM&M

**Book One - Secrets of the Inner Vault.** A first-person, grid-based, turn-based party RPG in the
spirit of the classic 1986 computer role-playing games, rebuilt from scratch with **Avalonia UI** and
**.NET 10 (C#)**. Gather six adventurers, map the dark cellars under Brindlemoor, brave the Hollow
Crypt and unseal the Inner Vault - or wander west into the Ashen Hills, where a stone wyrm stirs in
the Thornwick Mines and something with four heads nests in a drowned temple.

> **Disclaimer:** AVAM&M is an unofficial, non-commercial, clean-room fan re-implementation inspired only
> by the *mechanics* of 1980s CRPGs. It is not affiliated with, endorsed by, or connected to any
> publisher or rights holder, and contains none of their code, data, maps, text, graphics, music or
> sounds. All trademarks belong to their owners. All game content is original; all art and audio is
> openly licensed (CC0) - see [ASSETS_LICENSES.md](ASSETS_LICENSES.md).

**[Download AVAM&M v1.6.0](https://github.com/Harlock123/AVAMMB1/releases/tag/v1.6.0)** - Windows, Linux and macOS, x64 and ARM64
(always-current link: [latest release](https://github.com/Harlock123/AVAMMB1/releases/latest); direct file links are in
[Download and install](#download-and-install)).

Code identifiers, the solution and executables use the name **`AVAMMB1`** (`AVAMMB1.sln`,
`AVAMMB1.exe`, `AVAMMB1-1.6.0-linux-x64.tar.gz`, ...), because `&` is unsafe in paths.

## Features

- **Pseudo-3D first-person view** - a software ray caster with textured walls, floors and ceilings,
  sky, distance fog, darkness that needs light, billboard sprites (trees, fountains, chests, shop
  signs), thin walls with doors, locked doors and secret doors, plus solid terrain blocks.
- **Dungeon hazards**: spinners that silently turn the party, squares of magical darkness where no
  light helps, anti-magic squares where neither the party nor monsters can use magic (potions still
  work), and traps that damage, gas, teleport, drop the party down a pit or ring an alarm that
  summons monsters. Robbers may disarm traps before they fire.
- **Secret doors**: hidden doors look and block like walls until the party **searches** (F). The
  best searcher rolls (Intellect, Luck and a robber's thievery help); found doors appear as doors in
  the 3D view and in purple on the automap, and are remembered in save games.
- **Grid movement**: step forward/back, turn 90 degrees, strafe; keyboard *and* on-screen buttons.
  Steps and turns glide smoothly (about 0.15 s, eased) instead of snapping; pressing a key mid-move
  finishes it instantly, so fast input never lags. Can be switched off in Settings for classic movement.
- **Monsters in the 3D view**: battles play out in the first-person viewport - the front rank
  stands large in front of the party, the back rank smaller behind - while the controls and battle
  log take over the side panel. Monsters guarding scripted encounters stand visibly in their
  squares (gently bobbing) until defeated.
- **Animated combat**: monsters breathe and bob while idle, lunge toward the party when they
  attack, blink and shake when hit and fade out when slain; party portraits flash red when hurt
  (Settings toggle).
- **Automap and minimap** that record only what the party has actually seen, with your own
  **notes** on any square.
- **Quest journal**: every quest you have heard of, with its story so far and your current goal, plus
  the signs, inscriptions and warnings you have read (Clues). The game menu opens it too.
- **Combat helpers**: *Repeat* replays everyone's last action for the round (picking a new target
  if the old one fell), *Auto* fights with weapons and healing until the battle ends or someone is
  badly hurt, and monsters you have defeated before show their level, HP, armor class, undead-ness,
  resistances and weaknesses.
- **Party of up to six** with full character creation: 5 races, 6 classes (Knight, Paladin, Archer,
  Cleric, Sorcerer, Robber), 7 attributes rolled 3d6, sex, alignment, class requirements and
  class-specific paper-doll portraits. Quick Party option with six premade heroes.
- **Original-style rules**: HP/SP, armor class, to-hit and damage bonuses, multiple attacks,
  experience levels with gold-paid training, food consumed when resting, conditions (asleep, blinded,
  silenced, poisoned, diseased, paralyzed, unconscious, dead, stoned), trap disarming, saving throws.
- **Towns** with inns (rest, save, roster of up to 18 characters), temples (heal, cure, raise dead,
  donate), taverns (food, rumors), smithies and magic shops, training grounds, and **academies**
  where gold buys permanent statistic points (each lesson dearer than the last - a late-game gold sink).
- **Twenty-three maps**: six towns (Brindlemoor, Saltreach, Thornwick, Duskmere, Port Ashkar and
  Wintermere), four wilderness regions (the Greenvale Wilds, the Ashen Hills, the Sunscar Wastes and
  the Frostmark), twelve dungeon levels plus the final vault. The main quest chain (keys, quest items, flags) leads to a final boss
  and victory screen; optional side quests reward unique gear: the Thornwick Mines' Stone Wyrm and
  the Sunken Temple's Drowned Hydra (levels 7-9), and - across the bay by ferry - the Tomb of the
  Sun Kings and its undying king (levels 10-12). Optional deeper levels each end in a boss: the Old
  Cistern below the Brindlemoor cellars (levels 2-4), the Catacombs below the Hollow Crypt (levels
  5-8, behind a locked throne room), and - sealed until the Sun King falls - the post-game Sunless
  Deep (levels 13-15). By ship north from Saltreach lies the Frostmark (levels 11-14): the town of
  Wintermere, a tundra of wargs and frost giants, and the two-level Rime Halls where the ice dragon
  Rimefang lairs. Every town also posts a **bounty** - bring back a trophy from a monster's hoard.
- **Elite monsters**: about one random encounter in twenty is led by an elite (gold name) with double
  hit points, better armor and harder blows - worth triple experience and gold, with a chance of
  extra loot.
- **Turn-based combat**: initiative order, front/back ranks for both sides, melee and missile attacks,
  blocking, running, bribing; monster AI with special abilities, healing, fleeing and target
  selection; treasure, item drops and XP.
- **Magic**: 41 spells in two schools (cleric and sorcerer) across six spell levels - healing,
  curing, blessings and wards, single/group/all-enemy attacks, sleep and paralysis, armor-weakening,
  light, recall, raising the dead - plus potions, scrolls and wands. Three rare **tomes** (one sold,
  two hidden in the Ashen Hills) teach spells that can't be learned by levelling up.
- **Gold**: each character carries their own gold, and the party shares a **purse** that loot goes
  into. Purchases come out of the purse first and then from the buyer's own gold. From the character
  sheet you can deposit, withdraw, pool everyone's gold or share the purse evenly; a character who
  stays at an inn takes their own gold - and as much from the purse as you choose - with them.
- **Inventory and equipment**: 9 equipment slots, 12-slot backpacks, class restrictions,
  two-handed weapons, give/drop/use, charges.
- **Data-driven content**: monsters, items, spells, races, classes, shops and maps are JSON files you
  can edit (see [docs/CONTENT_FORMAT.md](docs/CONTENT_FORMAT.md)); put a `Content` folder next to the
  executable or pass `--content DIR` to mod the game without rebuilding.
- **Any screen size**: the interface scales to fit the window - crisp on a small laptop or a 4K
  monitor (Settings can switch back to a fixed 100% layout).
- **Accessibility**: three colour themes (Standard, High contrast, and Colour-blind friendly, which
  shows good news in blue and bad news in orange instead of green and red, using the Okabe-Ito
  palette), adjustable text size (90-130%), and rebindable keys and controller buttons.
- **3D view options**: classic 400 x 300, sharp 640 x 480 or high 800 x 600 internal resolution,
  with optional smooth scaling.
- **Gamepad support** (Xbox, PlayStation, Switch Pro and other SDL-supported controllers) for
  exploring, combat and every menu; exploring buttons can be rebound in Settings.
- **Save/load** (10 slots incl. quick-save, JSON) in the OS user-data directory; **settings** for
  music/effects volume, fullscreen, minimap and **rebindable keys**.
- **Music, ambience and sound effects** via OpenAL Soft (Silk.NET) + NVorbis, bundled for all six
  platforms: region music (towns, wilds, desert, marsh, caves, dungeons), a boss theme, and ambient
  loops (birdsong, river, wind, dripping water, deep dungeon) under the music, each with its own
  volume. The game silently falls back to no audio if no device is available.
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
| ![Guardian](docs/screenshots/15-guardian.png) A guardian waiting in its alcove | ![Ashen Hills](docs/screenshots/16-ashen-hills.png) The Ashen Hills marsh road |
| ![Thornwick](docs/screenshots/17-thornwick.png) Thornwick, the mining town | ![Duskmere](docs/screenshots/18-duskmere.png) Duskmere, the marsh village |
| ![Port Ashkar](docs/screenshots/19-port-ashkar.png) Port Ashkar on the Sunscar Coast | ![Oasis](docs/screenshots/20-sunscar-oasis.png) The oasis in the Sunscar Wastes |
| ![Old Cistern](docs/screenshots/21-old-cistern.png) An eel in the Old Cistern's flooded reservoir | ![Journal](docs/screenshots/22-journal.png) The quest journal |
| ![High contrast](docs/screenshots/23-high-contrast.png) High-contrast theme, 115% text, 800 x 600 view | ![Help](docs/screenshots/24-help.png) The in-game help |
| ![Wintermere](docs/screenshots/25-wintermere.png) Wintermere, the northern harbour town | ![Rime Halls](docs/screenshots/26-rime-halls.png) The Rime Halls |

## Download and install

Download the archive for your platform from the **[latest release](https://github.com/Harlock123/AVAMMB1/releases/latest)**
(all releases: [https://github.com/Harlock123/AVAMMB1/releases](https://github.com/Harlock123/AVAMMB1/releases)), or build it yourself (below). Direct links for v1.6.0:

| Platform | Download | How to run |
|---|---|---|
| Windows x64 | [AVAMMB1-1.6.0-win-x64.zip](https://github.com/Harlock123/AVAMMB1/releases/download/v1.6.0/AVAMMB1-1.6.0-win-x64.zip) | Unzip, run `AVAMMB1.exe`. SmartScreen may warn about an unsigned app: *More info -> Run anyway*. |
| Windows ARM64 | [AVAMMB1-1.6.0-win-arm64.zip](https://github.com/Harlock123/AVAMMB1/releases/download/v1.6.0/AVAMMB1-1.6.0-win-arm64.zip) | Same as above. |
| Linux x64 | [AVAMMB1-1.6.0-linux-x64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.6.0/AVAMMB1-1.6.0-linux-x64.tar.gz) | `tar xzf AVAMMB1-*.tar.gz && ./AVAMMB1`. Needs an X11 session (XWayland works) and the usual desktop libraries (fontconfig, libX11/libICE/libSM). |
| Linux ARM64 | [AVAMMB1-1.6.0-linux-arm64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.6.0/AVAMMB1-1.6.0-linux-arm64.tar.gz) | Same as above. |
| macOS Apple Silicon | [AVAMMB1-1.6.0-osx-arm64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.6.0/AVAMMB1-1.6.0-osx-arm64.tar.gz) | Extract, then open `AVAMMB1.app`. The app is **not notarized**: right-click -> *Open* the first time, or run `xattr -dr com.apple.quarantine AVAMMB1.app`. |
| macOS Intel | [AVAMMB1-1.6.0-osx-x64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.6.0/AVAMMB1-1.6.0-osx-x64.tar.gz) | Same as above. |

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
| Quest journal and clues | J | *Journal* |
| Help (how to play, your controls) | F1 / H | *Help* in the menu |
| Note on this automap square | N (or click any square on the automap) | *Note* |
| Quick save / quick load | F5 / F9 | |
| Game menu (save, load, settings) | Esc | *Menu* |
| Fullscreen | F11 or Alt+Enter | Settings |
| **Combat**: Fight / Run / Bribe | F, R, B | buttons |
| **Combat**: Attack, Shoot, Cast, Use item, Block, Run | A, S, C, U, B, R | buttons |
| **Combat**: pick target / spell / ally | 1-9 | click the monster |
| **Combat**: repeat everyone's last action / auto-fight on-off | E, O | *Repeat*, *Auto* |

**Gamepad** (any controller SDL recognises; Xbox button names shown):

| Context | Controls |
|---|---|
| Exploring (defaults; rebind in Settings > Controller) | D-pad / left stick: move and turn (hold to repeat) - LB / RB: strafe - A: use - X: search - Y: party sheet - View: map - Start or B: menu - LT: rest - RT: cast |
| Battle | A: fight / attack (shoots from the back rank) / continue - X: cast - Y: use item - B: block - View: run - LB / RB or left / right: change target - LT: repeat last actions - RT: auto-fight |
| Menus and dialogs | D-pad: move between buttons (left / right adjust sliders) - A: select - B: back |

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

### Build verification status (v1.6.0)

Every release is built by [`.github/workflows/release.yml`](.github/workflows/release.yml) on
GitHub's runners - each RID on its own operating system - after the test suite passes:

| RID | Built on | Actually executed |
|---|---|---|
| win-x64 | Windows runner | **yes** - `--smoke-test` passes in CI (UI render, movement, bundled OpenAL loads) |
| linux-x64 | Ubuntu runner | **yes** - `--smoke-test` passes in CI |
| osx-arm64 | macOS runner (ad-hoc signed `.app`) | **yes** - `--smoke-test` passes in CI, audio device opened; play-tested by the maintainer |
| linux-arm64 | Ubuntu runner (cross-compiled) | **yes** - the published release archive was downloaded and passes `--smoke-test` on an ARM64 Linux machine; the README screenshots are captured with this build |
| win-arm64 | Windows runner (cross-compiled) | **yes** - play-tested by the maintainer on Windows on ARM |
| osx-x64 | macOS runner (cross-compiled) | no |

The CI runners have no sound card, so on Windows and Linux the smoke test only checks that the
bundled OpenAL Soft library loads; on macOS (CI) and Linux ARM64 it also opens the audio device and
loads a test sound.

Gameplay verification: 180 unit and integration tests pass. They include scripted playthroughs that
walk the party with normal movement commands from Brindlemoor to the victory event, through all
three side quests and the three deeper levels, checks that every map is connected and every event
reachable, deterministic boss-balance checks (each boss beatable at its intended level, not trivial
two levels below), and a **balance simulator** - an automated player that grinds every zone in
route order from level 1 to 15 (fighting, camping, curing with spells and potions, opening the
zone's chests, training, selling loot, buying gear and potions, reloading after a wipe) and asserts
the pace, wipe rate and gold flow stay within tuned bounds (about 600 battles from a new party to
level 15). It skips boss hoards and quest rewards, so real players end up with a little more gold. The UI flow title -> party creation -> town -> shop -> dungeon -> combat -> inventory ->
automap with notes -> journal -> spells -> secret door -> Old Cistern -> Ashen Hills -> Sunscar Coast -> Wintermere -> Rime Halls -> high-contrast theme -> settings is exercised headlessly by `--screenshot`.
Gamepad support has been play-tested with a physical controller on Linux ARM64 (v1.4.0); on the
other platforms CI confirms the bundled SDL2 library loads, and the button mapping is unit tested.
The game is currently under testing.

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

### Done

- **v1.1.0** - Secret-door searching (Search action, hidden rooms with treasure); fixed audio on
  machines without a system OpenAL.
- **v1.2.0** - Smooth step/turn animation and animated monsters; monsters shown in the 3D view
  (battles staged in the viewport, guardians visible in their squares); dungeon hazards: spinners,
  magical darkness, anti-magic squares and teleport, pit, alarm and gas traps.
- **v1.3.0** - More towns and dungeons: the Ashen Hills region, the towns of Thornwick and Duskmere,
  the two-level Thornwick Mines and the Sunken Temple, with two side quests; new special items
  (Heartfire Ring, Lotus Amulet, Wyrmguard Blade, Wyrmscale Mail, Moonsilver Blade, Evening Star);
  boss balance tuning.
- **v1.4.0** - Per-character gold with a shared party purse (deposit, withdraw, pool, share,
  take gold along when leaving at an inn); 13 new spells including a new spell level 6, a new
  armor-weakening effect and Stone to Flesh; spell tomes. Interface scaling and gamepad support.
  The fifth town: Port Ashkar (by ferry from Saltreach), the Sunscar Wastes and the two-level Tomb
  of the Sun Kings for levels 9-12.
- **v1.5.0** - Save-game versioning and migration (older saves upgrade automatically, with a
  backup); region music, a boss theme and ambient sound loops with an Ambience volume setting;
  three new dungeon levels with new monsters, bosses and unique items (the Old Cistern, the
  Catacombs and the post-game Sunless Deep); a balance pass driven by an automated balance
  simulator (more gold from monsters, cheaper training and temple cures, more XP in the level 5-8
  zones, paralysis that wears off in battle).
- **v1.6.0** - Quest journal with clues, automap notes, combat Repeat and Auto-fight, monster
  knowledge; colour themes (high contrast, colour-blind friendly), text size and controller button
  remapping; 3D view resolution up to 800 x 600 with optional smooth scaling; the balance simulator
  now loots chests, uses potions and sells loot, and mid-game gold was raised.

### Planned

- Proper Developer ID signing / notarization for macOS and an installer for Windows
- Localization (translations)

## License

- Code and original game data: **MIT** - see [LICENSE](LICENSE).
- Art and audio: **CC0 1.0** third-party assets - see [ASSETS_LICENSES.md](ASSETS_LICENSES.md).
- Third-party libraries keep their own licenses (Avalonia MIT, OpenAL Soft LGPL-2.0+, ...).

## Credits

See [CREDITS.md](CREDITS.md) (also shown in-game under *Credits*). Graphics from the Dungeon Crawl
Stone Soup tile set; music by Juhani Junkala, cynicmusic and yd; sound effects by Kenney, rubberduck
and artisticdude - all CC0. Thank you!
