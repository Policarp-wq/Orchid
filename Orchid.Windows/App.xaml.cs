using Orchid.Ui.Layouts;
using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Windows;

public class App : Microsoft.Maui.Controls.Application
{
    private readonly WorkspaceState _state;

    public App(WorkspaceState state, AppTheme? theme)
    {
        _state = state;
        Resources.MergedDictionaries.Add(new Styles());
        if (theme is { } requested) UserAppTheme = requested;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new ContentPage { Content = new AdaptiveHost(_state) }) { Title = Strings.WindowTitle, Width = 1240, Height = 900 };
}
