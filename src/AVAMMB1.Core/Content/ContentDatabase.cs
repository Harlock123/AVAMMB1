using System.Text.Json;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Content;

/// <summary>All static game content, loaded from JSON and cross-validated.</summary>
public sealed class ContentDatabase
{
    private readonly Dictionary<string, GameMap> _maps = new(StringComparer.Ordinal);

    /// <summary>Races by id.</summary>
    public IReadOnlyDictionary<string, RaceDef> Races { get; private init; } = new Dictionary<string, RaceDef>();
    /// <summary>Classes by id.</summary>
    public IReadOnlyDictionary<string, ClassDef> Classes { get; private init; } = new Dictionary<string, ClassDef>();
    /// <summary>Items by id.</summary>
    public IReadOnlyDictionary<string, ItemDef> Items { get; private init; } = new Dictionary<string, ItemDef>();
    /// <summary>Monsters by id.</summary>
    public IReadOnlyDictionary<string, MonsterDef> Monsters { get; private init; } = new Dictionary<string, MonsterDef>();
    /// <summary>Spells by id.</summary>
    public IReadOnlyDictionary<string, SpellDef> Spells { get; private init; } = new Dictionary<string, SpellDef>();
    /// <summary>Journal quests in display order (optional <c>quests.json</c>).</summary>
    public IReadOnlyList<QuestDef> Quests { get; private init; } = [];

    /// <summary>Shops by id.</summary>
    public IReadOnlyDictionary<string, ShopDef> Shops { get; private init; } = new Dictionary<string, ShopDef>();
    /// <summary>Mod packs that were applied, in order.</summary>
    public IReadOnlyList<ModPack> Mods { get; private init; } = [];
    /// <summary>Global configuration.</summary>
    public GameConfigDef Config { get; private init; } = new();
    /// <summary>Maps by id.</summary>
    public IReadOnlyDictionary<string, GameMap> Maps => _maps;

    /// <summary>Gets an item definition or throws.</summary>
    /// <param name="id">Item id.</param>
    public ItemDef Item(string id) => Items.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"Unknown item '{id}'.");

    /// <summary>Gets a class definition or throws.</summary>
    /// <param name="id">Class id.</param>
    public ClassDef Class(string id) => Classes.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"Unknown class '{id}'.");

    /// <summary>Gets a race definition or throws.</summary>
    /// <param name="id">Race id.</param>
    public RaceDef Race(string id) => Races.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"Unknown race '{id}'.");

    /// <summary>Gets a monster definition or throws.</summary>
    /// <param name="id">Monster id.</param>
    public MonsterDef Monster(string id) => Monsters.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"Unknown monster '{id}'.");

    /// <summary>Gets a spell definition or throws.</summary>
    /// <param name="id">Spell id.</param>
    public SpellDef Spell(string id) => Spells.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"Unknown spell '{id}'.");

    /// <summary>Gets a map or throws.</summary>
    /// <param name="id">Map id.</param>
    public GameMap Map(string id) => _maps.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"Unknown map '{id}'.");

    /// <summary>Loads and validates content from a source.</summary>
    /// <param name="source">Where to read JSON files from.</param>
    /// <exception cref="InvalidDataException">Thrown when content is malformed or inconsistent.</exception>
    public static ContentDatabase Load(IContentSource source) => Load(source, []);

    /// <summary>
    /// Loads the base content, then each mod pack in order. Definitions merge by id (a new id adds, an
    /// existing id replaces), new maps are added, and <c>mapPatches.json</c> adds events to existing maps.
    /// </summary>
    /// <param name="source">Base content.</param>
    /// <param name="mods">Mod packs to apply, in order.</param>
    /// <exception cref="InvalidDataException">Thrown when content is malformed or inconsistent; mod errors name the pack.</exception>
    public static ContentDatabase Load(IContentSource source, IReadOnlyList<ModPack> mods)
    {
        var ctx = GameJsonContext.Default;
        var races = Index(Read(source, "races.json", ctx.ListRaceDef), r => r.Id, "race");
        var classes = Index(Read(source, "classes.json", ctx.ListClassDef), c => c.Id, "class");
        var items = Index(Read(source, "items.json", ctx.ListItemDef), i => i.Id, "item");
        var monsters = Index(Read(source, "monsters.json", ctx.ListMonsterDef), m => m.Id, "monster");
        var spells = Index(Read(source, "spells.json", ctx.ListSpellDef), s => s.Id, "spell");
        var shops = Index(Read(source, "shops.json", ctx.ListShopDef), s => s.Id, "shop");
        var quests = source.List().Contains("quests.json") ? Read(source, "quests.json", ctx.ListQuestDef) : [];
        var maps = new Dictionary<string, MapDef>(StringComparer.Ordinal);
        foreach (var path in source.List().Where(p => p.StartsWith("Maps/", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
        {
            var def = Read(source, path, ctx.MapDef);
            if (!maps.TryAdd(def.Id, def))
            {
                throw new InvalidDataException($"Duplicate map id '{def.Id}'.");
            }
        }

        foreach (var mod in mods)
        {
            try
            {
                var src = mod.Source;
                var files = src.List().ToHashSet(StringComparer.Ordinal);
                void Merge<T>(string file, Dictionary<string, T> into, System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<T>> info, Func<T, string> key)
                {
                    if (files.Contains(file))
                    {
                        foreach (var d in Read(src, file, info))
                        {
                            into[key(d)] = d;
                        }
                    }
                }
                Merge("races.json", races, ctx.ListRaceDef, r => r.Id);
                Merge("classes.json", classes, ctx.ListClassDef, c => c.Id);
                Merge("items.json", items, ctx.ListItemDef, i => i.Id);
                Merge("monsters.json", monsters, ctx.ListMonsterDef, m => m.Id);
                Merge("spells.json", spells, ctx.ListSpellDef, s => s.Id);
                Merge("shops.json", shops, ctx.ListShopDef, s => s.Id);
                if (files.Contains("quests.json"))
                {
                    foreach (var q in Read(src, "quests.json", ctx.ListQuestDef))
                    {
                        var at = quests.FindIndex(x => x.Id == q.Id);
                        if (at >= 0)
                        {
                            quests[at] = q;
                        }
                        else
                        {
                            quests.Add(q);
                        }
                    }
                }
                foreach (var path in files.Where(p => p.StartsWith("Maps/", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
                {
                    var def = Read(src, path, ctx.MapDef);
                    maps[def.Id] = def;
                }
                if (files.Contains("mapPatches.json"))
                {
                    foreach (var patch in Read(src, "mapPatches.json", ctx.ListMapPatchDef))
                    {
                        if (!maps.TryGetValue(patch.Map, out var target))
                        {
                            throw new InvalidDataException($"mapPatches.json: unknown map '{patch.Map}'.");
                        }
                        target.Events.AddRange(patch.AddEvents);
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Mod '{mod.Manifest.Name}' ({mod.Root}): {ex.Message}", ex);
            }
        }

        var db = new ContentDatabase
        {
            Races = races, Classes = classes, Items = items, Monsters = monsters, Spells = spells, Shops = shops,
            Config = Read(source, "game.json", ctx.GameConfigDef),
            Quests = quests,
            Mods = mods,
        };
        foreach (var def in maps.Values.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            db._maps[def.Id] = GameMap.Parse(def);
        }
        try
        {
            db.Validate();
        }
        catch (InvalidDataException ex) when (mods.Count > 0)
        {
            throw new InvalidDataException(ex.Message + "\n(Mods active: " + string.Join(", ", mods.Select(m => m.Manifest.Name)) + ")", ex);
        }
        return db;
    }

    private static T Read<T>(IContentSource source, string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        try
        {
            using var stream = source.Open(path);
            return JsonSerializer.Deserialize(stream, info) ?? throw new InvalidDataException($"{path} is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error reading {path} from {source.Description}: {ex.Message}", ex);
        }
    }

    private static Dictionary<string, T> Index<T>(List<T> items, Func<T, string> key, string what)
    {
        var dict = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!dict.TryAdd(key(item), item))
            {
                throw new InvalidDataException($"Duplicate {what} id '{key(item)}'.");
            }
        }
        return dict;
    }

    /// <summary>Checks cross references and returns a list of problems (empty when valid).</summary>
    public IReadOnlyList<string> FindProblems()
    {
        var problems = new List<string>();
        void Check(bool ok, string msg)
        {
            if (!ok)
            {
                problems.Add(msg);
            }
        }

        foreach (var c in Classes.Values)
        {
            foreach (var ab in c.Abilities)
            {
                Check(ClassAbility.All.Contains(ab), $"class {c.Id}: unknown ability {ab}");
            }
            foreach (var i in c.StartingItems)
            {
                Check(Items.ContainsKey(i), $"class {c.Id}: unknown starting item {i}");
            }
        }
        foreach (var i in Items.Values)
        {
            Check(i.UseSpell is null || Spells.ContainsKey(i.UseSpell), $"item {i.Id}: unknown spell {i.UseSpell}");
            foreach (var cl in i.Classes)
            {
                Check(Classes.ContainsKey(cl), $"item {i.Id}: unknown class {cl}");
            }
        }
        foreach (var m in Monsters.Values)
        {
            foreach (var d in m.Drops)
            {
                Check(Items.ContainsKey(d.Item), $"monster {m.Id}: unknown drop {d.Item}");
            }
        }
        foreach (var q in Quests)
        {
            Check(q.Stages.Count > 0, $"quest {q.Id}: no stages");
            foreach (var st in q.Stages)
            {
                Check((st.Flag is null ? 0 : 1) + (st.Item is null ? 0 : 1) + (st.Visited is null ? 0 : 1) == 1, $"quest {q.Id}: each stage needs exactly one of flag, item, visited");
                Check(st.Item is null || Items.ContainsKey(st.Item), $"quest {q.Id}: unknown item {st.Item}");
                Check(st.Visited is null || _maps.ContainsKey(st.Visited), $"quest {q.Id}: unknown map {st.Visited}");
                Check(st.Goal is null || (_maps.TryGetValue(st.Goal.Map, out var gm) && gm.InBounds(st.Goal.X, st.Goal.Y)),
                    $"quest {q.Id}: goal {st.Goal?.Map} ({st.Goal?.X},{st.Goal?.Y}) is not on a known map");
            }
        }
        Check(Quests.Select(q => q.Id).Distinct().Count() == Quests.Count, "duplicate quest id");
        foreach (var s in Shops.Values)
        {
            foreach (var i in s.Stock)
            {
                Check(Items.ContainsKey(i), $"shop {s.Id}: unknown item {i}");
            }
        }
        foreach (var p in Config.Premades)
        {
            Check(Races.ContainsKey(p.Race), $"premade {p.Name}: unknown race {p.Race}");
            Check(Classes.ContainsKey(p.Class), $"premade {p.Name}: unknown class {p.Class}");
        }
        Check(_maps.ContainsKey(Config.StartMap), $"game.json: unknown start map {Config.StartMap}");
        foreach (var map in _maps.Values)
        {
            foreach (var e in map.Def.Encounters.Concat(map.Def.NightEncounters))
            {
                Check(Monsters.ContainsKey(e.Monster), $"map {map.Id}: unknown encounter monster {e.Monster}");
            }
            Check(map.Def.LockedDoorKey is null || Items.ContainsKey(map.Def.LockedDoorKey), $"map {map.Id}: unknown key item");
            foreach (var ev in map.Def.Events)
            {
                var where = $"map {map.Id} event at ({ev.X},{ev.Y})";
                Check(ev.Shop is null || Shops.ContainsKey(ev.Shop), $"{where}: unknown shop {ev.Shop}");
                if (ev.Map is not null)
                {
                    Check(_maps.TryGetValue(ev.Map, out var dest) && dest.InBounds(ev.ToX, ev.ToY) && !dest.IsSolid(ev.ToX, ev.ToY),
                        $"{where}: bad teleport destination {ev.Map} ({ev.ToX},{ev.ToY})");
                }
                foreach (var i in ev.Items)
                {
                    Check(Items.ContainsKey(i), $"{where}: unknown item {i}");
                }
                Check(ev.RequiresItem is null || Items.ContainsKey(ev.RequiresItem), $"{where}: unknown required item {ev.RequiresItem}");
                foreach (var fm in ev.Monsters)
                {
                    Check(Monsters.ContainsKey(fm.Monster), $"{where}: unknown monster {fm.Monster}");
                }
                Check(!map.IsSolid(ev.X, ev.Y), $"{where}: event placed in a solid cell");
                Check(ev.Type != MapEventKind.Riddle || ev.Answers.Count > 0, $"{where}: a riddle needs answers");
                if (ev.Type == MapEventKind.Choice)
                {
                    Check(ev.Options.Count is >= 2 and <= 4 && ev.Options.All(o => o.Label.Length > 0), $"{where}: a choice needs two to four labelled options");
                    foreach (var i in ev.Options.SelectMany(o => o.Items))
                    {
                        Check(Items.ContainsKey(i), $"{where}: unknown item {i}");
                    }
                }
            }
        }
        return problems;
    }

    private void Validate()
    {
        var problems = FindProblems();
        if (problems.Count > 0)
        {
            throw new InvalidDataException("Content validation failed:\n" + string.Join("\n", problems));
        }
    }
}
