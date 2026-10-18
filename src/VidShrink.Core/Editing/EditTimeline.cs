namespace VidShrink.Core.Editing;

/// <summary>
/// Bir ya da birden cok kaynagin sirali kesim listesi; her parca kaynaginin sirasini tasir. Her degisiklik bir komuttur ve geri al / ileri al
/// komutun kendisini tersine ya da yeniden uygular; liste kopyalanmaz. Zamanlar
/// <see cref="EditTime"/> tick'idir. Surec acmaz, dosya okumaz.
/// </summary>
public sealed class EditTimeline
{
    private readonly List<EditClip> _clips;
    private readonly List<TextLayer> _texts;
    private readonly List<long> _extraSources;
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public EditTimeline(IEnumerable<EditClip> clips, long? sourceDuration = null, IEnumerable<TextLayer>? texts = null, IEnumerable<long>? extraSources = null,
        EditFrame frame = default)
    {
        Frame = frame.Normalized();
        ArgumentNullException.ThrowIfNull(clips);
        _clips = clips.ToList();
        _texts = texts?.ToList() ?? new List<TextLayer>();
        foreach (var text in _texts) text.Validate();
        Texts = _texts.AsReadOnly();
        if (_clips.Any(c => c is null))
            throw new ArgumentException("Listede bos parca var", nameof(clips));
        if (sourceDuration is <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceDuration), sourceDuration, "Kaynak suresi pozitif olmalidir");
        _extraSources = extraSources?.ToList() ?? new List<long>();
        if (_extraSources.Any(d => d <= 0))
            throw new ArgumentOutOfRangeException(nameof(extraSources), "Kaynak suresi pozitif olmalidir");
        Clips = _clips.AsReadOnly();
        SourceDuration = sourceDuration;
        ExtraSources = _extraSources.AsReadOnly();
    }

    /// <summary>Kaynagin tamamini ileri yonde, 1x hizla tasiyan tek parcali cizelge.</summary>
    public static EditTimeline FromSource(long sourceDuration) => new(new[] { new EditClip(0, sourceDuration) }, sourceDuration);

    public IReadOnlyList<EditClip> Clips { get; }

    public long? SourceDuration { get; }

    /// <summary>Sonradan eklenen kaynaklarin sureleri; sirasi 1'den baslar. Liste yalniz buyur, geri al kaynagi listeden dusurmez.</summary>
    public IReadOnlyList<long> ExtraSources { get; }

    /// <summary>Kaynagin suresi; ilk kaynagin suresi bilinmiyorsa ya da sira listede yoksa <c>null</c>.</summary>
    public long? DurationOf(int source)
        => source == 0 ? SourceDuration : source > 0 && source <= _extraSources.Count ? _extraSources[source - 1] : null;

    /// <summary>
    /// Yeni bir kaynagi kaydeder ve tamamini cizelgenin sonuna tek parca olarak ekler. Parcanin
    /// eklenmesi tek adimda geri alinir. Kaynagin sirasi doner.
    /// </summary>
    public int AddSource(long duration)
    {
        if (duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Kaynak suresi pozitif olmalidir");
        _extraSources.Add(duration);
        var source = _extraSources.Count;
        Execute(new InsertCommand(_clips.Count, new EditClip(0, duration) { Source = source }));
        return source;
    }

    /// <summary>
    /// Yeni bir kaynagi kaydeder ve tamamini <paramref name="at"/> anina araya ekler; sonraki parcalar iter.
    /// Tek adimda geri alinir. Konan parcanin sirasi doner.
    /// </summary>
    public int InsertSource(long duration, long at)
    {
        if (duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Kaynak suresi pozitif olmalidir");
        if (at < 0 || at > Duration)
            throw new ArgumentOutOfRangeException(nameof(at), at, "An cizelgenin disinda");
        _extraSources.Add(duration);
        return PlaceSource(_extraSources.Count, at, overwrite: false);
    }

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
    public long? ToTimeline(long source, int sourceIndex = 0)
    {
        long start = 0;
        foreach (var clip in _clips)
        {
            if (clip.Source == sourceIndex && clip.Contains(source)) return start + clip.ToOffset(source);
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

    /// <summary>Parcayi siler; parca bir gruptaysa grubun butunu tek adimda silinir.</summary>
    public void Delete(int index)
    {
        CheckIndex(index);
        if (_clips[index].Group is null)
        {
            Execute(new RemoveCommand(index, _clips[index]));
            return;
        }

        RemoveAll(WithGroups(new[] { index }));
    }

    /// <summary>
    /// Parcayi tasir; <paramref name="to"/> parcanin tasindiktan sonraki sirasidir. Parca bir
    /// gruptaysa grup bitisik blok olarak tasinir, parca blok icindeki yerini korur; tek adimda
    /// geri alinir.
    /// </summary>
    public bool Move(int from, int to)
    {
        CheckIndex(from);
        CheckIndex(to);
        if (from == to) return false;

        if (_clips[from].Group is null)
        {
            Execute(new MoveCommand(from, to));
            return true;
        }

        var members = WithGroups(new[] { from });
        var rest = Enumerable.Range(0, _clips.Count).Except(members).ToList();
        var at = Math.Clamp(to - members.ToList().IndexOf(from), 0, rest.Count);
        var order = rest.Take(at).Concat(members).Concat(rest.Skip(at));
        if (order.SequenceEqual(Enumerable.Range(0, _clips.Count))) return false;

        var moved = members.Select(i => _clips[i]).ToList();
        var steps = new List<IEditCommand>();
        foreach (var index in members.OrderByDescending(i => i))
        {
            var remove = new RemoveCommand(index, _clips[index]);
            remove.Apply(_clips);
            steps.Add(remove);
        }

        for (var i = 0; i < moved.Count; i++)
        {
            var insert = new InsertCommand(at + i, moved[i]);
            insert.Apply(_clips);
            steps.Add(insert);
        }

        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>Verilen siralari ayni gruptaki parcalarla genisletir; sirali ve tekrarsiz doner.</summary>
    public IReadOnlyList<int> WithGroups(IReadOnlyList<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        foreach (var index in indices) CheckIndex(index);

        var groups = indices.Select(i => _clips[i].Group).OfType<int>().ToHashSet();
        var picked = indices.ToHashSet();
        return Enumerable.Range(0, _clips.Count)
            .Where(i => picked.Contains(i) || _clips[i].Group is { } g && groups.Contains(g))
            .ToList();
    }

    /// <summary>
    /// Parcalari (ve gruplarinin uyelerini) yeni bir grupta toplar. Ikiden az parcada ya da hepsi
    /// zaten ayni gruptaysa <c>false</c> doner ve gecmise adim yazilmaz.
    /// </summary>
    public bool Group(IReadOnlyList<int> indices)
    {
        var members = WithGroups(indices);
        if (members.Count < 2) return false;
        var first = _clips[members[0]].Group;
        if (first is not null && members.All(i => _clips[i].Group == first)) return false;

        return Regroup(members, NextGroup());
    }

    /// <summary>Parcalarin (ve gruplarinin uyelerinin) grubunu cozer; tek adim. Gruplu parca yoksa <c>false</c>.</summary>
    public bool Ungroup(IReadOnlyList<int> indices) => Regroup(WithGroups(indices), null);

    /// <summary>Parcalari (gruplariyla) cizelge sirasiyla dondurur; gecmise adim yazmaz.</summary>
    public IReadOnlyList<EditClip> Copy(IReadOnlyList<int> indices) => WithGroups(indices).Select(i => _clips[i]).ToList();

    /// <summary>Parcalari (gruplariyla) kopyalar ve siler; tek adimda geri alinir. Kopyalanan parcalari dondurur.</summary>
    public IReadOnlyList<EditClip> Cut(IReadOnlyList<int> indices)
    {
        var members = WithGroups(indices);
        var copied = members.Select(i => _clips[i]).ToList();
        if (members.Count > 0) RemoveAll(members);
        return copied;
    }

    /// <summary>
    /// Parcalari <paramref name="at"/> anina koyar; an parcanin ortasindaysa once orada boler.
    /// Gruplar yeni kimlik alir. Tek adimda geri alinir. Ilk eklenen parcanin sirasini dondurur;
    /// liste bossa ya da bir parcanin kaynagi bu cizelgede yoksa <c>-1</c> doner ve adim yazilmaz.
    /// </summary>
    public int Paste(IReadOnlyList<EditClip> clips, long at)
    {
        ArgumentNullException.ThrowIfNull(clips);
        if (at < 0 || at > Duration)
            throw new ArgumentOutOfRangeException(nameof(at), at, "An cizelgenin disinda");
        if (clips.Count == 0 || !clips.All(Fits)) return -1;

        var map = new Dictionary<int, int>();
        var next = NextGroup();
        var placed = new List<EditClip>(clips.Count);
        foreach (var clip in clips)
        {
            if (clip.Group is not { } group)
            {
                placed.Add(clip);
                continue;
            }

            if (!map.TryGetValue(group, out var id)) map[group] = id = next++;
            placed.Add(clip with { Group = id });
        }

        var steps = new List<IEditCommand>();
        if (PlanSplit(at, out var boundary) is { } split)
        {
            split.Apply(_clips);
            steps.Add(split);
        }

        for (var i = 0; i < placed.Count; i++)
        {
            var insert = new InsertCommand(boundary + i, placed[i]);
            insert.Apply(_clips);
            steps.Add(insert);
        }

        Record(new CompositeCommand(steps));
        return boundary;
    }

    private bool Fits(EditClip clip)
        => DurationOf(clip.Source) is { } duration ? clip.SourceEnd <= duration : clip.Source == 0;

    private int NextGroup() => _clips.Select(c => c.Group ?? 0).DefaultIfEmpty(0).Max() + 1;

    private bool Regroup(IReadOnlyList<int> members, int? id)
    {
        var steps = new List<IEditCommand>();
        foreach (var index in members)
        {
            var before = _clips[index];
            if (before.Group == id) continue;
            var step = new ReplaceCommand(index, before, before with { Group = id });
            step.Apply(_clips);
            steps.Add(step);
        }

        if (steps.Count == 0) return false;
        Record(new CompositeCommand(steps));
        return true;
    }

    private void RemoveAll(IReadOnlyList<int> indices)
    {
        var steps = new List<IEditCommand>();
        foreach (var index in indices.OrderByDescending(i => i))
        {
            var remove = new RemoveCommand(index, _clips[index]);
            remove.Apply(_clips);
            steps.Add(remove);
        }

        Record(new CompositeCommand(steps));
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
        if (!CutRange(start, end, steps)) return false;

        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>
    /// Kaynagin tamamini <paramref name="at"/> anina tek parca olarak koyar. Araya eklemede sonraki
    /// parcalar kaynagin suresi kadar iter; ustune yazmada o anda baslayan ayni sure silinir ve yerine
    /// konur, cizelge ancak sonu tasarsa uzar. Tek adimda geri alinir. Konan parcanin sirasini
    /// dondurur; kaynak bu cizelgede yoksa <c>-1</c> doner ve adim yazilmaz.
    /// </summary>
    public int PlaceSource(int source, long at, bool overwrite)
    {
        if (at < 0 || at > Duration)
            throw new ArgumentOutOfRangeException(nameof(at), at, "An cizelgenin disinda");
        if (source < 0 || DurationOf(source) is not { } length) return -1;

        var steps = new List<IEditCommand>();
        if (overwrite) CutRange(at, Math.Min(at + length, Duration), steps);
        if (PlanSplit(at, out var boundary) is { } split)
        {
            split.Apply(_clips);
            steps.Add(split);
        }

        var insert = new InsertCommand(boundary, new EditClip(0, length) { Source = source });
        insert.Apply(_clips);
        steps.Add(insert);
        Record(new CompositeCommand(steps));
        return boundary;
    }

    private bool CutRange(long start, long end, List<IEditCommand> steps)
    {
        if (start == end) return false;
        var mark = steps.Count;
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
            for (var i = steps.Count - 1; i >= mark; i--) steps[i].Revert(_clips);
            steps.RemoveRange(mark, steps.Count - mark);
            return false;
        }

        for (var i = last - 1; i >= first; i--)
        {
            var remove = new RemoveCommand(i, _clips[i]);
            remove.Apply(_clips);
            steps.Add(remove);
        }

        return true;
    }

    /// <summary>
    /// Kaynak araliklarini butun parcalardan cikarir: kesen parca bolunur, tamami icinde kalan
    /// parca silinir, arkasi kayar. Tek adimda geri alinir. Hicbir sey degismezse ya da
    /// cizelgede parca kalmayacaksa <c>false</c> doner.
    /// </summary>
    public bool RemoveSource(IReadOnlyList<(long Start, long End)> ranges, int sourceIndex = 0)
        => ReshapeSource(ranges, Array.Empty<(long Start, long End)>(), 1m, sourceIndex);

    /// <summary>
    /// <paramref name="cuts"/> araliklarini cikarir, <paramref name="faster"/> araliklarini bolup
    /// hizini <paramref name="factor"/> ile carpar (en cok <see cref="EditClip.MaxSpeed"/>).
    /// Kesim hizlandirmanin onune gecer. Tek adimda geri alinir; hicbir sey degismezse ya da
    /// cizelgede parca kalmayacaksa <c>false</c> doner.
    /// </summary>
    public bool ReshapeSource(IReadOnlyList<(long Start, long End)> cuts, IReadOnlyList<(long Start, long End)> faster, decimal factor, int sourceIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(cuts);
        ArgumentNullException.ThrowIfNull(faster);
        if (cuts.Concat(faster).Any(r => r.Start < 0 || r.End < r.Start))
            throw new ArgumentOutOfRangeException(nameof(cuts), "Aralik negatif ya da ters");
        if (factor < 1m)
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Carpan 1'den kucuk olamaz");

        var plans = new List<(int Index, IReadOnlyList<EditClip> Pieces)>();
        var survivors = 0;
        for (var i = 0; i < _clips.Count; i++)
        {
            var pieces = _clips[i].Source == sourceIndex ? _clips[i].Reshape(cuts, faster, factor) : new[] { _clips[i] };
            survivors += pieces.Count;
            if (pieces.Count != 1 || !ReferenceEquals(pieces[0], _clips[i])) plans.Add((i, pieces));
        }

        if (plans.Count == 0 || survivors == 0) return false;

        var steps = new List<IEditCommand>();
        for (var p = plans.Count - 1; p >= 0; p--)
        {
            var (index, pieces) = plans[p];
            IEditCommand first = pieces.Count == 0
                ? new RemoveCommand(index, _clips[index])
                : new ReplaceCommand(index, _clips[index], pieces[0]);
            first.Apply(_clips);
            steps.Add(first);
            for (var k = 1; k < pieces.Count; k++)
            {
                var insert = new InsertCommand(index + k, pieces[k]);
                insert.Apply(_clips);
                steps.Add(insert);
            }
        }

        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>
    /// Butun parcalari kaynak sinirlarinda boler (<see cref="SceneSplit.Pieces"/>); hicbir parca
    /// silinmez, sure degismez. Tek adimda geri alinir. Bolunen parca sayisi doner; hicbir sinir
    /// kabul edilmezse 0 doner ve gecmise adim yazilmaz.
    /// </summary>
    public int SplitSource(IReadOnlyList<long> boundaries, long minPiece, int sourceIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(boundaries);

        var plans = new List<(int Index, IReadOnlyList<EditClip> Pieces)>();
        for (var i = 0; i < _clips.Count; i++)
        {
            if (_clips[i].Source != sourceIndex) continue;
            var pieces = SceneSplit.Pieces(_clips[i], boundaries, minPiece);
            if (pieces.Count > 1) plans.Add((i, pieces));
        }

        if (plans.Count == 0) return 0;

        var steps = new List<IEditCommand>();
        var added = 0;
        for (var p = plans.Count - 1; p >= 0; p--)
        {
            var (index, pieces) = plans[p];
            var first = new ReplaceCommand(index, _clips[index], pieces[0]);
            first.Apply(_clips);
            steps.Add(first);
            for (var k = 1; k < pieces.Count; k++)
            {
                var insert = new InsertCommand(index + k, pieces[k]);
                insert.Apply(_clips);
                steps.Add(insert);
                added++;
            }
        }

        Record(new CompositeCommand(steps));
        return added;
    }

    /// <summary>
    /// Kaynak araliklarinin cizelgede kapladigi yerler, sirali ve birlesik. Hizli, yavas ve ters
    /// parcada da dogru yere duser; silinmis kaynak cizelgede yer tutmaz.
    /// </summary>
    public IReadOnlyList<(long Start, long End)> SourceToTimeline(IReadOnlyList<(long Start, long End)> ranges, int sourceIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var spans = new List<(long Start, long End)>();
        long at = 0;
        foreach (var clip in _clips)
        {
            foreach (var (start, end) in ranges)
            {
                if (clip.Source == sourceIndex && clip.OffsetsOf(start, end) is { } o && o.To > o.From) spans.Add((at + o.From, at + o.To));
            }

            at += clip.TimelineLength;
        }

        spans.Sort();
        var merged = new List<(long Start, long End)>();
        foreach (var span in spans)
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End)
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
            else
                merged.Add(span);
        }

        return merged;
    }

    public bool RippleTrimHead(long time)
    {
        if (time < 0 || time > Duration)
            throw new ArgumentOutOfRangeException(nameof(time), time, "An cizelgenin disinda");

        var index = ClipIndexAt(time);
        if (index < 0) return false;
        var start = ClipStart(index);
        return time > start && DeleteRange(start, time);
    }

    public bool RippleTrimTail(long time)
    {
        if (time < 0 || time > Duration)
            throw new ArgumentOutOfRangeException(nameof(time), time, "An cizelgenin disinda");

        var index = ClipIndexAt(time);
        if (index < 0) return false;
        var start = ClipStart(index);
        return time > start && DeleteRange(time, start + _clips[index].TimelineLength);
    }

    public (long Min, long Max) TrimEdgeRange(int index, bool head)
    {
        CheckIndex(index);
        var clip = _clips[index];
        var movesStart = head != clip.Reversed;
        var neighbor = head ? index - 1 : index + 1;
        var other = neighbor >= 0 && neighbor < _clips.Count && _clips[neighbor].Source == clip.Source ? _clips[neighbor] : null;

        if (movesStart)
        {
            long min = 0;
            if (other is not null && other.SourceEnd <= clip.SourceStart) min = other.SourceEnd;
            else if (other is not null && other.SourceStart < clip.SourceEnd) min = clip.SourceStart;
            return (min, clip.SourceEnd - 1);
        }

        var max = DurationOf(clip.Source) ?? long.MaxValue;
        if (other is not null && other.SourceStart >= clip.SourceEnd) max = Math.Min(max, other.SourceStart);
        else if (other is not null && other.SourceEnd > clip.SourceStart) max = clip.SourceEnd;
        return (clip.SourceStart + 1, Math.Max(clip.SourceStart + 1, max));
    }

    public bool TrimEdge(int index, bool head, long sourceEdge)
    {
        var (min, max) = TrimEdgeRange(index, head);
        if (sourceEdge < min || sourceEdge > max)
            throw new ArgumentOutOfRangeException(nameof(sourceEdge), sourceEdge, "Kenar kaynagin ya da komsu parcanin sinirini asiyor");

        var before = _clips[index];
        var after = head != before.Reversed
            ? before.WithRange(sourceEdge, before.SourceEnd)
            : before.WithRange(before.SourceStart, sourceEdge);
        if (after == before) return false;

        Execute(new ReplaceCommand(index, before, after));
        return true;
    }

    /// <summary>Parcalari gruplariyla birlikte tek adimda siler.</summary>
    public bool DeleteMany(IReadOnlyList<int> indices)
    {
        var order = WithGroups(indices);
        if (order.Count == 0) return false;

        RemoveAll(order);
        return true;
    }

    public IReadOnlyList<long> EditPoints
    {
        get
        {
            var points = new List<long>(_clips.Count + 1) { 0 };
            long at = 0;
            foreach (var clip in _clips)
            {
                at += clip.TimelineLength;
                points.Add(at);
            }

            return points;
        }
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

    /// <summary>
    /// Parcayi hiz rampasina boler (<see cref="SpeedRamp.Pieces"/>): dilim hizlari
    /// <paramref name="from"/>'dan <paramref name="to"/>'ya dogrusal gider. Tek adimda geri alinir;
    /// parca rampaya yetecek kadar uzun degilse <c>false</c> doner.
    /// </summary>
    public bool RampSpeed(int index, decimal from, decimal to, int slices = SpeedRamp.DefaultSlices)
    {
        CheckIndex(index);
        var pieces = SpeedRamp.Pieces(_clips[index], from, to, slices);
        if (pieces.Count < 2) return false;

        var steps = new List<IEditCommand>();
        var first = new ReplaceCommand(index, _clips[index], pieces[0]);
        first.Apply(_clips);
        steps.Add(first);
        for (var k = 1; k < pieces.Count; k++)
        {
            var insert = new InsertCommand(index + k, pieces[k]);
            insert.Apply(_clips);
            steps.Add(insert);
        }

        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>Secili parcalarin akici agir cekimini acar ya da kapatir; tek adimda geri alinir. Degisiklik yoksa <c>false</c> doner.</summary>
    public bool SetSmooth(IReadOnlyList<int> indices, bool smooth)
    {
        ArgumentNullException.ThrowIfNull(indices);
        foreach (var index in indices) CheckIndex(index);

        var steps = new List<IEditCommand>();
        foreach (var index in indices.Distinct())
        {
            var before = _clips[index];
            if (before.Smooth == smooth) continue;
            var step = new ReplaceCommand(index, before, before with { Smooth = smooth });
            step.Apply(_clips);
            steps.Add(step);
        }

        if (steps.Count == 0) return false;
        Record(new CompositeCommand(steps));
        return true;
    }

    public bool HasEffects => !Frame.IsSource || _clips.Any(c => !c.Effects.IsNeutral);

    public EditFrame Frame { get; private set; }

    public bool SetFrame(EditFrame frame)
    {
        var next = frame.Normalized();
        if (next == Frame) return false;
        Execute(new FrameCommand(f => Frame = f, Frame, next));
        return true;
    }

    /// <summary>
    /// Verilen parcalarin ayarlarini <paramref name="change"/> ile yazar; sonuc gecerli araliga
    /// cekilir. Tek adimda geri alinir. Hicbir parca degismezse <c>false</c> doner.
    /// </summary>
    public bool SetEffects(IReadOnlyList<int> indices, Func<ClipEffects, ClipEffects> change)
    {
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(change);
        var order = indices.Distinct().OrderBy(i => i).ToList();
        foreach (var index in order) CheckIndex(index);

        var steps = new List<IEditCommand>();
        foreach (var index in order)
        {
            var before = _clips[index];
            var effects = change(before.Effects).Normalized();
            var after = before with { Effects = effects.IsNeutral ? ClipEffects.None : effects };
            if (after == before) continue;
            var step = new ReplaceCommand(index, before, after);
            step.Apply(_clips);
            steps.Add(step);
        }

        if (steps.Count == 0) return false;
        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>
    /// Secili parcalarin etiketini yazar (<see cref="EditClip.Label"/>); bos etiket siler. Tek
    /// adimda geri alinir; hicbir parca degismezse <c>false</c> doner ve gecmise adim yazilmaz.
    /// </summary>
    public bool SetLabel(IReadOnlyList<int> indices, string? label)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var order = indices.Distinct().OrderBy(i => i).ToList();
        foreach (var index in order) CheckIndex(index);

        var normalized = EditClip.NormalizeLabel(label);
        var steps = new List<IEditCommand>();
        foreach (var index in order)
        {
            var before = _clips[index];
            if (before.Label == normalized) continue;
            var step = new ReplaceCommand(index, before, before with { Label = normalized });
            step.Apply(_clips);
            steps.Add(step);
        }

        if (steps.Count == 0) return false;
        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>Ilk klipten sonraki en az bir klibin gecisi var; teslim o zaman Tam kipte kodlanir.</summary>
    public bool HasTransitions => _clips.Skip(1).Any(c => c.Transition is not null);

    /// <summary>
    /// Secili kliplerin onceki klipten gecisini yazar; <c>null</c> gecisi kaldirir ve klipler kesme ile birlesir. Tek adimda
    /// geri alinir; hicbir klip degismezse <c>false</c> doner ve gecmise adim yazilmaz.
    /// </summary>
    public bool SetTransition(IReadOnlyList<int> indices, ClipTransition? transition)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var order = indices.Distinct().OrderBy(i => i).ToList();
        foreach (var index in order) CheckIndex(index);

        var steps = new List<IEditCommand>();
        foreach (var index in order)
        {
            var before = _clips[index];
            if (before.Transition == transition) continue;
            var step = new ReplaceCommand(index, before, before with { Transition = transition });
            step.Apply(_clips);
            steps.Add(step);
        }

        if (steps.Count == 0) return false;
        Record(new CompositeCommand(steps));
        return true;
    }

    /// <summary>
    /// <paramref name="index"/>'teki klibin onceki klibe binme suresi: ilk klipte ya da gecissiz klipte 0, yoksa istenen sure
    /// iki klibin kisasinin yarisina kirpilir; boylece bir klip iki yanindaki gecisle tamamen yutulmaz.
    /// </summary>
    public long TransitionLength(int index)
    {
        CheckIndex(index);
        if (index == 0 || _clips[index].Transition is not { } transition) return 0;
        var limit = Math.Min(_clips[index - 1].TimelineLength, _clips[index].TimelineLength) / 2;
        return Math.Max(0, Math.Min(transition.Duration, limit));
    }

    /// <summary>Butun gecislerin binme suresi toplami; teslimin suresi <see cref="Duration"/> eksi bu degerdir.</summary>
    public long TransitionOverlap => Enumerable.Range(0, _clips.Count).Sum(TransitionLength);

    /// <summary>
    /// T1 izinin metin klipleri, eklenme sirasiyla; sonraki klip ustte cizilir. Zamanlari
    /// cizelge zamanidir, klip silmek ya da kaydirmak metni tasimaz.
    /// </summary>
    public IReadOnlyList<TextLayer> Texts { get; }

    public bool HasText => _texts.Count > 0;

    /// <summary>A2 muzik izi; yoksa <c>null</c>. Kaynak sesinin ustune karisir, klip duzenlemeleri onu tasimaz.</summary>
    public EditMusic? Music { get; private set; }

    public bool HasMusic => Music is not null;

    /// <summary>Muzik izini koyar, degistirir ya da (<c>null</c>) kaldirir. Tek adimda geri alinir; degisiklik yoksa <c>false</c>.</summary>
    public bool SetMusic(EditMusic? music)
    {
        if (music == Music) return false;
        Execute(new MusicCommand(m => Music = m, Music, music));
        return true;
    }

    /// <summary>Metni sona ekler ve sirasini dondurur. Tek adimda geri alinir.</summary>
    public int AddText(TextLayer text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text.Validate();
        Execute(new TextCommand(_texts, _texts.Count, null, text));
        return _texts.Count - 1;
    }

    /// <summary>Metni yeni baslangica tasir; suresi ve anahtar kareleri katmanla gider.</summary>
    public bool MoveText(int index, long start)
    {
        CheckText(index);
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, "Metin cizelgenin basindan once baslayamaz");
        return ReplaceText(index, _texts[index].MovedTo(start));
    }

    /// <summary>Metnin bir kenarini kirpar ya da uzatir; diger kenar yerinde kalir.</summary>
    public bool TrimText(int index, bool head, long edge)
    {
        CheckText(index);
        var before = _texts[index];
        var (start, end) = head ? (edge, before.End) : (before.Start, edge);
        if (start < 0 || end <= start)
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "Kenar metnin obur kenarini asiyor");
        return ReplaceText(index, before.TrimmedTo(start, end));
    }

    public void DeleteText(int index)
    {
        CheckText(index);
        Execute(new TextCommand(_texts, index, _texts[index], null));
    }

    /// <summary>Metnin ozelliklerini yazar (metin, boyut, renk...). Degisiklik yoksa <c>false</c>.</summary>
    public bool UpdateText(int index, TextLayer after)
    {
        CheckText(index);
        ArgumentNullException.ThrowIfNull(after);
        return ReplaceText(index, after);
    }

    /// <summary>
    /// Kayitli bir durumu tek geri alma adimi olarak yukler: parcalar ve metinler birlikte
    /// degisir, geri al yukleme oncesine doner. Durum zaten ayniysa <c>false</c>.
    /// </summary>
    public bool Restore(IEnumerable<EditClip> clips, IEnumerable<TextLayer> texts)
    {
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(texts);
        var nextClips = clips.ToArray();
        var nextTexts = texts.ToArray();
        if (nextClips.Any(c => c is null))
            throw new ArgumentException("Listede bos parca var", nameof(clips));
        foreach (var text in nextTexts) text.Validate();
        if (nextClips.SequenceEqual(_clips) && nextTexts.SequenceEqual(_texts)) return false;

        Execute(new RestoreCommand(_texts, _clips.ToArray(), _texts.ToArray(), nextClips, nextTexts));
        return true;
    }

    /// <summary>Parcalar, metinler ve muzik izi birlikte tek geri alma adimi olarak yuklenir. Durum zaten ayniysa <c>false</c>.</summary>
    public bool Restore(IEnumerable<EditClip> clips, IEnumerable<TextLayer> texts, EditMusic? music)
    {
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(texts);
        var nextClips = clips.ToArray();
        var nextTexts = texts.ToArray();
        if (nextClips.Any(c => c is null))
            throw new ArgumentException("Listede bos parca var", nameof(clips));
        foreach (var text in nextTexts) text.Validate();
        var sameBody = nextClips.SequenceEqual(_clips) && nextTexts.SequenceEqual(_texts);
        if (sameBody && music == Music) return false;

        var musicStep = new MusicCommand(m => Music = m, Music, music);
        Execute(sameBody ? musicStep : new CompositeCommand(new IEditCommand[] { new RestoreCommand(_texts, _clips.ToArray(), _texts.ToArray(), nextClips, nextTexts), musicStep }));
        return true;
    }

    public bool Restore(IEnumerable<EditClip> clips, IEnumerable<TextLayer> texts, EditMusic? music, EditFrame frame)
    {
        var next = frame.Normalized();
        if (next == Frame) return Restore(clips, texts, music);
        var before = Frame;
        var body = Restore(clips, texts, music);
        var step = new FrameCommand(f => Frame = f, before, next);
        if (!body)
        {
            Execute(step);
            return true;
        }

        var last = _undo.Pop();
        step.Apply(_clips);
        Record(new CompositeCommand(new[] { last, step }));
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

    private int ClipIndexAt(long time)
    {
        long start = 0;
        for (var i = 0; i < _clips.Count; i++)
        {
            var length = _clips[i].TimelineLength;
            if (time < start + length) return i;
            start += length;
        }

        return -1;
    }

    private void CheckIndex(int index)
    {
        if (index < 0 || index >= _clips.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Parca sirasi listenin disinda");
    }

    private bool ReplaceText(int index, TextLayer after)
    {
        after.Validate();
        var before = _texts[index];
        if (after == before) return false;
        Execute(new TextCommand(_texts, index, before, after));
        return true;
    }

    private void CheckText(int index)
    {
        if (index < 0 || index >= _texts.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Metin sirasi listenin disinda");
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
