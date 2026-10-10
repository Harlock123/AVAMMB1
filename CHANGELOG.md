# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses semantic versioning.

## [Unreleased]

## [1.12.0] - 2026-10-10

### Added
- Installers and packages alongside the archives: a Windows installer (per-user, no admin rights,
  Start-menu and optional desktop shortcut, uninstaller), a macOS disk image and a Linux AppImage, for
  every platform and architecture. CI builds them and smoke-tests the installed / mounted / packaged
  game. They are not code-signed. Made with `packaging/package.sh` and `packaging/package.ps1`.
- The Daily Challenge (title screen): six ready heroes at level 15 - their gear smithed to +2, with an
  everburning lantern and potions - start straight in that day's Depths Below. Every level is the same
  for everyone on that date (fights and loot still roll for each player); it is played by ironman
  rules (one self-kept save, so a run can be continued the same day). Climbing out by any stair up
  ends the run, and the depth reached goes into the Hall of Fame, which keeps each day's best and the
  best ever.
- Towns by time of day: Port Ashkar's Night Market opens only after dark (rare goods, at a price);
  thieves work the streets of every town at night - cutpurses in Brindlemoor and Saltreach, footpads
  in Thornwick and Duskmere, Guild Blades in Port Ashkar and Wintermere; temples ask a quarter less for
  healing at dawn (05:00-08:00); and each tavern has a rumour of the day instead of a new one every
  round. Map events can be `openAt` "night" or "day", and towns can have `nightEncounters` with a
  `nightEncounterChance`.
- A travel map (T, or Travel map in the menu): from any town or the open country, travel to a town
  you have visited. The road is planned through towns and open country, time passes as if it were
  walked (rations, lantern oil and poison included; a sea voyage takes a day or so), ship fares are paid, and each stretch of open
  country may hold an ambush that ends the journey there.
- Describe surroundings (L, or Describe surroundings in the menu for controllers): the log says where
  the party is and which way it faces, what lies ahead ("open for 6 squares, then water"), to either
  side and behind, and what can be seen - places, stairs, treasure, riddle doors, guardians - with
  distances. Settings > Accessibility can also describe each step briefly; screen readers read the log.
- New spells. Utility: Reveal Secrets (sorcerer, level 3 - secret doors within three squares),
  Levitate (sorcerer, level 4 - float over floor traps and pits for a while) and Sense Minds (cleric,
  level 3 - what guardians and lairs wait on this level, and which way). And a seventh circle, learned
  only from tomes sold in Wintermere or found in the Depths Below: Starfall (sorcerer - every foe) and
  Divine Intervention (cleric - the whole party healed). Casters reach the seventh circle at level 13.
- Class abilities for the casters. Clerics: Turn Undead (V, once per battle) - each undead foe may
  flee (40% + 8% per level the cleric has over it; turned undead count as beaten for experience and
  gold), and undead five or more levels weaker crumble to dust; unique undead are unmoved. Sorcerers:
  Overcharge (Y) - cast a spell at half again the sorcerer's level, for double the spell points.
  Auto-fight turns undead when two or more are present.
- An automated playthrough test: following only the journal's "Where next?" goals, with real movement
  through the map connections, a party finishes every quest - main and side - in one run. It guards the
  whole game against content that can no longer be completed.

### Fixed
- "Where next?" could point at a place behind a locked door the party had no key for (the Barrow
  Throne for the Barrow-King's bounty); it now leads to whatever gives the key first.

## [1.11.0] - 2026-10-10

### Added
- The Depths Below, an endless post-game dungeon: once the Umbral Wyrm is dead, a stair beneath its
  lair in the Sunless Deep leads down into generated levels - a new 16x16 maze each descent, with a
  stair down at its far end, hoards, a guarded lair, traps, patches of magical darkness and, every
  third level, a healing spring. The first level is about as hard as the Sunless Deep; from the fourth,
  monsters gain 10% hit points and damage a level, and pay 10% more experience and gold. A save made
  down there brings back the very same level. The deepest level reached is kept in the Chronicle and
  the Hall of Fame, and reaching level 10 earns Deep Delver.
- Weather and spell effects: snow falls over the Frostmark and Wintermere and rain over the Duskmere
  marsh (drawn by the 3D view; Settings > Display turns it off), and in battle every spell and breath
  attack washes the scene with its colour - fire orange, cold blue, magic violet, healing green. Maps
  set `weather` ("snow" or "rain") in their data.
- Controller-only play: pressing A on any text box (character names, renaming at the inn, riddle
  answers, automap notes) opens an on-screen keyboard - the D-pad moves between keys, A types, B
  cancels, Done finishes. Character creation and the inn also have a Random name button (names to
  suit each race).
- Hall of Fame (title screen): every victory, and every ironman party that falls, is recorded with
  its party, difficulty, days, play time, monsters slain and achievements; achievements earned in any
  game are collected there too. It lives beside the settings, not in a save.
- Ironman mode (a checkbox when creating the party): the game keeps a single save itself - written as
  you go, after every battle and when you quit - quick load is disabled, and if the whole party falls
  the save is deleted and the run goes into the Hall of Fame. A new achievement, Iron Will, is for
  finishing the main quest in ironman mode.
- Smithing: the smithies of Brindlemoor, Saltreach, Thornwick, Port Ashkar and Wintermere improve
  weapons (+1 to hit and damage per step) and armor, shields, helmets, gloves and boots (+1 AC per
  step), up to +5. Each step costs gold - (price + 200) x step squared - and as many gems as the step,
  giving late-game gold and gems a use. Improved items show their bonus ("Long Sword +2"), count in
  shop comparisons and sell for more. Shops have a Smithing list next to selling.

### Changed
- Hard is now a harder climb as well as a costlier one: bosses have 40% more hit points (other
  monsters 30%), elite-led encounters are twice as common, and battles give 10% less experience. The
  simulated playthrough takes about 5% more battles on Hard, and every boss stays beatable on Hard by
  a party two levels above its intended level.
- Code: the game session (about 1,400 lines) is split by area into partial-class files - core state,
  movement and time, map events, quests, puzzles, and combat - with no change in behaviour.

## [1.10.0] - 2026-10-10

### Added
- Puzzles and choices. Riddle doors (type the answer: the Door of the Scribe in the Tomb of the Sun
  Kings, the Silent Door in the Hollow Belfry; wrong answers hurt), a pressure plate that opens a hidden
  alcove in the Brindlemoor cellars, and a lever that raises a portcullis in the upper mines. Choices
  with consequences: Sister Veyl asks you to keep her secret or hand her to the Watch, and the Choir's
  tithe can be kept or sent home to the six towns (who thank you for it). New event types `riddle` and
  `choice`, and `openWhenMet` gates, for mod packs (see CONTENT_FORMAT.md).
- The Chronicle (a new Journal tab): the story so far in numbers - days in the field, time played,
  steps, monsters slain, unique monsters defeated, battles won and fled, companions fallen, gold found
  and the most held, chests opened, secret doors found, locks picked, spells cast, nights at inns - and
  22 achievements (First Blood, Wayfarer, Keeper of Secrets, Bounty Hunter, Hard Won...), announced
  in the log as they are earned and kept with each saved game.
- A bestiary and an item compendium (new Journal tabs). The bestiary has every creature the party has
  defeated, with its picture, level, hit points, armour, attacks and special abilities, defences
  (immunities, resistances, weaknesses), how many you have slain and the explored places where it is
  found. The compendium has every item the party has carried or seen in a shop, with its stats, value
  and who can use it.
- "Where next?": the journal shows where each open quest leads ("Next: Loremaster's Hall, Thornwick
  (7,9)"), and the automap and minimap mark it with a diamond and list every quest's next place. The
  game works it out from the map events (and follows a locked hoard back to its guardian); quest stages
  can also name a `goal` (see CONTENT_FORMAT.md), and the content check validates it. Settings > Game
  turns the markers off.
- Character looks: choose a hairstyle (18, which leaves the helmet or hood off) and a beard (8) for
  each portrait when creating a character, and change name and looks later at any inn ("Name &
  looks"). Names stay unique, trimmed and at most 16 letters. The hair and beard tiles are CC0 Dungeon
  Crawl paper-doll layers.
- Accessibility: an interface zoom (100-150%) for big or high-resolution screens when the interface
  is not fitted to the window; a battle text speed (Instant, Fast, Normal, Slow) that shows battle
  messages one line at a time (acting, or Continue, shows the rest; auto-fight waits for the text);
  and a LOW badge with bold hit points on the card of a badly hurt character, so it does not depend
  on colour.
- The Silent Choir, a mid-game quest chain (levels 8-12) through all six towns. Magistrate Holloway
  in Brindlemoor, Loremaster Durin in Thornwick (a ledger to take from the Choir's bursar in the Deep
  Mines), the Saltreach Customs House, Sister Veyl in Duskmere, the Sealed Archive in Port Ashkar (a
  key to take from Keeper Isaura in the Tomb of the Sun Kings), and Archmage Sela in Wintermere.
  It leads to the Hollow Belfry, a new two-level dungeon in the east of the Ashen Hills, and its boss,
  the Choirmaster. New: 10 monsters, 4 unique items (Bellbreaker, the Choirmaster's Cowl, the Signet
  of the Concord, the Ward of Voices) and 2 new wall textures, all from the CC0 Dungeon Crawl tiles.
- Autosave: entering a new area and facing a boss saves into three rotating autosave slots (Auto 1-3,
  kept apart from the quick and manual slots; Settings > Game turns it off). Continue picks up the
  newest save, autosaves included.
- Saved games show a picture of the view and the time played; the Load screen lists only real
  saves, newest first.
- Shops compare gear with what the shopper wears ("Better than Brannoc's Long Sword (Dmg 1d8 ->
  2d4+1)", upgrades in green), and "Sell junk" sells every backpack weapon, armour, ring or amulet
  nobody in the party could use as an upgrade (its tooltip lists them; lanterns, consumables and
  quest items are never junk). Such items are marked "not needed by anyone".
- Difficulty: Easy (monsters -25% HP and damage, +25% gold, fewer encounters), Normal, or Hard
  (+30% HP, +25% damage, -15% gold, more encounters). Chosen next to "Begin the Adventure" and
  changeable any time in Settings > Game; saved with each game.
- Survival mode (optional, off by default): besides resting, everyone eats 1 food per day on the
  clock (a rest or a night at an inn counts as that day's meal). Without food a character loses a
  tenth of their HP each day - hunger never kills, but leaves them weak. Warns at 2 days of food.
- Change the marching order without a mouse: the character sheet has Move left / Move right buttons
  (keys `[` and `]`; a controller reaches the buttons like any other). The sheet follows the
  character as they move.

### Changed
- Balance check of the mid-game: the simulated playthrough now includes the Hollow Belfry and runs on
  Easy and Hard as well as Normal (all three finish the route; Hard costs about 15% less gold, twice
  the potions and a third less gear). The Silent Choir's two lieutenants were too easy for mini-bosses
  and are now real fights: Bursar Quill (party level 7; 200 HP) and Keeper Isaura (level 10; 320 HP,
  now guarded by two Choir Cantors). Survival mode's food cost was measured: a rest is the day's meal,
  so camping parties pay nothing extra, and a party that never camps eats one ration each a day -
  about 6 gold a day for six.
- macOS Intel (osx-x64) builds are now verified (play-tested on an Intel Mac).

### Fixed
- Several README screenshots taken after the Choirmaster fight showed the game-over screen (the
  screenshot tour now wins that fight and refuses to capture a game-over screen by mistake).
- The Run and Bribe buttons in battle now show their keys, like Fight: "Run 50% (R)", "Bribe 120 gold (B)".

## [1.9.1] - 2026-10-09

### Fixed
- Pressing Use on a lantern (or any weapon or armour) on the character sheet now equips it, instead of
  saying it "cannot be used like that". Equipping a lantern says how far it lights and how to refill it.
- A Flask of Oil also fills a lantern that is still in a backpack (not only an equipped one), and a
  full lantern explains that the flask should be kept until it burns down.
- The rest message and in-game help now say when food is eaten (1 unit per character per rest;
  travelling and inn stays use none).

## [1.9.0] - 2026-10-09

### Added
- Drag and drop to change the marching order: drag a party card along the bar at the bottom of the
  screen and drop it on another place (the others shift along). Works anywhere outside battle, not
  just at an inn; a click on a card still opens the character sheet.
- Keyboard picking for battle spells and items: each entry in the list shows its key (1-9, then A-Z
  for longer spell lists) and pressing it picks that spell or item. The number pad works too, and a
  spell that cannot be cast says why (e.g. not enough SP).
- Audio polish. The Frostmark and the Rime Halls have their own music ("Beyond the Frozen Veil");
  crickets replace birdsong outdoors at night; footsteps sound like the ground underfoot (snow and
  ice, water, grass and earth, stone); lighting a torch, filling a lantern, picking a lock and a
  knight's guard each have a sound.
- Class abilities. Knights: Guard a companion for the round - attacks aimed at them strike the
  knight, who counts as blocking. Paladins: Lay on Hands once per battle (heals 3 x level + 5 and
  cures poison). Archers: Aimed shot (+4 to hit, double damage, not two rounds running). Robbers:
  sneak attack (double damage in the first round) and lock-picking - walking into a locked door makes
  the best robber try (35% + 5% per level, luck helps, up to 95%); story locks (`masterLocks` on a
  map, e.g. the Barrow-King's throne room) still need their key. Combat has an ability button (G / L
  / T, RB on a controller); auto-fight uses Lay on Hands and Aimed shots. Abilities are class data
  (`abilities` in classes.json), so mod packs can grant them.
- Day and night. A clock runs as you travel (3 minutes a step, 8 hours a rest; a night at the inn
  lasts until 07:00), shown under the gold. At dusk the sky darkens; at night (20:00-05:00) the open
  country and towns are dark: sight outdoors drops to 4 squares unless your light reaches further
  (lanterns burn outdoors at night, not in towns), encounters are half as likely again, elites twice
  as common, and the wilds, the Ashen Hills, the Sunscar Wastes and the Frostmark have night-only
  monsters (`nightEncounters` in map data). Shops, training grounds and academies close at night.
  Saves from earlier versions get a clock from their step count.
- Mod packs: put a folder with a `pack.json` and any monsters, items, spells, shops, quests, races,
  classes, maps, graphics or audio into the `Mods` folder. Definitions merge by id (new ids add,
  existing ids replace), `mapPatches.json` adds events to existing maps, and pack graphics and audio
  take priority over the built-in files. A Mods screen (title screen) switches packs on and off;
  `--check-content` validates the game and its packs and names the pack at fault; saves record the
  packs they were made with. A guide (docs/modding/MODDING.md) and an example pack (The Old Well)
  are included.

## [1.8.0] - 2026-10-09

### Added
- Lanterns and oil (Angband style). A Brass Lantern (150 gold) is equipped in the new Light slot and
  lights the way 8 squares ahead, with a warm glow, for the whole party. It holds 1500 steps of oil,
  burns only in dark places, and warns when it runs low. A Flask of Oil (4 gold) adds 750 steps -
  or is thrown in battle for 2d6 fire damage. The Everburning Lantern, which needs no oil, is hidden
  in the Rime Halls. Oil is also found in early chests and on low-level elite monsters.
- The character sheet shows a lantern's oil; the top-right counter shows lantern oil and torch light.

### Changed
- Controller in battle: RB is now the class ability (LB or left / right still change target).
- Brightness tiers in dark places: no light 1 square, torch 5, light spells and the Scroll of Light 6,
  lantern 8 (before, any light showed 7). The automap now also records the corridor ahead, as far as
  your light reaches, and updates when you turn.
- The help's Light section covers the tiers, and a new Lanterns and oil section explains lanterns.

## [1.7.1] - 2026-10-09

### Changed
- Light lasts longer: torches 150 steps (was 100), Holy Light 200 (was 150), Glowlight 250 (was 200)
  and the Scroll of Light 400 (was 300).
- The in-game help has a Light section: how to use a torch, the light spells and scroll, the Light
  counter, and magical darkness.

## [1.7.0] - 2026-10-09

### Added
- "What's new" screen: after an update the game shows the release notes for every version you have
  not seen yet (once); the title screen's *What's new* button shows them all. The notes are read
  from the CHANGELOG.md that ships inside the game.
- Help screen (F1 or H, the title screen, or the game menu): how to play, plus your current keyboard
  and controller controls, including any you have rebound.
- The Frostmark, a sixth town and region for levels 11-14, by ship from Saltreach (200 gold):
  Wintermere (inn, temple, training, an outfitter selling top-tier gear, and an academy), the
  Frostmark tundra and the two-level Rime Halls with the ice dragon Rimefang. Two quests: the Jarl's
  hunt for Rimefang's heart and Scholar Aldous's lost expedition. 9 new monsters, 14 new items and
  23 new CC0 DCSS tiles (the snow floor is a lightened DCSS tile).
- A bounty in every older town: bring a trophy from a monster's hoard (the Greenvale ogre, the
  wyvern roost, the renegade chief, the Barrow-King, the Emperor scorpion) for gold, experience and
  gear. All new quests appear in the journal.
- Academies (Wintermere and Port Ashkar): pay for permanent statistic points, 1000 x (points
  already bought + 1) gold each, up to 10 per character - a sink for late-game gold.
- Detailed textures (Settings > 3D view): 128 x 128 walls and floors from Screaming Brain Studios'
  CC0 Tiny Texture Pack for the towns and most dungeons, hand-matched to each place; distinctive
  classic textures (the Inner Vault, the mines, the Sunless Deep, ice, rock, sand, water, doors) stay
  as they are. Textures larger than 32 pixels are mipmapped so distant walls and floors do not shimmer.
- Elite monsters: 1 random encounter in 20 is led by an elite (gold name) with double hit points,
  +2 armor class and +50% damage, worth triple experience and gold and possibly extra loot.

### Fixed
- Encounter messages use the right article ("an Ice Beast", "The Sun King" rather than "a The Sun King").
- The balance simulator no longer fills its backpacks with consumables it never sells (which had
  stopped it buying gear); it now buys gear in the late game and studies at academies with gold to spare.

## [1.6.0] - 2026-10-09

### Added
- Quest journal (J, or from the game menu): the main quest and every side quest you have heard of,
  with the story so far and the current goal; completed quests listed last. A Clues tab collects
  the signs, inscriptions and warnings you have read. Quests are data (`quests.json`): stages are
  reached by a story flag, an item carried or a map visited.
- Automap notes: click any square on the automap (or press N for your own square) and write a note;
  notes show as a small marker on the automap and minimap and are saved with the game.
- Combat: *Repeat* (E / LT) replays every character's last action for the rest of the round,
  choosing a new target if the old one fell; *Auto* (O / RT) fights with weapons and healing (spells
  and potions) at a watchable pace and pauses if anyone drops below a quarter of their hit points.
- Monster knowledge: once you have defeated a kind of monster, the combat screen shows the selected
  target's level, HP, armor class, whether it is undead, and its immunities, resistances and
  weaknesses.

- Accessibility: colour themes (Standard, High contrast, Colour-blind friendly using the Okabe-Ito
  palette - good news blue, bad news orange), text size 90-130%, and rebindable controller buttons
  for exploring (Settings > Controller: press Rebind, then the button; Start always opens the menu).
- 3D view resolution (classic 400 x 300, 640 x 480, 800 x 600) and optional smooth scaling. The smoke
  test reports render times; in a Release build on an ARM64 Linux machine 800 x 600 takes about
  4 ms per frame.

### Changed
- The game screen's button bar has Journal and Note buttons; the message log is now headed "Log".
- Settings are split into General, Keyboard and Controller tabs.
- All interface colours and font sizes are theme resources (`AVAMMB1.App/Theming/Theme.cs`).
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
