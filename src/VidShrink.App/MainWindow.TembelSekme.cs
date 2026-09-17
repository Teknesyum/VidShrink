using Avalonia;
using Avalonia.Controls;
using VidShrink.App.Recorder;

namespace VidShrink.App;

public partial class MainWindow
{
    private RecorderView? _recorderPane;

    /// <summary>
    /// Kaydedici sekmesinin icerigi ilk secildiginde kuruluyor. XAML'in icinde dururken
    /// <c>InitializeComponent</c>'in olculen payinin 89,4 ms'i buydu ve acilista sekme
    /// hicbir zaman secili degil; kurulum sekmeye basilan ana tasindi.
    ///
    /// <para>Kenar payi <c>SectionMargin</c> belirtecinden okunuyor, XAML'deki degerin
    /// aynisi; burada sayi yazilmiyor.</para>
    /// </summary>
    private RecorderView RecorderPane
    {
        get
        {
            if (_recorderPane is not null) return _recorderPane;

            _recorderPane = new RecorderView();
            if (this.TryFindResource("SectionMargin", out var pay) && pay is Thickness kalinlik)
                _recorderPane.Margin = kalinlik;

            _recorderPane.OpenInShrink = OpenInShrinkAsync;
            _recorderPane.OpenInPlayer = OpenInPlayerAsync;
            _recorderPane.RecordingDelivered = FollowRecordingAsync;
            PageRecorder.Content = _recorderPane;
            return _recorderPane;
        }
    }

    private void KaydediciSekmesiSecildi()
    {
        if (Tabs.SelectedItem is TabItem secili && ReferenceEquals(secili, TabRecorder)) _ = RecorderPane;
    }
}
