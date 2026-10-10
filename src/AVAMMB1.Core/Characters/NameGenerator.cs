using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Characters;

/// <summary>Random character names from race-flavoured syllables (for players without a keyboard).</summary>
public static class NameGenerator
{
    private static readonly Dictionary<(string Race, Sex Sex), (string[] Starts, string[] Ends)> Syllables = new()
    {
        [("human", Sex.Male)] = (["Al", "Bran", "Cor", "Ed", "Gar", "Hal", "Rod", "Wil", "Ter", "Os", "Mar", "Ced"], ["ric", "win", "an", "ald", "mund", "ion", "as", "ard"]),
        [("human", Sex.Female)] = (["Ad", "El", "Is", "Mar", "Ro", "Sel", "Te", "Wyn", "Ali", "Bri", "Cath"], ["a", "eth", "ine", "ia", "wen", "ora", "elle", "ys"]),
        [("elf", Sex.Male)] = (["Ae", "Cal", "Ela", "Fin", "Lae", "Thal", "Syl", "Ilv"], ["ndril", "rion", "thas", "dor", "anor", "ion", "ar"]),
        [("elf", Sex.Female)] = (["Ae", "Ari", "Ela", "Fae", "Lia", "Syl", "Ny", "Ilm"], ["wyn", "riel", "dra", "lith", "ra", "ssa", "nae"]),
        [("dwarf", Sex.Male)] = (["Thor", "Bal", "Dur", "Gim", "Bro", "Kaz", "Dor", "Hel"], ["in", "ur", "ak", "rik", "gar", "nar", "grim"]),
        [("dwarf", Sex.Female)] = (["Dis", "Hel", "Bry", "Kat", "Ing", "Ver", "Til", "Ama"], ["dis", "ga", "hild", "ra", "dra", "na", "wyn"]),
        [("gnome", Sex.Male)] = (["Bim", "Fen", "Nim", "Pip", "Tob", "Zook", "Wiz", "Dob"], ["wick", "ble", "kin", "ny", "bo", "le", "per"]),
        [("gnome", Sex.Female)] = (["Bel", "Nix", "Pip", "Tin", "Wil", "Zan", "Fen", "Lil"], ["la", "sie", "wen", "na", "py", "ble", "ka"]),
        [("halforc", Sex.Male)] = (["Gro", "Mak", "Ur", "Kar", "Dru", "Thok", "Gar", "Brak"], ["gash", "k", "nak", "zug", "rok", "mar", "gul"]),
        [("halforc", Sex.Female)] = (["Ur", "Sha", "Gri", "Ba", "Vol", "Ker", "Mog", "Yar"], ["za", "gha", "ka", "ra", "na", "sha", "ga"]),
    };

    /// <summary>A name such as "Bramwin", "Thorgrim" or "Pipwick" (at most 16 letters).</summary>
    /// <param name="race">Race id.</param>
    /// <param name="sex">Sex.</param>
    /// <param name="rng">Random source.</param>
    public static string Generate(string race, Sex sex, IRandomSource rng)
    {
        var (starts, ends) = Syllables.TryGetValue((race, sex), out var s) ? s : Syllables[("human", sex)];
        return CharacterFactory.CleanName(starts[rng.Next(0, starts.Length)] + ends[rng.Next(0, ends.Length)]);
    }
}
