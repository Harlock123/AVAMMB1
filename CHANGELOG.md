# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses semantic versioning.

## [1.3.0] - 2026-10-09

### Added
- The Ashen Hills: a second wilderness region (autumn woods, marsh, a wyvern roost), reached by a new
  mountain pass on the west edge of the Greenvale Wilds.
- Two new towns with full services: Thornwick (mining town, Deep Forge) and Duskmere (marsh village,
  herbalist).
- Three new dungeon levels: the Thornwick Mines (upper level and the Deep Delvings) and the Sunken
  Temple (flooded halls, a secret reliquary), with spinners, darkness, traps and an alarm.
- Two optional side quests: return the Heartstone to Foreman Halvard (slay the Stone Wyrm) and bring
  Sister Ilsa a Moon Lotus (slay the Drowned Hydra). Rewards: Heartfire Ring, Lotus Amulet,
  Wyrmguard Blade, Wyrmscale Mail, Moonsilver Blade, Evening Star.
- 16 new monsters (two bosses), 9 new items, 2 new shops; new CC0 Dungeon Crawl tiles listed in
  ASSETS_LICENSES.md. Tavern rumors and the crossroads signpost point to the new region.
- Tests: world connectivity, a walk-through of both side quests, and deterministic boss balance
  checks.

### Changed
- Boss balance: the Vault Warden is tougher (240 HP, AC 10) so the finale is a real fight for a
  level 6-7 party; previously a level 5 party won every time.

## [1.2.0] - 2026-10-09

### Added
- Smooth movement: steps and turns in the 3D view are animated (eased, ~0.15 s) instead of
  snapping. A new key press mid-animation completes the current move instantly. The renderer now
  supports a free camera (fractional position and any angle).
- Animated combat: idle breathing/bobbing (two tempos so groups don't move in lock-step), attack
  lunges, hit flash and shake, slain monsters fading out (kept on screen, not targetable), and a red
  flash/shake on party portraits that take damage. `CombatEngine.MonsterActed` event drives it.
- Settings toggles: *Smooth movement* and *Animated monsters in combat* (both on by default).
- Monsters in the 3D view: battles are staged in the first-person viewport (front rank large,
  back rank smaller behind; click a monster to target it) with the combat controls and log in the
  side panel instead of a pop-up window. Slain monsters fade out before the next rank steps up.
- Guardians of scripted encounters (chieftain, spiders, lich, ogre, Warden...) stand visibly in
  their squares, bobbing gently, until defeated. Sprites support an idle bob in the ray caster.

- Dungeon hazards: `spinner` events (silent random facing), terrain flags `darkness` (light is useless:
  view distance 1) and `antiMagic` (no spells, scrolls or wands for the party, no special abilities
  for monsters; potions still work), with messages when entering a zone and a status-line note.
- Trap effects: `teleport` (to a set square or a random reachable one), `pit` (damage, then a fall),
  `alarm` (summons the listed monsters or a group from the map's table), alongside damage/gas traps.
- Content: a spinner and a sleeping-gas trap in the cellars; two spinners, a teleport glyph, an
  alarm, a pit and a pocket of magical darkness in the crypt; an anti-magic corridor and a dark
  stretch in the Inner Vault.

### Fixed
- The selected combat target's red border never showed (local values overrode the style).
- A trap or spell that puts the whole party to sleep outside combat no longer causes a game over:
  the party sleeps for a while (risking an ambush) and wakes up.

## [1.1.0] - 2026-10-09

### Added
- Secret-door searching: `S` edges block like walls until found with the new Search action
  (default key F, *Search* button). The chance uses the best searcher's Intellect and Luck plus
  thievery; each search takes game time and may attract wandering monsters. Found doors render as
  doors, show in purple on the automap and persist in save games.
- Two hidden rooms with treasure: a smuggler's cache in the Brindlemoor Cellars and an embalmer's
  room in the Hollow Crypt, each with a hint near the hidden wall.
- *Save (F5)* button in the exploration screen; `14-secret-door` screenshot.

### Fixed
- The bundled OpenAL Soft library is now loaded in single-file builds (previously audio only worked
  where a system OpenAL was installed, so Windows and most Linux machines had no sound).
- Intermittent crash while saving `--screenshot` images.

## [1.0.0] - 2026-10-08

### Added
- `AVAMMB1.Core` rules engine: dice, characters (5 races, 6 classes, 7 attributes, alignment),
  progression and training, conditions, inventory/equipment, spells (28 learnable + item effects),
  turn-based combat with ranks and monster AI, map events and quest flags, random encounters,
  resting/food, town services, JSON save games and settings.
- Data-driven content in `Assets/Data`: 6 maps (Brindlemoor, Brindlemoor Cellars, Greenvale Wilds,
  Saltreach, Hollow Crypt, Inner Vault), 29 monsters, 53 items, 36 spell definitions, 4 shops,
  premade party and a complete quest line with a final boss and victory ending.
- `AVAMMB1.App` Avalonia UI: title, party creation, exploration with software ray-caster view,
  minimap/automap, combat, shops, inn (roster, save), temple, tavern, training, character sheet,
  spellcasting, save/load, settings with key rebinding, credits, victory/game-over screens.
- OpenAL Soft + NVorbis audio with CC0 music and sound effects; CC0 Dungeon Crawl Stone Soup tiles.
- Headless `--screenshot` and `--smoke-test` modes; README screenshots generated by the game.
- xUnit test suite; `build-all.sh` / `build-all.ps1` single-file publishing for win/linux/osx x64+arm64;
  GitHub Actions release workflow.
