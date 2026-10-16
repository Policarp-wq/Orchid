using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.State;
using Orchid.Windows.Backend;

namespace Orchid.Windows;

public static class MauiProgram
{
    private const string FakeOption = "--fake", FixtureOption = "--fixture", ThemeOption = "--theme";

    /// <summary>
    /// Composes the UI with the Windows MIDI input and metronome, or with fakes (<c>--fake</c>: two fake inputs and a scripted performance).
    /// The store is in memory in both cases until a file format exists (IMPLEMENTATION-DOD D5). <c>--fixture &lt;name&gt;</c> opens that
    /// fixture in review at startup, <c>--theme light|dark</c> forces a theme.
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var arguments = Environment.GetCommandLineArgs();
        var store = new InMemoryRecordingStore();
        var services = arguments.Contains(FakeOption)
            ? new OrchidServices(new FakeMidiInputCatalog { Inputs = { new("fake-piano", "Digital Piano"), new("fake-controller", "MIDI Controller") } },
                new ScriptedRecorder(TimeProvider.System, Fixtures.FiveMinutes()), store, TimeProvider.System)
            : new OrchidServices(new WindowsMidiInputCatalog(), new WindowsRecorder(), store, TimeProvider.System);
        var state = new WorkspaceState(services);
        if (Option(arguments, FixtureOption) is { } fixture) OpenFixture(state, store, fixture);
        AppTheme? theme = Option(arguments, ThemeOption)?.ToLowerInvariant() switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => null,
        };

        return MauiApp.CreateBuilder()
            .UseMauiApp(_ => new App(state, theme))
            .ConfigureFonts(fonts => fonts.AddFont("lucide.ttf", "Lucide"))
            .Build();
    }

    // The in-memory store completes synchronously, so waiting here cannot block on the UI thread.
    private static void OpenFixture(WorkspaceState state, InMemoryRecordingStore store, string name)
    {
        var path = $"fixture-{name}.orchid";
        store.Files[path] = new LoadResult.Loaded(Fixtures.ByName(name), path);
        state.OpenAsync(path).GetAwaiter().GetResult();
    }

    private static string? Option(string[] arguments, string name)
    {
        var index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}
