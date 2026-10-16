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
        });

    public ReviewToolbar() => InitializeComponent();

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
