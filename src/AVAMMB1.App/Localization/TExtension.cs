using Avalonia.Markup.Xaml;
using AVAMMB1.Core.Info;

namespace AVAMMB1.App.Localization;

/// <summary>
/// Translated text in XAML: <c>Content="{l:T 'New Game'}"</c> shows "New Game" in the current language
/// (see <see cref="Loc"/>). Screens pick up a language change the next time they are shown.
/// </summary>
/// <param name="text">The English text.</param>
public sealed class TExtension(string text) : MarkupExtension
{
    /// <summary>Creates an empty one (XAML).</summary>
    public TExtension() : this("")
    {
    }

    /// <summary>The English text.</summary>
    public string Text { get; set; } = text;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Text);
}
