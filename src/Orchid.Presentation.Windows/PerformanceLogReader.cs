using System.Globalization;
using Orchid.Application;
using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal static class PerformanceLogReader
{
    private const string PianoLogHeader = "ORCHID-PIANO-LOG 1";
    private const string RhythmLogHeader = "ORCHID-RHYTHM-LOG 2";

    public static PerformanceSession Read(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
        {
            throw new InvalidDataException("The selected file is empty.");
        }

        return lines[0] switch
        {
            PianoLogHeader => ReadPianoLog(lines),
            RhythmLogHeader => ReadRhythmLog(lines),
            _ => throw new InvalidDataException("The selected file is not an Orchid performance log.")
        };
    }

    private static PerformanceSession ReadPianoLog(IReadOnlyList<string> lines)
    {
        var notes = new List<RecordedNote>();

        for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var fields = SplitFields(line);

            if (fields.Length != 4 || fields[0] != "NOTE")
            {
                throw InvalidEntry(lineIndex);
            }

            notes.Add(new RecordedNote(
                ParseMidiNote(fields[1], lineIndex),
                Channel: 0,
                Velocity: 0,
                ParseMilliseconds(fields[2], lineIndex),
                ParseMilliseconds(fields[3], lineIndex)));
        }

        var orderedNotes = notes.OrderBy(note => note.StartOffset).ToArray();
        var duration = orderedNotes.Length == 0 ? TimeSpan.Zero : orderedNotes.Max(note => note.EndOffset);

        return new PerformanceSession(null, duration, orderedNotes);
    }

    private static PerformanceSession ReadRhythmLog(IReadOnlyList<string> lines)
    {
        Tempo? tempo = null;
        TimeSpan? duration = null;
        var notes = new List<RecordedNote>();

        for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var fields = SplitFields(line);

            switch (fields[0])
            {
                case "TEMPO" when fields.Length == 3:
                    tempo = ParseTempo(fields, lineIndex);
                    break;
                case "TIME-SIGNATURE" when fields.Length == 2 && fields[1] == "4/4":
                    break;
                case "DURATION" when fields.Length == 2:
                    duration = ParseMilliseconds(fields[1], lineIndex);
                    break;
                case "NOTE" when fields.Length == 6:
                    notes.Add(ParseRhythmNote(fields, lineIndex));
                    break;
                default:
                    throw InvalidEntry(lineIndex);
            }
        }

        if (tempo is null || duration is null)
        {
            throw new InvalidDataException("The rhythm log does not contain complete session settings.");
        }

        return new PerformanceSession(tempo, duration.Value, notes.OrderBy(note => note.StartOffset).ToArray());
    }

    private static Tempo ParseTempo(IReadOnlyList<string> fields, int lineIndex)
    {
        if (!decimal.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var bpm) ||
            !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator) ||
            !Enum.IsDefined((RhythmicValue)denominator))
        {
            throw InvalidValues(lineIndex);
        }

        try
        {
            return new Tempo(bpm, (RhythmicValue)denominator);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw InvalidValues(lineIndex);
        }
    }

    private static RecordedNote ParseRhythmNote(IReadOnlyList<string> fields, int lineIndex)
    {
        if (!int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var channel) ||
            channel is < 0 or > 15 ||
            !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var velocity) ||
            velocity is < 0 or > 127)
        {
            throw InvalidValues(lineIndex);
        }

        return new RecordedNote(
            ParseMidiNote(fields[1], lineIndex),
            channel,
            velocity,
            ParseMilliseconds(fields[4], lineIndex),
            ParseMilliseconds(fields[5], lineIndex));
    }

    private static int ParseMidiNote(string value, int lineIndex)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var midiNoteNumber) ||
            midiNoteNumber is < 0 or > 127)
        {
            throw InvalidValues(lineIndex);
        }

        return midiNoteNumber;
    }

    private static TimeSpan ParseMilliseconds(string value, int lineIndex)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds) ||
            !double.IsFinite(milliseconds) ||
            milliseconds < 0)
        {
            throw InvalidValues(lineIndex);
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static string[] SplitFields(string line)
    {
        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static InvalidDataException InvalidEntry(int zeroBasedLineIndex)
    {
        return new InvalidDataException($"Invalid performance log entry at line {zeroBasedLineIndex + 1}.");
    }

    private static InvalidDataException InvalidValues(int zeroBasedLineIndex)
    {
        return new InvalidDataException($"Invalid performance log values at line {zeroBasedLineIndex + 1}.");
    }
}
