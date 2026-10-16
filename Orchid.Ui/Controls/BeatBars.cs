using Orchid.Ui.Theme;

namespace Orchid.Ui.Controls;

public record BeatsChangedEventArgs(IReadOnlyList<bool> Beats);

/// <summary>
/// One vertical bar per beat of the measure. A tall bar (two filled blocks) is a strong beat, a short bar (one block) is weak.
/// The bar count follows <see cref="Beats"/>, so any meter (3, 4, 6, 8 ...) lays itself out, wrapping onto further rows when blocks
/// would get narrower than <c>BeatBlockMinWidth</c>. The control owns no other state: bind <see cref="Beats"/>, listen to
/// <see cref="BeatsChanged"/>. A read-only bar lights <see cref="CurrentBeat"/> in the accent for the live view.
/// Platform-neutral contract: docs/orchid-design/UI-SPEC.md, "Shared components".
/// </summary>
public class BeatBars : ContentView
{
    private const double Spacing = 8;

    public static readonly BindableProperty BeatsProperty = BindableProperty.Create(nameof(Beats),
        typeof(IReadOnlyList<bool>), typeof(BeatBars), Array.Empty<bool>(), BindingMode.TwoWay,
        propertyChanged: (bindable, _, _) => ((BeatBars)bindable).Update());

    public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(nameof(IsReadOnly),
        typeof(bool), typeof(BeatBars), false,
        propertyChanged: (bindable, _, _) => ((BeatBars)bindable).Update());

    /// <summary>The lit beat from 0, or -1 for none.</summary>
    public static readonly BindableProperty CurrentBeatProperty = BindableProperty.Create(nameof(CurrentBeat),
        typeof(int), typeof(BeatBars), -1,
        propertyChanged: (bindable, _, _) => ((BeatBars)bindable).Update());

    public static readonly BindableProperty BlockMaxWidthProperty = BindableProperty.Create(nameof(BlockMaxWidth),
        typeof(double), typeof(BeatBars), 56d, propertyChanged: (bindable, _, _) => ((BeatBars)bindable).Rebuild());

    public static readonly BindableProperty BlockHeightProperty = BindableProperty.Create(nameof(BlockHeight),
        typeof(double), typeof(BeatBars), 28d, propertyChanged: (bindable, _, _) => ((BeatBars)bindable).Rebuild());

    private readonly Grid _grid = new() { ColumnSpacing = Spacing, RowSpacing = Spacing, HorizontalOptions = LayoutOptions.Center };
    private readonly List<(Button Top, Button Bottom)> _bars = [];
    private BeatRows.Layout _layout;

    public BeatBars()
    {
        SetDynamicResource(BlockMaxWidthProperty, "BeatBlockMaxWidth");
        SetDynamicResource(BlockHeightProperty, "BeatBlockHeight");
        Content = _grid;
        SizeChanged += (_, _) =>
        {
            if (BeatRows.For(Beats.Count, Width, MinBlockWidth, BlockMaxWidth, Spacing) != _layout) Rebuild();
        };
    }

    public IReadOnlyList<bool> Beats
    {
        get => (IReadOnlyList<bool>)GetValue(BeatsProperty);
        set => SetValue(BeatsProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public int CurrentBeat
    {
        get => (int)GetValue(CurrentBeatProperty);
        set => SetValue(CurrentBeatProperty, value);
    }

    public double BlockMaxWidth
    {
        get => (double)GetValue(BlockMaxWidthProperty);
        set => SetValue(BlockMaxWidthProperty, value);
    }

    public double BlockHeight
    {
        get => (double)GetValue(BlockHeightProperty);
        set => SetValue(BlockHeightProperty, value);
    }

    public event EventHandler<BeatsChangedEventArgs>? BeatsChanged;

    private static double MinBlockWidth => ThemeResources.Get<double>("BeatBlockMinWidth");

    private void Update()
    {
        var beats = Beats;
        if (_bars.Count != beats.Count) Rebuild();
        else Apply();
    }

    private void Rebuild()
    {
        var beats = Beats;
        _layout = BeatRows.For(beats.Count, Width, MinBlockWidth, BlockMaxWidth, Spacing);
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        _bars.Clear();
        for (var column = 0; column < _layout.Columns; column++) _grid.ColumnDefinitions.Add(new ColumnDefinition(_layout.BlockWidth));
        for (var row = 0; row < _layout.Rows; row++) _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var beat = 0; beat < beats.Count; beat++)
        {
            var index = beat;
            // The top block toggles the beat; the bottom block only ever sets it to weak.
            var top = CreateBlock();
            var bottom = CreateBlock();
            top.Clicked += (_, _) => Set(index, !Beats[index]);
            bottom.Clicked += (_, _) => Set(index, false);
            var number = new Label { Text = (beat + 1).ToString(System.Globalization.CultureInfo.CurrentCulture), HorizontalOptions = LayoutOptions.Center };
            number.SetDynamicResource(StyleProperty, "SecondaryLabel");
            var bar = new VerticalStackLayout { Spacing = 4, Children = { top, bottom, number } };
            _grid.Add(bar, beat % _layout.Columns, beat / _layout.Columns);
            _bars.Add((top, bottom));
        }
        Apply();
    }

    private Button CreateBlock() => new() { HeightRequest = BlockHeight, MinimumHeightRequest = BlockHeight, Padding = 0, HorizontalOptions = LayoutOptions.Fill };

    private void Apply()
    {
        for (var beat = 0; beat < _bars.Count; beat++)
        {
            var strong = Beats[beat];
            var now = beat == CurrentBeat;
            var (top, bottom) = _bars[beat];
            top.SetDynamicResource(StyleProperty, strong ? now ? "BeatBlockNow" : "BeatBlockOn" : "BeatBlockOff");
            bottom.SetDynamicResource(StyleProperty, strong ? now ? "BeatBlockNow" : "BeatBlockOn" : now ? "BeatBlockNow" : "BeatBlockWeak");
            // Read-only ignores input but keeps full colors; a disabled button would fade the live metronome.
            top.InputTransparent = bottom.InputTransparent = IsReadOnly;
            var number = beat + 1;
            var description = now ? Display.Format(Strings.BeatCurrentDescription, number)
                : Display.Format(strong ? Strings.BeatStrongDescription : Strings.BeatWeakDescription, number);
            SemanticProperties.SetDescription(top, description);
            SemanticProperties.SetDescription(bottom, Display.Format(Strings.BeatMakeWeakDescription, number));
            ToolTipProperties.SetText(top, Display.Format(strong ? Strings.BeatStrongTooltip : Strings.BeatWeakTooltip, number));
            ToolTipProperties.SetText(bottom, Display.Format(Strings.BeatWeakTooltip, number));
        }
    }

    private void Set(int beat, bool strong)
    {
        if (IsReadOnly || Beats[beat] == strong) return;
        var beats = Beats.ToArray();
        beats[beat] = strong;
        Beats = beats;
        BeatsChanged?.Invoke(this, new BeatsChangedEventArgs(beats));
    }
}
