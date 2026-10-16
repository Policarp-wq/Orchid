using Orchid.Ui.Controls;
using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Views;

public partial class ReviewMoreBody : ContentView
{
    public ReviewMoreBody() => InitializeComponent();

    private void OnGridSettings(object? sender, EventArgs args)
    {
        if (BindingContext is ReviewState review)
            Modal.Of(this)?.Present(Strings.GridSettings, new GridSettingsBody { BindingContext = review });
    }
}
