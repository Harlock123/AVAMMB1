using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>A monster card in the battle line-up.</summary>
public sealed partial class MonsterViewModel : ObservableObject
{
    /// <summary>Creates the card.</summary>
    /// <param name="monster">Monster.</param>
    /// <param name="index">Index in the combat's monster list.</param>
    /// <param name="sprite">Sprite.</param>
    /// <param name="animated">Whether animations are enabled.</param>
    public MonsterViewModel(MonsterInstance monster, int index, Bitmap? sprite, bool animated)
    {
        Monster = monster;
        Index = index;
        Sprite = sprite;
        Animated = animated;
    }

    /// <summary>Whether animations are enabled.</summary>
    public bool Animated { get; }
    /// <summary>Idle motion variant A (alternates by position so monsters don't bob in lock-step).</summary>
    public bool IdleA => Animated && !IsDead && Index % 2 == 0;
    /// <summary>Idle motion variant B.</summary>
    public bool IdleB => Animated && !IsDead && Index % 2 == 1;
    /// <summary>Killed (shown faded until the battle ends).</summary>
    public bool IsDead => Monster.IsDead;

    /// <summary>Briefly true after taking damage (drives the hit shake).</summary>
    [ObservableProperty]
    private bool _isHit;

    /// <summary>Briefly true after acting (drives the attack lunge).</summary>
    [ObservableProperty]
    private bool _isAttacking;

    /// <summary>Monster.</summary>
    public MonsterInstance Monster { get; }
    /// <summary>Index.</summary>
    public int Index { get; }
    /// <summary>Sprite.</summary>
    public Bitmap? Sprite { get; }
    /// <summary>Label.</summary>
    public string Label => Monster.Label;
    /// <summary>HP fraction.</summary>
    public double HpFraction => Math.Clamp((double)Monster.Hp / Monster.MaxHp, 0, 1);
    /// <summary>Status text.</summary>
    public string Status => (Monster.Conditions & Condition.Asleep) != 0 ? "asleep" : (Monster.Conditions & Condition.Paralyzed) != 0 ? "held" : "";

    /// <summary>In melee range.</summary>
    [ObservableProperty]
    private bool _isFront;

    /// <summary>Currently targeted.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Refreshes display.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>A spell or item choice in combat.</summary>
/// <param name="Id">Spell id or backpack index.</param>
/// <param name="Label">Display text.</param>
/// <param name="Target">Targeting.</param>
/// <param name="Enabled">Whether selectable.</param>
public sealed record CombatChoice(string Id, string Label, TargetKind Target, bool Enabled);

/// <summary>Phases of the combat UI.</summary>
public enum CombatPhase
{
    /// <summary>Fight / run / bribe.</summary>
    Opening,
    /// <summary>Choosing an action for the active character.</summary>
    Action,
    /// <summary>Choosing a spell.</summary>
    Spell,
    /// <summary>Choosing an item.</summary>
    Item,
    /// <summary>Choosing an ally target.</summary>
    Ally,
    /// <summary>Battle over.</summary>
    Finished,
}

/// <summary>Turn-based combat overlay.</summary>
public sealed partial class CombatViewModel : ViewModelBase
{
    private readonly GameViewModel _game;
    private readonly CombatEngine _combat;
    private readonly HashSet<MonsterInstance> _acted = new();
    private readonly HashSet<MonsterInstance> _justDied = new();
    private readonly Dictionary<object, int> _hpSnapshot = new();
    private CombatAction? _pending;
    private bool AnimationsOn => _game.Services.Settings.AnimateMonsters;

    /// <summary>Creates the overlay.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="combat">Battle.</param>
    public CombatViewModel(GameViewModel game, CombatEngine combat)
    {
        _game = game;
        _combat = combat;
        for (var i = 0; i < combat.Monsters.Count; i++)
        {
            var m = combat.Monsters[i];
            Monsters.Add(new MonsterViewModel(m, i, game.Services.Textures.Bitmap("Monsters/" + m.Def.Sprite), AnimationsOn));
        }
        combat.MonsterActed += m => _acted.Add(m);
        TakeSnapshot();
        Party = game.Party;
        game.Services.Audio.PlayMusic(combat.Monsters.Any(m => m.Def.Boss) ? "boss" : "battle");
        var names = combat.Monsters.GroupBy(m => m.Def).Select(g => g.Count() == 1 ? $"a {g.Key.Name}" : $"{g.Count()} {g.Key.PluralName}");
        Log.Add(new MessageViewModel(new GameMessage($"You face {string.Join(", ", names)}!", MessageKind.Bad)));
        var bribe = combat.BribeCost;
        BribeLabel = bribe is { } b ? $"Bribe ({b} gold)" : "Bribe (refused)";
        CanBribe = bribe is not null;
        RefreshAll();
    }

    /// <summary>All monster cards (including slain ones, for keyboard targeting order).</summary>
    public ObservableCollection<MonsterViewModel> Monsters { get; } = new();

    /// <summary>Monsters standing in melee range, drawn large in the 3D view.</summary>
    public ObservableCollection<MonsterViewModel> FrontRank { get; } = new();

    /// <summary>Monsters behind the front rank, drawn smaller.</summary>
    public ObservableCollection<MonsterViewModel> BackRank { get; } = new();
    /// <summary>Party.</summary>
    public ObservableCollection<PartyMemberViewModel> Party { get; }
    /// <summary>Battle log.</summary>
    public ObservableCollection<MessageViewModel> Log { get; } = new();
    /// <summary>Spell or item choices.</summary>
    public ObservableCollection<CombatChoice> Choices { get; } = new();

    /// <summary>UI phase.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpening), nameof(IsAction), nameof(IsChoosing), nameof(IsAlly), nameof(IsFinished))]
    private CombatPhase _phase = CombatPhase.Opening;

    /// <summary>Prompt line.</summary>
    [ObservableProperty]
    private string _prompt = "Fight, run, or try a bribe?";

    /// <summary>Selected target index.</summary>
    [ObservableProperty]
    private int _targetIndex = -1;

    /// <summary>Opening phase.</summary>
    public bool IsOpening => Phase == CombatPhase.Opening;
    /// <summary>Action phase.</summary>
    public bool IsAction => Phase == CombatPhase.Action;
    /// <summary>Spell/item list phase.</summary>
    public bool IsChoosing => Phase is CombatPhase.Spell or CombatPhase.Item;
    /// <summary>Ally selection phase.</summary>
    public bool IsAlly => Phase == CombatPhase.Ally;
    /// <summary>Battle over.</summary>
    public bool IsFinished => Phase == CombatPhase.Finished;
    /// <summary>Bribe button label.</summary>
    public string BribeLabel { get; }
    /// <summary>Whether bribing is possible.</summary>
    public bool CanBribe { get; }
    /// <summary>Round counter.</summary>
    public string RoundText => _combat.Round > 0 ? $"Round {_combat.Round}" : "";
    /// <summary>Chance to flee.</summary>
    public string RunText => $"Run ({_combat.RunChance}%)";
    /// <summary>Whether the active character can melee.</summary>
    public bool CanMelee => _combat.ActiveCharacter is { } c && _combat.IsInFrontRank(c);
    /// <summary>Whether the active character can shoot.</summary>
    public bool CanShoot => _combat.ActiveCharacter is { } c && _game.Services.Session.Rules.HasMissileWeapon(c);
    /// <summary>Whether the active character knows spells.</summary>
    public bool CanCast => _combat.ActiveCharacter is { } c && _game.Services.Session.Rules.KnownSpells(c).Any();

    private void AddLog(IEnumerable<GameMessage> messages)
    {
        var list = messages.ToList();
        _game.Services.PlayCues(list);
        foreach (var m in list.Where(m => m.Text.Length > 0))
        {
            Log.Add(new MessageViewModel(m));
        }
        while (Log.Count > 60)
        {
            Log.RemoveAt(0);
        }
    }

    private void RefreshAll()
    {
        var front = _combat.MeleeTargets.ToHashSet();
        foreach (var m in Monsters.ToList())
        {
            if (m.Monster.Fled)
            {
                Monsters.Remove(m);
                continue;
            }
            m.IsFront = front.Contains(m.Monster);
            m.Refresh();
        }
        if (TargetIndex < 0 || !Monsters.Any(m => m.Index == TargetIndex && m.Monster.IsActive))
        {
            TargetIndex = Monsters.FirstOrDefault(m => m.Monster.IsActive)?.Index ?? -1;
        }
        foreach (var m in Monsters)
        {
            m.IsSelected = m.Index == TargetIndex;
        }
        // Stage: the first three monsters still standing (or just slain) form the front rank.
        var onStage = Monsters.Where(m => m.Monster.IsActive || _justDied.Contains(m.Monster)).ToList();
        Sync(FrontRank, onStage.Take(Core.Combat.CombatEngine.FrontRank));
        Sync(BackRank, onStage.Skip(Core.Combat.CombatEngine.FrontRank));
        foreach (var p in Party)
        {
            p.IsActive = ReferenceEquals(p.Character, _combat.ActiveCharacter);
            p.Refresh();
        }
        OnPropertyChanged(nameof(RoundText));
        OnPropertyChanged(nameof(RunText));
        OnPropertyChanged(nameof(CanMelee));
        OnPropertyChanged(nameof(CanShoot));
        OnPropertyChanged(nameof(CanCast));
    }

    private static void Sync(ObservableCollection<MonsterViewModel> target, IEnumerable<MonsterViewModel> wanted)
    {
        var list = wanted.ToList();
        if (!target.SequenceEqual(list))
        {
            target.Clear(); // only rebuilt when membership changes, so running animations aren't reset
            foreach (var m in list)
            {
                target.Add(m);
            }
        }
    }

    private void TakeSnapshot()
    {
        _hpSnapshot.Clear();
        foreach (var m in _combat.Monsters)
        {
            _hpSnapshot[m] = m.Hp;
        }
        foreach (var c in _combat.Party)
        {
            _hpSnapshot[c] = c.Hp;
        }
    }

    private static void Pulse(Action<bool> set, int milliseconds)
    {
        set(false);
        set(true);
        Avalonia.Threading.DispatcherTimer.RunOnce(() => set(false), TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>Plays hit, lunge and hurt animations for what changed since the last update.</summary>
    private void Animate()
    {
        _justDied.Clear();
        foreach (var card in Monsters)
        {
            if (_hpSnapshot.TryGetValue(card.Monster, out var before) && before > 0 && card.Monster.IsDead)
            {
                _justDied.Add(card.Monster); // keep it on stage for one update so it can fade out
            }
        }
        if (AnimationsOn)
        {
            foreach (var card in Monsters)
            {
                if (_hpSnapshot.TryGetValue(card.Monster, out var hp) && card.Monster.Hp < hp)
                {
                    Pulse(v => card.IsHit = v, 420);
                }
                else if (_acted.Contains(card.Monster) && card.Monster.IsActive)
                {
                    Pulse(v => card.IsAttacking = v, 450);
                }
            }
            foreach (var p in Party)
            {
                if (_hpSnapshot.TryGetValue(p.Character, out var hp) && p.Character.Hp < hp)
                {
                    Pulse(v => p.IsHurt = v, 450);
                }
            }
        }
        _acted.Clear();
        TakeSnapshot();
    }

    private void AfterEngine(IReadOnlyList<GameMessage> log)
    {
        AddLog(log);
        Animate();
        if (_combat.Outcome != CombatOutcome.Ongoing)
        {
            Finish();
        }
        else if (_combat.ActiveCharacter is { } c)
        {
            Phase = CombatPhase.Action;
            Prompt = $"{c.Name}'s turn" + (_combat.IsInFrontRank(c) ? "" : " (back rank)") + ". Choose an action.";
        }
        RefreshAll();
    }

    private void Finish()
    {
        var outcome = _combat.Outcome;
        var (_, rewards) = _game.Services.Session.EndCombat();
        AddLog(rewards);
        _game.AddMessages(rewards.Select(r => r with { Sound = null }));
        Phase = CombatPhase.Finished;
        Prompt = outcome switch
        {
            CombatOutcome.Victory => "Victory!",
            CombatOutcome.Fled => "You escaped.",
            CombatOutcome.Bribed => "The monsters take the gold and leave.",
            _ => "Defeat...",
        };
        foreach (var p in Party)
        {
            p.IsActive = false;
        }
    }

    [RelayCommand]
    private void Fight()
    {
        _game.Services.Audio.PlaySfx("swing");
        AfterEngine(_combat.Advance());
    }

    [RelayCommand]
    private void Flee() => Act(new CombatAction(CombatActionKind.Run));

    [RelayCommand]
    private void Bribe() => AfterEngine(_combat.TryBribe());

    private void Act(CombatAction action)
    {
        if (Phase == CombatPhase.Opening)
        {
            // Running from the opening screen: the monsters may still get the first strike.
            AfterEngine(_combat.Advance());
            if (_combat.Outcome != CombatOutcome.Ongoing)
            {
                return;
            }
        }
        AfterEngine(_combat.Act(action));
    }

    [RelayCommand]
    private void Attack() => Act(new CombatAction(CombatActionKind.Attack, TargetIndex));

    [RelayCommand]
    private void Shoot() => Act(new CombatAction(CombatActionKind.Shoot, TargetIndex));

    [RelayCommand]
    private void Block() => Act(new CombatAction(CombatActionKind.Block));

    [RelayCommand]
    private void Run() => Act(new CombatAction(CombatActionKind.Run));

    /// <summary>Selects a target monster.</summary>
    /// <param name="m">Monster card.</param>
    [RelayCommand]
    private void Target(MonsterViewModel m)
    {
        if (!m.Monster.IsActive)
        {
            return;
        }
        TargetIndex = m.Index;
        RefreshAll();
    }

    [RelayCommand]
    private void ShowSpells()
    {
        if (_combat.ActiveCharacter is not { } c)
        {
            return;
        }
        Choices.Clear();
        var caster = _game.Services.Session.Spells;
        foreach (var s in _game.Services.Session.Rules.KnownSpells(c).Where(s => s.Combat))
        {
            Choices.Add(new CombatChoice(s.Id, $"L{s.Level} {s.Name} ({s.Cost} SP)", s.Target, caster.CanCast(c, s, true) is null));
        }
        Phase = CombatPhase.Spell;
        Prompt = $"{c.Name} has {c.Sp} SP. Choose a spell.";
    }

    [RelayCommand]
    private void ShowItems()
    {
        if (_combat.ActiveCharacter is not { } c)
        {
            return;
        }
        Choices.Clear();
        var db = _game.Services.Content;
        for (var i = 0; i < c.Backpack.Count; i++)
        {
            var d = db.Item(c.Backpack[i].ItemId);
            if (d.UseSpell is not null && db.Spell(d.UseSpell).Combat)
            {
                Choices.Add(new CombatChoice(i.ToString(System.Globalization.CultureInfo.InvariantCulture), d.Name, db.Spell(d.UseSpell).Target, true));
            }
        }
        Phase = CombatPhase.Item;
        Prompt = Choices.Count == 0 ? $"{c.Name} has nothing usable in battle." : "Choose an item to use.";
    }

    /// <summary>Picks a spell or item.</summary>
    /// <param name="choice">Choice.</param>
    [RelayCommand]
    private void Choose(CombatChoice choice)
    {
        if (!choice.Enabled)
        {
            return;
        }
        var isSpell = Phase == CombatPhase.Spell;
        _pending = isSpell
            ? new CombatAction(CombatActionKind.Cast, TargetIndex, SpellId: choice.Id)
            : new CombatAction(CombatActionKind.UseItem, TargetIndex, ItemIndex: int.Parse(choice.Id, System.Globalization.CultureInfo.InvariantCulture));
        if (choice.Target == TargetKind.Ally)
        {
            Phase = CombatPhase.Ally;
            Prompt = "Choose a party member.";
            return;
        }
        var action = _pending;
        _pending = null;
        AfterEngine(_combat.Act(action));
    }

    /// <summary>Picks an ally target.</summary>
    /// <param name="member">Member.</param>
    [RelayCommand]
    private void ChooseAlly(PartyMemberViewModel member)
    {
        if (_pending is null)
        {
            return;
        }
        var action = _pending with { Ally = member.Index };
        _pending = null;
        AfterEngine(_combat.Act(action));
        if (Phase == CombatPhase.Ally)
        {
            Phase = CombatPhase.Action;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _pending = null;
        Phase = CombatPhase.Action;
        Prompt = _combat.ActiveCharacter is { } c ? $"{c.Name}'s turn. Choose an action." : "";
    }

    [RelayCommand]
    private void Continue() => _game.CombatFinished();

    /// <summary>True while a list (spells, items, allies) is open, where the D-pad should move between buttons.</summary>
    public bool WantsMenuNavigation => Phase is CombatPhase.Spell or CombatPhase.Item or CombatPhase.Ally;

    /// <summary>Executes a controller command.</summary>
    /// <param name="command">Command.</param>
    public void Gamepad(AVAMMB1.Core.Input.CombatCommand command)
    {
        switch (command)
        {
            case AVAMMB1.Core.Input.CombatCommand.Primary when Phase == CombatPhase.Opening:
                Fight();
                break;
            case AVAMMB1.Core.Input.CombatCommand.Primary when Phase == CombatPhase.Finished:
                Continue();
                break;
            case AVAMMB1.Core.Input.CombatCommand.Primary when Phase == CombatPhase.Action:
                if (CanMelee) { Attack(); } else if (CanShoot) { Shoot(); } else { Block(); }
                break;
            case AVAMMB1.Core.Input.CombatCommand.Cast when Phase == CombatPhase.Action && CanCast:
                ShowSpells();
                break;
            case AVAMMB1.Core.Input.CombatCommand.UseItem when Phase == CombatPhase.Action:
                ShowItems();
                break;
            case AVAMMB1.Core.Input.CombatCommand.Block when Phase == CombatPhase.Action:
                Block();
                break;
            case AVAMMB1.Core.Input.CombatCommand.Run when Phase == CombatPhase.Opening:
                Flee();
                break;
            case AVAMMB1.Core.Input.CombatCommand.Run when Phase == CombatPhase.Action:
                Run();
                break;
            case AVAMMB1.Core.Input.CombatCommand.PreviousTarget:
            case AVAMMB1.Core.Input.CombatCommand.NextTarget:
                {
                    var targets = Monsters.Where(m => m.Monster.IsActive).ToList();
                    if (targets.Count == 0)
                    {
                        break;
                    }
                    var i = targets.FindIndex(m => m.Index == TargetIndex);
                    var step = command == AVAMMB1.Core.Input.CombatCommand.NextTarget ? 1 : -1;
                    Target(targets[((i < 0 ? 0 : i + step) % targets.Count + targets.Count) % targets.Count]);
                    break;
                }
        }
    }

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key >= Key.D1 && key <= Key.D9)
        {
            var n = key - Key.D1;
            if (Phase == CombatPhase.Ally && n < Party.Count)
            {
                ChooseAlly(Party[n]);
            }
            else if (IsChoosing && n < Choices.Count)
            {
                Choose(Choices[n]);
            }
            else if (n < Monsters.Count)
            {
                Target(Monsters[n]);
            }
            return true;
        }
        switch (Phase)
        {
            case CombatPhase.Opening:
                if (key is Key.F or Key.Enter or Key.Space) { Fight(); return true; }
                if (key == Key.R) { Flee(); return true; }
                if (key == Key.B && CanBribe) { Bribe(); return true; }
                return key == Key.Escape;
            case CombatPhase.Action:
                if (key == Key.A && CanMelee) { Attack(); return true; }
                if (key == Key.S && CanShoot) { Shoot(); return true; }
                if (key == Key.C && CanCast) { ShowSpells(); return true; }
                if (key == Key.U) { ShowItems(); return true; }
                if (key == Key.B) { Block(); return true; }
                if (key == Key.R) { Run(); return true; }
                return key == Key.Escape;
            case CombatPhase.Spell:
            case CombatPhase.Item:
            case CombatPhase.Ally:
                if (key == Key.Escape) { Cancel(); return true; }
                return false;
            case CombatPhase.Finished:
                if (key is Key.Enter or Key.Space or Key.Escape) { Continue(); return true; }
                return false;
        }
        return false;
    }
}
