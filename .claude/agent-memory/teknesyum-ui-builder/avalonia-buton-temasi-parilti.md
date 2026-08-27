---
name: avalonia-buton-temasi-parilti
description: Button ControlTheme'inde BoxShadow setter'ı AVLN2000 ile düşer; parıltı şablonun Border#Root gövdesine verilir, gövde BasedOn ile GhostButton'dan alınır
metadata:
  type: project
---

Yeni bir düğme teması yazarken iki tuzak:

- **`Button`'da `BoxShadow` yok.** `<Setter Property="BoxShadow" .../>` derlemede
  `AVLN2000: Unable to resolve suitable regular or attached property BoxShadow on type
  Button` ile düşer. Parıltı şablonun gövdesine verilir:

      <Style Selector="^ /template/ Border#Root">
        <Setter Property="BoxShadow" Value="{StaticResource GlowBlue}"/>
      </Style>
      <Style Selector="^:disabled /template/ Border#Root">
        <Setter Property="BoxShadow" Value="{StaticResource GlowNone}"/>
      </Style>

  `Border#Root` adı `Themes/Controls.axaml` içindeki düğme şablonundan geliyor.

- **Şablon yazma, `BasedOn` kullan.** Şablonsuz bir `ControlTheme` hiçbir şey çizmez.
  `BasedOn="{StaticResource GhostButton}"` gövdeyi getirir; üstüne yalnız renk, ölçü ve
  yarıçap ezmesi yazılır. `Controls.axaml` genellikle `owns` dışında olduğu için tek
  yol budur ([[vidshrink-owns-listesi-daraltiyor]]).

`Playback.axaml`'dan `Controls.axaml` anahtarlarına `StaticResource` ile erişilebiliyor:
`Theme.axaml` `Controls.axaml`'ı içeriyor ve `App.axaml` `Theme.axaml`'ı `Playback.axaml`'dan
önce birleştiriyor.
