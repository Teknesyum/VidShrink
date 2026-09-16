using Avalonia;

namespace VidShrink.App;

/// <summary>
/// XAML ağacının içine konan iz noktası. <c>InitializeComponent</c> tek bir işaret
/// bırakıyor ve içeride nereye harcandığı görünmüyordu; bu iliştirilmiş özellik
/// derlenmiş XAML'de öğe kurulurken yazıldığı için iki işaret arasındaki fark
/// aradaki ağacın gerçek bedelini verir.
///
/// <para>Görüntüye dokunmaz: yalnız bir özellik yazımı, görsel ağaçta karşılığı yok.
/// İz kapalıyken <see cref="AcilisIzi.Yaz"/> ilk satırda dönüyor.</para>
/// </summary>
internal static class AcilisIsareti
{
    public static readonly AttachedProperty<string?> AdProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Ad", typeof(AcilisIsareti));

    static AcilisIsareti()
    {
        AdProperty.Changed.AddClassHandler<AvaloniaObject>((_, e) =>
        {
            if (e.NewValue is string { Length: > 0 } ad) AcilisIzi.Yaz(ad);
        });
    }

    public static void SetAd(AvaloniaObject hedef, string? deger) => hedef.SetValue(AdProperty, deger);

    public static string? GetAd(AvaloniaObject hedef) => hedef.GetValue(AdProperty);
}
