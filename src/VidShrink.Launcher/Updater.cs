using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Sessiz fark güncellemesi. Manifest tek başına çekilir, yalnız özeti tutmayan dosyalar
/// arşivden aralık isteğiyle indirilir, hepsi doğrulandıktan sonra uygulama klasörüne
/// geçer. Hiçbir hata açılışı engellemez.
///
/// Manifestin <c>launcher</c> alanı kurulum kökündeki başlatıcıyı da sayar; o satırlar
/// kendi arşivinden inip <see cref="LauncherUpdate"/> üzerinden yerine geçer. Uygulama
/// dosyaları önce yerleşir, başlatıcı en son kurulur: sıra tersine dönerse yeni başlatıcı
/// eski uygulamayı açar. Sıranın kendisi <see cref="UpdateRollout"/> içinde.
///
/// Manifestin <c>shell</c> alanı kurulum kökündeki kabuk klasörünü sayar. O satırlar da
/// başlatıcının arşivinden iner ama geçiş dansına girmez: çalışan süreç onları tutmadığı
/// için <see cref="ShellUpdate"/> doğrudan üstlerine yazar.
///
/// Başlatıcı geçişi burada yapılmaz, yalnız kurulur. Geçişi çıkışta yerine geçecek ikili
/// yapar; döndürülen değer o çağrının gerekip gerekmediğidir.
///
/// Bu çağrı açılış yolunda değil: uygulama ekrana geldikten sonra koşar
/// (<c>Program.cs</c>). İndirme hemen, kurulum klasörden koşan uygulama kapanınca. Eskiden açılış kapısının içindeydi ve bu yüzden bütçesi 90
/// saniyeydi; ölçülen 0.3.0 → 0.4.1 farkı 375 dosya ve 134,8 MB, yani o bütçede
/// bitmesi mümkün değildi. Yarıda kalan sahne de silindiği için her açılış sıfırdan
/// başlıyor, kurulum hiç yakınsamıyordu.
///
/// Bekleme ve kurulum <see cref="KurulumBekleyeni"/> içinde: klasör başına tek arka plan
/// bekleyeni olur, ikinci açılış indirmez de beklemez. Sürüm zaten kuruluysa kurulum
/// sessizce atlanır, hata işareti yazılmaz: kurulu sürüm sahnenin sürümüne eşit ya da
/// ondan yeniyse tur eskiye düşürmez. Elle "Yükle" yolu da aynı yuvayı yokluyor ve her
/// beklemesi kısa: yuva 3 sn, kilitler 20 sn, klasörün boşalması 10 sn, indirme
/// <see cref="ElleButcesi"/>. Üç bekleme bütçesinin her biri ayrı ölçülü ve ölçü
/// vazgeçme süresini sayıyor: yuva başka kopyada tutuluyorken elle yol 3030 ms'de
/// vazgeçiyor ve indirmeye hiç girmiyor (<c>YuvaButcesiVazgecmeSuresiniBelirler</c>),
/// indirme kilidi tutuluyorken 20000 ms'de vazgeçiyor
/// (<c>IndirmeKilidiButcesiVazgecmeSuresiniBelirler</c>), kurulum kilidi tutuluyorken
/// 20011 ms'de vazgeçip klasörü eski sürümde bırakıyor
/// (<c>ElleKurulumKilidiButcesiVazgecmeSuresiniBelirler</c>). Arka plan turunun yuva ve
/// indirme bütçesi sıfır: aynı düzenekte 0 ms'de vazgeçiyor. Kurulum kilidini ise
/// kısa tutmada beklemeyi sürdürüyor, kilit bırakılınca kuruyor
/// (<c>ArkaPlanKurulumKilidiKisaTutmadaVazgecmez</c>). Hangi kolun koştuğunu ölçünün
/// kendisi kanıtlıyor: indirme sayacı ve klasörün içeriği bekleme boyunca yerinde
/// duruyor, kilit boşken aynı çağrı yeni sürümü kuruyor. Elle Yükle'nin uygulamayı
/// doğurma süresinin ortancası 3138 ms
/// (<c>ElleYukleArkaPlanIndirirkenAcilisiGeciktirmez</c>, n=7, 3110-3178 ms).
/// Sayılar ve mutasyon tablosu <c>docs/olcumler/bekleme-butceleri.md</c>.
/// </summary>
internal static class Updater
{
    /// <summary>
    /// İndirme dahil tüm güncellemenin üst sınırı. Açılış yolunda olmadığı için geniş:
    /// yavaş hatta yarım kalan iş sahnede kalır, sonraki tur kaldığı yerden sürer.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Aynı anda inen dosya sayısı. Dosyalar tek tek inerken hattın kendisi değil her
    /// isteğin gidiş dönüşü sınırdı: ölçülen 0.3.0 → 0.4.1 farkı 375 dosya, yani 375 ayrı
    /// tur. Şerit sayısı bunu böler; sayı bant genişliğini doyurmaya değil gecikmeyi
    /// örtmeye yetecek kadar, sunucuya yüklenmeyecek kadar küçük.
    /// </summary>
    private const int Lanes = UpdateStaging.LauncherLanes;

    /// <summary>
    /// Elle "Yükle" yolunun indirme bütçesi. Bu çağrı uygulamayı doğurmadan önce koşuyor;
    /// uygulama ekrana gelmeden geçen süre buradan gelir. Sahne uygulamanın indirme
    /// adımından zaten kalmış olduğu için bu tur çoğunlukla yalnız doğrulama yapar, yarım
    /// kalırsa uygulama açıldıktan sonraki arka plan turu geniş bütçesiyle sürdürür.
    /// </summary>
    internal static readonly TimeSpan ElleButcesi = TimeSpan.FromSeconds(60);

    private const string MutexName = UpdateStaging.MutexName;

    public static bool Run(string baseDirectory, string appDirectory, bool force = false)
    {
        if (!force && !UpdateCheck.AutoUpdateEnabled()) return false;
        if (Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_DISABLED") == "1") return false;

        return KurulumBekleyeni.Calistir(baseDirectory, appDirectory, force, MutexName, () =>
        {
            using var cancellation = new CancellationTokenSource(force ? ElleButcesi : Budget);
            return UpdateStaging.StageAsync(
                baseDirectory, appDirectory, Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE"),
                Lanes, null, "VidShrink-Launcher", null, cancellation.Token).GetAwaiter().GetResult();
        }, Rehearsing);
    }

    internal const string RehearsalVariable = "VIDSHRINK_UPDATE_PROVA";

    internal static bool Rehearsing =>
        Environment.GetEnvironmentVariable(RehearsalVariable) == "1";
}

