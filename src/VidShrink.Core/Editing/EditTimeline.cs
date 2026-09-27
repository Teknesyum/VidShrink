namespace VidShrink.Core.Editing;

/// <summary>
/// Tek kaynagin sirali kesim listesi. Her degisiklik bir komuttur ve geri al / ileri al
/// komutun kendisini tersine ya da yeniden uygular; liste kopyalanmaz. Zamanlar
/// <see cref="EditTime"/> tick'idir. Surec acmaz, dosya okumaz.
/// </summary>
public sealed class EditTimeline
{
    private readonly List<EditClip> _clips;
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public EditTimeline(IEnumerable<EditClip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        _clips = clips.ToList();
        if (_clips.Any(c => c is null))
            throw new ArgumentException("Listede bos parca var", nameof(clips));
        Clips = _clips.AsReadOnly();
    }

    /// <summary>Kaynagin tamamini ileri yonde, 1x hizla tasiyan tek parcali cizelge.</summary>
    public static EditTimeline FromSource(long sourceDuration) => new(new[] { new EditClip(0, sourceDuration) });

    public IReadOnlyList<EditClip> Clips { get; }

    public long Duration => Clips.Sum(c => c.TimelineLength);

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary><paramref name="index"/>'teki parcanin cizelgedeki baslangici; liste sonu icin sure.</summary>
    public long ClipStart(int index)
    {
        if (index < 0 || index > _clips.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Parca sirasi listenin disinda");

        long start = 0;
        for (var i = 0; i < index; i++) start += _clips[i].TimelineLength;
        return start;
    }

    /// <summary>Cizelgenin <paramref name="time"/> aninda gosterilen kaynak tick'i. Gecerli aralik [0, Duration).</summary>
    public long ToSource(long time)
    {
        long start = 0;
        if (time >= 0)
        {
            foreach (var clip in _clips)
            {
                var length = clip.TimelineLength;
                if (time < start + length) return clip.ToSource(time - start);
                start += length;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(time), time, "An cizelgenin disinda");
    }

    /// <summary>Kaynak tick'inin cizelgede ilk gorundugu an; silinmis kaynakta <c>null</c>.</summary>
    public long? ToTimeline(long source)
    {
        long start = 0;
        foreach (var clip in _clips)
        {
            if (clip.Contains(source)) return start + clip.ToOffset(source);
            start += clip.TimelineLength;
        }

        return null;
    }

    /// <summary>
    /// <paramref name="time"/> aninda keser. Parca kenarinda, cizelgenin basinda ya da sonunda
    /// kesilecek bir sey yoktur ve <c>false</c> doner. Yavas parcada kesim o anin kaynak tick'ine
    /// oturur, sinir en cok bir kaynak tick'i geriye kayar.
    /// </summary>
    public bool Split(long time)
    {
        if (time < 0 || time > Duration)
            throw new ArgumentOutOfRangeException(nameof(time), time, "An cizelgenin disinda");

        var command = PlanSplit(time, out _);
        if (command is null) return false;

        Execute(command);
        return true;
    }

    public void Delete(int index)
    {
        CheckIndex(index);
        Execute(new RemoveCommand(index, _clips[index]));
    }

    /// <summary>Parcayi tasir; <paramref name="to"/> parcanin tasindiktan sonraki sirasidir.</summary>
    public bool Move(int from, int to)
    {
        CheckIndex(from);
        CheckIndex(to);
        if (from == to) return false;

        Execute(new MoveCommand(from, to));
        return true;
    }

    /// <summary>
    /// [<paramref name="start"/>, <paramref name="end"/>) araligini siler ve arkasindakini
    /// kaydirir. Tek adimda geri alinir. Bos aralikta <c>false</c> doner.
    /// </summary>
    public bool DeleteRange(long start, long end)
    {
        if (start < 0 || end < start || end > Duration)
            throw new ArgumentOutOfRangeException(nameof(end), end, "Aralik cizelgenin disinda ya da ters");
        if (start == end) return false;

        var steps = new List<IEditCommand>();
        var endSplit = PlanSplit(end, out var last);
        if (endSplit is not null)
        {
            endSplit.Apply(_clips);
            steps.Add(endSplit);
        }

        var startSplit = PlanSplit(start, out var first);
        if (startSplit is not null)
        {
            startSplit.Apply(_clips);
            steps.Add(startSplit);
            if (startSplit.Index < last) last++;
        }

        if (first >= last)
        {
            for (var i = steps.Count - 1; i >= 0; i--) steps[i].Revert(_clips);
            return false;
        }

        for (var i = last - 1; i >= first; i--)
        {
            var remove = new RemoveCommand(i, _clips[i]);
            remove.Apply(_clips);
            steps.Add(remove);
        }

        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>
    /// Parcanin hizini yazar. Isaret yonu belirler: eksi deger parcayi geri, arti deger ileri
    /// yapar; mutlak deger 0,01'e yuvarlanip hiz olur. Sifir ve yuvarlandiktan sonra 0,01-100
    /// disinda kalan deger reddedilir. Degisiklik yoksa <c>false</c> doner.
    /// </summary>
    public bool SetSpeed(int index, decimal speed)
    {
        CheckIndex(index);
        if (speed == 0)
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Hiz sifir olamaz; geri yon eksi isaretle yazilir");

        var magnitude = decimal.Round(Math.Abs(speed), 2, MidpointRounding.AwayFromZero);
        if (magnitude < EditClip.MinSpeed || magnitude > EditClip.MaxSpeed)
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Hizin mutlak degeri 0,01 ile 100 arasinda olmalidir");

        var before = _clips[index];
        var after = before.WithMotion(magnitude, speed < 0);
        if (after == before) return false;

        Execute(new ReplaceCommand(index, before, after));
        return true;
    }

    /// <summary>
    /// Butun parcalara ayni hizi yazar; tek adimda geri alinir. Sinirlar <see cref="SetSpeed"/>
    /// ile aynidir. Hicbir parca degismezse <c>false</c> doner.
    /// </summary>
    public bool SetSpeedAll(decimal speed)
    {
        if (speed == 0)
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Hiz sifir olamaz; geri yon eksi isaretle yazilir");

        var magnitude = decimal.Round(Math.Abs(speed), 2, MidpointRounding.AwayFromZero);
        if (magnitude < EditClip.MinSpeed || magnitude > EditClip.MaxSpeed)
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Hizin mutlak degeri 0,01 ile 100 arasinda olmalidir");

        var steps = new List<IEditCommand>();
        for (var i = 0; i < _clips.Count; i++)
        {
            var before = _clips[i];
            var after = before.WithMotion(magnitude, speed < 0);
            if (after == before) continue;
            var step = new ReplaceCommand(i, before, after);
            step.Apply(_clips);
            steps.Add(step);
        }

        if (steps.Count == 0) return false;
        Record(new CompositeCommand(steps));
        return true;
    }

    public bool Undo()
    {
        if (!CanUndo) return false;

        var command = _undo.Pop();
        command.Revert(_clips);
        _redo.Push(command);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;

        var command = _redo.Pop();
        command.Apply(_clips);
        _undo.Push(command);
        return true;
    }

    private SplitCommand? PlanSplit(long time, out int boundary)
    {
        long start = 0;
        for (var i = 0; i < _clips.Count; i++)
        {
            var clip = _clips[i];
            var length = clip.TimelineLength;
            if (time == start)
            {
                boundary = i;
                return null;
            }

            if (time < start + length)
            {
                if (clip.SplitAt(time - start) is not { } pieces)
                {
                    boundary = i;
                    return null;
                }

                boundary = i + 1;
                return new SplitCommand(i, clip, pieces.First, pieces.Second);
            }

            start += length;
        }

        boundary = _clips.Count;
        return null;
    }

    private void CheckIndex(int index)
    {
        if (index < 0 || index >= _clips.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Parca sirasi listenin disinda");
    }

    private void Execute(IEditCommand command)
    {
        command.Apply(_clips);
        Record(command);
    }

    private void Record(IEditCommand command)
    {
        _undo.Push(command);
        _redo.Clear();
    }
}
