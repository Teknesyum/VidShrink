using System.Text.Json.Serialization;

namespace VidShrink.App.Localization;

/// <summary>
/// Dil kataloğunun serileştirme bağlamı. Katalog her açılışta okunuyor ve
/// <c>Deserialize&lt;Dictionary&lt;string, string&gt;&gt;</c> çağrısı çalışma anında
/// sözlük dönüştürücüsünü yansımayla kuruyordu; AOT çözümlemesi bunu IL2026/IL3050 ile
/// işaretliyor (<c>docs/olcumler/hipersurus-h.md</c> H4, <c>Strings.cs:347</c>).
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CatalogJson : JsonSerializerContext;
