# AVAM&M Architecture

AVAM&M is split into three projects:

| Project | Depends on | Purpose |
|---|---|---|
| `AVAMMB1.Core` | .NET only | All rules and game state. No UI, no audio, no file dialogs. Fully unit tested. |
| `AVAMMB1.App` | Core, Avalonia, Silk.NET, NVorbis | Avalonia desktop UI, rendering, audio, input, DI composition. Assembly/executable name `AVAMMB1`. |
| `AVAMMB1.Tests` | Core, xUnit | Unit, integration and balance tests (`Balance/BalanceSimulator` plays the whole route automatically). |

## Core

```
ContentDatabase  <- IContentSource (EmbeddedContentSource | DirectoryContentSource)
     |               loads races/classes/items/monsters/spells/shops/game.json + Maps/*.json,
     |               parses maps into GameMap and cross-validates every reference.
     v
Rulebook          stat bonuses, armor class, to-hit, damage, attacks/round, XP table,
     |            caster level / max spell level / SP, saving throws, damage & healing.
     v
GameSession  ---- owns GameState (party, roster, gold, position, flags, automap, clock)
  |  Move/Turn/Rest/Interact -> StepResult (messages + sound cues, interaction, combat, story, victory)
  |  map events: message, shop, inn, temple, tavern, training, teleport, treasure,
  |              encounter, trap, fountain, quest, victory (with flag/item requirements, once-only)
  |  hazards: spinners, magical darkness / anti-magic terrain, damage/gas/teleport/pit/alarm traps
  |  random encounters from weighted per-map tables
  +-- CombatEngine   initiative, front ranks (first three standing members / monsters),
  |                  party actions, monster AI (abilities, self-heal, smart targeting, fleeing),
  |                  rewards. Pauses whenever a party member needs a decision.
  +-- SpellCaster    spell effects in and out of combat; item use (potions, scrolls, wands, food, torches)
  +-- Inventory      equip/unequip (slots, class restrictions, two-handed), give, discard
  +-- TownServices   inn, temple, tavern, training, shop buy/sell
SaveGameService / SettingsStore -> System.Text.Json source generation (GameJsonContext)
UserDataPaths -> OS-appropriate data folder (override with AVAMMB1_DATA_DIR)
```

Randomness always flows through `IRandomSource`, so tests can script dice rolls
(`ScriptedRandom`) and automation can use a fixed seed.

### Maps

`GameMap` supports two encodings that can be mixed through the terrain legend:

* **Edges** - thin walls between cells (`|`/`-` walls, `D` doors, `L` locked doors, `S` secret doors),
  as in classic dungeon crawlers. Stored as two edge arrays so the wall between two cells is shared.
* **Blocks** - one character per cell; terrain entries mark cells as `solid` (blocks movement) and
  optionally non-`opaque` (trees and water: you can see past them).

`GameMap.Reachable` (BFS) is used by tests to guarantee every event can be reached.

## App

* **Composition**: `App.BuildServices` registers `ContentDatabase`, `IRandomSource`, `GameSession`,
  `SaveGameService`, `SettingsStore`, `IAudioService`, `TextureCache`, `GameServices` and
  `MainViewModel` in `Microsoft.Extensions.DependencyInjection`.
* **MVVM**: CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`).
  `MainViewModel.CurrentScreen` switches screens; `GameViewModel.Overlay` hosts dialogs (combat,
  shops, inn, temple, tavern, training, character sheet, spells, automap, menus).
  `ViewLocator` maps view models to views with a `switch` (no reflection). Compiled bindings are on.
* **Input**: `MainWindow` intercepts keys in the tunnel phase and forwards them to the current screen;
  bindable actions are looked up in `GameSettings.KeyBindings` (Avalonia `Key` names).
  `GamepadService` reads controllers through SDL2's GameController API on a background thread and
  posts presses to the UI thread; `GamepadMapping` (Core, unit tested) turns a button into an
  exploration action, a combat command or menu navigation depending on context. Menu navigation
  is spatial: the D-pad focuses the nearest button in that direction inside the top-most dialog.
* **Scaling**: screens are laid out for 1280x800; with *Fit interface to window* the window wraps
  them in a `Viewbox`, so the whole UI (vector text and controls, nearest-neighbour pixel art) scales
  uniformly to any window size.
* **Rendering**: `SceneRenderer` is a CPU ray caster writing a 400x300 BGRA framebuffer:
  DDA through the grid checking thin walls before entering each cell and opaque cells after,
  textured floor/ceiling casting, sky gradient, distance fog / darkness, z-buffered billboards.
  `SceneView` copies the framebuffer into a `WriteableBitmap` and scales it with nearest-neighbor
  filtering. Frames are rendered only when the scene changes; for a single step or 90-degree turn,
  `SceneView` interpolates a free `Camera` (fractional position, any angle) on
  `TopLevel.RequestAnimationFrame` for ~0.15 s with smoothstep easing. A new move during an
  animation starts from the previous destination, so input is never delayed.
* **Combat layout**: `GameViewModel.Combat` (separate from dialog overlays) shows
  `CombatStageView` over the 3D view (front/back rank collections) and `CombatPanelView` in the
  side panel. Encounter guardians are added to the scene as bobbing billboards.
* **Combat animation**: `CombatViewModel` compares HP snapshots and listens to
  `CombatEngine.MonsterActed` after each engine step, then pulses `IsHit` / `IsAttacking` /
  `IsHurt` flags that toggle style classes with keyframe animations (transform properties) in
  `CombatStageView.axaml` and `GameView.axaml`.
  `MapView` draws the automap with vector primitives from the explored-cell bitmap in `GameState`.
* **Textures**: `TextureCache` loads PNGs from Avalonia resources (`avares://AVAMMB1/Assets/...`),
  decodes them to raw pixels for the ray caster, and composites paper-doll portraits.
* **Audio**: `OpenAlAudioService` (Silk.NET OpenAL + OpenAL Soft natives for every RID) decodes Ogg
  Vorbis with NVorbis; sound effects are cached buffers on a pool of sources. Music and the map's
  ambient loop are two independent `StreamChannel`s, each streaming on its own background thread
  with four queued buffers and looping; each has its own volume. Battles that include a monster
  flagged `boss` switch to the boss theme. If no device is available a `NullAudioService` is used.
* **Headless mode**: `HeadlessRunner` starts Avalonia with the headless platform and real Skia
  rendering, drives the actual view models (`--screenshot DIR` captures README screenshots,
  `--smoke-test` validates a build, including OpenAL initialization).

## Persistence formats

* Save games: `slot<N>.json` (`SaveFile` wrapper with format version, game version, name, timestamp,
  summary and the full `GameState`). On load the raw JSON is upgraded step by step by
  `SaveMigrations` (format 1 = releases 1.0-1.4, format 2 = 1.5+); corrupt files and saves from a
  newer game are rejected with a message instead of crashing. Overwriting an older-format save keeps a
  `.v<N>.bak` copy. Real saves from every release live in `tests/AVAMMB1.Tests/Fixtures/Saves` -
  add one whenever the format changes.
* Settings: `settings.json` (`GameSettings`), normalized on load (missing bindings restored).

## Build & release

`Directory.Build.props` sets `net10.0`, nullable, latest C#, embedded PDBs and
`TreatWarningsAsErrors`. `build-all.sh`/`build-all.ps1` publish single-file self-contained
ReadyToRun builds for six RIDs; `.github/workflows/release.yml` tests, builds on Windows/Linux/macOS
runners, smoke-tests each runner's native build and attaches archives to a GitHub Release on `v*` tags.
