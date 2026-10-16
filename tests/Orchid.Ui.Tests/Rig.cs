using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.State;

namespace Orchid.Ui.Tests;

/// <summary>A workspace on fakes and a manual clock, initialized like the app does at startup.</summary>
internal sealed class Rig
{
    private Rig(RecordingData script)
    {
        Catalog = new FakeMidiInputCatalog { Inputs = { new MidiInputInfo("a", "Piano"), new MidiInputInfo("b", "Controller") } };
        Recorder = new ScriptedRecorder(Clock, script);
        State = new WorkspaceState(new OrchidServices(Catalog, Recorder, Store, Clock));
    }

    public ManualClock Clock { get; } = new();
    public FakeMidiInputCatalog Catalog { get; }
    public ScriptedRecorder Recorder { get; }
    public InMemoryRecordingStore Store { get; } = new();
    public WorkspaceState State { get; }

    public static async Task<Rig> CreateAsync(RecordingData? script = null)
    {
        var rig = new Rig(script ?? Fixtures.Shared());
        await rig.State.InitializeAsync(CancellationToken.None);
        return rig;
    }

    /// <summary>Opens a recording in review through the store, as Open log does.</summary>
    public async Task OpenAsync(RecordingData recording)
    {
        Store.Files["opened"] = new LoadResult.Loaded(recording, "opened");
        await State.OpenAsync("opened");
    }
}
