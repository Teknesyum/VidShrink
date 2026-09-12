[[netlestirme:012]]

# Netleştirme: teknesyum-ui taramasinin kapanmayan 18 bulgusu icin ne yapalim: kurallari eklent

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

teknesyum-ui taramasinin kapanmayan 18 bulgusu icin ne yapalim: kurallari eklentide duzeltmek mi (Avalonia'nin ToolTip.Tip'i, verbatim kanit bloklari, ayni renkli duraklarin sayilmamasi), projeye muafiyet yolu eklemek mi, yoksa oldugu gibi birakip kancanin sayisini gurultu olarak kabul etmek mi? Hangi bulgu gercek arayuz borcu, hangisi kuralin kendi kusuru?

## Elde olan olgular

# Ölçülen olgular — `teknesyum-ui` taramasının kapanmayan 18 bulgusu

Depo VidShrink (.NET 8 + Avalonia 12.1.2). Tarama komutu
`node <eklenti>/scripts/scan.js .`, son satır: `642 files · 18 open · 0 fixed · 7 error(s)`.

Bu tur 28'den 18'e indi: 26 palet dosyası `Themes/Palette/<Ad>/Theme.axaml` düzenine
taşınarak `colour/raw-colour` sıfırlandı (kural belirteç dosyasını yalnız dosya adından
tanıyor), dokuz düğmeye `AutomationProperties.Name` yazıldı, bir cümle birleştirme tek
anahtara indi.

## Kalan 18'in dökümü

| Kural | Adet | Yer |
|---|---|---|
| `forms/unmeasured-label` | 9 | 7'si `docs/olcumler/kabuk-entegrasyonu.md:357-360,366,367` + `kabuk-istegi-tuketimi.md:553`, 1'i `CHANGELOG.md:605`, 1'i `tools/sahne-butcesi/DUZENEK.md:111` |
| `states/disabled-affordance` | 6 | `src/VidShrink.App/MainWindow.axaml:426,777,784,1025,1032,1129` |
| `forms/no-ui-string-literal` | 1 | `MainWindow.axaml:85` |
| `forms/no-sentence-concat` | 1 | `src/VidShrink.App/Playback/PlayerView.Window.cs` |
| `colour/background-gradient` | 1 | `src/VidShrink.App/Themes/Playback.axaml:174` |

## 1. `states/disabled-affordance` (6) — kural WPF varsayımıyla yazılmış

Kuralın kodu (`scripts/rules/states.js:599-604`):

```js
if (!/IsEnabled\s*=\s*"False"/.test(m[2])) continue;
if (/ToolTip\s*=/.test(m[2])) continue;
out.push({
  line: lineAt(text, m.index),
  message: '<' + m[1] + '> IsEnabled="False" without a ToolTip explaining why',
});
```

Avalonia'da düz `ToolTip` özelliği **yok**; ipucu `ToolTip.Tip` ekli özelliğiyle verilir.
`/ToolTip\s*=/` deseni `ToolTip.Tip=` içinde eşleşmiyor (ToolTip'ten sonra `.` geliyor).

Depodaki altı denetimin ipucu **zaten var** ve çalışıyor; üçü şu biçimde:

```xml
<Button x:Name="BtnCancel" ... IsEnabled="False" Click="OnCancel">
  <Button.Styles>
    <Style Selector="Button:disabled">
      <Setter Property="ToolTip.Tip" Value="{loc:Text main.action.cancel.disabled-tip}"/>
    </Style>
  </Button.Styles>
</Button>
```

biri de gövdeli:

```xml
<CheckBox x:Name="ChkFastGpu" ... IsEnabled="False" Content="{loc:Text main.fast-gpu}">
  <ToolTip.Tip>
    <TextBlock x:Name="TipFastGpu" Theme="{StaticResource TipText}" loc:Bullets.Text="{loc:Text main.fast-gpu.tip}"/>
  </ToolTip.Tip>
</CheckBox>
```

Bir alt ajan bu altısını `ToolTip.Tip="{Binding $self.IsEnabled, Converter=...}"` biçimine
çevirmeyi denedi: derleme temiz geçti, ama (a) tarama yine altısını işaretledi, (b)
`ChkFastGpu`'nun madde boyayıcısı (`loc:Bullets.Text`) düştü. Değişiklik geri alındı.

## 2. `forms/unmeasured-label` (9)

Kuralın kodu (`scripts/rules/forms.js:530-549`): `.md` dosyasında `defaults?` kelimesi ve
bir rakam aynı satırda geçiyorsa, satırda birebir `(default, unmeasured)` ya da `measured`
kelimesi yoksa bulgu üretir.

Yedisi ölçüm günlüklerindeki `reg query` **ham çıktısı**; oradaki "default" Windows'un
kendi ayrılmış değer adı, bir yazılım varsayılanı değil:

```
   HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\DefaultIcon  ->  (Default) = C:\...\VidShrink.exe,0
```

Depo kuralı "kanıt verbatim yazılır ve linklenir" diyor; bu satırlara etiket basmak
kanıtı değiştirmek olur, ayrıca "ölçülmedi" demek yalan olur — tam olarak ölçülmüş.

`CHANGELOG.md:605` "WhatsApp defaults: 16 MB" diyor; 16 gerçek WhatsApp yayın sınırı,
kodda `MainWindow.axaml.cs:42` `WhatsAppTargetMb = 16` sabiti.

`DUZENEK.md:111` satırındaki kelime ffprobe'un çıktı biçimi adı: `-of default=nw=1`.
Yani "default" burada bir varsayılan değer değil, bir bayrağın değeri.

## 3. `forms/no-ui-string-literal` (1)

`MainWindow.axaml:85` içinde `"Buy Me a Coffee"` dizesi kaynakta duruyor. Depoda bunu
**bilerek** pimleyen bir ölçü var: `BrandSpellingTests` (`SettingsTabTests.cs`) hem XAML
kaynağında bu birebir dizeyi, hem de İngilizce yerel dosyasında **yokluğunu** ölçüyor.
Daha önce `main.sponsor.label` anahtarıyla yerelleştirilmesi denenmiş ve geri alınmış;
sebep `BiciminTests.cs` içinde not düşülmüş (marka adı her dilde aynı yazılır).

## 4. `colour/background-gradient` (1)

`Themes/Playback.axaml:174`'teki `PlaybackScrimVeil` üç duraklı; kural kabuğun geçişinin
on bir duraklı olmasını istiyor. Alt ajan sekiz durak ekledi ama hepsini aynı
`PlaybackScrimColor` yaptı: sayaç 11'e çıktı, görüntü bir piksel değişmedi. Geri alındı.

## Bağlam

- Kural dosyaları depoda değil, makinedeki eklenti önbelleğinde:
  `~/.claude/plugins/cache/teknesyum/teknesyum-ui/0.2.0/scripts/rules/`.
- `scan.js` iki ayar dosyası okuyor (`<kök>/.claude/teknesyum-ui.json`, sonra
  `~/.claude/teknesyum-ui.json`) ama ikisi de bu kuralların kümelerine ad ekleyemiyor;
  bugün bir muafiyet yolu yok. Projede `.claude/teknesyum-ui.json` hiç yok.
- Eklenti Teknesyum'un kendi ürünü, yani yamalanabilir; sürümü `0.2.0`.
- Kanca her turun sonunda sayıyı basıyor: `18 UI violations, first kabuk-entegrasyonu.md:357`.
