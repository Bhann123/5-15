using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FimmFimmtan.Core;

namespace FimmFimmtan.App.Controls;

/// <summary>Horizontal 0–2 scale: the child's score as a bar, with the norm group's 90th and 98th percentile zones behind it.</summary>
public sealed class NormBar : Control
{
    public static readonly StyledProperty<ScaleResult?> ResultProperty =
        AvaloniaProperty.Register<NormBar, ScaleResult?>(nameof(Result));

    static NormBar() => AffectsRender<NormBar>(ResultProperty);

    public ScaleResult? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    private const double Max = 2.0;
    private static readonly IBrush Track = new SolidColorBrush(Color.Parse("#EEF1F5"));
    private static readonly IBrush Zone90 = new SolidColorBrush(Color.Parse("#FBEBC4"));
    private static readonly IBrush Zone98 = new SolidColorBrush(Color.Parse("#F6D2CD"));
    private static readonly IBrush BarTypical = new SolidColorBrush(Color.Parse("#3B7CB8"));
    private static readonly IBrush Bar90 = new SolidColorBrush(Color.Parse("#D89512"));
    private static readonly IBrush Bar98 = new SolidColorBrush(Color.Parse("#C2410C"));
    private static readonly IPen MeanPen = new Pen(new SolidColorBrush(Color.Parse("#64748B")), 1.5);

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var trackH = Math.Min(14, h);
        var y = (h - trackH) / 2;
        double X(double v) => Math.Clamp(v / Max, 0, 1) * w;

        ctx.DrawRectangle(Track, null, new RoundedRect(new Rect(0, y, w, trackH), 3));
        if (Result is not { Norm: { IsAvailable: true } n } r) return;

        ctx.DrawRectangle(Zone90, null, new Rect(X(n.P90), y, X(n.P98) - X(n.P90), trackH));
        ctx.DrawRectangle(Zone98, null, new RoundedRect(new Rect(X(n.P98), y, w - X(n.P98), trackH), 0, 3, 3, 0));

        if (r.Score is { } s)
        {
            var brush = r.Level switch { Level.Above98 => Bar98, Level.Above90 => Bar90, _ => BarTypical };
            var barH = trackH * 0.55;
            ctx.DrawRectangle(brush, null, new RoundedRect(new Rect(0, y + (trackH - barH) / 2, Math.Max(3, X(s)), barH), 2));
        }
        ctx.DrawLine(MeanPen, new Point(X(n.Mean), y - 2), new Point(X(n.Mean), y + trackH + 2));
    }
}
