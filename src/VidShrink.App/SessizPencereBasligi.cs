using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Templates;
using Avalonia.Styling;

namespace VidShrink.App;

/// <summary>
/// Avalonia 12, istemci alanı başlığa uzatılmış pencerede tam ekranda fare en üst piksele
/// değince pencere adını taşıyan 30 px'lik kendi başlığını açar. Kabuğun başlığı zaten
/// kendi sekme şeridi; bu şablon o ikinci başlığı boş bırakır.
/// </summary>
internal sealed class SessizPencereBasligi : IWindowDrawnDecorationsTemplate
{
    internal static ControlTheme Tema { get; } = new(typeof(WindowDrawnDecorations))
    {
        Setters = { new Setter(WindowDrawnDecorations.TemplateProperty, new SessizPencereBasligi()) }
    };

    public TemplateResult<WindowDrawnDecorationsContent> Build() => new(new WindowDrawnDecorationsContent(), new NameScope());

    object? ITemplate.Build() => Build();
}
