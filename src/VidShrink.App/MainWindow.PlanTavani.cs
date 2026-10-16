using System;
using Avalonia.Controls;

namespace VidShrink.App;

public partial class MainWindow
{
    /// <summary>
    /// Plan panelinin tavanı (<c>PlanPanelMaxHeight</c>) gerekçe listesi için var: açılan liste
    /// önizlemeyi ezmesin diye panel orada durur, taşma <c>PlanScroll</c>'a düşer. Katlı hâldeki
    /// özet (olgular, çizgi, gerekçe başlığı) o tavanın altında kalmak zorunda değil; K6'nın sözü
    /// özetin kaymamasıdır. Belirteç Latin satırına göre seçilmişti ve bir dil özeti tavanın üstüne
    /// itince özet de kayıyordu: Linux'ta Tay yazısının yedek yazı tipi satırı 16 px'te 24,2 px
    /// (Noto Sans Thai; fontconfig'in Loma'sı 25,4) istiyor, Atkinson'ın Latin satırı 20,8. 1136x720'de
    /// Tay özeti 486 px istiyor, görüş alanı 483 px kalıyordu (Loma'da 497/482).
    /// <para>
    /// Etkin tavan belirteç ile özetin istediği yerin büyüğü; panel ve satır (ayırıcının tavanı)
    /// birlikte. Özet tavana sığan her dilde tavan belirtecin kendisi kalır, yerleşim değişmez.
    /// Özetin boyu ölçüden okunur: gövdenin istediği boy eksi açık gerekçe listesinin payı, artı
    /// panelin kaydırıcı dışındaki payı (dolgu, kenar, başlık). Hepsi <c>DesiredSize</c>'tan:
    /// olaylar yerleştirme sırasında geliyor ve o anda ölçü geçişi bitmiş, ama ebeveynin
    /// <c>Bounds</c>'u çocuğunkinden sonra yazılıyor. İlk sürüm panelin <c>Bounds</c>'unu okuyordu;
    /// kaydırıcının olayında panelin eski boyunu görüp tavanı geri indiriyordu
    /// (<c>PlanTavaniKatliOzetiKesmez</c>, panel 626 iken tavan 576 okundu).
    /// </para>
    /// </summary>
    private void WatchPlanCeiling()
    {
        PlanBody.SizeChanged += (_, _) => FitPlanCeilingToSummary();
        PlanScroll.SizeChanged += (_, _) => FitPlanCeilingToSummary();
    }

    private void FitPlanCeilingToSummary()
    {
        var token = Scalar("PlanPanelMaxHeight", double.PositiveInfinity);
        var reasons = PlanReasons.IsVisible ? PlanReasons.DesiredSize.Height + PlanBody.Spacing : 0;
        var summary = PlanBody.DesiredSize.Height - reasons;
        var chrome = PlanPanel.DesiredSize.Height - PlanScroll.DesiredSize.Height;
        var ceiling = Math.Max(token, Math.Ceiling(chrome + summary));
        if (ceiling.Equals(PlanPanel.MaxHeight)) return;
        PlanPanel.MaxHeight = ceiling;
        PlanPanelRow.MaxHeight = ceiling;
    }
}
