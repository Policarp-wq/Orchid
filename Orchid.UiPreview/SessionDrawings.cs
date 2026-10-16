namespace Orchid.UiPreview;

internal static class PreviewColors
{
    // Reads the Light/Dark pair of a token defined in Theme.xaml for the current app theme.
    public static Color Get(string token)
    {
        var app = Microsoft.Maui.Controls.Application.Current
            ?? throw new InvalidOperationException("Theme colors require a running application.");
        var key = token + (app.RequestedTheme == AppTheme.Dark ? "Dark" : "Light");
        return app.Resources.TryGetValue(key, out var color) && color is Color value
            ? value
            : throw new InvalidOperationException($"Theme color '{key}' is not defined.");
    }
}

internal class TimelineDrawing(PreviewSession session) : IDrawable
{
    public int GridDenominator { get; set; } = 8;
    public const float PixelsPerSecond = 160;
    private const float Top = 48;
    private const float LaneHeight = 24;
    private int Lowest => session.Notes.Count == 0 ? 60 : Math.Min(60, session.Notes.Min(note => note.Midi));
    private int Highest => session.Notes.Count == 0 ? 72 : Math.Max(72, session.Notes.Max(note => note.Midi));
    public float ContentHeight => Top + (Highest - Lowest + 1) * LaneHeight;
    public float ContentWidth => (float)Math.Max(8, Math.Ceiling(session.Duration) + 2) * PixelsPerSecond;
    public float ViewportOffset { get; set; }
    internal float TimeToX(double time) => (float)(time * PixelsPerSecond) - ViewportOffset;
    internal RectF Bounds(PreviewNote note) => new(TimeToX(note.Attack), Top + (Highest - note.Midi) * LaneHeight,
        Math.Max(2, (float)(note.Duration * PixelsPerSecond)), LaneHeight - 2);
    internal static float PanOffset(float current, double delta, float maxOffset) =>
        (float)Math.Clamp(current + delta, 0, Math.Max(0, maxOffset));

    public PreviewNote? HitTest(PointF point) => session.Notes.LastOrDefault(note => Bounds(note).Contains(point));

    public void DrawLabels(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = PreviewColors.Get("Surface");
        canvas.FillRectangle(dirtyRect);
        canvas.FontSize = 12;
        canvas.FontColor = PreviewColors.Get("TextSecondary");
        for (var midi = Lowest; midi <= Highest; midi++)
            canvas.DrawString(new PreviewNote(midi, 0, 0).Name, 4, Top + (Highest - midi) * LaneHeight,
                dirtyRect.Width - 8, LaneHeight, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = PreviewColors.Get("Surface");
        canvas.FillRectangle(dirtyRect);
        var start = ViewportOffset / PixelsPerSecond;
        var end = (ViewportOffset + dirtyRect.Width) / PixelsPerSecond;
        DrawLanes(canvas, dirtyRect);
        DrawGrid(canvas, dirtyRect, start, end);
        DrawRulerLabels(canvas, start, end);
        DrawNotes(canvas, start, end);
        canvas.StrokeColor = PreviewColors.Get("Accent");
        canvas.StrokeSize = 2;
        canvas.DrawLine(TimeToX(session.Position), 0, TimeToX(session.Position), dirtyRect.Height);
    }

    // Black-key rows are tinted and each octave ends with a line under C, instead of a line under every semitone.
    private void DrawLanes(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = PreviewColors.Get("KeyRow");
        canvas.StrokeColor = PreviewColors.Get("GridBeat");
        canvas.StrokeSize = 1;
        for (var midi = Lowest; midi <= Highest; midi++)
        {
            var y = Top + (Highest - midi) * LaneHeight;
            if (KeyboardDrawing.IsBlack(midi)) canvas.FillRectangle(0, y, dirtyRect.Width, LaneHeight);
            if (midi % 12 == 0) canvas.DrawLine(0, y + LaneHeight, dirtyRect.Width, y + LaneHeight);
        }
    }

    private void DrawGrid(ICanvas canvas, RectF dirtyRect, double start, double end)
    {
        var firstClick = Math.Max(0, (int)Math.Floor(start / session.PulseSeconds));
        var strongBeats = session.StrongBeats;
        canvas.FillColor = PreviewColors.Get("StrongBeatTint");
        for (var click = firstClick; click * session.PulseSeconds <= end; click++)
            if (strongBeats.Count == session.PulseDenominator && strongBeats[click % session.PulseDenominator])
                canvas.FillRectangle(TimeToX(click * session.PulseSeconds), Top,
                    (float)(session.PulseSeconds * PixelsPerSecond), dirtyRect.Height - Top);
        // Subdivisions are ruler ticks only, so the field shows at most two line styles: bar and beat.
        var step = session.QuarterSeconds * 4 / GridDenominator;
        canvas.StrokeColor = PreviewColors.Get("GridTick");
        canvas.StrokeSize = 1;
        for (var index = Math.Max(0, (int)Math.Floor(start / step)); index * step <= end; index++)
            canvas.DrawLine(TimeToX(index * step), Top - 6, TimeToX(index * step), Top);
        var bar = PreviewColors.Get("GridBar");
        var beat = PreviewColors.Get("GridBeat");
        for (var click = firstClick; click * session.PulseSeconds <= end; click++)
        {
            var measureStart = click % session.PulseDenominator == 0;
            canvas.StrokeColor = measureStart ? bar : beat;
            canvas.StrokeSize = measureStart ? 1.5f : 1;
            canvas.DrawLine(TimeToX(click * session.PulseSeconds), Top, TimeToX(click * session.PulseSeconds), dirtyRect.Height);
        }
        canvas.StrokeColor = beat;
        canvas.StrokeSize = 1;
        canvas.DrawLine(0, Top, dirtyRect.Width, Top);
    }

    private void DrawRulerLabels(ICanvas canvas, double start, double end)
    {
        var secondary = PreviewColors.Get("TextSecondary");
        canvas.FontSize = 12;
        var labelStride = Math.Max(1, (int)Math.Ceiling(24 / (session.QuarterSeconds * PixelsPerSecond)));
        var firstBeat = Math.Max(0, (int)Math.Floor(start / session.QuarterSeconds / labelStride) * labelStride);
        for (var quarter = firstBeat; quarter * session.QuarterSeconds <= end; quarter += labelStride)
        {
            var x = TimeToX(quarter * session.QuarterSeconds);
            if (quarter % 4 == 0)
            {
                // A measure start shows its bar number on a plate; the other beats keep their plain beat number.
                var text = (quarter / 4 + 1).ToString();
                var width = 8 + 7 * text.Length;
                canvas.FillColor = PreviewColors.Get("BarLabelFill");
                canvas.FillRoundedRectangle(x + 2, 24, width, 16, 4);
                canvas.FontColor = PreviewColors.Get("BarLabelText");
                canvas.DrawString(text, x + 2, 24, width, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            else
            {
                canvas.FontColor = secondary;
                canvas.DrawString((quarter % 4 + 1).ToString(), x - 8, 24, 16, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
        canvas.FontColor = secondary;
        for (var second = Math.Max(0, (int)Math.Floor(start)); second <= end; second++)
            canvas.DrawString($"{second} s", TimeToX(second) - 16, 0, 36, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private void DrawNotes(ICanvas canvas, double start, double end)
    {
        canvas.StrokeSize = 1;
        foreach (var note in session.Notes.Where(note => note.Release >= start && note.Attack <= end))
        {
            var bounds = Bounds(note);
            var held = note.Attack <= session.Position && session.Position < note.Release;
            canvas.FillColor = PreviewColors.Get(held ? "SelectedFill" : "NoteFill");
            canvas.FillRoundedRectangle(bounds, 3);
            canvas.StrokeColor = PreviewColors.Get(held ? "SelectedStroke" : "NoteStroke");
            canvas.DrawRoundedRectangle(bounds, 3);
            canvas.FontColor = PreviewColors.Get(held ? "SelectedText" : "NoteText");
            if (bounds.Width >= 36) canvas.DrawString(note.Name, bounds, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}

internal class PitchLabelsDrawing(TimelineDrawing timeline) : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect) => timeline.DrawLabels(canvas, dirtyRect);
}

internal class KeyboardDrawing(PreviewSession session) : IDrawable
{
    private const float KeyCorner = 4;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var whiteNotes = Enumerable.Range(48, 36).Where(midi => !IsBlack(midi)).ToArray();
        var held = session.HeldNotes.Select(note => note.Midi).ToHashSet();
        var width = dirtyRect.Width / whiteNotes.Length;
        canvas.FontSize = 10;
        canvas.StrokeSize = 1;
        for (var i = 0; i < whiteNotes.Length; i++)
        {
            var midi = whiteNotes[i];
            var bounds = new RectF(i * width, 0, width, dirtyRect.Height);
            canvas.FillColor = PreviewColors.Get(held.Contains(midi) ? "KeyHeld" : "KeyWhite");
            canvas.FillRoundedRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height, 0, 0, KeyCorner, KeyCorner);
            canvas.StrokeColor = PreviewColors.Get("Border");
            canvas.DrawLine(bounds.Right, 0, bounds.Right, bounds.Bottom);
            canvas.FontColor = PreviewColors.Get(held.Contains(midi) ? "KeyHeldText" : "KeyLabel");
            if (midi % 12 == 0) canvas.DrawString(new PreviewNote(midi, 0, 0).Name, bounds.Left, bounds.Bottom - 22, width, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        for (var i = 0; i < whiteNotes.Length - 1; i++)
        {
            var midi = whiteNotes[i] + 1;
            if (!IsBlack(midi)) continue;
            var keyHeight = dirtyRect.Height * .62f;
            var left = (i + 1) * width - width * .3f;
            canvas.FillColor = PreviewColors.Get(held.Contains(midi) ? "KeyHeldBlack" : "KeyBlack");
            canvas.FillRoundedRectangle(left, 0, width * .6f, keyHeight, 0, 0, KeyCorner - 1, KeyCorner - 1);
        }
    }

    internal static bool IsBlack(int midi) => midi % 12 is 1 or 3 or 6 or 8 or 10;
}
