namespace VidShrink.Core.Editing;

public sealed partial class EditTimeline
{
    public int AppendSources(IReadOnlyList<long> durations, ClipTransition? transition, bool joinPrevious, EditMusic? music = null)
    {
        ArgumentNullException.ThrowIfNull(durations);
        if (durations.Count == 0) throw new ArgumentException("Kaynak listesi bos", nameof(durations));
        if (durations.Any(d => d <= 0))
            throw new ArgumentOutOfRangeException(nameof(durations), "Kaynak suresi pozitif olmalidir");

        var first = _clips.Count;
        var steps = new List<IEditCommand>();
        for (var i = 0; i < durations.Count; i++)
        {
            _extraSources.Add(durations[i]);
            var joined = i > 0 || (joinPrevious && first > 0);
            var step = new InsertCommand(_clips.Count, new EditClip(0, durations[i]) { Source = _extraSources.Count, Transition = joined ? transition : null });
            step.Apply(_clips);
            steps.Add(step);
        }

        if (music is not null && music != Music)
        {
            var step = new MusicCommand(m => Music = m, Music, music);
            step.Apply(_clips);
            steps.Add(step);
        }

        Record(new CompositeCommand(steps));
        return first;
    }
}
