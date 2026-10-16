using Orchid.Ui.Contracts;
using Orchid.Ui.Controls;
using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Views;

public partial class SetupPanel : ContentView
{
    public SetupPanel()
    {
        InitializeComponent();
        InputField.ItemText = item => ((MidiInputInfo)item).Name;
        PulseField.ItemText = item => Display.Format(Strings.PulseValue, item);
        CountInField.ItemText = item => Display.CountIn((int)item);
    }

    private void OnEditStrongBeats(object? sender, EventArgs args)
    {
        if (BindingContext is WorkspaceState state)
            Modal.Of(this)?.Present(Strings.StrongBeats, new StrongBeatsBody { BindingContext = state.Setup });
    }
}
