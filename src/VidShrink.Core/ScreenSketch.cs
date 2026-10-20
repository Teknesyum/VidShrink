namespace VidShrink.Core;

public enum SketchTool
{
    Pen,
    Arrow,
    Box,
    Number,
    Text
}

/// <summary>Çizimin mürekkebi; uygulama her birini paletin bir rengine bağlar.</summary>
public enum SketchInk
{
    Ember,
    Blue,
    Pink,
    Green
}

public readonly record struct SketchPoint(double X, double Y);

/// <summary>Ekrana çizilmiş tek iz. Numara izinde <see cref="Number"/>, yazı izinde <see cref="Text"/> dolu; <see cref="Ink"/> çizildiği andaki mürekkep.</summary>
public sealed record SketchStroke(SketchTool Tool, IReadOnlyList<SketchPoint> Vertices, int Number = 0, string Text = "", SketchInk Ink = SketchInk.Ember);

/// <summary>
/// later.md #42, EKB-04: kayıt sürerken ekrana çizim. İzler eklendiği sırayla durur; geri alma son izi düşürür,
/// numara izi düşünce sıradaki numara da geri gelir. Kalem, ok ve kutu basışta başlar, sürüklemeyle uzar,
/// bırakışta biter; hiç uzamamış ok ve kutu düşer, kalemin tek noktası nokta olarak kalır.
/// </summary>
public sealed class ScreenSketch
{
    private readonly List<SketchStroke> _strokes = new();
    private List<SketchPoint>? _active;
    private SketchTool _activeTool;
    private SketchInk _activeInk;
    private SketchTool _tool = SketchTool.Pen;
    private SketchInk _ink = SketchInk.Ember;

    public SketchTool Tool
    {
        get => _tool;
        set
        {
            if (_tool == value) return;
            _tool = value;
            Selected?.Invoke();
        }
    }

    /// <summary>Bundan sonra çizilecek izlerin mürekkebi; çizilmiş izler kendi mürekkebinde kalır.</summary>
    public SketchInk Ink
    {
        get => _ink;
        set
        {
            if (_ink == value) return;
            _ink = value;
            Selected?.Invoke();
        }
    }

    public SketchInk NextInk()
    {
        var inks = Enum.GetValues<SketchInk>();
        Ink = inks[(Array.IndexOf(inks, Ink) + 1) % inks.Length];
        return Ink;
    }

    /// <summary>Araç ya da mürekkep değişti; araç paneli seçimini buna göre gösterir.</summary>
    public event Action? Selected;

    public IReadOnlyList<SketchStroke> Strokes => _strokes;

    public SketchStroke? Active => _active is null ? null : new SketchStroke(_activeTool, _active.ToArray(), Ink: _activeInk);

    public int NextNumber => _strokes.Count(s => s.Tool == SketchTool.Number) + 1;

    public event Action? Changed;

    public void Press(SketchPoint point)
    {
        switch (Tool)
        {
            case SketchTool.Number:
                _strokes.Add(new SketchStroke(SketchTool.Number, new[] { point }, NextNumber, Ink: Ink));
                Changed?.Invoke();
                return;
            case SketchTool.Text:
                return;
        }

        _activeTool = Tool;
        _activeInk = Ink;
        _active = new List<SketchPoint> { point };
        Changed?.Invoke();
    }

    public void Move(SketchPoint point)
    {
        if (_active is null) return;
        if (_activeTool == SketchTool.Pen)
        {
            if (_active[^1] != point) _active.Add(point);
        }
        else if (_active.Count == 1)
        {
            _active.Add(point);
        }
        else
        {
            _active[1] = point;
        }

        Changed?.Invoke();
    }

    public void Release()
    {
        if (_active is null) return;
        var points = _active;
        _active = null;
        if (_activeTool == SketchTool.Pen || (points.Count == 2 && points[0] != points[1]))
            _strokes.Add(new SketchStroke(_activeTool, points.ToArray(), Ink: _activeInk));
        Changed?.Invoke();
    }

    public void AddText(SketchPoint point, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _strokes.Add(new SketchStroke(SketchTool.Text, new[] { point }, Text: text.Trim(), Ink: Ink));
        Changed?.Invoke();
    }

    public bool Undo()
    {
        if (_active is not null)
        {
            _active = null;
            Changed?.Invoke();
            return true;
        }

        if (_strokes.Count == 0) return false;
        _strokes.RemoveAt(_strokes.Count - 1);
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (_strokes.Count == 0 && _active is null) return;
        _strokes.Clear();
        _active = null;
        Changed?.Invoke();
    }

    /// <summary>Kutunun sol üst köşesi ve boyu; sürükleme yönünden bağımsız.</summary>
    public static (SketchPoint TopLeft, double Width, double Height) Box(SketchPoint a, SketchPoint b)
        => (new SketchPoint(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>
    /// Ok başının iki kanadı: uçtan geriye, okun doğrultusuyla 30° açı yapan <paramref name="size"/> boyunda iki çizgi.
    /// Ok, kanattan kısaysa kanat okun boyuna iner.
    /// </summary>
    public static (SketchPoint Left, SketchPoint Right) ArrowHead(SketchPoint from, SketchPoint to, double size)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) return (to, to);
        var wing = Math.Min(size, length);
        var angle = Math.Atan2(dy, dx);
        const double spread = Math.PI / 6;
        return (new SketchPoint(to.X - wing * Math.Cos(angle - spread), to.Y - wing * Math.Sin(angle - spread)),
            new SketchPoint(to.X - wing * Math.Cos(angle + spread), to.Y - wing * Math.Sin(angle + spread)));
    }

    /// <summary>Çizim kipindeki araç tuşu: P kalem, A ok, B ya da R kutu, N numara, T yazı.</summary>
    public static SketchTool? ToolOf(char key) => char.ToUpperInvariant(key) switch
    {
        'P' => SketchTool.Pen,
        'A' => SketchTool.Arrow,
        'B' or 'R' => SketchTool.Box,
        'N' => SketchTool.Number,
        'T' => SketchTool.Text,
        _ => null
    };
}
