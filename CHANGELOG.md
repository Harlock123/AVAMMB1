# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses semantic versioning.

## [Unreleased]

### Changed
- Balance simulator plays more like a person: it opens each zone's chests, uses healing potions in
  battle and cure potions in the field, keeps a few potions in stock, equips better loot and sells
  the rest. Its findings: chests add only 100-500 gold per zone, and from level 3 to 9 training ate
  all battle gold, leaving nothing for gear - so ordinary monsters of level 3-7 now carry 40% more
  gold and those of level 8-9 20% more.

### Fixed
- The README's verification table now records the maintainer's play-tests of the Windows ARM64
  and macOS Apple Silicon builds.

## [1.5.0] - 2026-10-09

### Added
- Save-game versioning and migration: save format 2 records the game version that wrote it and no
  longer stores computed values; older saves are upgraded step by step on load (`SaveMigrations`).
  Overwriting an older-format save keeps a backup (`slotN.json.v1.bak`). Saves from a newer game are
  refused with a clear message. Genuine saves written by every release (1.0.0-1.4.0) are test fixtures.
- Music and atmosphere: four new CC0 tracks (desert, marsh, caves, and a boss theme that plays
  whenever a unique boss is in the fight) and five CC0 ambient loops (birds, river, wind, dripping
  water, deep dungeon) that play under the music. Every town, wilderness and dungeon now has its own
  music and ambience (`ambience` map field, `boss` monster flag). New Ambience volume slider in Settings.
- Three new dungeon levels (19 maps in total), each with a boss, a secret room, hazards and unique
  rewards, using new CC0 DCSS tiles:
  - **The Old Cistern** (below the Brindlemoor cellars, levels 2-4): flooded reservoirs, eels and
    oozes, and the Mother of Oozes (drops the Drainwarden's Buckler).
  - **The Catacombs** (below the Hollow Crypt, levels 5-8): the Barrow Key in the ossuary opens the
    throne room of the Wight King (Gravewarden's Blade, Bone Ward amulet).
  - **The Sunless Deep** (below the Sun King's Chamber, post-game, levels 13-15): sealed until the
    Sun King falls; deep trolls, shades, vampire knights, bone dragons and the Umbral Wyrm
    (Umbral Blade, Ring of the Deep).
  - 17 new monsters and 6 new items; boss balance tests cover the three new bosses.

- Balance simulator (`tests/AVAMMB1.Tests/Balance`): an automated player that grinds each zone in
  route order - fighting with simple tactics, camping, curing with its own spells, visiting town to
  train, heal and buy gear, and reloading after a wipe - and reports battles, wipes, XP and gold flow
  per zone. Balance tests assert every zone is reachable at its intended level within bounds
  (about 600 battles from level 1 to 15, at most 120 per zone, almost no wipes) and that every zone
  pays its way.

### Changed
- Economy and pacing, tuned with the simulator (it showed training cost several hundred battles'
  worth of gold and some zones lost money to temple cures):
  - Training costs 12 x level^2 gold per character (was 50 x level^2).
  - Temple cures for conditions cost 15 x level (was 40 x level); healing and raising are unchanged.
  - Every ordinary monster now carries gold (about 2 + 1.5 x level^2 on average); bosses keep their hoards.
  - Level 5-8 monsters give more XP, so the Ashen Hills, the Catacombs and the Sunken Temple no
    longer need 90-130 battles per level.
- Paralyzed characters now have a 20% chance per round to recover during a battle (monsters
  already did), so a long fight against a paralyzing boss is no longer lost while the party is unhurt.
- Settings: the volume sliders now share a row with their labels.
- The headless screenshot script trains the demo party before each harder region so it no longer
  depends on lucky dice; new screenshot of the Old Cistern.

## [1.4.0] - 2026-10-09

### Added
- Per-character gold with a shared party purse. Loot and sales go to the purse; costs are paid from
  the purse first, then from the character being served (or everyone, for party-wide costs such as
  the inn, food and bribes). Character sheet: deposit, withdraw, deposit all, pool everyone's gold,
  share the purse evenly. Leaving a character at an inn asks how much purse gold they take along;
  they keep their own gold and bring it back when they rejoin. New characters carry their own
  starting gold (a new party pools it into the purse).
- 13 new spells (41 learnable in total), including spell level 6 (at caster level 11): Cleanse
  Fever, Searing Light, Free Movement, Greater Heal, Holy Word, Divine Restoration, Frost Needle,
  Weaken Armor (new `debuffArmor` effect), Deep Slumber, Prismatic Ray, and the tome-only Stone to
  Flesh, Chain Lightning and Iron Grip.
- Spell tomes (`tome` item kind): Chain Lightning is sold in Saltreach; Stone to Flesh and Iron Grip
  are hidden in the Sunken Temple and the Stone Wyrm's hoard.
- Interface scaling: the 1280x800 layout scales uniformly to any window size (Settings: *Fit
  interface to window*, on by default); the window can now be as small as 640x400.
- Gamepad support via SDL2 (Silk.NET.SDL, natives for all six platforms): exploration, combat and
  spatial menu navigation, hold-to-repeat movement, a Settings toggle and connection status.
  `--smoke-test` checks that SDL loads.
- The fifth town: Port Ashkar on the Sunscar Coast, reached by a 100-gold ferry from Saltreach
  (new `fare` option on teleports). The Sunscar Wastes (oasis, scorpion nest, sphinx shrine) and the
  two-level Tomb of the Sun Kings with the Sun King boss and the Sun Disk side quest, for levels
  9-12. 12 new monsters, 4 new items, 2 shops (the Grand Bazaar and House of Seven Flasks).
- Tests: gamepad mapping, ferry fares, the Sun Disk quest walk-through, sphinx / Sun King balance.

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
