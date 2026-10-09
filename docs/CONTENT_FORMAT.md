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
`torch` (`lightSteps`), `quest` (cannot be sold or dropped), `misc`.
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
`sprite` refers to `Graphics/Monsters/<sprite>.png`.

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
| `light` | `magnitude` steps of light |
| `locate`, `createFood` (`magnitude`), `recall` | Utility |

`target`: `none, ally, party, enemy, enemyGroup` (all monsters of the chosen kind), `allEnemies`.
`learnable: false` makes an item-only effect. Characters learn every spell of their school up to
their maximum spell level automatically.

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
| `music` | `title`, `town`, `dungeon`, `battle` (`Audio/Music/<key>.ogg`) |
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
| `inn`, `temple`, `tavern`, `training` | `name`, `priceFactor`; tavern `rumors[]` |
| `teleport` | `map`, `toX`, `toY`, `facing` |
| `treasure`, `quest` | `gold`, `gems`, `xp`, `items`, `consumeItem` |
| `encounter` | `monsters: [{ "monster": "kobold", "count": "1d4" }]`; `setFlag` is set on victory |
| `trap` | `trap` effect: `damage` (default: `damage` per member, `conditions` such as a sleeping gas), `teleport` (to `map`/`toX`/`toY`, or a random reachable square if none is given), `pit` (`damage`, then fall like a teleport), `alarm` (fights `monsters`, or a group from the map's encounter table). Robbers may disarm traps. |
| `spinner` | No fields: silently turns the party to a random facing (optional `text`) |
| `fountain` | `heal`, `restoreSp`, `conditions` cured |
| `victory` | Ends the game after showing `text` |

Several events can share a cell; they fire in order. `once` events are remembered in the save game.
`blocking` events stop the party from entering until their requirements are met (showing
`failText`). `feature` draws a billboard from `Graphics/Features`.

## Adding art or audio

Drop PNGs into the matching `Assets/Graphics/<folder>` (32x32 RGBA recommended; textures tile) and
Ogg Vorbis files into `Assets/Audio/Music` or `Assets/Audio/Sfx`, then reference them by file name
(without extension). Only use assets whose license allows redistribution (CC0/CC-BY), and add them
to `ASSETS_LICENSES.md` and `CREDITS.md`.
