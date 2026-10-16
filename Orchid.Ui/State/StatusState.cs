using Orchid.Ui.Contracts;
using Orchid.Ui.Theme;

namespace Orchid.Ui.State;

public enum SaveState { NotSaved, Saving, Saved, Failed }

/// <summary>File name, save state, the banner problem and the recovery offer. Saving is shown only when the store confirmed it.</summary>
public sealed class StatusState : ObservableObject
{
    private string _fileName = "";
    private SaveState _saveState;
    private string? _bannerText;
    private string? _bannerAction;
    private RecordingData? _recovery;

    public string FileName { get => _fileName; set => Set(ref _fileName, value); }

    public SaveState SaveState
    {
        get => _saveState;
        set
        {
            if (Set(ref _saveState, value)) Raise(nameof(SaveText));
        }
    }

    public string SaveText => SaveState switch
    {
        SaveState.Saving => Strings.SaveSaving,
        SaveState.Saved => Strings.SaveSaved,
        SaveState.Failed => Strings.SaveFailed,
        _ => Strings.SaveNotSaved,
    };

    /// <summary>The banner under the header: a device, audio, save or load problem. Null hides it.</summary>
    public string? BannerText
    {
        get => _bannerText;
        private set
        {
            if (Set(ref _bannerText, value)) Raise(nameof(HasBanner));
        }
    }

    public bool HasBanner => BannerText is not null;

    /// <summary>The banner button: Retry for a failed save, Dismiss otherwise.</summary>
    public string? BannerAction { get => _bannerAction; private set => Set(ref _bannerAction, value); }

    public ProblemCode? BannerProblem { get; private set; }

    public RecordingData? Recovery
    {
        get => _recovery;
        set
        {
            if (Set(ref _recovery, value)) Raise(nameof(HasRecovery));
        }
    }

    public bool HasRecovery => Recovery is not null;

    public void ShowProblem(ProblemCode code)
    {
        BannerProblem = code;
        BannerAction = code is ProblemCode.SaveFailed or ProblemCode.DiskLow ? Strings.Retry : Strings.Dismiss;
        BannerText = Display.Problem(code);
    }

    public void ShowLoadProblem(LoadResult result)
    {
        BannerProblem = null;
        BannerAction = Strings.Dismiss;
        BannerText = result switch
        {
            LoadResult.Missing => Strings.LoadMissing,
            LoadResult.UnsupportedVersion => Strings.LoadUnsupported,
            _ => Strings.LoadInvalid,
        };
    }

    public void ClearBanner()
    {
        BannerProblem = null;
        BannerAction = null;
        BannerText = null;
    }
}
