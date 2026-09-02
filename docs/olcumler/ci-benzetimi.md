# ci-gibi-kos.sh — CI ile karşılaştırma

## Yanlış temsilin tarihi

T66'dan miras `tools/ci-gibi-kos.sh`, ffmpeg'i PATH'ten siliyordu — o tarihte CI de
ffmpeg görmüyordu, betik doğruydu. T115 CI'ya ffmpeg kurulumunu ekledikten sonra
(`.github/workflows/ci.yml`, GyanD/codexffmpeg 9.0) CI ffmpeg'i görmeye başladı,
betik hâlâ siliyordu — o gün yanlış temsile döndü. T95 tur 3 bu betikle `--no-build`
koştu, çıkış kodu 0 aldı ve sonucu "tam suit" diye rapora yazacaktı; betiğin kendisi
kapı da içermiyordu (`dotnet test` çıktısını hiç `kosum-kapisi.ps1`'e vermiyordu),
yani Failed/Skipped özetini hiç okumuyordu. Fark, T118 sözleşmesiyle bugün ölçüldü.

## İki dörtlü

Gerçek CI koşumu: id `33589639249`, headSha `0e122f2728fd429f5136ae3bc6a784736a51f85b`.

| kaynak | Failed | Passed | Skipped | Total | Süre |
|---|---|---|---|---|---|
| CI (koşum 33589639249) | 1 | 1162 | 17 | 1180 | 18 m 59 s |
| `ci-gibi-kos.sh` (aynı taban, `T118-ci-benzetimi` = `0e122f2` + yorum-only commit) | ÖLÇÜLDÜ_DOLDUR | ÖLÇÜLDÜ_DOLDUR | ÖLÇÜLDÜ_DOLDUR | ÖLÇÜLDÜ_DOLDUR | ÖLÇÜLDÜ_DOLDUR |

Betik `361b96e`'de (headSha `0e122f2`'nin bir yorum-only commit ilerisi — `.github/workflows/ci.yml`
içindeki fark yalnız açıklama satırları, `-MinimumTotal 1134 -MaximumSkipped 30` her
ikisinde de aynı) koştu; kaynak ve derlenen ikili headSha'daki ile birebir aynı.

Makine paylaşımlı, dokuz ajan aynı anda koşuyor: yukarıdaki süre CI'in tek-koşum
süresiyle doğrudan kıyaslanamaz, yalnız kayıt için tutuldu.

## Seçim: (a) — betik adını hak ediyor

ffmpeg silme kaldırıldı, kapı parametreleri `.github/workflows/ci.yml`'den okunuyor
(`-MinimumTotal`, `-MaximumSkipped`), `kosum-kapisi.ps1` CI'daki ile aynı şekilde
çağrılıyor.

Gerekçe: T115'ten sonra CI de bu makine de ffmpeg'i görüyor — "ffmpeg'siz ortam"
artık ne CI'ı ne gerçek geliştirme makinesini temsil ediyor, temsil edilecek bir CI
farkı kalmadı. Asıl risk ffmpeg değildi: eski betik `dotnet test`'i çıktısını hiç
okumadan çalıştırıyordu, `kosum-kapisi.ps1`'i hiç çağırmıyordu — Failed/Skipped
özetini denetlemiyordu (T95 tur 3'ün 0 çıkış kodu buradan geldi). Yeni betik CI'ın
attığı iki adımı da atıyor: `dotnet build -warnaserror`, sonra `kosum-kapisi.ps1`
aynı parametrelerle. Kapı parametreleri koda gömülmedi, her koşumda
`.github/workflows/ci.yml`'den okunuyor — CI parametre değiştirirse betik otomatik
izler, ikinci bir kayma başlamaz.

## Uygulama

- `tools/ci-gibi-kos.sh`: ffmpeg PATH silme satırı kaldırıldı. `-MinimumTotal` /
  `-MaximumSkipped` `.github/workflows/ci.yml`'deki `kosum-kapisi.ps1` satırından
  `grep` ile okunuyor. `dotnet build VidShrink.sln -c Release -warnaserror` sonra
  `kosum-kapisi.ps1` aynı parametrelerle çağrılıyor (`pwsh` varsa `pwsh`, yoksa
  `powershell` — bu makinede yalnız Windows PowerShell 5.1 var, `pwsh` yok; proje
  içindeki diğer kullanım da `powershell -NoProfile -ExecutionPolicy Bypass`,
  `test-kapi.ps1` ve `docs/olcumler/suit-esszamanli-kosum.md` aynı yolu izliyor).
- İlk satır her koşumda ne temsil ettiğini yazıyor: `CI TEMSILI: ffmpeg PATH'te
  birakiliyor, kapi .github/workflows/ci.yml'den okunuyor (-MinimumTotal N
  -MaximumSkipped M).`
- `--self-test`: ffmpeg/ffprobe PATH'te mi, `-MinimumTotal`/`-MaximumSkipped`
  `ci.yml`'den okunabiliyor mu, `dotnet` ve `powershell`/`pwsh` var mı — kontrol
  eder, tam süiti koşturmaz. Bu makinede geçti (ffmpeg WinGet üzerinden PATH'te,
  `-MinimumTotal=1134`, `-MaximumSkipped=30`).
- Bilinmeyen argüman (`--self-test` dışında) `DURDU` yazıp çıkış kodu 3 verir.

## Ölçülmedi

- Betiğin çıktısı ile CI'ın çıktısındaki bireysel test isimleri satır satır
  karşılaştırılmadı — yalnız Failed/Passed/Skipped/Total dörtlüsü kıyaslandı.
- Farklı makinelerdeki (ör. başka bir ajanın makinesi) `--self-test` sonucu
  ölçülmedi; yalnız bu makinede koşuldu.
- Betiğin macOS/Linux altında davranışı ölçülmedi — proje Windows'a kilitli,
  test edilmedi.

## Kanıt commit'leri: hiçbir ref'te değildi

T115 tur 1'in iki ara kanıt commit'i hiçbir dalda/etikette değildi, `git gc`
onları silebilirdi:

- `kanit/T115-f2f05f5f` → `f2f05f5f9fd0e68b29b388d502a4e3543371f259` —
  "gecici kanit: T115 ffmpeg kurulumu T110 uzerinde (silinecek)". T115'in ffmpeg
  kurulum adımının T110 üzerinde ilk denendiği koşumun kanıtı; buradan T115
  dalına taşındı.
- `kanit/T115-0a56868f` → `0a56868fa4cc1a2ccb8917bbc0f607b76961ad52` —
  "gecici kanit: sha256 duzeltmesi (ilk kosumda K7'yi zaten kanitladi)". sha256
  doğrulama adımı eklendikten sonraki koşumun kanıtı; T115'in K7'sini (indirilen
  ffmpeg'in doğrulanması) ilk kez bu koşum kanıtladı.

Her ikisi de etiketlendi ve `origin`'e itildi (bkz. Çıktı). İçerikleri
değiştirilmedi.

## Ek iş: handbrake-motoru.md satır atıfları

T111 `docs/olcumler/auto-mod.md`'ye komşu satırlar ekledi/çıkardı, `docs/inceleme/handbrake-motoru.md`
içindeki bazı `auto-mod.md:N` atıfları kaydı. Sekiz atıf tek tek açılıp hedef
cümle karşılaştırıldı: beşi hâlâ doğru satırı gösteriyordu (`:202-204`, `:283-287`,
`:289`, `:214,216`, `:214` — bir kısmı önceki bir kaymadan sonra başka bir eklemeyle
rastlantısal olarak yeniden hizalanmış), üçü kaymıştı ve düzeltildi: preset 6
satırı `:209` → `:208`, HandBrakeCLI komutu `:209` → `:210`, `-g 300` kazanç
cümlesi `:250` → `:403-404`.

**T0'ın verdiği `:223`/`:225` bu branch'te doğrulanmadı.** Kontrat metni
preset 6 için `:223`, HandBrakeCLI komutu için `:225` diyordu (T111'in
`b17c8f9` commit'indeki kendi notundan). Bu worktree'nin tabanı olan
`origin/T115-ci-ffmpeg`'teki `docs/olcumler/auto-mod.md`'de o iki satırda
farklı içerik var: `:223` "Boyut farkı: uzman-biz 15,02 MiB..." cümlesi,
`:225` "**HandBrake - auto:** ortalama +1,269..." cümlesi — ikisi de K3
bölümünün devamı, preset ya da HandBrakeCLI komutuyla ilgisi yok. Sebep:
`b17c8f9`'dan sonra T111 dalında beş commit daha `auto-mod.md`'ye satır
ekledi/çıkardı (`9bef2a0`, `29412b0`, `536fb44`, `72d4c6c`, `63cb851`,
dal ucu), bunlardan `29412b0` satır ~46 civarına 6 satır ekleyerek 202+
bölgesini kaydırdı — ama bu beş commit **T111 henüz main'e/`T115`'e
birleşmedi**, dolayısıyla bu worktree'nin `auto-mod.md`'si hâlâ daha eski
bir an. Bu belgedeki `:208`/`:210` düzeltmesi doğrudan bu worktree'de
`grep -n` ile doğrulanan içeriğe dayanıyor — T111 birleşince yeniden
kayabilir, o zaman üçüncü bir düzeltme turu gerekir.

Bayatlama şöyle oluyor: yerleşim yeniden temellendirilirken (satır ekleme/çıkarma)
üstündeki referans cümle güncellenmiyor, ve atıf başka bir ekleme ile rastlantısal
doğruya dönebildiği için "sayı doğru görünüyor" testi tek başına güvenilir değil —
her seferinde hedef cümle açılıp okunmalı. Yakalama önerisi: `docs/inceleme/`
altındaki `dosya.md:N` atıflarını çıkarıp hedef dosyada o satırın hâlâ referans
metnindeki alıntıyı içerdiğini denetleyen küçük bir betik (CI'a değil, isteğe
bağlı bir `verify` adımına) eklenebilir — uygulanmadı, yalnız öneri.
