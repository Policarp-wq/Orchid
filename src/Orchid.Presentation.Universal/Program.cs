using System.Globalization;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

var isDebugLoggingEnabled = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
{
    Console.WriteLine("Live MIDI device input is supported by DryWetMidi only on Windows and macOS.");
    Console.WriteLine("This console is running without MIDI device enumeration.");
    return;
}

InputDevice[] devices;

try
{
    devices = InputDevice.GetAll().ToArray();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Unable to enumerate MIDI input devices: {exception.Message}");
    return;
}

if (devices.Length == 0)
{
    Console.WriteLine("No MIDI input devices found.");
    return;
}

Console.WriteLine("Available MIDI input devices:");

for (var index = 0; index < devices.Length; index++)
{
    Console.WriteLine($"[{index}] {devices[index].Name}");
}

Console.Write("Select a MIDI input device by index: ");

if (!int.TryParse(Console.ReadLine(), out var selectedIndex) ||
    selectedIndex < 0 ||
    selectedIndex >= devices.Length)
{
    Console.Error.WriteLine("The selected MIDI input device index is invalid.");
    return;
}

using var selectedDevice = InputDevice.GetByName(devices[selectedIndex].Name);
selectedDevice.SilentNoteOnPolicy = SilentNoteOnPolicy.NoteOff;
using var cancellationSource = new CancellationTokenSource();
var outputLock = new object();
var pressedNotes = new Dictionary<(int Channel, int NoteNumber), DateTimeOffset>();
var logDirectoryPath = Path.Combine(Environment.CurrentDirectory, "logs");
Directory.CreateDirectory(logDirectoryPath);

var logFilePath = Path.Combine(
    logDirectoryPath,
    $"midi-events-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");

using var logWriter = new StreamWriter(logFilePath, append: false)
{
    AutoFlush = true
};

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

selectedDevice.EventReceived += OnMidiEventReceived;

void OnMidiEventReceived(object? sender, MidiEventReceivedEventArgs eventArgs)
{
    var receivedAt = DateTimeOffset.UtcNow;

    lock (outputLock)
    {
        try
        {
            if (isDebugLoggingEnabled)
            {
                WriteLogLine(
                    $"{FormatTimestamp(receivedAt)} | Received | " +
                    $"{eventArgs.Event.GetType().Name} | {eventArgs.Event}");
            }

            HandleMidiEvent(eventArgs.Event, receivedAt);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"MIDI event processing failed: {exception}");
        }
    }
}

void HandleMidiEvent(MidiEvent midiEvent, DateTimeOffset receivedAt)
{
    switch (midiEvent)
    {
        case NoteOnEvent noteOnEvent when (int)noteOnEvent.Velocity > 0:
            HandlePressedNote(noteOnEvent, receivedAt);
            break;
        case NoteOffEvent noteOffEvent:
            HandleReleasedNote(
                (int)noteOffEvent.Channel,
                (int)noteOffEvent.NoteNumber,
                (int)noteOffEvent.Velocity,
                receivedAt);
            break;
    }
}

try
{
    selectedDevice.StartEventsListening();
    Console.WriteLine($"Listening to '{selectedDevice.Name}'. Press Ctrl+C to stop.");
    Console.WriteLine($"Writing note events to '{logFilePath}'.");
    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationSource.Token);
}
catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
{
    Console.WriteLine();
    Console.WriteLine("MIDI input listening stopped.");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"MIDI input failed: {exception.Message}");
}

void HandlePressedNote(NoteOnEvent noteOnEvent, DateTimeOffset receivedAt)
{
    var channel = (int)noteOnEvent.Channel;
    var noteNumber = (int)noteOnEvent.NoteNumber;
    var velocity = (int)noteOnEvent.Velocity;

    pressedNotes[(channel, noteNumber)] = receivedAt;

    WriteLogLine(
        $"{FormatTimestamp(receivedAt)} | Pressed | {GetNoteName(noteNumber)} | " +
        $"Piano key {GetPianoKeyNumber(noteNumber)} | MIDI {noteNumber} | " +
        $"Channel {channel + 1} | Velocity {velocity}");
}

void HandleReleasedNote(int channel, int noteNumber, int releaseVelocity, DateTimeOffset receivedAt)
{
    var durationText = "unknown";

    if (pressedNotes.Remove((channel, noteNumber), out var pressedAt))
    {
        durationText = $"{(receivedAt - pressedAt).TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)} s";
    }

    WriteLogLine(
        $"{FormatTimestamp(receivedAt)} | Released | {GetNoteName(noteNumber)} | " +
        $"Piano key {GetPianoKeyNumber(noteNumber)} | MIDI {noteNumber} | " +
        $"Channel {channel + 1} | Release velocity {releaseVelocity} | " +
        $"Held {durationText}");
}

void WriteLogLine(string line)
{
    Console.WriteLine(line);
    logWriter.WriteLine(line);
}

static string FormatTimestamp(DateTimeOffset timestamp)
{
    return timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}

static string GetNoteName(int noteNumber)
{
    string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    var noteName = noteNames[noteNumber % noteNames.Length];
    var octave = (noteNumber / noteNames.Length) - 1;

    return $"{noteName}{octave}";
}

static string GetPianoKeyNumber(int noteNumber)
{
    const int firstPianoMidiNote = 21;
    const int lastPianoMidiNote = 108;

    return noteNumber is < firstPianoMidiNote or > lastPianoMidiNote
        ? "N/A"
        : (noteNumber - firstPianoMidiNote + 1).ToString(CultureInfo.InvariantCulture);
}
