# Asset Licenses

Every non-code asset shipped with AVAM&M is listed here. All third-party assets were downloaded from the source URLs below, their license was checked on the source page (and in the bundled license files where present) before use, and **all of them are CC0 1.0 (public domain dedication)**. No assets with NC, ND or unclear licenses are used. Attribution is not legally required for CC0, but we credit every author anyway (see also [CREDITS.md](CREDITS.md) and the in-game Credits screen).

The game code is licensed separately under the MIT license (see [LICENSE](LICENSE)).

## Modifications

- All PNG images were losslessly re-encoded to 32-bit RGBA so every rendering backend can read them; pixels are unchanged.
- Character portraits are composited at runtime from the unmodified race base tiles and paper-doll layer tiles.
- All audio was transcoded to Ogg Vorbis (sound effects downmixed to mono) with ffmpeg; music tracks are otherwise unedited.
- Ambient loops were downmixed to mono at 22 kHz; the two park ambiences were trimmed to 90 seconds with a 2-second crossfade so they loop seamlessly.
- Files were renamed to describe their in-game use (original names are listed below).

## Sources

| Pack | Author | Source | License |
|---|---|---|---|
| Dungeon Crawl 32x32 tiles ("Dungeon Crawl Stone Soup Full" archive) | Dungeon Crawl Stone Soup team and rltiles contributors (OpenGameArt submission by MedicineStorm) | <https://opengameart.org/content/dungeon-crawl-32x32-tiles> | CC0 1.0 |
| RPG Audio | Kenney Vleugels (Kenney.nl) | <https://kenney.nl/assets/rpg-audio> | CC0 1.0 |
| 80 CC0 RPG SFX | rubberduck | <https://opengameart.org/content/80-cc0-rpg-sfx> | CC0 1.0 |
| RPG Sound Pack | artisticdude | <https://opengameart.org/content/rpg-sound-pack> | CC0 1.0 |
| 5 Chiptunes (Action) - "Title Screen" | Juhani Junkala (SubspaceAudio) | <https://opengameart.org/content/5-chiptunes-action> | CC0 1.0 |
| Town Theme RPG | cynicmusic | <https://opengameart.org/content/town-theme-rpg> | CC0 1.0 |
| Battle Theme A | cynicmusic | <https://opengameart.org/content/battle-theme-a> | CC0 1.0 |
| Dungeon Ambience | yd | <https://opengameart.org/content/dungeon-ambience> | CC0 1.0 |
| Desert Calmness and Fighting (Orchestral 141) | Dizzy Crow | <https://opengameart.org/content/desert-calmness-and-fighting-orchestral-141> | CC0 1.0 |
| Swamp Theme Loop | beardalaxy | <https://opengameart.org/content/swamp-theme-loop> | CC0 1.0 |
| 8-bit Cave Loop | Wolfgang_ | <https://opengameart.org/content/8-bit-cave-loop> | CC0 1.0 |
| Battle Theme B for RPG | cynicmusic | <https://opengameart.org/content/battle-theme-b-for-rpg> | CC0 1.0 |
| Wind Whoosh Loop | SketchMan3 | <https://opengameart.org/content/wind-whoosh-loop> | CC0 1.0 |
| Dripping Water Loop | qubodup | <https://opengameart.org/content/dripping-water-loop> | CC0 1.0 |
| Loopable Dungeon Ambience | JaggedStone | <https://opengameart.org/content/loopable-dungeon-ambience> | CC0 1.0 |
| Park Ambiences | Thimras | <https://opengameart.org/content/park-ambiences> | CC0 1.0 |

## Original works (made for this project)

| File | Description | License |
|---|---|---|
| `Assets/Icons/avammb1.png`, `.ico`, `.icns` | Application icon composed from two DCSS item tiles (long sword, crystal orb) on a solid background | CC0 1.0 (derived from CC0 tiles) |
| `Assets/Data/**/*.json` | All maps, monsters, items, spells, races, classes, shops and story text | MIT (same as code) |

## File-by-file listing

| File in `/Assets` | Source pack | Original file |
|---|---|---|
| `Audio/Ambience/birds.ogg` | Park Ambiences | `park_ambience_birds.wav` |
| `Audio/Ambience/deep.ogg` | Loopable Dungeon Ambience | `dungeon_ambient_1_0.ogg` |
| `Audio/Ambience/drips.ogg` | Dripping Water Loop | `atmosbasement.mp3_.flac` |
| `Audio/Ambience/river.ogg` | Park Ambiences | `park_ambience_river.wav` |
| `Audio/Ambience/wind.ogg` | Wind Whoosh Loop | `wind woosh loop` |
| `Audio/Music/battle.ogg` | Battle Theme A | `battleThemeA.mp3` |
| `Audio/Music/boss.ogg` | Battle Theme B for RPG | `battleThemeB.mp3` |
| `Audio/Music/caves.ogg` | 8-bit Cave Loop | `8BitCave.wav` |
| `Audio/Music/desert.ogg` | Desert Calmness and Fighting | `Negev Desert Loop` |
| `Audio/Music/dungeon.ogg` | Dungeon Ambience | `dungeon002_0.ogg` |
| `Audio/Music/marsh.ogg` | Swamp Theme Loop | `swamp_in_game.ogg` |
| `Audio/Music/title.ogg` | 5 Chiptunes | `Juhani Junkala [Retro Game Music Pack] Title Screen.wav` |
| `Audio/Music/town.ogg` | Town Theme RPG | `TownTheme.mp3` |
| `Audio/Sfx/book.ogg` | RPG Audio | `bookOpen.ogg` |
| `Audio/Sfx/bump.ogg` | 80 CC0 RPG SFX | `stones_01.ogg` |
| `Audio/Sfx/chest.ogg` | 80 CC0 RPG SFX | `lock_01.ogg` |
| `Audio/Sfx/coins.ogg` | 80 CC0 RPG SFX | `item_coins_01.ogg` |
| `Audio/Sfx/door.ogg` | RPG Audio | `doorOpen_1.ogg` |
| `Audio/Sfx/drink.ogg` | RPG Sound Pack | `inventory/bottle.wav` |
| `Audio/Sfx/equip.ogg` | RPG Sound Pack | `inventory/chainmail1.wav` |
| `Audio/Sfx/fire.ogg` | 80 CC0 RPG SFX | `spell_fire_01.ogg` |
| `Audio/Sfx/heal.ogg` | RPG Sound Pack | `inventory/bubble.wav` |
| `Audio/Sfx/hit.ogg` | 80 CC0 RPG SFX | `blade_01.ogg` |
| `Audio/Sfx/levelup.ogg` | 80 CC0 RPG SFX | `item_gem_01.ogg` |
| `Audio/Sfx/miss.ogg` | RPG Sound Pack | `battle/swing2.wav` |
| `Audio/Sfx/monster_die.ogg` | 80 CC0 RPG SFX | `creature_die_01.ogg` |
| `Audio/Sfx/monster_hurt.ogg` | 80 CC0 RPG SFX | `creature_hurt_01.ogg` |
| `Audio/Sfx/party_hurt.ogg` | RPG Sound Pack | `NPC/gutteral beast/mnstr1.wav` |
| `Audio/Sfx/roar.ogg` | 80 CC0 RPG SFX | `creature_roar_01.ogg` |
| `Audio/Sfx/spell.ogg` | 80 CC0 RPG SFX | `spell_01.ogg` |
| `Audio/Sfx/step.ogg` | RPG Audio | `footstep00.ogg` |
| `Audio/Sfx/swing.ogg` | RPG Sound Pack | `battle/swing.wav` |
| `Audio/Sfx/ui.ogg` | RPG Sound Pack | `interface/interface1.wav` |
| `Graphics/Doll/body_chain.png` | Dungeon Crawl 32x32 tiles | `player/body/chainmail.png` |
| `Graphics/Doll/body_leather_green.png` | Dungeon Crawl 32x32 tiles | `player/body/leather_green.png` |
| `Graphics/Doll/body_leather_jacket.png` | Dungeon Crawl 32x32 tiles | `player/body/leather_jacket.png` |
| `Graphics/Doll/body_plate.png` | Dungeon Crawl 32x32 tiles | `player/body/plate.png` |
| `Graphics/Doll/body_robe_blue.png` | Dungeon Crawl 32x32 tiles | `player/body/robe_blue.png` |
| `Graphics/Doll/body_robe_white.png` | Dungeon Crawl 32x32 tiles | `player/body/robe_white_blue.png` |
| `Graphics/Doll/boots_brown.png` | Dungeon Crawl 32x32 tiles | `player/boots/short_brown.png` |
| `Graphics/Doll/boots_brown2.png` | Dungeon Crawl 32x32 tiles | `player/boots/short_brown_2.png` |
| `Graphics/Doll/boots_gold.png` | Dungeon Crawl 32x32 tiles | `player/boots/middle_gold.png` |
| `Graphics/Doll/boots_gray.png` | Dungeon Crawl 32x32 tiles | `player/boots/middle_gray.png` |
| `Graphics/Doll/hair_female.png` | Dungeon Crawl 32x32 tiles | `player/hair/fem_red.png` |
| `Graphics/Doll/hair_male.png` | Dungeon Crawl 32x32 tiles | `player/hair/short_black.png` |
| `Graphics/Doll/head_helm_plume.png` | Dungeon Crawl 32x32 tiles | `player/head/helm_plume.png` |
| `Graphics/Doll/head_helm_red.png` | Dungeon Crawl 32x32 tiles | `player/head/helm_red.png` |
| `Graphics/Doll/head_hood_black.png` | Dungeon Crawl 32x32 tiles | `player/head/hood_black_2.png` |
| `Graphics/Doll/head_hood_green.png` | Dungeon Crawl 32x32 tiles | `player/head/hood_green.png` |
| `Graphics/Doll/head_wizard_blue.png` | Dungeon Crawl 32x32 tiles | `player/head/wizard_blue.png` |
| `Graphics/Doll/legs_black.png` | Dungeon Crawl 32x32 tiles | `player/legs/pants_black.png` |
| `Graphics/Doll/legs_chain.png` | Dungeon Crawl 32x32 tiles | `player/legs/leg_armor_0.png` |
| `Graphics/Doll/legs_green.png` | Dungeon Crawl 32x32 tiles | `player/legs/pants_darkgreen.png` |
| `Graphics/Doll/legs_plate.png` | Dungeon Crawl 32x32 tiles | `player/legs/leg_armor_1.png` |
| `Graphics/Doll/legs_white.png` | Dungeon Crawl 32x32 tiles | `player/legs/pants_l_white.png` |
| `Graphics/Doll/shield_buckler.png` | Dungeon Crawl 32x32 tiles | `player/hand_left/buckler_round_2.png` |
| `Graphics/Doll/shield_holy.png` | Dungeon Crawl 32x32 tiles | `player/hand_left/shield_holy.png` |
| `Graphics/Doll/shield_knight.png` | Dungeon Crawl 32x32 tiles | `player/hand_left/shield_knight_blue.png` |
| `Graphics/Doll/weapon_bow.png` | Dungeon Crawl 32x32 tiles | `player/hand_right/bow.png` |
| `Graphics/Doll/weapon_long_sword.png` | Dungeon Crawl 32x32 tiles | `player/hand_right/long_sword.png` |
| `Graphics/Doll/weapon_mace.png` | Dungeon Crawl 32x32 tiles | `player/hand_right/mace_new.png` |
| `Graphics/Doll/weapon_mace2.png` | Dungeon Crawl 32x32 tiles | `player/hand_right/mace_2_new.png` |
| `Graphics/Doll/weapon_short_sword.png` | Dungeon Crawl 32x32 tiles | `player/hand_right/short_sword.png` |
| `Graphics/Doll/weapon_staff.png` | Dungeon Crawl 32x32 tiles | `player/hand_right/staff_mage.png` |
| `Graphics/Features/altar.png` | Dungeon Crawl 32x32 tiles | `dungeon/altars/altar_base.png` |
| `Graphics/Features/chest.png` | Dungeon Crawl 32x32 tiles | `dungeon/chest.png` |
| `Graphics/Features/column.png` | Dungeon Crawl 32x32 tiles | `dungeon/statues/crumbled_column_1.png` |
| `Graphics/Features/entrance.png` | Dungeon Crawl 32x32 tiles | `dungeon/gateways/enter.png` |
| `Graphics/Features/fountain.png` | Dungeon Crawl 32x32 tiles | `dungeon/blue_fountain.png` |
| `Graphics/Features/mangrove.png` | Dungeon Crawl 32x32 tiles | `dungeon/trees/mangrove_1.png` |
| `Graphics/Features/mine_stairs_down.png` | Dungeon Crawl 32x32 tiles | `dungeon/gateways/rock_stairs_down.png` |
| `Graphics/Features/mine_stairs_up.png` | Dungeon Crawl 32x32 tiles | `dungeon/gateways/rock_stairs_up.png` |
| `Graphics/Features/oasis.png` | Dungeon Crawl 32x32 tiles | `dungeon/sparkling_fountain.png` |
| `Graphics/Features/portal.png` | Dungeon Crawl 32x32 tiles | `dungeon/gateways/portal.png` |
| `Graphics/Features/shop_armor.png` | Dungeon Crawl 32x32 tiles | `dungeon/shops/shop_armor.png` |
| `Graphics/Features/shop_books.png` | Dungeon Crawl 32x32 tiles | `dungeon/shops/shop_books.png` |
| `Graphics/Features/shop_food.png` | Dungeon Crawl 32x32 tiles | `dungeon/shops/shop_food.png` |
| `Graphics/Features/shop_general.png` | Dungeon Crawl 32x32 tiles | `dungeon/shops/shop_general.png` |
| `Graphics/Features/shop_potions.png` | Dungeon Crawl 32x32 tiles | `dungeon/shops/shop_potions.png` |
| `Graphics/Features/shop_weapon.png` | Dungeon Crawl 32x32 tiles | `dungeon/shops/shop_weapon.png` |
| `Graphics/Features/stairs_down.png` | Dungeon Crawl 32x32 tiles | `dungeon/gateways/stone_stairs_down.png` |
| `Graphics/Features/stairs_up.png` | Dungeon Crawl 32x32 tiles | `dungeon/gateways/stone_stairs_up.png` |
| `Graphics/Features/statue.png` | Dungeon Crawl 32x32 tiles | `dungeon/statues/granite_statue.png` |
| `Graphics/Features/tomb_entrance.png` | Dungeon Crawl 32x32 tiles | `dungeon/gateways/enter_tomb.png` |
| `Graphics/Features/tree.png` | Dungeon Crawl 32x32 tiles | `dungeon/trees/tree_2_yellow.png` |
| `Graphics/Features/tree_autumn.png` | Dungeon Crawl 32x32 tiles | `dungeon/trees/tree_1_red.png` |
| `Graphics/Items/amulet.png` | Dungeon Crawl 32x32 tiles | `item/amulet/celtic_blue.png` |
| `Graphics/Items/ancient_sword.png` | Dungeon Crawl 32x32 tiles | `item/weapon/ancient_sword.png` |
| `Graphics/Items/banded_mail.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/banded_mail_1.png` |
| `Graphics/Items/battle_axe.png` | Dungeon Crawl 32x32 tiles | `item/weapon/battle_axe_1.png` |
| `Graphics/Items/blessed_blade.png` | Dungeon Crawl 32x32 tiles | `item/weapon/blessed_blade.png` |
| `Graphics/Items/book.png` | Dungeon Crawl 32x32 tiles | `item/book/leather_new.png` |
| `Graphics/Items/boots.png` | Dungeon Crawl 32x32 tiles | `item/armor/feet/boots_1_brown_new.png` |
| `Graphics/Items/broad_sword.png` | Dungeon Crawl 32x32 tiles | `item/weapon/long_sword_3.png` |
| `Graphics/Items/chain_mail.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/chain_mail_1.png` |
| `Graphics/Items/club.png` | Dungeon Crawl 32x32 tiles | `item/weapon/club_new.png` |
| `Graphics/Items/crossbow.png` | Dungeon Crawl 32x32 tiles | `item/weapon/ranged/crossbow_1.png` |
| `Graphics/Items/crystal.png` | Dungeon Crawl 32x32 tiles | `item/misc/misc_crystal_new.png` |
| `Graphics/Items/dagger.png` | Dungeon Crawl 32x32 tiles | `item/weapon/dagger_new.png` |
| `Graphics/Items/dragon_scale.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/green_dragon_scale_mail.png` |
| `Graphics/Items/dwarven_mail.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/dwarven_ringmail.png` |
| `Graphics/Items/evening_star.png` | Dungeon Crawl 32x32 tiles | `item/weapon/eveningstar_1_new.png` |
| `Graphics/Items/flail.png` | Dungeon Crawl 32x32 tiles | `item/weapon/flail_1_new.png` |
| `Graphics/Items/gauntlets.png` | Dungeon Crawl 32x32 tiles | `item/armor/hands/gauntlet_1.png` |
| `Graphics/Items/gem.png` | Dungeon Crawl 32x32 tiles | `item/misc/misc_stone_new.png` |
| `Graphics/Items/gold.png` | Dungeon Crawl 32x32 tiles | `item/gold/gold_pile_10.png` |
| `Graphics/Items/great_sword.png` | Dungeon Crawl 32x32 tiles | `item/weapon/greatsword_1_new.png` |
| `Graphics/Items/halberd.png` | Dungeon Crawl 32x32 tiles | `item/weapon/halberd_1.png` |
| `Graphics/Items/hand_axe.png` | Dungeon Crawl 32x32 tiles | `item/weapon/hand_axe_1_new.png` |
| `Graphics/Items/helmet.png` | Dungeon Crawl 32x32 tiles | `item/armor/headgear/helmet_1.png` |
| `Graphics/Items/key.png` | Dungeon Crawl 32x32 tiles | `item/misc/key.png` |
| `Graphics/Items/large_shield.png` | Dungeon Crawl 32x32 tiles | `item/armor/shields/large_shield_1_new.png` |
| `Graphics/Items/leather_armor.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/leather_armor_1.png` |
| `Graphics/Items/long_bow.png` | Dungeon Crawl 32x32 tiles | `item/weapon/ranged/longbow_1.png` |
| `Graphics/Items/long_sword.png` | Dungeon Crawl 32x32 tiles | `item/weapon/long_sword_1_new.png` |
| `Graphics/Items/mace.png` | Dungeon Crawl 32x32 tiles | `item/weapon/mace_1_new.png` |
| `Graphics/Items/morning_star.png` | Dungeon Crawl 32x32 tiles | `item/weapon/morningstar_1_new.png` |
| `Graphics/Items/orb.png` | Dungeon Crawl 32x32 tiles | `item/misc/misc_orb.png` |
| `Graphics/Items/padded_armor.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/animal_skin_1_new.png` |
| `Graphics/Items/plate_mail.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/plate_mail_1.png` |
| `Graphics/Items/potion_blue.png` | Dungeon Crawl 32x32 tiles | `item/potion/brilliant_blue_new.png` |
| `Graphics/Items/potion_gold.png` | Dungeon Crawl 32x32 tiles | `item/potion/golden.png` |
| `Graphics/Items/potion_green.png` | Dungeon Crawl 32x32 tiles | `item/potion/emerald.png` |
| `Graphics/Items/potion_red.png` | Dungeon Crawl 32x32 tiles | `item/potion/ruby_new.png` |
| `Graphics/Items/ration.png` | Dungeon Crawl 32x32 tiles | `item/food/bread_ration_new.png` |
| `Graphics/Items/ring.png` | Dungeon Crawl 32x32 tiles | `item/ring/silver.png` |
| `Graphics/Items/ring_gold.png` | Dungeon Crawl 32x32 tiles | `item/ring/gold.png` |
| `Graphics/Items/ring_mail.png` | Dungeon Crawl 32x32 tiles | `item/armor/torso/elven_ringmail.png` |
| `Graphics/Items/scarab_amulet.png` | Dungeon Crawl 32x32 tiles | `item/amulet/face_1_gold.png` |
| `Graphics/Items/scimitar.png` | Dungeon Crawl 32x32 tiles | `item/weapon/scimitar_1_new.png` |
| `Graphics/Items/scroll.png` | Dungeon Crawl 32x32 tiles | `item/scroll/scroll-red.png` |
| `Graphics/Items/scroll_blue.png` | Dungeon Crawl 32x32 tiles | `item/scroll/scroll-blue.png` |
| `Graphics/Items/short_bow.png` | Dungeon Crawl 32x32 tiles | `item/weapon/ranged/shortbow_1.png` |
| `Graphics/Items/short_sword.png` | Dungeon Crawl 32x32 tiles | `item/weapon/short_sword_1_new.png` |
| `Graphics/Items/sling.png` | Dungeon Crawl 32x32 tiles | `item/weapon/ranged/sling_1.png` |
| `Graphics/Items/small_shield.png` | Dungeon Crawl 32x32 tiles | `item/armor/shields/buckler_1_new.png` |
| `Graphics/Items/spear.png` | Dungeon Crawl 32x32 tiles | `item/weapon/spear_1.png` |
| `Graphics/Items/staff.png` | Dungeon Crawl 32x32 tiles | `item/weapon/quarterstaff_new.png` |
| `Graphics/Items/sun_crown.png` | Dungeon Crawl 32x32 tiles | `item/armor/headgear/helmet_ego_1.png` |
| `Graphics/Items/torch.png` | Dungeon Crawl 32x32 tiles | `item/misc/misc_lantern.png` |
| `Graphics/Items/war_hammer.png` | Dungeon Crawl 32x32 tiles | `item/weapon/war_hammer.png` |
| `Graphics/Monsters/bandit.png` | Dungeon Crawl 32x32 tiles | `monster/human_new.png` |
| `Graphics/Monsters/barrow_wight.png` | Dungeon Crawl 32x32 tiles | `monster/undead/wight_new.png` |
| `Graphics/Monsters/basilisk.png` | Dungeon Crawl 32x32 tiles | `monster/animals/basilisk.png` |
| `Graphics/Monsters/bog_body.png` | Dungeon Crawl 32x32 tiles | `monster/undead/bog_body.png` |
| `Graphics/Monsters/cave_bat.png` | Dungeon Crawl 32x32 tiles | `monster/animals/bat.png` |
| `Graphics/Monsters/cellar_rat.png` | Dungeon Crawl 32x32 tiles | `monster/animals/rat.png` |
| `Graphics/Monsters/crocodile.png` | Dungeon Crawl 32x32 tiles | `monster/animals/crocodile.png` |
| `Graphics/Monsters/crypt_lich.png` | Dungeon Crawl 32x32 tiles | `monster/undead/lich.png` |
| `Graphics/Monsters/dire_wolf.png` | Dungeon Crawl 32x32 tiles | `monster/animals/wolf.png` |
| `Graphics/Monsters/drowned_hydra.png` | Dungeon Crawl 32x32 tiles | `monster/dragons/hydra_3_new.png` |
| `Graphics/Monsters/dwarf_berserker.png` | Dungeon Crawl 32x32 tiles | `monster/deep_dwarf_berserker.png` |
| `Graphics/Monsters/efreet.png` | Dungeon Crawl 32x32 tiles | `monster/demons/efreet.png` |
| `Graphics/Monsters/emperor_scorpion.png` | Dungeon Crawl 32x32 tiles | `monster/animals/emperor_scorpion.png` |
| `Graphics/Monsters/ghoul.png` | Dungeon Crawl 32x32 tiles | `monster/undead/ghoul.png` |
| `Graphics/Monsters/giant_leech.png` | Dungeon Crawl 32x32 tiles | `monster/animals/giant_leech.png` |
| `Graphics/Monsters/giant_scorpion.png` | Dungeon Crawl 32x32 tiles | `monster/animals/giant_scorpion.png` |
| `Graphics/Monsters/giant_spider.png` | Dungeon Crawl 32x32 tiles | `monster/animals/spider.png` |
| `Graphics/Monsters/gnoll_brute.png` | Dungeon Crawl 32x32 tiles | `monster/gnoll_new.png` |
| `Graphics/Monsters/goblin.png` | Dungeon Crawl 32x32 tiles | `monster/goblin_new.png` |
| `Graphics/Monsters/green_slime.png` | Dungeon Crawl 32x32 tiles | `monster/amorphous/acid_blob.png` |
| `Graphics/Monsters/guardian_mummy.png` | Dungeon Crawl 32x32 tiles | `monster/undead/guardian_mummy.png` |
| `Graphics/Monsters/harpy.png` | Dungeon Crawl 32x32 tiles | `monster/harpy.png` |
| `Graphics/Monsters/hill_giant.png` | Dungeon Crawl 32x32 tiles | `monster/stone_giant_new.png` |
| `Graphics/Monsters/hill_wyvern.png` | Dungeon Crawl 32x32 tiles | `monster/dragons/wyvern_new.png` |
| `Graphics/Monsters/hobgoblin.png` | Dungeon Crawl 32x32 tiles | `monster/hobgoblin_new.png` |
| `Graphics/Monsters/jackal_guard.png` | Dungeon Crawl 32x32 tiles | `monster/anubis_guard.png` |
| `Graphics/Monsters/kobold.png` | Dungeon Crawl 32x32 tiles | `monster/kobold_new.png` |
| `Graphics/Monsters/kobold_chief.png` | Dungeon Crawl 32x32 tiles | `monster/big_kobold_new.png` |
| `Graphics/Monsters/manticore.png` | Dungeon Crawl 32x32 tiles | `monster/manticore.png` |
| `Graphics/Monsters/marsh_frog.png` | Dungeon Crawl 32x32 tiles | `monster/animals/giant_frog.png` |
| `Graphics/Monsters/marsh_naga.png` | Dungeon Crawl 32x32 tiles | `monster/naga.png` |
| `Graphics/Monsters/minotaur.png` | Dungeon Crawl 32x32 tiles | `monster/minotaur.png` |
| `Graphics/Monsters/mummy.png` | Dungeon Crawl 32x32 tiles | `monster/undead/mummy.png` |
| `Graphics/Monsters/mummy_priest.png` | Dungeon Crawl 32x32 tiles | `monster/undead/mummy_priest.png` |
| `Graphics/Monsters/naga_seer.png` | Dungeon Crawl 32x32 tiles | `monster/naga_mage.png` |
| `Graphics/Monsters/naga_warden.png` | Dungeon Crawl 32x32 tiles | `monster/naga_warrior.png` |
| `Graphics/Monsters/necromancer.png` | Dungeon Crawl 32x32 tiles | `monster/necromancer_new.png` |
| `Graphics/Monsters/ogre.png` | Dungeon Crawl 32x32 tiles | `monster/ogre_new.png` |
| `Graphics/Monsters/orc_hexer.png` | Dungeon Crawl 32x32 tiles | `monster/orc_wizard_new.png` |
| `Graphics/Monsters/orc_raider.png` | Dungeon Crawl 32x32 tiles | `monster/orc_warrior_new.png` |
| `Graphics/Monsters/pit_viper.png` | Dungeon Crawl 32x32 tiles | `monster/animals/snake.png` |
| `Graphics/Monsters/rattlebones.png` | Dungeon Crawl 32x32 tiles | `monster/undead/skeletons/skeleton_humanoid_small_new.png` |
| `Graphics/Monsters/renegade_dwarf.png` | Dungeon Crawl 32x32 tiles | `monster/deep_dwarf.png` |
| `Graphics/Monsters/rock_beetle.png` | Dungeon Crawl 32x32 tiles | `monster/animals/boulder_beetle.png` |
| `Graphics/Monsters/rock_troll.png` | Dungeon Crawl 32x32 tiles | `monster/rock_troll.png` |
| `Graphics/Monsters/salamander.png` | Dungeon Crawl 32x32 tiles | `monster/salamander_firebrand.png` |
| `Graphics/Monsters/scarab.png` | Dungeon Crawl 32x32 tiles | `monster/animals/boring_beetle.png` |
| `Graphics/Monsters/sphinx.png` | Dungeon Crawl 32x32 tiles | `monster/sphinx_new.png` |
| `Graphics/Monsters/stone_wyrm.png` | Dungeon Crawl 32x32 tiles | `monster/dragons/dragon.png` |
| `Graphics/Monsters/sun_king.png` | Dungeon Crawl 32x32 tiles | `monster/undead/greater_mummy.png` |
| `Graphics/Monsters/swamp_drake.png` | Dungeon Crawl 32x32 tiles | `monster/swamp_drake.png` |
| `Graphics/Monsters/tomb_wraith.png` | Dungeon Crawl 32x32 tiles | `monster/undead/shadow_wraith.png` |
| `Graphics/Monsters/troll.png` | Dungeon Crawl 32x32 tiles | `monster/troll.png` |
| `Graphics/Monsters/vault_warden.png` | Dungeon Crawl 32x32 tiles | `monster/vault/vault_warden.png` |
| `Graphics/Monsters/wild_dog.png` | Dungeon Crawl 32x32 tiles | `monster/animals/jackal_new.png` |
| `Graphics/Monsters/wraith.png` | Dungeon Crawl 32x32 tiles | `monster/undead/wraith.png` |
| `Graphics/Monsters/zombie.png` | Dungeon Crawl 32x32 tiles | `monster/undead/zombies/zombie_small.png` |
| `Graphics/Portraits/dwarf_female.png` | Dungeon Crawl 32x32 tiles | `player/base/dwarf_female.png` |
| `Graphics/Portraits/dwarf_male.png` | Dungeon Crawl 32x32 tiles | `player/base/dwarf_male.png` |
| `Graphics/Portraits/elf_female.png` | Dungeon Crawl 32x32 tiles | `player/base/elf_female.png` |
| `Graphics/Portraits/elf_male.png` | Dungeon Crawl 32x32 tiles | `player/base/elf_male.png` |
| `Graphics/Portraits/gnome_female.png` | Dungeon Crawl 32x32 tiles | `player/base/gnome_female.png` |
| `Graphics/Portraits/gnome_male.png` | Dungeon Crawl 32x32 tiles | `player/base/gnome_male.png` |
| `Graphics/Portraits/halforc_female.png` | Dungeon Crawl 32x32 tiles | `player/base/orc_female.png` |
| `Graphics/Portraits/halforc_male.png` | Dungeon Crawl 32x32 tiles | `player/base/orc_male.png` |
| `Graphics/Portraits/human_female.png` | Dungeon Crawl 32x32 tiles | `player/base/human_female.png` |
| `Graphics/Portraits/human_male.png` | Dungeon Crawl 32x32 tiles | `player/base/human_male.png` |
| `Graphics/Textures/door.png` | Dungeon Crawl 32x32 tiles | `dungeon/doors/closed_door.png` |
| `Graphics/Textures/door_locked.png` | Dungeon Crawl 32x32 tiles | `dungeon/doors/runed_door.png` |
| `Graphics/Textures/floor_bog.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/bog_green_0_new.png` |
| `Graphics/Textures/floor_cobble.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/rect_gray_0_new.png` |
| `Graphics/Textures/floor_dirt.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/grey_dirt_0_new.png` |
| `Graphics/Textures/floor_earth.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/dirt_full_new.png` |
| `Graphics/Textures/floor_grass.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/grass/grass_0_new.png` |
| `Graphics/Textures/floor_limestone.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/limestone_0.png` |
| `Graphics/Textures/floor_marble.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/marble_floor_1.png` |
| `Graphics/Textures/floor_pebble.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/pebble_brown_0_new.png` |
| `Graphics/Textures/floor_sand.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/sand_1.png` |
| `Graphics/Textures/floor_sandstone.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/sandstone_floor_0.png` |
| `Graphics/Textures/floor_tomb.png` | Dungeon Crawl 32x32 tiles | `dungeon/floor/tomb_0_new.png` |
| `Graphics/Textures/shallow_water.png` | Dungeon Crawl 32x32 tiles | `dungeon/water/shallow_water.png` |
| `Graphics/Textures/tree.png` | Dungeon Crawl 32x32 tiles | `dungeon/trees/tree_1_yellow.png` |
| `Graphics/Textures/wall_cellar.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/stone_gray_0.png` |
| `Graphics/Textures/wall_crypt.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/tomb_0.png` |
| `Graphics/Textures/wall_deep.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/lair_0_new.png` |
| `Graphics/Textures/wall_desert_town.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/sandstone_wall_3.png` |
| `Graphics/Textures/wall_dungeon.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/stone_brick_1.png` |
| `Graphics/Textures/wall_marble.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/marble_wall_1.png` |
| `Graphics/Textures/wall_mine.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/pebble_red_0_new.png` |
| `Graphics/Textures/wall_mountain.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/stone_dark_0.png` |
| `Graphics/Textures/wall_sandstone.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/sandstone_wall_0.png` |
| `Graphics/Textures/wall_sunvault.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/stone_black_marked_0.png` |
| `Graphics/Textures/wall_temple.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/relief_0.png` |
| `Graphics/Textures/wall_tomb2.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/relief_brown_0.png` |
| `Graphics/Textures/wall_town.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/brick_brown_0.png` |
| `Graphics/Textures/wall_town2.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/stone_2_brown_0.png` |
| `Graphics/Textures/wall_town3.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/brick_gray_0.png` |
| `Graphics/Textures/wall_town4.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/brick_dark_0.png` |
| `Graphics/Textures/wall_town5.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/stone_2_gray_0.png` |
| `Graphics/Textures/wall_vault.png` | Dungeon Crawl 32x32 tiles | `dungeon/wall/crystal_wall_lightblue.png` |
| `Graphics/Textures/water.png` | Dungeon Crawl 32x32 tiles | `dungeon/water/deep_water.png` |
