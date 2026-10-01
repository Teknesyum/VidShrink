namespace VidShrink.Core.Editing;

internal interface IEditCommand
{
    void Apply(List<EditClip> clips);

    void Revert(List<EditClip> clips);
}

internal sealed class SplitCommand : IEditCommand
{
    private readonly EditClip _original;
    private readonly EditClip _first;
    private readonly EditClip _second;

    public SplitCommand(int index, EditClip original, EditClip first, EditClip second)
    {
        Index = index;
        _original = original;
        _first = first;
        _second = second;
    }

    public int Index { get; }

    public void Apply(List<EditClip> clips)
    {
        clips[Index] = _first;
        clips.Insert(Index + 1, _second);
    }

    public void Revert(List<EditClip> clips)
    {
        clips.RemoveAt(Index + 1);
        clips[Index] = _original;
    }
}

internal sealed class RemoveCommand : IEditCommand
{
    private readonly int _index;
    private readonly EditClip _clip;

    public RemoveCommand(int index, EditClip clip)
    {
        _index = index;
        _clip = clip;
    }

    public void Apply(List<EditClip> clips) => clips.RemoveAt(_index);

    public void Revert(List<EditClip> clips) => clips.Insert(_index, _clip);
}

internal sealed class InsertCommand : IEditCommand
{
    private readonly int _index;
    private readonly EditClip _clip;

    public InsertCommand(int index, EditClip clip)
    {
        _index = index;
        _clip = clip;
    }

    public void Apply(List<EditClip> clips) => clips.Insert(_index, _clip);

    public void Revert(List<EditClip> clips) => clips.RemoveAt(_index);
}

internal sealed class MoveCommand : IEditCommand
{
    private readonly int _from;
    private readonly int _to;

    public MoveCommand(int from, int to)
    {
        _from = from;
        _to = to;
    }

    public void Apply(List<EditClip> clips) => Shift(clips, _from, _to);

    public void Revert(List<EditClip> clips) => Shift(clips, _to, _from);

    private static void Shift(List<EditClip> clips, int from, int to)
    {
        var clip = clips[from];
        clips.RemoveAt(from);
        clips.Insert(to, clip);
    }
}

internal sealed class ReplaceCommand : IEditCommand
{
    private readonly int _index;
    private readonly EditClip _before;
    private readonly EditClip _after;

    public ReplaceCommand(int index, EditClip before, EditClip after)
    {
        _index = index;
        _before = before;
        _after = after;
    }

    public void Apply(List<EditClip> clips) => clips[_index] = _after;

    public void Revert(List<EditClip> clips) => clips[_index] = _before;
}

/// <summary>
/// T1 izindeki tek degisiklik: <c>before</c> bos ise ekleme, <c>after</c> bos ise silme,
/// ikisi doluysa yer degistirme. Metin listesini kurucuda yakalar; klip listesine dokunmaz,
/// boylece metin ve klip degisiklikleri ayni geri al yiginina girer.
/// </summary>
internal sealed class TextCommand : IEditCommand
{
    private readonly List<TextLayer> _texts;
    private readonly int _index;
    private readonly TextLayer? _before;
    private readonly TextLayer? _after;

    public TextCommand(List<TextLayer> texts, int index, TextLayer? before, TextLayer? after)
    {
        _texts = texts;
        _index = index;
        _before = before;
        _after = after;
    }

    public void Apply(List<EditClip> clips) => Swap(_before, _after);

    public void Revert(List<EditClip> clips) => Swap(_after, _before);

    private void Swap(TextLayer? from, TextLayer? to)
    {
        if (from is null) _texts.Insert(_index, to!);
        else if (to is null) _texts.RemoveAt(_index);
        else _texts[_index] = to;
    }
}

internal sealed class CompositeCommand : IEditCommand
{
    private readonly IReadOnlyList<IEditCommand> _steps;

    public CompositeCommand(IReadOnlyList<IEditCommand> steps) => _steps = steps;

    public void Apply(List<EditClip> clips)
    {
        foreach (var step in _steps) step.Apply(clips);
    }

    public void Revert(List<EditClip> clips)
    {
        for (var i = _steps.Count - 1; i >= 0; i--) _steps[i].Revert(clips);
    }
}
