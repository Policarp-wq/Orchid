using Orchid.Ui.State;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Layouts;

public partial class WideShell : ContentView
{
    private const double Gap = 16;

    public WideShell() => InitializeComponent();

    /// <summary>The moment panel sits beside the timeline when there is room and below it as one line otherwise; nothing is rebuilt.</summary>
    public void Apply(LayoutKind kind)
    {
        var side = kind == LayoutKind.WideSide;
        Toolbar.IsCompact = kind == LayoutKind.Compact;
        Moment.IsCompact = !side;
        Grid.SetRow(Moment, side ? 0 : 1);
        Grid.SetColumn(Moment, side ? 1 : 0);
        ReviewBody.ColumnDefinitions[1].Width = side ? GridLength.Auto : new GridLength(0);
        ReviewBody.RowDefinitions[1].Height = side ? new GridLength(0) : GridLength.Auto;
        ReviewBody.ColumnSpacing = side ? Gap : 0;
        ReviewBody.RowSpacing = side ? 0 : Gap;
        if (side) Moment.SetDynamicResource(WidthRequestProperty, "MomentPanelWidth");
        else
        {
            Moment.RemoveDynamicResource(WidthRequestProperty);
            Moment.WidthRequest = -1;
        }
    }

    private async void OnOpenLog(object? sender, EventArgs args)
    {
        if (BindingContext is not WorkspaceState state) return;
        var file = await FilePicker.Default.PickAsync();
        if (file is not null) await state.OpenAsync(file.FullPath);
    }

    private void OnBannerAction(object? sender, EventArgs args)
    {
        if (BindingContext is WorkspaceState state) state.BannerActionCommand.Execute(null);
    }
}
