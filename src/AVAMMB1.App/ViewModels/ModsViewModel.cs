using System.Collections.ObjectModel;
using Avalonia.Input;
using AVAMMB1.App.Services;
using AVAMMB1.Core.Persistence;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>Mods screen: the packs found on disk, switched on or off (applies after a restart).</summary>
public sealed partial class ModsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly HashSet<string> _initiallyOff;

    /// <summary>Creates the screen.</summary>
    /// <param name="main">Root view model.</param>
    public ModsViewModel(MainViewModel main)
    {
        _main = main;
        var off = main.Services.Settings.DisabledMods;
        _initiallyOff = off.ToHashSet(StringComparer.Ordinal);
        foreach (var m in App.Mods.All)
        {
            Packs.Add(new ModRow(m.Id, $"{m.Manifest.Name}  v{m.Manifest.Version}" + (m.Manifest.Author.Length > 0 ? $"  by {m.Manifest.Author}" : ""),
                m.Manifest.Description, m.Root, !off.Contains(m.Id)));
        }
        Problems = string.Join("\n", App.Mods.Problems);
        Folder = UserDataPaths.ModsDirectory;
        Active = App.Mods.Active.Count == 0 ? "No mod packs are active." : "Active now: " + string.Join(", ", App.Mods.Active.Select(m => m.Manifest.Name));
    }

    /// <summary>Packs found.</summary>
    public ObservableCollection<ModRow> Packs { get; } = new();
    /// <summary>Whether no packs were found.</summary>
    public bool NoPacks => Packs.Count == 0;
    /// <summary>Unreadable packs.</summary>
    public string Problems { get; }
    /// <summary>Whether any pack could not be read.</summary>
    public bool HasProblems => Problems.Length > 0;
    /// <summary>The user's Mods folder.</summary>
    public string Folder { get; }
    /// <summary>Which packs this session uses.</summary>
    public string Active { get; }

    /// <summary>Restart notice.</summary>
    [ObservableProperty]
    private string _notice = "";

    /// <summary>Ensures the Mods folder exists (for "Open folder").</summary>
    public string EnsureFolder()
    {
        Directory.CreateDirectory(Folder);
        return Folder;
    }

    [RelayCommand]
    private void Back()
    {
        var off = Packs.Where(p => !p.Enabled).Select(p => p.Id).ToList();
        var s = _main.Services.Settings;
        s.DisabledMods = off;
        _main.Services.SaveSettings();
        if (!off.ToHashSet(StringComparer.Ordinal).SetEquals(_initiallyOff))
        {
            Notice = "Saved. Restart AVAM&M to apply the change - mod packs load at start-up.";
            return;
        }
        _main.ShowTitle();
    }

    [RelayCommand]
    private void Close() => _main.ShowTitle();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key == Key.Escape)
        {
            Close();
            return true;
        }
        return false;
    }
}

/// <summary>A mod pack row.</summary>
public sealed partial class ModRow(string id, string title, string description, string folder, bool enabled) : ObservableObject
{
    /// <summary>Pack id.</summary>
    public string Id { get; } = id;
    /// <summary>Name, version and author.</summary>
    public string Title { get; } = title;
    /// <summary>Description.</summary>
    public string Description { get; } = description;
    /// <summary>Folder on disk.</summary>
    public string Folder { get; } = folder;

    /// <summary>Whether the pack is switched on.</summary>
    [ObservableProperty]
    private bool _enabled = enabled;
}
