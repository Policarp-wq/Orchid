using Orchid.Application;
using Orchid.AudioChecks;
using Orchid.Core;
using Orchid.Infrastructure;
using Orchid.Platform.Windows;
using System.Diagnostics;
using Melanchall.DryWetMidi.Multimedia;

if (args.Contains("--midi-list"))
{
    MidiRecordingCheck.ListDevices();
    return;
}

if (args.Length == 2 && args[0] == "--midi")
{
    using var device = InputDevice.GetByIndex(int.Parse(args[1]));
    var storage = new TimingStorage(new StopwatchTimeProvider());
    var listener = new KeyListener(new MusicSound(MusicalNote.C, 4),
        new MidiKeyConverterProvider(new MidiNoteKeyConverter()), storage);
    var input = new DryWetMidiInput(device, listener);
    await new MidiRecordingCheck(device, input, storage).Run();
    return;
}

if (!MetronomePlayback.IsRequested(args)) return;

using var logListener = new ConsoleTraceListener(useErrorStream: true);
var trace = new TraceSource("Orchid.Metronome", SourceLevels.Critical);
trace.Listeners.Clear();
trace.Listeners.Add(logListener);

var score = new MusicScoreDefinition(TimeSignature.WaltzTime, 120, MusicalNote.C, 4);
using var sound = new NAudioBeatSoundPlayer();
var session = new MusicPlayThrough(new TaskDelayProvider(), score, score.Bpm, sound, trace);
await new MetronomePlayback(session).Run();
