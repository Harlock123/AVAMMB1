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

**[Download AVAM&M v1.13.0](https://github.com/Harlock123/AVAMMB1/releases/tag/v1.13.0)** - Windows, Linux and macOS, x64 and ARM64
(always-current link: [latest release](https://github.com/Harlock123/AVAMMB1/releases/latest); direct file links are in
[Download and install](#download-and-install)).

Code identifiers, the solution and executables use the name **`AVAMMB1`** (`AVAMMB1.sln`,
`AVAMMB1.exe`, `AVAMMB1-1.13.0-linux-x64.tar.gz`, ...), because `&` is unsafe in paths.

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
- **Day and night**: a clock runs as you travel; nights are dark outdoors and in towns, bring out
  night-only monsters and close the shops - plan around the inn, or carry a lantern.
- **Light and lanterns**: dungeons are dark; torches (5 squares), light spells (6) and an equippable
  **Brass Lantern** (8 squares, warm light) that burns oil you buy by the flask - Angband style. A
  flask of oil can also be thrown in battle as a fire bomb.
- **Automap and minimap** that record only what the party has actually seen, with your own
  **notes** on any square.
- **Quest journal**: every quest you have heard of, with its story so far and your current goal, plus
  the signs, inscriptions and warnings you have read (Clues). The game menu opens it too.
- **Class abilities**: knights Guard a companion (blows strike the knight instead), paladins Lay on
  Hands once a battle, archers take Aimed shots (+4 to hit, double damage, not two rounds running),
  robbers sneak-attack for double damage in the first round and pick locked doors, clerics Turn
  Undead once a battle (undead flee or crumble), and sorcerers Overcharge a spell (half again the
  power for double the spell points).
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
- **Character looks**: pick a hairstyle and beard for each portrait at creation; rename characters
  and restyle them at any inn.
- **Where next?**: the journal, automap and minimap show where each open quest leads.
- **Puzzles and choices**: riddle doors (type the answer), levers and portcullises, pressure plates,
  and decisions with consequences.
- **The Chronicle**: statistics for your game (monsters slain, gold found, secrets, spells...) and 22
  achievements.
- **Bestiary and compendium**: Journal tabs for every creature you have defeated (attacks, defences,
  where found, how many slain) and every item you have seen.
- **Autosave and save pictures**: three rotating autosaves (new areas, before bosses); every save
  shows a picture of the view and the time played.
- **Towns by day and night**: a night market, thieves in the streets after dark, dawn prayers at the
  temples and a rumour of the day in every tavern.
- **Travel map**: travel to any town you have visited - time passes, fares are paid, and the open
  country may ambush you on the way.
- **Daily Challenge**: today's Depths - the same levels for everyone - with a ready party; the Hall of
  Fame keeps each day's best depth.
- **The Depths Below**: an endless post-game dungeon of generated levels, harder and richer each
  level down.
- **Weather and spell effects**: snow and rain in the open country, and colour flashes for spells
  and breath attacks in battle.
- **Controller-only play**: an on-screen keyboard for names, riddles and notes (A on a text box), and
  random names to suit each race.
- **Hall of Fame and ironman**: finished runs and achievements across all your games; an optional
  ironman mode with a single self-kept save where a party wipe ends the run.
- **Smithing**: improve weapons and armor up to +5 at the smithies, for gold and gems.
- **Shopping help**: gear for sale is compared with what the shopper wears, and "Sell junk" sells
  everything nobody in the party could use as an upgrade.
- **Difficulty and survival**: Easy, Normal or Hard (monster health and damage, gold, encounter
  rate), chosen at party creation and changeable in Settings; an optional survival mode where
  everyone eats a ration a day and goes hungry without.
- **Drag-and-drop marching order**: drag a party card along the bar at the bottom of the screen to
  change places (anywhere but in battle); a click still opens the character sheet. Without a mouse,
  Move left / Move right on the character sheet (`[` / `]`, or the buttons with a controller).
- **Twenty-five maps**: six towns (Brindlemoor, Saltreach, Thornwick, Duskmere, Port Ashkar and
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
  Mid-game, **The Silent Choir** (levels 7-12) runs through all six towns: Brindlemoor's magistrate,
  a ledger from the Deep Mines, the Saltreach customs house, a runaway singer in Duskmere, a key
  hidden in the Tomb of the Sun Kings - and the two-level Hollow Belfry in the Ashen Hills, where the
  Choirmaster is recasting a bell that steals voices. Its rewards include four unique items.
- **Elite monsters**: about one random encounter in twenty is led by an elite (gold name) with double
  hit points, better armor and harder blows - worth triple experience and gold, with a chance of
  extra loot.
- **Turn-based combat**: initiative order, front/back ranks for both sides, melee and missile attacks,
  blocking, running, bribing; monster AI with special abilities, healing, fleeing and target
  selection; treasure, item drops and XP.
- **Magic**: 55 spells in two schools (cleric and sorcerer) across seven spell levels - healing,
  curing, blessings and wards, single/group/all-enemy attacks, sleep and paralysis, armor-weakening,
  light, recall, raising the dead, and utility magic (Reveal Secrets, Levitate, Sense Minds) - plus
  potions, scrolls and wands. Rare **tomes** teach spells that can't be learned by levelling up,
  including the seventh circle (Starfall, Divine Intervention), sold in Wintermere and found in the
  Depths Below.
- **Gold**: each character carries their own gold, and the party shares a **purse** that loot goes
  into. Purchases come out of the purse first and then from the buyer's own gold. From the character
  sheet you can deposit, withdraw, pool everyone's gold or share the purse evenly; a character who
  stays at an inn takes their own gold - and as much from the purse as you choose - with them.
- **Inventory and equipment**: 9 equipment slots, 12-slot backpacks, class restrictions,
  two-handed weapons, give/drop/use, charges.
- **Mod packs**: drop a folder of JSON (plus optional graphics and music) into the Mods folder to add
  monsters, items, spells, quests and whole dungeons, or link new maps into the world with map
  patches - no rebuilding. A Mods screen switches packs on and off, and `--check-content` reports
  problems. See [docs/modding/MODDING.md](docs/modding/MODDING.md) and the example pack.
- **Map editor** (Mods > Map editor): draw walls, doors and squares, place events, set encounters,
  test-play the map with a party, and save it as a mod pack with a way in from the world.
- **Data-driven content**: monsters, items, spells, races, classes, shops and maps are JSON files you
  can edit (see [docs/CONTENT_FORMAT.md](docs/CONTENT_FORMAT.md)); put a `Content` folder next to the
  executable or pass `--content DIR` to mod the game without rebuilding.
- **Any screen size**: the interface scales to fit the window - crisp on a small laptop or a 4K
  monitor (Settings can switch back to a fixed layout, with an interface zoom of 100-150%).
- **Languages**: the interface in English or German (Settings > Language); more translations can be
  added as a file - `Assets/Lang/template.json` lists every string, and mod packs can bring their own.
  Game content and most log messages are English for now.
- **Accessibility**: three colour themes (Standard, High contrast, and Colour-blind friendly, which
  shows good news in blue and bad news in orange instead of green and red, using the Okabe-Ito
  palette), adjustable text size (90-130%), adjustable battle text speed (lines appear one at a time),
  a LOW badge on badly hurt characters that does not rely on colour, "Describe surroundings" (L) and
  optional step-by-step descriptions in the log for screen readers, and rebindable keys and
  controller buttons.
- **3D view options**: classic 400 x 300, sharp 640 x 480 or high 800 x 600 internal resolution,
  optional smooth scaling, and optional **detailed textures** - 128 x 128 walls and floors (CC0,
  Screaming Brain Studios) for the towns and most dungeons, with mipmapping so distant walls do not
  shimmer; places with a distinctive classic look keep their original tiles.
- **Gamepad support** (Xbox, PlayStation, Switch Pro and other SDL-supported controllers) for
  exploring, combat and every menu; exploring buttons can be rebound in Settings.
- **Save/load** (10 slots incl. quick-save, JSON) in the OS user-data directory; **settings** for
  music/effects volume, fullscreen, minimap and **rebindable keys**.
- **Music, ambience and sound effects** via OpenAL Soft (Silk.NET) + NVorbis, bundled for all six
  platforms: region music (towns, wilds, desert, marsh, caves, dungeons), a boss theme, and ambient
  loops (birdsong, night crickets, river, wind, dripping water, deep dungeon) under the music, each
  with its own volume, and footsteps that change with the ground (stone, snow, water, grass). The game silently falls back to no audio if no device is available.
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
| ![Detailed textures](docs/screenshots/27-detailed-textures.png) Detailed textures at 800 x 600 (the Hollow Crypt) | ![Lantern](docs/screenshots/28-lantern.png) Lantern light in the cellars |
| ![Night](docs/screenshots/29-night.png) The Ashen Hills road at night | ![Battle spells](docs/screenshots/30-combat-spells.png) Battle spells with their keys |
| ![Load game](docs/screenshots/31-load-game.png) Saves with pictures, play time and autosaves | ![Choirmaster](docs/screenshots/32-choirmaster.png) The Choirmaster in the Bell Chamber |
| ![Inn looks](docs/screenshots/33-inn-looks.png) Renaming and restyling at the inn | ![Bestiary](docs/screenshots/34-bestiary.png) The bestiary |
| ![Items](docs/screenshots/35-items.png) The item compendium | ![Chronicle](docs/screenshots/36-chronicle.png) The Chronicle: statistics and achievements |
| ![Riddle](docs/screenshots/37-riddle.png) A riddle door in the Hollow Belfry | ![Choice](docs/screenshots/38-choice.png) A choice: Sister Veyl's secret |
| ![Smithing](docs/screenshots/39-smithing.png) Smithing: improving gear up to +5 | ![Hall of Fame](docs/screenshots/40-hall-of-fame.png) The Hall of Fame |
| ![Keyboard](docs/screenshots/41-keyboard.png) The on-screen keyboard for controller players | ![Depths](docs/screenshots/42-depths.png) The Depths Below, level 1 |
| ![Travel](docs/screenshots/43-travel.png) The travel map | ![Daily](docs/screenshots/44-daily-challenge.png) The Daily Challenge |
| ![Daily result](docs/screenshots/45-daily-result.png) A daily challenge result card, ready to share | ![Tip](docs/screenshots/46-tip.png) A tip for new players |
| ![Hireling](docs/screenshots/47-hireling.png) A hireling joins at the inn | ![Victory](docs/screenshots/48-ending-victory.png) Victory - and New Game+ |
| ![New Game+](docs/screenshots/49-new-game-plus.png) New Game+ begins | ![Map editor](docs/screenshots/50-map-editor.png) The map editor |
| ![Test play](docs/screenshots/51-map-test-play.png) Test-playing an edited map | |

## Download and install

Download the archive for your platform from the **[latest release](https://github.com/Harlock123/AVAMMB1/releases/latest)**
(all releases: [https://github.com/Harlock123/AVAMMB1/releases](https://github.com/Harlock123/AVAMMB1/releases)), or build it yourself (below). Direct links for v1.13.0:

| Platform | Download | How to run |
|---|---|---|
| Windows x64 | [AVAMMB1-1.13.0-win-x64.zip](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-win-x64.zip) | Unzip, run `AVAMMB1.exe`. SmartScreen may warn about an unsigned app: *More info -> Run anyway*. |
| Windows ARM64 | [AVAMMB1-1.13.0-win-arm64.zip](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-win-arm64.zip) | Same as above. |
| Linux x64 | [AVAMMB1-1.13.0-linux-x64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-linux-x64.tar.gz) | `tar xzf AVAMMB1-*.tar.gz && ./AVAMMB1`. Needs an X11 session (XWayland works) and the usual desktop libraries (fontconfig, libX11/libICE/libSM). |
| Linux ARM64 | [AVAMMB1-1.13.0-linux-arm64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-linux-arm64.tar.gz) | Same as above. |
| macOS Apple Silicon | [AVAMMB1-1.13.0-osx-arm64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-osx-arm64.tar.gz) | Extract, then open `AVAMMB1.app`. The app is **not notarized**: right-click -> *Open* the first time, or run `xattr -dr com.apple.quarantine AVAMMB1.app`. |
| macOS Intel | [AVAMMB1-1.13.0-osx-x64.tar.gz](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-osx-x64.tar.gz) | Same as above. |
| Windows x64 (installer) | [AVAMMB1-1.13.0-win-x64-setup.exe](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-win-x64-setup.exe) | Run it; installs for you only (no admin), with a Start-menu shortcut and an uninstaller. Unsigned: *More info -> Run anyway*. |
| Windows ARM64 (installer) | [AVAMMB1-1.13.0-win-arm64-setup.exe](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-win-arm64-setup.exe) | Same as above. |
| Linux x64 (AppImage) | [AVAMMB1-1.13.0-linux-x64.AppImage](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-linux-x64.AppImage) | `chmod +x AVAMMB1-*.AppImage && ./AVAMMB1-*.AppImage` (needs FUSE; or add `--appimage-extract-and-run`). |
| Linux ARM64 (AppImage) | [AVAMMB1-1.13.0-linux-arm64.AppImage](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-linux-arm64.AppImage) | Same as above. |
| macOS Apple Silicon (disk image) | [AVAMMB1-1.13.0-osx-arm64.dmg](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-osx-arm64.dmg) | Open, drag `AVAMMB1.app` to Applications; not notarized (see below). |
| macOS Intel (disk image) | [AVAMMB1-1.13.0-osx-x64.dmg](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-osx-x64.dmg) | Same as above. |
| Linux x64 (Flatpak) | [AVAMMB1-1.13.0-x86_64.flatpak](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-x86_64.flatpak) | `flatpak install --user AVAMMB1-*.flatpak`, then `flatpak run io.github.Harlock123.AVAMMB1` (needs the Flathub remote for the runtime). |
| Linux ARM64 (Flatpak) | [AVAMMB1-1.13.0-aarch64.flatpak](https://github.com/Harlock123/AVAMMB1/releases/download/v1.13.0/AVAMMB1-1.13.0-aarch64.flatpak) | Same as above. |
**Installers and packages** (from v1.12.0 on): a Windows installer
`AVAMMB1-<version>-<rid>-setup.exe` (installs for the current user without admin rights, Start-menu
and optional desktop shortcut, uninstall from *Settings > Apps*; unsigned, so SmartScreen may warn), a
macOS disk image `AVAMMB1-<version>-<rid>.dmg` (drag `AVAMMB1.app` to Applications; same
notarization note as below) and a Linux AppImage `AVAMMB1-<version>-<rid>.AppImage`
(`chmod +x AVAMMB1-*.AppImage && ./AVAMMB1-*.AppImage`; needs FUSE 2/3, or add
`--appimage-extract-and-run`). From v1.13.0 on, releases also include a Flatpak bundle,
`AVAMMB1-<version>-<x86_64|aarch64>.flatpak` (`flatpak install --user AVAMMB1-*.flatpak`, then start it
from the menu or with `flatpak run io.github.Harlock123.AVAMMB1`) - it suits the Steam Deck's desktop
mode; see [packaging/flatpak](packaging/flatpak/README.md), which also covers submitting to Flathub.
Saves and settings are the same as with the archives.

**Updates:** the game asks GitHub about once a day whether a newer release exists and, if so, says so
on the title screen with a link to the download page. It never downloads or installs anything, and
sends nothing but that request; switch it off in *Settings* ("Check for a new version at start").

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
| Light a torch / fill a lantern | open the sheet (I), pick the Torch or Flask of Oil, *Use* | |
| Fullscreen | F11 or Alt+Enter | Settings |
| **Combat**: Fight / Run / Bribe | F, R, B | buttons |
| **Combat**: Attack, Shoot, Cast, Use item, Block, Run | A, S, C, U, B, R | buttons |
| **Combat**: pick target / spell / ally | 1-9 | click the monster |
| **Combat**: repeat everyone's last action / auto-fight on-off | E, O | *Repeat*, *Auto* |
| **Combat**: class ability - Guard (knight), Lay on Hands (paladin), Aimed shot (archer) | G, L, T | ability button |

**Gamepad** (any controller SDL recognises; Xbox button names shown):

| Context | Controls |
|---|---|
| Exploring (defaults; rebind in Settings > Controller) | D-pad / left stick: move and turn (hold to repeat) - LB / RB: strafe - A: use - X: search - Y: party sheet - View: map - Start or B: menu - LT: rest - RT: cast |
| Battle | A: fight / attack (shoots from the back rank) / continue - X: cast - Y: use item - B: block - View: run - LB or left / right: change target - RB: class ability - LT: repeat last actions - RT: auto-fight |
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
Installers and packages are made from those builds by `packaging/package.sh <rid>` (macOS `.dmg`
with `hdiutil`; Linux AppImage - `appimagetool` is downloaded) and `pwsh packaging/package.ps1 -Rid <rid>`
(Windows, [Inno Setup](https://jrsoftware.org/isinfo.php) 6 - `packaging/windows/AVAMMB1.iss`).
CI builds and smoke-tests them too: the installer is installed silently and the installed game run,
the disk image is mounted and its app run, and the AppImage is run.
**Trimming is intentionally disabled**: Avalonia, the DI container and the toolkit rely on
reflection that trimming can break; ReadyToRun is used instead for faster startup.

### Build verification status (v1.13.0)

Every release is built by [`.github/workflows/release.yml`](.github/workflows/release.yml) on
GitHub's runners - each RID on its own operating system - after the test suite passes:

| RID | Built on | Actually executed |
|---|---|---|
| win-x64 | Windows runner | **yes** - `--smoke-test` passes in CI (UI render, movement, bundled OpenAL loads) |
| linux-x64 | Ubuntu runner | **yes** - `--smoke-test` passes in CI |
| osx-arm64 | macOS runner (ad-hoc signed `.app`) | **yes** - `--smoke-test` passes in CI, audio device opened; play-tested by the maintainer |
| linux-arm64 | Ubuntu runner (cross-compiled) | **yes** - the published release archive was downloaded and passes `--smoke-test` on an ARM64 Linux machine; the README screenshots are captured with this build |
| win-arm64 | Windows runner (cross-compiled) | **yes** - play-tested by the maintainer on Windows on ARM |
| osx-x64 | macOS runner (cross-compiled) | **yes** - play-tested by the maintainer on an Intel Mac |

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
automap with notes -> journal -> spells -> secret door -> Old Cistern -> Ashen Hills -> Sunscar Coast -> Wintermere -> Rime Halls -> the Bell Chamber -> high-contrast theme -> detailed textures -> settings is exercised headlessly by `--screenshot`.
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
- **v1.7.0** - The Frostmark: a sixth town (Wintermere, by ship from Saltreach), a tundra and the
  two-level Rime Halls with the ice dragon Rimefang; a bounty quest in every town; academies that
  sell permanent stat points; elite monsters; optional detailed 128 x 128 textures with mipmapping;
  a "What's new" screen after updates and in-game help (F1).
- **v1.7.1** - Light lasts longer (torch 150 steps, Holy Light 200, Glowlight 250, Scroll of Light
  400) and the in-game help explains how to light the way.
- **v1.8.0** - Lanterns and oil, Angband style: an equippable Brass Lantern (8 squares, warm light)
  that burns oil, flasks of oil to refill it or throw as fire bombs, the Everburning Lantern; light
  brightness tiers (torch 5, spells 6, lantern 8) and an automap that maps as far as your light reaches.
- **v1.9.0** - Mod packs (a Mods folder, merge-by-id content, map patches, a Mods screen,
  `--check-content`); day and night (a clock, dark nights outdoors, night-only monsters, closed
  shops); class abilities (Guard, Lay on Hands, Aimed shot, sneak attack, lock-picking); audio polish
  (Frostmark music, night crickets, footsteps by surface); keyboard keys for battle spell and item
  lists; drag-and-drop marching order on the party bar.
- **v1.9.1** - Pressing Use on a lantern equips it; oil also fills a lantern in a backpack; the
  rest message and help explain when food is eaten.
- **v1.10.0** - Difficulty levels and survival mode; rotating autosaves, save pictures and play time;
  shop comparisons and Sell junk; the Silent Choir quest chain and the Hollow Belfry; accessibility
  (interface zoom, battle text speed, LOW badge); character looks and renaming; "Where next?" quest
  markers; bestiary, item compendium and the Chronicle (statistics, 22 achievements); riddle doors,
  levers, pressure plates and choices; balance pass with Easy/Hard simulations.
- **v1.11.0** - Smithing (+1 to +5); a harder Hard (tougher bosses, more elites, slower experience);
  the Hall of Fame and ironman mode; an on-screen keyboard and random names for controller players;
  snow, rain and spell colour effects; the Depths Below, an endless post-game dungeon; code split of
  the game session.
- **v1.12.0** - Installers and packages (Windows installer, macOS disk image, Linux AppImage);
  Turn Undead and Overcharge; utility spells and a seventh circle from tomes; Describe surroundings;
  the travel map; town life by time of day (Night Market, street thieves, dawn prayers, daily
  rumours); the Daily Challenge; an automated playthrough of the whole game by its journal.
- **v1.13.0** - Hirelings at the inns; New Game+ with Ascendant gear; tips for new players; a
  shareable Daily Challenge result; an update check; save fixtures for every release; Flatpak bundles;
  translations (German included); a map editor with test play.

### Planned

- Proper Developer ID signing / notarization for macOS and code signing for Windows

## License

- Code and original game data: **MIT** - see [LICENSE](LICENSE).
- Art and audio: **CC0 1.0** third-party assets - see [ASSETS_LICENSES.md](ASSETS_LICENSES.md).
- Third-party libraries keep their own licenses (Avalonia MIT, OpenAL Soft LGPL-2.0+, ...).

## Credits

See [CREDITS.md](CREDITS.md) (also shown in-game under *Credits*). Graphics from the Dungeon Crawl
Stone Soup tile set; music by Juhani Junkala, cynicmusic and yd; sound effects by Kenney, rubberduck
and artisticdude - all CC0. Thank you!
