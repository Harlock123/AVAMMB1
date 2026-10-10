namespace AVAMMB1.Core.Session;

/// <summary>Category of a log message (used for coloring).</summary>
public enum MessageKind
{
    /// <summary>Neutral information.</summary>
    Info,
    /// <summary>Something good happened.</summary>
    Good,
    /// <summary>Something bad happened.</summary>
    Bad,
    /// <summary>Combat narration.</summary>
    Combat,
    /// <summary>Treasure and rewards.</summary>
    Loot,
    /// <summary>Story or quest text.</summary>
    Story,
}

/// <summary>A message for the game log, optionally with a sound effect cue.</summary>
/// <param name="Text">Message text.</param>
/// <param name="Kind">Category.</param>
/// <param name="Sound">Sound effect key to play, if any.</param>
public sealed record GameMessage(string Text, MessageKind Kind = MessageKind.Info, string? Sound = null)
{
    /// <summary>A visual effect for battle ("fire", "cold", "electric", "acid", "magic", "holy", "heal"), if any.</summary>
    public string? Effect { get; init; }
}
