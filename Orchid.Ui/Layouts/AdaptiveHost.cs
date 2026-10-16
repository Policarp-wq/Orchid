using System.ComponentModel;
using Orchid.Ui.Controls;
using Orchid.Ui.State;
using Orchid.Ui.Theme;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Layouts;

/// <summary>
/// The root of the UI: owns the shell, the single modal overlay and the frame timer. It measures itself, asks <see cref="LayoutRules"/> which
/// arrangement fits and applies it to the shell that was built once. The timer runs only while the state is ticking (live or playing).
/// Windows-only key handling is in the platform partial.
/// </summary>
public partial class AdaptiveHost : ContentView, IModalPresenter
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly WorkspaceState _state;
    private readonly WideShell _shell = new();
    private readonly ModalDialog _modal = new();
    private IDispatcherTimer? _timer;
    private Action? _closed;
    private (string Title, View? Body, Action? Closed)? _suspended;

    public AdaptiveHost(WorkspaceState state)
    {
        _state = state;
        BindingContext = state;
        _shell.BindingContext = state;
        Content = new Grid { Children = { _shell, _modal } };
        _modal.Closed += OnModalClosed;
        state.PropertyChanged += OnStateChanged;
        SizeChanged += (_, _) => ApplyLayout();
        Loaded += async (_, _) =>
        {
            AttachPlatformKeys();
            await state.InitializeAsync(CancellationToken.None);
        };
        Unloaded += (_, _) => StopTimer();
    }

    /// <summary>Space stops a recording and Ctrl+0 fits the review; handled in the platform partial.</summary>
    partial void AttachPlatformKeys();

    public void Present(string title, View body, Action? closed = null)
    {
        if (_modal.IsOpen)
        {
            // One modal at a time: the open one waits and comes back when this one closes.
            _suspended = (_modal.Title, _modal.Body, _closed);
        }
        _closed = closed;
        _modal.Title = title;
        _modal.Body = body;
        _modal.Open();
    }

    public void Dismiss() => _modal.Close();

    private void OnModalClosed(object? sender, EventArgs args)
    {
        var closed = _closed;
        _closed = null;
        closed?.Invoke();
        if (_suspended is not { } previous) return;
        _suspended = null;
        _closed = previous.Closed;
        _modal.Title = previous.Title;
        _modal.Body = previous.Body;
        _modal.Open();
    }

    private void ApplyLayout()
    {
        if (Width <= 0 || Height <= 0) return;
        var thresholds = new LayoutThresholds(ThemeResources.Get<double>("CompactMaxWidth"),
            ThemeResources.Get<double>("CompactMaxHeight"), ThemeResources.Get<double>("SidePanelMinWidth"));
        _shell.Apply(LayoutRules.For(Width, Height, thresholds));
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(WorkspaceState.IsTicking)) return;
        if (_state.IsTicking) StartTimer();
        else StopTimer();
    }

    private void StartTimer()
    {
        _timer ??= CreateTimer();
        _timer.Start();
    }

    private IDispatcherTimer CreateTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = FrameInterval;
        timer.Tick += (_, _) => _state.Tick();
        return timer;
    }

    private void StopTimer() => _timer?.Stop();
}
