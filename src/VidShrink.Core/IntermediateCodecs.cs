namespace VidShrink.Core;

/// <summary>
/// Bir ara kodek profili: kurgu masasinin bekledigi sabit kaliteli basamak.
/// </summary>
/// <param name="Encoder">ffmpeg kodlayici adi.</param>
/// <param name="Profile"><c>-profile:v</c> degeri.</param>
/// <param name="PixelFormat">Profilin yazildigi piksel bicimi.</param>
/// <param name="Label">Arayuzde gorunen ad; urun adidir, cevrilmez.</param>
public sealed record IntermediateProfile(string Encoder, string Profile, string PixelFormat, string Label)
{
    /// <summary>Arayuz ogesinin ve ayarin tasidigi kimlik: <c>kodlayici:profil</c>.</summary>
    public string Id => Encoder + ":" + Profile;

    /// <summary>
    /// Doluysa <c>-profile:v</c> yerine yazilan kodlayici secenekleri; arsiv kodekleri profil degil
    /// kendi anahtarlariyla ayarlanir.
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
}

/// <summary>
/// ProRes, DNxHR ve arsiv kodekleri (FFV1, CineForm): kurgu ve saklama icin ara kodekler. Bit hizini profil ve kare boyutu belirler, hedef
/// boyuta surulemezler; bu yuzden yalniz Donustur yolunda, profil secimiyle yasarlar. Hedef
/// boyut yolu (<see cref="PlanParser"/>, kodek kilidi) onlari acik bir hatayla reddeder.
/// <para>
/// Profil adlari ve piksel bicimleri <c>ffmpeg -h encoder=prores_ks</c> ile
/// <c>ffmpeg -h encoder=dnxhd</c> ciktisindan; her biri bir kez gercekten kodlandi ve uydurma
/// bir profil adiyla olumsuz kontrolden gecti (<c>docs/olcumler/k12-ara-kodekler.md</c>).
/// </para>
/// </summary>
public static class IntermediateCodecs
{
    public const string ProRes = "prores_ks";
    public const string DnxHr = "dnxhd";
    public const string Ffv1 = "ffv1";
    public const string CineForm = "cfhd";

    public static readonly IReadOnlyList<IntermediateProfile> Profiles = new[]
    {
        new IntermediateProfile(ProRes, "proxy", "yuv422p10le", "ProRes 422 Proxy"),
        new IntermediateProfile(ProRes, "lt", "yuv422p10le", "ProRes 422 LT"),
        new IntermediateProfile(ProRes, "standard", "yuv422p10le", "ProRes 422"),
        new IntermediateProfile(ProRes, "hq", "yuv422p10le", "ProRes 422 HQ"),
        new IntermediateProfile(ProRes, "4444", "yuv444p10le", "ProRes 4444"),
        new IntermediateProfile(DnxHr, "dnxhr_lb", "yuv422p", "DNxHR LB"),
        new IntermediateProfile(DnxHr, "dnxhr_sq", "yuv422p", "DNxHR SQ"),
        new IntermediateProfile(DnxHr, "dnxhr_hq", "yuv422p", "DNxHR HQ"),
        new IntermediateProfile(DnxHr, "dnxhr_hqx", "yuv422p10le", "DNxHR HQX"),
        new IntermediateProfile(DnxHr, "dnxhr_444", "yuv444p10le", "DNxHR 444")
    };

    /// <summary>
    /// Arsiv kodekleri: FFV1 kayipsiz (seviye 3, her kare anahtar, dilim CRC'si) ve CineForm 10 bit
    /// 4:2:2 <c>film3+</c>. Yerel ffmpeg 9.0'da mkv, mov ve avi'ye yazildi; cfhd mp4'e yazilmiyor
    /// ("Could not find tag for codec cfhd"), ffv1 mp4'te de kapali.
    /// </summary>
    public static readonly IReadOnlyList<IntermediateProfile> ArchiveProfiles = new[]
    {
        new IntermediateProfile(Ffv1, "archive", "", "FFV1") { Options = new[] { "-level", "3", "-g", "1", "-slicecrc", "1" } },
        new IntermediateProfile(CineForm, "film3+", "yuv422p10le", "CineForm") { Options = new[] { "-quality", "film3+" } }
    };

    /// <summary>Ara ve arsiv profillerinin tamami.</summary>
    public static readonly IReadOnlyList<IntermediateProfile> All = Profiles.Concat(ArchiveProfiles).ToArray();

    public static bool IsIntermediate(string? codec)
        => codec is not null && All.Any(p => p.Encoder.Equals(codec, StringComparison.OrdinalIgnoreCase));

    /// <summary>Kodlayicinin o profili; kodlayici ara kodek degilse ya da profil tabloda yoksa <c>null</c>.</summary>
    public static IntermediateProfile? Find(string? codec, string? profile)
        => codec is null || profile is null
            ? null
            : All.FirstOrDefault(p => p.Encoder.Equals(codec, StringComparison.OrdinalIgnoreCase)
                                      && p.Profile.Equals(profile, StringComparison.OrdinalIgnoreCase));

    /// <summary><see cref="IntermediateProfile.Id"/> ile arama; tanimayan <c>null</c> doner.</summary>
    public static IntermediateProfile? FromId(string? id)
        => id is null ? null : All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
