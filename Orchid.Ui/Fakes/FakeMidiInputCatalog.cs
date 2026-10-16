using Orchid.Ui.Contracts;

namespace Orchid.Ui.Fakes;

public sealed class FakeMidiInputCatalog : IMidiInputCatalog
{
    /// <summary>Add or remove inputs between refreshes to simulate devices coming and going.</summary>
    public List<MidiInputInfo> Inputs { get; } = [];

    public Task<IReadOnlyList<MidiInputInfo>> GetInputsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MidiInputInfo>>([.. Inputs]);
}
