using Orchid.Ui.Controls;
using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Views;

public partial class ReviewToolbar : ContentView
{
    /// <summary>Moves the rare actions (zoom, Fit, Follow, Fold, grid settings) into an overflow modal.</summary>
    public static readonly BindableProperty IsCompactProperty = BindableProperty.Create(nameof(IsCompact), typeof(bool), typeof(ReviewToolbar), false,
        propertyChanged: (bindable, _, compact) =>
        {
            var toolbar = (ReviewToolbar)bindable;
            toolbar.Tools.IsVisible = !(bool)compact;
            toolbar.More.IsVisible = (bool)compact;
            toolbar.PlaceTools();
        });

    public ReviewToolbar()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PlaceTools();
    }

    // Changes only with the window width, never with content: the transport has reserved widths.
    private void PlaceTools()
    {
        if (Width <= 0) return;
        var needed = Transport.Measure(double.PositiveInfinity, double.PositiveInfinity).Width + Root.ColumnSpacing
            + Tools.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        var wrap = Tools.IsVisible && needed > Width;
        Grid.SetRow(Tools, wrap ? 1 : 0);
        Grid.SetColumn(Tools, wrap ? 0 : 1);
        Grid.SetColumnSpan(Tools, wrap ? 2 : 1);
        Tools.HorizontalOptions = wrap ? LayoutOptions.Start : LayoutOptions.End;
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private void OnGridSettings(object? sender, EventArgs args)
    {
        if (BindingContext is ReviewState review)
            Modal.Of(this)?.Present(Strings.GridSettings, new GridSettingsBody { BindingContext = review });
    }

    private void OnMore(object? sender, EventArgs args)
    {
        if (BindingContext is ReviewState review)
            Modal.Of(this)?.Present(Strings.More, new ReviewMoreBody { BindingContext = review });
    }
}
