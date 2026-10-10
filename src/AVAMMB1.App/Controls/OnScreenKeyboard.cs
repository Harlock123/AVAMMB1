using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace AVAMMB1.App.Controls;

/// <summary>
/// A controller-friendly keyboard that types into a text box: the D-pad moves between keys, A presses
/// one, B cancels (restoring the old text) and Done finishes.
/// </summary>
public sealed class OnScreenKeyboard : UserControl
{
    private static readonly string[] Rows = ["1234567890", "QWERTYUIOP", "ASDFGHJKL'", "ZXCVBNM-.?"];
    private readonly TextBox _target;
    private readonly string _original;
    private readonly List<Button> _letters = new();
    private bool _upper = true;

    /// <summary>Raised when the keyboard should close.</summary>
    public event EventHandler? Closed;

    /// <summary>Creates a keyboard for a text box.</summary>
    /// <param name="target">The text box to type into.</param>
    public OnScreenKeyboard(TextBox target)
    {
        _target = target;
        _original = target.Text ?? "";
        var rows = new StackPanel { Spacing = 4 };
        rows.Children.Add(new TextBlock { Text = "Keyboard - D-pad to move, A to type, B to cancel", Opacity = 0.75, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var row in Rows)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var ch in row)
            {
                var key = Key(ch.ToString(), 40, (_, _) => Type(ch));
                if (char.IsLetter(ch))
                {
                    _letters.Add(key);
                }
                line.Children.Add(key);
            }
            rows.Children.Add(line);
        }
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        bottom.Children.Add(Key("Shift", 70, (_, _) => Shift()));
        bottom.Children.Add(Key("Space", 150, (_, _) => Type(' ')));
        bottom.Children.Add(Key("Delete", 80, (_, _) => Delete()));
        bottom.Children.Add(Key("Cancel", 80, (_, _) => Cancel()));
        bottom.Children.Add(Key("Done", 80, (_, _) => Done(), bold: true));
        rows.Children.Add(bottom);
        Styles.Add(new Style(x => x.OfType<Button>().Class(":focus").Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
        {
            Setters =
            {
                new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderBrushProperty, new SolidColorBrush(Color.Parse("#E8C468"))),
                new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderThicknessProperty, new Thickness(2)),
            },
        });
        Content = new Border
        {
            Classes = { "dialog" },
            Padding = new Thickness(12),
            Child = rows,
        };
    }

    private static Button Key(string label, double width, EventHandler<RoutedEventArgs> click, bool bold = false)
    {
        var b = new Button
        {
            Content = label,
            Width = width,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FocusAdorner = null, // the overlay layer misplaces focus adorners; the focused key is outlined below instead
        };
        b.Click += click;
        return b;
    }

    /// <summary>The first key (focused when the keyboard opens).</summary>
    public Button FirstKey => _letters[0];

    private void Type(char ch)
    {
        var text = _target.Text ?? "";
        if (_target.MaxLength > 0 && text.Length >= _target.MaxLength)
        {
            return;
        }
        var c = char.IsLetter(ch) ? (_upper ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch)) : ch;
        _target.Text = text + c;
        if (_upper && char.IsLetter(ch))
        {
            Shift(); // capitals for the first letter, then lower case
        }
    }

    private void Delete()
    {
        var text = _target.Text ?? "";
        if (text.Length > 0)
        {
            _target.Text = text[..^1];
        }
        if ((_target.Text ?? "").Length == 0 && !_upper)
        {
            Shift();
        }
    }

    private void Shift()
    {
        _upper = !_upper;
        foreach (var b in _letters)
        {
            var s = (string)b.Content!;
            b.Content = _upper ? s.ToUpperInvariant() : s.ToLowerInvariant();
        }
    }

    /// <summary>Closes, keeping the text.</summary>
    public void Done() => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>Closes, restoring the text it started with.</summary>
    public void Cancel()
    {
        _target.Text = _original;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
