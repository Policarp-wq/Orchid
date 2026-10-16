using Orchid.Ui.Contracts;

namespace Orchid.Ui.Fakes;

/// <summary>Keeps recordings in memory. A path that is not in <see cref="Files"/> loads as missing.</summary>
public sealed class InMemoryRecordingStore : IRecordingStore
{
    private int _saved;

    /// <summary>Set to make every save fail with this code.</summary>
    public ProblemCode? SaveFailure { get; set; }

    /// <summary>What a path loads as; put Invalid, UnsupportedVersion or an empty recording here to simulate bad files.</summary>
    public Dictionary<string, LoadResult> Files { get; } = [];

    /// <summary>An unfinished recording offered for recovery at startup.</summary>
    public RecordingData? Unfinished { get; set; }

    public Task<SaveResult> SaveAsync(RecordingData recording, CancellationToken cancellationToken)
    {
        if (SaveFailure is { } code) return Task.FromResult<SaveResult>(new SaveResult.Failed(code));
        var path = $"recording-{++_saved}.orchid";
        Files[path] = new LoadResult.Loaded(recording, path);
        return Task.FromResult<SaveResult>(new SaveResult.Saved(path));
    }

    public Task<LoadResult> LoadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(Files.TryGetValue(path, out var result) ? result : new LoadResult.Missing());

    public Task<RecordingData?> FindUnfinishedAsync(CancellationToken cancellationToken) => Task.FromResult(Unfinished);
}
