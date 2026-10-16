using Orchid.Ui.Controls;
using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Views;

/// <summary>Grid display settings. They change only the drawing, never the recording.</summary>
public partial class GridSettingsBody : ContentView
{
    public GridSettingsBody()
    {
        InitializeComponent();
        SubdivisionField.ItemText = item => Display.Format(Strings.SubdivisionValue, item);
    }

    private void OnSubdivision(object? sender, ItemSelectedEventArgs args)
    {
        if (BindingContext is ReviewState review) review.GridSettings = review.GridSettings with { Subdivision = (int)args.Item };
    }

    private void OnShowSubdivisions(object? sender, ToggledEventArgs args)
    {
        if (BindingContext is ReviewState review) review.GridSettings = review.GridSettings with { ShowSubdivisions = args.Value };
    }

    private void OnShowStrongBeats(object? sender, ToggledEventArgs args)
    {
        if (BindingContext is ReviewState review) review.GridSettings = review.GridSettings with { ShowStrongBeats = args.Value };
    }
}
