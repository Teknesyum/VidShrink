using System;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kayıt sürerken konan bölüm işaretleri. İşareti oturum tutar ve kayıt bitince dosyaya bölüm
/// olarak yazar; burası yalnız basışı oturuma iletir ve sonucu söyler.
///
/// <para><b>Bildirim var olan yüzeyde.</b> Basışın karşılığı durum satırına ve mini şeritte
/// sayacın yanına yazılır; kayda girebilecek yeni bir pencere ya da katman açılmaz.</para>
///
/// <para><b>Yazılamayan işaret söylenir.</b> Kap bölüm taşımıyorsa ya da yeniden paketleme
/// düştüyse sonuç panelinin uyarı satırı bunu yazar; işaretler dosyanın yanına ayrı bir dosya
/// olarak bırakılmaz.</para>
/// </summary>
internal partial class RecorderView
{
    private string _chapterText = string.Empty;

    /// <summary>İşaret yalnız kayıt koşarken konur; duraklatılmış kayıtta çıktı zamanı ilerlemiyor.</summary>
    internal bool CanMarkChapter => _session is { State: RecorderState.Running } && !_stopping;

    /// <summary>Son işaretin bildirimi; oturum yokken boş.</summary>
    internal string ChapterText => _session is null ? string.Empty : _chapterText;

    internal string WarningText => WarningRow.IsVisible ? TxtWarning.Text ?? string.Empty : string.Empty;

    /// <summary>Basışı oturuma verir; işareti koyan iş parametre, ölçü süreç açmadan kendi işini verir.</summary>
    internal bool MarkChapter() => _session is { } session && MarkChapter(session.Mark);

    internal bool MarkChapter(Func<int?> mark)
    {
        if (mark() is not { } chapter) return false;

        _chapterText = Say("recorder.chapter.marked", chapter);
        TxtError.IsVisible = false;
        ShowNotice(_chapterText);
        RefreshMini();
        return true;
    }

    /// <summary>
    /// Dosyaya yazılamayan işaretleri uyarı satırında söyler. Satırda başka bir uyarı varsa onun
    /// altına eklenir; yoksa satır uyarı kipinde açılır.
    /// </summary>
    private void ShowLostChapters(RecordResult result, bool warned)
    {
        if (result.MarksLost <= 0) return;

        var text = Say(result.ChaptersUnsupported ? "recorder.chapter.unsupported" : "recorder.chapter.failed");
        if (warned)
        {
            TxtWarning.Text = TxtWarning.Text + Environment.NewLine + text;
            return;
        }

        TxtWarning.Text = text;
        DurumuGoster(uyari: true);
    }
}
