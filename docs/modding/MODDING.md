# Modding AVAM&M

AVAM&M's content (monsters, items, spells, shops, quests, races, classes and maps) is plain JSON.
A **mod pack** adds to the base game - or changes parts of it - without replacing anything else,
and without rebuilding the game.

## Where packs go

Each pack is one folder inside a `Mods` folder. The game looks in:

- your data folder's `Mods` (shown on the **Mods** screen; *Open folder* opens it):
  Windows `%APPDATA%\AVAMMB1\Mods`, macOS `~/Library/Application Support/AVAMMB1/Mods`,
  Linux `~/.local/share/AVAMMB1/Mods`;
- a `Mods` folder next to the game executable;
- any folder given with `--mods <folder>` on the command line.

Packs load at start-up in folder-name order. Switch them on or off on the **Mods** screen (title
screen) and restart.

## What a pack contains

| File | Purpose |
|---|---|
| `pack.json` | **Required.** `{ "id": "my-pack", "name": "My Pack", "version": "1.0", "author": "...", "description": "..." }`. The id is recorded in save games. |
| `monsters.json`, `items.json`, `spells.json`, `shops.json`, `races.json`, `classes.json`, `quests.json` | Lists in the same format as the game's own files (see [CONTENT_FORMAT.md](../CONTENT_FORMAT.md)). An entry with a **new id is added**; an entry with an **existing id replaces** the game's (later packs win). |
| `Maps/*.json` | New maps (or a map with an existing id, which replaces it). |
| `mapPatches.json` | Events added to existing maps - this is how a pack links its new places into the world without replacing the base maps: `[ { "map": "brindlemoor", "addEvents": [ { "x": 13, "y": 4, "type": "teleport", ... } ] } ]` |
| `Graphics/...png` | Images, by the same paths as the game's `Assets/Graphics` (e.g. `Graphics/Monsters/my_monster.png` for a monster whose `sprite` is `my_monster`; `Graphics/Textures/...`, `Graphics/Items/...`, `Graphics/Features/...`). A pack's image with the same path as a built-in one replaces it. 32 x 32 RGBA PNGs match the built-in art. |
| `Audio/Music/*.ogg`, `Audio/Ambience/*.ogg`, `Audio/Sfx/*.ogg` | Ogg Vorbis music, ambient loops and sound effects (a map's `music`/`ambience` key is the file name). |
| `Lang/*.json` | Interface translations: `{ "code": "fr", "name": "Français", "strings": { "New Game  (N)": "Nouvelle partie  (N)", ... } }`. Start from the game's `Assets/Lang/template.json`, which lists every string; keep `{0}`-style placeholders and `{Action}` key names. Untranslated strings stay English; a pack's language replaces a built-in one with the same code. |

`game.json` (start position, starting party, intro) cannot be changed by a pack.

## Checking a pack

Run the game with `--check-content` (optionally `--mods <folder>`): it lists the packs it found,
loads everything and prints any problems - a bad JSON file, an unknown item in a shop, a teleport to a
map that does not exist - naming the pack. The exit code is 0 when all is well.

```
AVAMMB1 --check-content --mods ./my-mods
```

The same checks run when the game starts; a broken pack shows an error window instead of the game.

## Saves

Saves record which packs were active. Loading a save made with a pack that is now missing shows a
warning, or - if the party stands in a map that pack added - explains which packs to enable.

## The example pack

[`example-pack/`](example-pack/) adds a stair from Brindlemoor down into a small dungeon under the old
well, a new monster with its own sprite, a charm and a journal entry. Copy the folder into your Mods
folder to try it; its tests (`ModPackTests`) walk a party through it.

## Licensing

Only ship assets you have the right to share. The game's own art and audio are CC0 (see
ASSETS_LICENSES.md), so you may reuse them in packs.
