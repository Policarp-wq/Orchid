namespace Orchid.Ui.Theme;

/// <summary>
/// The drawing colors of one theme, read once from the token pairs instead of by name on every frame.
/// <see cref="Current"/> is rebuilt when the application's theme changes.
/// </summary>
public sealed record Palette(
    Color Surface, Color SubtleSurface, Color Text, Color TextSecondary, Color Border, Color Accent,
    Color NoteFill, Color NoteStroke, Color NoteText, Color SelectedFill, Color SelectedStroke, Color SelectedText,
    Color GridBar, Color GridBeat, Color GridTick, Color StrongBeatTint, Color KeyRow, Color BarLabelFill, Color BarLabelText)
{
    private static Palette? _current;
    private static AppTheme _theme;

    public static Palette Current
    {
        get
        {
            var theme = Microsoft.Maui.Controls.Application.Current?.RequestedTheme ?? AppTheme.Light;
            if (_current is null || theme != _theme)
            {
                _current = Read(theme);
                _theme = theme;
            }
            return _current;
        }
    }

    // The pedal span and its tails are the note outline color, lighter.
    public Color PedalFill => NoteStroke.WithAlpha(.6f);
    public Color TailFill => NoteStroke.WithAlpha(.3f);

    private static Palette Read(AppTheme theme)
    {
        var suffix = theme == AppTheme.Dark ? "Dark" : "Light";
        Color Token(string name) => ThemeResources.Get<Color>(name + suffix);
        return new Palette(
            Token("Surface"), Token("SubtleSurface"), Token("Text"), Token("TextSecondary"), Token("Border"), Token("Accent"),
            Token("NoteFill"), Token("NoteStroke"), Token("NoteText"), Token("SelectedFill"), Token("SelectedStroke"), Token("SelectedText"),
            Token("GridBar"), Token("GridBeat"), Token("GridTick"), Token("StrongBeatTint"), Token("KeyRow"), Token("BarLabelFill"), Token("BarLabelText"));
    }
}
