using Orchid.Application;

namespace Orchid.Presentation.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var midiInput = new DryWetMidiInput();
        using var metronome = new NAudioMetronome();
        var sessionRecorder = new RhythmSessionRecorder(new StopwatchClock());

        System.Windows.Forms.Application.Run(new RhythmSessionForm(midiInput, metronome, sessionRecorder));
    }
}
