# Kapanan Pencere Ayar Dosyasını Yeniden Yazıyordu

Belirti test artığıydı: `MainWindow` kuran sınıflar kendi ayar dosyalarını sildikten sonra
klasörde `settings-*.json` kalıyordu (`.calisma/test-ciktilari/t173/` ve `meta-sil-arayuz/`).
`MetaSilArayuzTests` bunu her kolun başında klasörü süpürerek örtüyordu.

## Kusur

Yazan, kapatılmış pencerenin kendisi. İkinci pencere kurulurken alınan yığın izi:

```
MainWindow..ctor
  InitializeComponent → !XamlIlPopulate   MainWindow.axaml:line 774   (RbCodecAuto GroupName="ShrinkCodec" IsChecked="True")
    ToggleButton.set_IsChecked → RadioButton.IsCheckedChanged
      RadioButtonGroupManager.OnCheckedChanged
        [kapalı pencerenin düğmesi] IRadioButton.set_IsChecked(false)
          Watch lambda   MainWindow.axaml.cs:642
            SaveSettings
```

Avalonia aynı `GroupName`'i taşıyan seçenek düğmelerini tek grup sayıyor; yeni pencerenin
XAML'i varsayılan düğmeyi işaretleyince eski pencerenin işaretli düğmesi düşüyor. Eski
pencere kapanmış ama yaşıyor, `Watch` aboneliği duruyor ve değişimi ayar dosyasına yazıyor.
Tek pencere kuruluşunda kapalı pencerenin `SaveSettings`'i 6 kez çağrıldı.

Yazılan değer de yanlış: kullanıcının seçimi değil, düşürülmüş düğme.

## Kullanıcıya Dokunuyor Mu

Uygulama süreç başına tek `MainWindow` kuruyor (`App.axaml.cs`, `ShrinkJobWindow` kendi
penceresi). İkinci kuruluş yalnız testte oluyor; bu yüzden `CHANGELOG.md`'ye girmedi.
Koruma yine de kaynağa kondu: yazan kaynak, ve düzenekte süpürmek kusuru gizliyordu.

## Düzeltme

`MainWindow.OnClosed` `_kapandi` bayrağını kaldırıyor; `SaveSettings` ve `SaveAppSettings`
bayrak kalkıkken dönüyor. Ayar her değişimde yazıldığı için kapanışta ayrı bir kayıt yok;
bayrağın kestiği bir yazma kalmıyor.

`MetaSilArayuzTests`'in klasör süpürmesi kaldırıldı.

## Ölçü

| | `t173` artığı | `meta-sil-arayuz` artığı |
|---|---|---|
| Düzeltmeden önce (iki sınıf, tek koşum) | 4 | 2 |
| Düzeltmeden sonra | 0 | 0 |
| Mutasyon (`_kapandi` hiç kalkmıyor) | 6 | 14 |

`AyarKaliciligiTests.KapananPencereAyarDosyasiniYenidenYazmaz`: açık pencere dosyayı yazar
(olumlu kontrol), kapatılıp dosya silinir, ikinci pencere kurulur ve kapalı pencerenin
düğmesi ayrıca çevrilir; dosya geri gelmez. Mutasyonda 1/35 kırmızı (`Assert.False`),
düzeltmeyle 35/35.

## Ölçülmeyen

- Kapatılmamış iki pencere arasındaki aynı çapraz etki (üründe bu durum yok).
- `RecorderView.axaml` da `GroupName` kullanıyor (4 yer); kapalı pencerenin kaydedici
  görünümünün aynı yolla `recorder-settings.json` yazıp yazmadığına bakılmadı.
- Avalonia'nın grup yöneticisinin iç mekanizması; yalnız yığın izi okundu.
