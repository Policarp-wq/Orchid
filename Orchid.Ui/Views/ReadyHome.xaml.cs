using Orchid.Ui.State;

namespace Orchid.Ui.Views;

public partial class ReadyHome : ContentView
{
    public ReadyHome() => InitializeComponent();

    private async void OnOpenLog(object? sender, EventArgs args)
    {
        if (BindingContext is not WorkspaceState state) return;
        var file = await FilePicker.Default.PickAsync();
        if (file is not null) await state.OpenAsync(file.FullPath);
    }
}
