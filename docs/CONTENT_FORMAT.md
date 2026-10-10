# Editing AVAM&M Content

All game content lives in `Assets/Data` as JSON. The files are embedded into the game at build
time, so after editing either **rebuild**, or **mod without rebuilding**:

* copy `Assets/Data` to a folder called `Content` next to the `AVAMMB1` executable, or
* start the game with `AVAMMB1 --content path/to/Data`.

Content is validated on load: unknown ids, bad dice, events in walls, unreachable teleport
destinations and similar mistakes stop the game with a clear message (and the unit tests check that
every map event is reachable). JSON comments and trailing commas are allowed. Property names and
enum values are case-insensitive.

**Dice** are written as strings: `"3d6"`, `"1d8+2"`, `"d4-1"`, or a plain number `"30"`.

**Conditions** (flags, comma separated): `asleep, blinded, silenced, poisoned, diseased, paralyzed,
unconscious, dead, stoned`. **Elements**: `physical, fire, cold, electric, acid, magic, holy, poison`.
**Stats**: `might, intellect, personality, endurance, speed, accuracy, luck`.

## game.json

| Field | Meaning |
|---|---|
| `title`, `intro`, `victoryText` | Text shown at the start and end |
| `startMap`, `startX`, `startY`, `startFacing` | Where new games begin |
| `startingGoldPerMember`, `startingFood`, `maxFood` | Economy |
| `premades` | Characters for *Quick Party*: `name, race, class, sex, alignment, stats{}` |
| `hirelings` | Adventurers for hire at inns: `id, name, race, class, sex, alignment, stats{}, level, town` (a town map with an inn), `wage` (gold a day), optional `hair`, `beard`, `blurb` |

## races.json

```json
{ "id": "elf", "name": "Elf", "portrait": "elf", "description": "...",
  "statModifiers": { "intellect": 2, "endurance": -2 }, "resistances": { "magic": 10 } }
```
`portrait` selects `Graphics/Portraits/<portrait>_<male|female>.png`.

## classes.json

| Field | Meaning |
|---|---|
| `hitDie` | HP die rolled per level (max at level 1) |
| `requirements` | Minimum final stats, e.g. `{ "might": 15 }` |
| `allowedAlignments` | Empty = any |
| `spellSchool`, `spellStartLevel` | `cleric` or `sorcerer`; level spellcasting begins |
| `attackPerLevel`, `missileBonus`, `extraAttackEvery` | Combat progression |
| `xpBase` | XP table: level L needs `xpBase * (2^(L-1) - 1)` (linear after 12) |
| `thievery` | Base trap-disarm chance (0 = none) |
| `startingItems` | Item ids, equipped automatically |
| `portraitLayers` | Paper-doll images drawn over the race portrait (`{sex}` placeholder allowed) |

## items.json

```json
{ "id": "long_sword", "name": "Long Sword", "kind": "weapon", "icon": "long_sword", "price": 50,
  "damage": "1d8", "hitBonus": 0, "damageBonus": 0, "armorClass": 0, "twoHanded": false,
  "classes": ["knight","paladin","archer"], "statBonuses": { "might": 2 },
  "useSpell": null, "charges": 0, "lightSteps": 0, "foodUnits": 0, "description": "" }
```
`kind`: `weapon, missile, armor, shield, helmet, gloves, boots, ring, amulet` (equippable),
`potion, scroll, wand` (cast `useSpell` when used; wands use `charges`), `food` (`foodUnits`),
`torch` (`lightSteps`, `lightRadius`), `lantern` (equipped in the Light slot; `lightRadius`, `fuelCapacity` steps of
oil kept in the item's charges - set `charges` to the same value so a new lantern comes full, or `fuelCapacity` 0 for
a lantern that never needs oil), `oil` (`fuelAmount` steps added to a lantern; give it a combat `useSpell` to make it
throwable), `tome` (reading it permanently teaches `teachSpell` to a caster of that
school who can already cast its level; the tome is consumed), `quest` (cannot be sold or dropped),
`misc`.
`icon` refers to `Graphics/Items/<icon>.png`.

## monsters.json

```json
{ "id": "orc_hexer", "name": "Orc Hexer", "plural": "Orc Hexers", "sprite": "orc_hexer",
  "level": 4, "hitPoints": "3d8", "armorClass": 3, "speed": 11, "xp": 60, "gold": "3d10",
  "attacks":   [ { "verb": "jabs", "damage": "1d4", "ranged": true,
                   "element": "physical", "inflicts": "poisoned", "inflictChance": 0 } ],
  "abilities": [ { "name": "a hex bolt", "chance": 35, "damage": "2d6", "allTargets": false,
                   "element": "magic", "inflicts": "asleep", "selfHeal": "0" } ],
  "drops": [ { "item": "scroll_light", "chance": 20 } ],
  "resistances": { "fire": 50 }, "undead": false, "regenerates": 0,
  "cowardly": false, "smart": true, "bribable": false }
```
Non-`ranged` attacks only work from the front rank (first three monsters). `smart` monsters target
the weakest member; `cowardly` ones may flee when badly hurt. Negative resistance = vulnerability.
`sprite` refers to `Graphics/Monsters/<sprite>.png`. Any non-boss monster can appear as an **elite** in a random encounter (5% of encounters): double
hit points, +2 armor class, +50% damage, triple experience and gold, and a 50% chance of extra loot
chosen by its level. Set `"boss": true` on unique bosses: any battle that
includes one plays the boss theme instead of the normal battle music.

## Class abilities

`classes.json` entries may list `abilities`: `guard`, `layOnHands`, `aimedShot`, `sneakAttack`,
`pickLocks` (knight, paladin, archer and robber have them by default). Maps can set
`"masterLocks": true` so their locked doors cannot be picked.

## spells.json

```json
{ "id": "s_fireball", "name": "Fireball", "school": "sorcerer", "level": 3, "cost": 5,
  "effect": "damage", "target": "enemyGroup", "amount": "4d6", "perLevel": 1, "element": "fire",
  "combat": true, "explore": false, "learnable": true, "description": "..." }
```
| `effect` | Uses |
|---|---|
| `damage` | `amount` (+`perLevel` x caster level), `element`, `undeadOnly` |
| `heal`, `restoreSp` | `amount`, `perLevel` |
| `cure` | `conditions` removed |
| `inflict` | `conditions` applied to enemies (saving throw vs caster level) |
| `raise` | Dead ally back to 1 HP |
| `buffArmor`, `buffHit` | `magnitude` bonus for the battle |
| `debuffArmor` | Enemies lose `magnitude` armor class for the battle |
| `light` | `magnitude` steps of light, reaching `lightRadius` squares (default 6) |
| `locate`, `createFood` (`magnitude`), `recall` | Utility |

`target`: `none, ally, party, enemy, enemyGroup` (all monsters of the chosen kind), `allEnemies`.
`learnable: false` makes an item-only effect. Characters learn every spell of their school up to
their maximum spell level automatically (spell level = (caster level + 1) / 2, up to 6), except
spells marked `"tome": true`, which are learned only by reading a `tome` item. A `cure` spell whose
`conditions` include `stoned` can target a petrified ally.

## quests.json (optional)

The quest journal. A quest appears once its **first** stage is reached; each stage is reached by
exactly one of a story `flag`, an `item` carried by anyone in the party, or a map `visited`. The
journal shows every reached stage (the last one is the current goal) and `doneText` once `doneFlag`
is set. Nothing extra is stored in saves - the journal is rebuilt from flags, items and the automap.

```json
{ "id": "heartstone", "title": "The Heartstone of Thornwick", "main": false,
  "doneFlag": "heartstone_returned", "doneText": "Foreman Halvard has the Heartstone back...",
  "stages": [
    { "flag": "halvard_met", "text": "A wyrm took the Heartstone. Bring it back." },
    { "item": "heartstone", "text": "Return the Heartstone to Foreman Halvard." },
    { "visited": "mines2", "text": "..." } ] }
```

**Where next?** The journal and automap mark where each open quest leads. The game works this out
from the map events: the place that reaches the *next* stage (an event that sets its flag, a chest or
guardian that gives its item - a hoard locked behind a flag points at whatever sets that flag - or a
passage to its map), otherwise the place that wants what the current stage gave (`requiresFlag` /
`requiresItem`). When that guess is wrong, give the stage an explicit goal:
`{ "item": "belfry_key", "text": "...", "goal": { "map": "hills", "x": 17, "y": 7, "name": "The Hollow Belfry" } }`.

## shops.json

```json
{ "id": "brindle_smithy", "name": "Hammer & Tongs Smithy", "greeting": "...",
  "priceFactor": 1.0, "stock": [ "dagger", "long_sword" ] }
```

## Maps (`Maps/*.json`)

| Field | Meaning |
|---|---|
| `id`, `name`, `kind` | `kind`: `town`, `outdoor`, `dungeon` |
| `format`, `width`, `height`, `grid` | See below |
| `terrain` | Legend: character -> `{ solid, opaque, texture, floor, feature, name, mapColor, darkness, antiMagic }` |
| `wallTexture`, `floorTexture`, `ceilingTexture` | `Graphics/Textures/<key>.png`; no ceiling = sky |
| `skyColor` | Hex color for the sky gradient |
| `dark` | Needs light to see further than one cell |
| `music` | Track key: `title`, `town`, `dungeon`, `caves`, `desert`, `marsh` (`Audio/Music/<key>.ogg`; `battle` and `boss` are used for fights) |
| `nightEncounters` | Outdoor maps: extra encounter entries (same format as `encounters`); at night half of the encounters come from this list |
| `ambience` | Optional looping ambient sound under the music: `birds`, `river`, `wind`, `drips`, `deep` (`Audio/Ambience/<key>.ogg`) |
| `encounterChance`, `encounters` | Percent per step; weighted `{ monster, count, weight }` |
| `lockedDoorKey`, `lockedDoorFlag` | Item or flag that opens `L` doors |
| `events` | See below |

### Grid formats

**`blocks`** - `height` rows of `width` characters; each character is a cell looked up in
`terrain` (`.` = plain floor). An unlisted `#` is a solid wall.

```
"################",
"#......#.......#",
"#.BBB..#..###..#",
```

**`edges`** - `2*height+1` rows of `2*width+1` characters. Cells sit at odd row/column positions,
the characters between them are walls: `-` or `|` wall, `D` door, `L` locked door, `S` secret
door, space = open. `+` corners are ignored.

A secret door looks and blocks exactly like a wall until the party searches next to it (Search,
default key F). It is then drawn as a door, shown in purple on the automap, and can be walked
through; discoveries are stored in the save game. A good pattern is a dead-end alcove whose only
opening is an `S`, with a once-only `message` hint event on the cell outside it.

```
"+-+-+-+",
"|. .D.|",     <- cell (0,0) open to (1,0); door between (1,0) and (2,0)
"+ +-+ +",
"|.|. .|",
"+-+-+-+"
```

Coordinates: `x` grows east, `y` grows south, `(0,0)` is the north-west corner.

In `edges` maps the cell characters can also refer to the terrain legend, which is how special
squares are marked. For example with `"terrain": { "d": { "darkness": true }, "a": { "antiMagic": true } }`
a cell written as `d` is magical darkness (light sources and spells do not help: the party sees
one square) and `a` is anti-magic (no spells, scrolls or wands for the party and no special abilities
for monsters; potions still work). Both show their `mapColor` on the automap once explored.

### Events

```json
{ "x": 3, "y": 13, "type": "quest", "id": "pell_ember", "once": true,
  "name": "Archivist Pell's Study", "text": "...", "failText": "...",
  "requiresFlag": "pell_met", "requiresNotFlag": null, "requiresItem": "ember_shard",
  "consumeItem": true, "setFlag": "ember_given", "blocking": false,
  "gold": "100", "gems": 0, "xp": 150, "items": ["crypt_key"], "feature": "shop_books" }
```

| `type` | Extra fields |
|---|---|
| `message` | `text` (shown as a story dialog) |
| `shop` | `shop` id |
| `inn`, `temple`, `tavern`, `training`, `academy` | `name`, `priceFactor`; tavern `rumors[]`. An academy sells permanent statistic points: 1000 x (points already bought + 1) gold x `priceFactor`, at most 10 per character and 25 per statistic. |
| `teleport` | `map`, `toX`, `toY`, `facing`; optional `fare` (gold the party pays, e.g. a ferry - travel fails with `failText` if it can't pay) |
| `treasure`, `quest` | `gold`, `gems`, `xp`, `items`, `consumeItem` |
| `encounter` | `monsters: [{ "monster": "kobold", "count": "1d4" }]`; `setFlag` is set on victory |
| `trap` | `trap` effect: `damage` (default: `damage` per member, `conditions` such as a sleeping gas), `teleport` (to `map`/`toX`/`toY`, or a random reachable square if none is given), `pit` (`damage`, then fall like a teleport), `alarm` (fights `monsters`, or a group from the map's encounter table). Robbers may disarm traps. |
| `spinner` | No fields: silently turns the party to a random facing (optional `text`) |
| `fountain` | `heal`, `restoreSp`, `conditions` cured |
| `victory` | Ends the game after showing `text` |
| `riddle` | Asks `text`; the party types an answer. Any of `answers` (case, punctuation and a leading "a"/"an"/"the" ignored) shows `successText`, sets `setFlag` and grants the rewards (`gold`, `gems`, `items`, `xp`); a wrong answer shows `failText` and deals `penalty` dice to everyone (optional). |
| `choice` | Shows `text` and two to four `options`: `{ "label": "...", "text": "...", "setFlags": [...], "gold": "...", "gems": 0, "items": [...], "xp": 0 }`. The party may decide later; once chosen, only that option happens. |

Several events can share a cell; they fire in order. `once` events are remembered in the save game.
`blocking` events stop the party from entering until their requirements are met (showing
`failText`). A map's optional `weather` ("snow" or "rain") falls over its outdoor view. An event with `openAt` ("night" or "day") works only then, showing `failText` otherwise; a town's `nightEncounters` (with `nightEncounterChance`, per step) are its streets after dark. `feature` draws a billboard from `Graphics/Features`; with `openWhenMet` it is drawn only
while the requirements are unmet - a portcullis that disappears once its lever (a `message` event with
`setFlag`, e.g. `"feature": "lever"` or `"pressure_plate"`) has been pulled.

## Adding art or audio

Drop PNGs into the matching `Assets/Graphics/<folder>` (32x32 RGBA recommended; textures tile) and
Ogg Vorbis files into `Assets/Audio/Music` or `Assets/Audio/Sfx`, then reference them by file name
(without extension). Only use assets whose license allows redistribution (CC0/CC-BY), and add them
to `ASSETS_LICENSES.md` and `CREDITS.md`.
