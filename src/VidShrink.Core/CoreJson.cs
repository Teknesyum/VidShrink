using System.Text.Json;
using System.Text.Json.Serialization;
using VidShrink.Core.Share;

namespace VidShrink.Core;

/// <summary>
/// Kaynaktan üretilmiş serileştirme bağlamları. <c>JsonSerializer.Deserialize&lt;T&gt;</c>
/// çağrısı çalışma anında tipi yansımayla gezer; AOT çözümlemesi bunu IL2026/IL3050 ile
/// işaretliyor ve kırpma tipin alanlarını atabiliyor
/// (<c>docs/olcumler/hipersurus-h.md</c> H4). Üretilen <c>JsonTypeInfo</c> alındığında
/// yansıma hiç kurulmaz.
///
/// <para>Bağlam başına bir seçenek kümesi var, çünkü seçenekler bağlama gömülüdür.
/// Her bağlamın nitelikleri, yerini aldığı <c>JsonSerializerOptions</c> ile birebir
/// aynı olacak; ayrılırsa ayrıştırma davranışı sessizce değişir.</para>
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(EncodePlan))]
[JsonSerializable(typeof(ShareTargetTable))]
internal sealed partial class GevsekJson : JsonSerializerContext;

/// <summary><see cref="ShareResult"/> defterinin seçenekleri: girintili, boş alan yazılmaz.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<ShareLink>))]
[JsonSerializable(typeof(IReadOnlyList<ShareLink>))]
internal sealed partial class PaylasimJson : JsonSerializerContext;

/// <summary><see cref="WatchState"/>: girintili, küçük deve adlandırması.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WatchState))]
internal sealed partial class IzlemeJson : JsonSerializerContext;

/// <summary>
/// Seçeneksiz çağrılar. Tek örnek kanalı açılış yolunda: kabuktan gelen yollar her
/// açılışta bir kez kodlanıp çözülüyor.
/// </summary>
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
internal sealed partial class YolJson : JsonSerializerContext;
