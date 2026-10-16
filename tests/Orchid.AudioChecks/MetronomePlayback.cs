using Orchid.Application;

namespace Orchid.AudioChecks;

sealed class MetronomePlayback
{
    private readonly MusicPlayThrough _session;

    public MetronomePlayback(MusicPlayThrough session)
    {
        _session = session;
    }

    public static bool IsRequested(string[] args)
    {
        if (args.Contains("--play")) return true;
        Console.WriteLine("Use --play for metronome playback, --midi-list to list inputs, or --midi <index> to record MIDI.");
        return false;
    }

    public async Task Run()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try { await _session.Start(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
}
