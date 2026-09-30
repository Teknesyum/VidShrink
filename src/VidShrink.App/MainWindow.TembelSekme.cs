using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.App.Recorder;

namespace VidShrink.App;

public partial class MainWindow
{
    private RecorderView? _recorderPane;
    private EditorView? _editorPane;

    /// <summary>
    /// Kaydedici sekmesinin icerigi ilk secildiginde kuruluyor. XAML'in icinde dururken
    /// <c>InitializeComponent</c>'in olculen payinin 89,4 ms'i buydu ve acilista sekme
    /// hicbir zaman secili degil; kurulum sekmeye basilan ana tasindi.
    ///
    /// <para>Kenar payi <c>PageMargin</c> belirtecinden okunuyor, XAML'deki degerin
    /// aynisi; burada sayi yazilmiyor.</para>
    /// </summary>
    private RecorderView RecorderPane
    {
        get
        {
            if (_recorderPane is not null) return _recorderPane;

            _recorderPane = new RecorderView();
            if (this.TryFindResource("PageMargin", out var pay) && pay is Thickness kalinlik)
                _recorderPane.Margin = kalinlik;

            _recorderPane.OpenInShrink = OpenInShrinkAsync;
            _recorderPane.OpenInPlayer = OpenInPlayerAsync;
            _recorderPane.OpenInEditor = path => OpenInEditorAsync(path);
            _recorderPane.RecordingDelivered = FollowRecordingAsync;
            _recorderPane.ShrinkForFinish = ShrinkForFinishAsync;
            PageRecorder.Content = _recorderPane;
            return _recorderPane;
        }
    }

    internal RecorderView RecorderPaneForTest => RecorderPane;

    /// <summary>
    /// Kaydedicinin "Küçült ve panoya kopyala" kolu. Kayıt, Küçült sekmesinin seçili hedefiyle
    /// kuyruk penceresine gider (<see cref="OpenBatch"/>); işin çıktısı döner, iş düşerse ya da
    /// pencere iş bitmeden kapanırsa <c>null</c>.
    /// </summary>
    internal Task<string?> ShrinkForFinishAsync(string path)
    {
        var done = new TaskCompletionSource<string?>();
        var window = OpenBatch(new[] { path });
        window.JobFinished += (_, output) => done.TrySetResult(output);
        window.Closed += (_, _) => done.TrySetResult(null);
        window.Show();
        return done.Task;
    }

    private void KaydediciSekmesiSecildi()
    {
        if (Tabs.SelectedItem is TabItem secili && ReferenceEquals(secili, TabRecorder)) _ = RecorderPane;
    }

    private EditorView EditorPane
    {
        get
        {
            if (_editorPane is not null) return _editorPane;

            _editorPane = new EditorView { KnownInfo = Media.InfoFor, OpenInPlayer = OpenInPlayerAsync };
            PageEditor.Content = _editorPane;
            return _editorPane;
        }
    }

    internal EditorView EditorPaneForTest => EditorPane;

    internal int EditorTabIndex => Tabs.Items.IndexOf(TabEditor);

    private void DuzenleyiciSekmesiSecildi()
    {
        if (Tabs.SelectedItem is TabItem secili && ReferenceEquals(secili, TabEditor))
        {
            EditorPane.Activate(Media.Path);
            return;
        }

        _editorPane?.Deactivate();
    }
}
