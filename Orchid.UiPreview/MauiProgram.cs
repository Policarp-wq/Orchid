namespace Orchid.UiPreview;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .ConfigureFonts(fonts => fonts.AddFont("lucide.ttf", "Lucide"))
        .Build();
}
