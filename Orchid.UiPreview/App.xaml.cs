namespace Orchid.UiPreview;

public class App : Microsoft.Maui.Controls.Application
{
    public App() => Resources.MergedDictionaries.Add(new Theme());

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new WorkspacePage()) { Title = "Orchid · UI preview", Width = 1240, Height = 900 };
}
