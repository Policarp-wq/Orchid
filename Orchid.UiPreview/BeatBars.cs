namespace Orchid.UiPreview;

public record BeatsChangedEventArgs(IReadOnlyList<bool> Beats);

/// <summary>
/// One vertical bar per beat of the measure. A tall bar (two filled blocks) is a strong beat, a short bar (one block) is weak.
/// The bar count follows <see cref="Beats"/>, so any meter (3, 4, 6, 8 ...) lays itself out. The control owns no other state:
/// bind <see cref="Beats"/>, listen to <see cref="BeatsChanged"/>. Platform-neutral contract: docs/orchid-design/UI-SPEC.md, "Shared components".
/// </summary>
public class BeatBars : ContentView
{
    private const double BlockHeight = 28, MaxBarWidth = 56;

    public static readonly BindableProperty BeatsProperty = BindableProperty.Create(nameof(Beats),
        typeof(IReadOnlyList<bool>), typeof(BeatBars), Array.Empty<bool>(),
        propertyChanged: (bindable, _, _) => ((BeatBars)bindable).Update());

    public IReadOnlyList<bool> Beats
    {
        get => (IReadOnlyList<bool>)GetValue(BeatsProperty);
        set => SetValue(BeatsProperty, value);
    }

    public event EventHandler<BeatsChangedEventArgs>? BeatsChanged;

    private readonly Grid _grid = new() { ColumnSpacing = 8 };
    private readonly List<(Button Top, Button Bottom)> _bars = [];

    public BeatBars() => Content = _grid;

    private void Update()
    {
        var beats = Beats;
        if (_bars.Count != beats.Count) Rebuild(beats.Count);
        for (var beat = 0; beat < beats.Count; beat++) Apply(beat, beats[beat]);
    }

    private void Rebuild(int count)
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _bars.Clear();
        for (var beat = 0; beat < count; beat++)
        {
            var index = beat;
            // The top block toggles the beat; the bottom block only ever sets it to weak.
            var top = CreateBlock();
            var bottom = CreateBlock();
            top.Clicked += (_, _) => Set(index, !Beats[index]);
            bottom.Clicked += (_, _) => Set(index, false);
            var number = new Label { Text = $"{beat + 1}", HorizontalOptions = LayoutOptions.Center,
                Style = ThemeResources.Get<Style>("SecondaryLabel") };
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var bar = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center,
                MaximumWidthRequest = MaxBarWidth, Children = { top, bottom, number } };
            _grid.Add(bar, beat);
            _bars.Add((top, bottom));
        }
    }

    private static Button CreateBlock() => new()
    {
        HeightRequest = BlockHeight, MinimumHeightRequest = BlockHeight, WidthRequest = MaxBarWidth, Padding = 0,
        HorizontalOptions = LayoutOptions.Fill,
    };

    private void Apply(int beat, bool strong)
    {
        var (top, bottom) = _bars[beat];
        top.Style = ThemeResources.Get<Style>(strong ? "BeatBlockOn" : "BeatBlockOff");
        bottom.Style = ThemeResources.Get<Style>(strong ? "BeatBlockOn" : "BeatBlockWeak");
        var state = strong ? "strong" : "weak";
        SemanticProperties.SetDescription(top, $"Beat {beat + 1}: {state}, activate to make it {(strong ? "weak" : "strong")}");
        SemanticProperties.SetDescription(bottom, $"Beat {beat + 1}: make weak");
        ToolTipProperties.SetText(top, $"Beat {beat + 1}: {state}");
        ToolTipProperties.SetText(bottom, $"Beat {beat + 1}: weak");
    }

    private void Set(int beat, bool strong)
    {
        if (Beats[beat] == strong) return;
        Beats = PreviewSession.WithBeat(Beats, beat, strong);
        BeatsChanged?.Invoke(this, new BeatsChangedEventArgs(Beats));
    }
}
