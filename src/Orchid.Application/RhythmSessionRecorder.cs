using Orchid.Core;

namespace Orchid.Application;

public sealed class RhythmSessionRecorder(IMonotonicClock clock)
{
    private readonly object syncRoot = new();
    private readonly Dictionary<(int Channel, int Note), ActiveNote> activeNotes = [];
    private readonly List<RecordedNote> completedNotes = [];
    private Tempo? tempo;
    private bool isRecording;

    public bool IsRecording
    {
        get
        {
            lock (syncRoot)
            {
                return isRecording;
            }
        }
    }

    public TimeSpan Elapsed => clock.Elapsed;

    public void Start(Tempo sessionTempo)
    {
        ArgumentNullException.ThrowIfNull(sessionTempo);

        lock (syncRoot)
        {
            if (isRecording)
            {
                throw new InvalidOperationException("A rhythm session is already running.");
            }

            tempo = sessionTempo;
            activeNotes.Clear();
            completedNotes.Clear();
            clock.Restart();
            isRecording = true;
        }
    }

    public void Record(MidiNoteEventArgs midiEvent)
    {
        ArgumentNullException.ThrowIfNull(midiEvent);

        lock (syncRoot)
        {
            if (!isRecording)
            {
                return;
            }

            var eventOffset = clock.Elapsed;
            var key = (midiEvent.Channel, midiEvent.MidiNoteNumber);

            if (midiEvent.IsPressed)
            {
                CompleteActiveNote(key, eventOffset);
                activeNotes[key] = new ActiveNote(midiEvent.Velocity, eventOffset);
                return;
            }

            CompleteActiveNote(key, eventOffset);
        }
    }

    public RhythmSession Stop()
    {
        lock (syncRoot)
        {
            if (!isRecording || tempo is null)
            {
                throw new InvalidOperationException("No rhythm session is running.");
            }

            var sessionDuration = clock.Elapsed;
            clock.Stop();

            foreach (var key in activeNotes.Keys.ToArray())
            {
                CompleteActiveNote(key, sessionDuration);
            }

            isRecording = false;

            return new RhythmSession(
                tempo,
                sessionDuration,
                completedNotes.OrderBy(note => note.StartOffset).ToArray());
        }
    }

    private void CompleteActiveNote((int Channel, int Note) key, TimeSpan endOffset)
    {
        if (!activeNotes.Remove(key, out var activeNote))
        {
            return;
        }

        var duration = endOffset > activeNote.StartOffset
            ? endOffset - activeNote.StartOffset
            : TimeSpan.Zero;

        completedNotes.Add(new RecordedNote(
            key.Note,
            key.Channel,
            activeNote.Velocity,
            activeNote.StartOffset,
            duration));
    }

    private sealed record ActiveNote(int Velocity, TimeSpan StartOffset);
}
