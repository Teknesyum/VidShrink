using System.Text.Json.Serialization;

namespace VidShrink.Cli;

/// <summary>CLI metin kataloğunun serileştirme bağlamı; gerekçesi App tarafıyla aynı.</summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CliJson : JsonSerializerContext;
