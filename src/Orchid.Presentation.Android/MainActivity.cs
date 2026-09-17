using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace Orchid.Presentation.Android;

[Activity(
    Label = "Orchid MIDI Probe",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = global::Android.Content.PM.ScreenOrientation.SensorLandscape)]
internal sealed class MainActivity : Activity
{
    private const int MaximumLogLines = 500;
    private readonly Queue<string> logLines = new();
    private TextView? logTextView;
    private ScrollView? logScrollView;
    private AndroidMidiProbe? midiProbe;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SetContentView(CreateContentView());
        midiProbe = new AndroidMidiProbe(this, AppendLogLine);
        RefreshProbe();
    }

    protected override void OnDestroy()
    {
        midiProbe?.Dispose();
        midiProbe = null;
        base.OnDestroy();
    }

    private View CreateContentView()
    {
        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        root.SetPadding(ToPixels(16), ToPixels(12), ToPixels(16), ToPixels(12));

        var title = new TextView(this)
        {
            Text = "Orchid MIDI Probe",
            TextSize = 24
        };
        root.AddView(title);

        var actions = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };

        var refreshButton = new Button(this) { Text = "Refresh" };
        refreshButton.Click += (_, _) => RefreshProbe();
        actions.AddView(refreshButton);

        var clearButton = new Button(this) { Text = "Clear output" };
        clearButton.Click += (_, _) => ClearLog();
        actions.AddView(clearButton);
        root.AddView(actions);

        logTextView = new TextView(this)
        {
            TextSize = 14
        };
        logTextView.SetTypeface(
            global::Android.Graphics.Typeface.Monospace,
            global::Android.Graphics.TypefaceStyle.Normal);
        logTextView.SetTextIsSelectable(true);
        logTextView.SetPadding(0, ToPixels(8), 0, ToPixels(8));

        logScrollView = new ScrollView(this);
        logScrollView.AddView(logTextView);
        root.AddView(
            logScrollView,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                0,
                weight: 1));

        return root;
    }

    private void RefreshProbe()
    {
        ClearLog();
        AppendLogLine("Scanning the device...");
        AppendLogLine(string.Empty);
        midiProbe?.Scan();
    }

    private void ClearLog()
    {
        logLines.Clear();

        if (logTextView is not null)
        {
            logTextView.Text = string.Empty;
        }
    }

    private void AppendLogLine(string line)
    {
        logLines.Enqueue(line);

        while (logLines.Count > MaximumLogLines)
        {
            logLines.Dequeue();
        }

        if (logTextView is not null)
        {
            logTextView.Text = string.Join(System.Environment.NewLine, logLines);
            logScrollView?.Post(() => logScrollView.FullScroll(FocusSearchDirection.Down));
        }
    }

    private int ToPixels(int densityIndependentPixels)
    {
        return (int)Math.Round(densityIndependentPixels * Resources!.DisplayMetrics!.Density);
    }
}
