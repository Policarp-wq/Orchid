using Orchid.Core;

namespace Orchid.Application;

public sealed record RhythmSession(
    Tempo Tempo,
    TimeSpan Duration,
    IReadOnlyList<RecordedNote> Notes);
