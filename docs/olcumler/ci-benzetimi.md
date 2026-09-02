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
| `ci-gibi-kos.sh` (aynı taban, `T118-ci-benzetimi` = `0e122f2` + yorum-only commit) | 0 | 1163 | 17 | 1180 | 22 m 19 s |

Betik `361b96e`'de (headSha `0e122f2`'nin bir yorum-only commit ilerisi — `.github/workflows/ci.yml`
içindeki fark yalnız açıklama satırları, `-MinimumTotal 1134 -MaximumSkipped 30` her
ikisinde de aynı) koştu; kaynak ve derlenen ikili headSha'daki ile birebir aynı. Ağaç:
`origin/T115-ci-ffmpeg`, worktree HEAD `361b96e`.

Makine paylaşımlı, dokuz ajan aynı anda koşuyor: yukarıdaki süre CI'in tek-koşum
süresiyle doğrudan kıyaslanamaz, yalnız kayıt için tutuldu (CI'in 18 m 59 s'i tek
başına bir runner'da; bu 22 m 19 s dokuz ajanla paylaşılan bir makinede — süre farkı
bu yüzden **karşılaştırılabilir değil**, ayrı bir bulgu değil).

**Farklı hücrelerin sebebi:**

- **Skipped (17=17) ve Total (1180=1180) birebir eşleşiyor** — betik CI ile aynı test
  yüzeyini koşturuyor, atlanan testler de aynı. Bu, K2(a)'nın (ffmpeg silme kaldırma +
  parametre hizalama) doğru test setini çalıştırdığının doğrudan kanıtı.
- **Failed (CI: 1, betik: 0) ve buna bağlı Passed (CI: 1162, betik: 1163) farklı** —
  hangi testin CI'da başarısız olduğu bu ölçümde görülmedi (CI logunun test-adı
  detayına bu sözleşmede inilmedi, **ölçülmedi**). Muhtemel açıklama tek-seferlik
  bir kararsız (flaky) test ya da makineye özgü bir zamanlama farkı; iki koşum da
  aynı commit'te ama farklı makinede (CI runner'ı vs bu paylaşımlı Windows makinesi)
  — kesin sebep bu sözleşmenin kapsamı dışında, iddia edilmiyor.
- **Kapı sonucu ayrıca farklı ve önemli**: CI'da kapı 1 gerçek başarısızlık yüzünden
  düştü. Bu makinede kapı da düştü ama **hiç ilgisiz bir sebeple**: `kosum-kapisi.ps1`
  (`kod=66 sart=Basarisiz/Failed ozeti yok`) — yani Failed/Passed özet satırını hiç
  bulamadı, `Başarısız: 0` cümlesi gerçekte üretilmiş olsa bile. Bu betiğin
  (`ci-gibi-kos.sh`) hatası değil: `kosum-kapisi.ps1` `-InputFile` verilmediği için
  `dotnet test`'i kendi başlatıp `2>&1` ile canlı yakalıyor (T115-owned, satır ~20-24);
  bu makinede dotnet'in Türkçe konsol çıktısı ("Başarısız", "ş"/"ı" harfleri) bu canlı
  yakalama sırasında PowerShell 5.1'in öntanımlı konsol kod sayfasıyla bozuluyor
  olabilir — kaydettiğim log dosyasında baytlar doğru UTF-8 (`\xc5\x9f`=ş, `\xc4\xb1`=ı)
  ama kapının kendi regex'i (`ş`, `ı` tam eşleşme bekliyor) sıfır eşleşme
  buldu. Bu bir hipotez, **kesin köken bu sözleşmede doğrulanmadı** (ölçülmedi) —
  ve `kosum-kapisi.ps1` T115'in `owns`'unda, burada düzeltilmedi/düzeltilemezdi.
  T0'a aktarılması gereken ayrı bir bulgu: **bu makinede `ci-gibi-kos.sh` betiği CI'ı
  doğru şekilde çağırsa bile, kapının kendisi bu ortamda güvenilir bir PASS/FAIL
  vermiyor** — K2(a)'nın "betik adını hak ediyor" kararı bu bulguyu geçersiz kılmıyor
  (betik CI'ı doğru temsil ediyor), ama temsil ettiği kapı bu makinede kırık.

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
içindeki bazı `auto-mod.md:N` atıfları kaydı. Dokuz atıf bulundu (sekizi
`grep "auto-mod.md"` ile, dokuzuncusu — "aynı belge (`:316`)" — yakın satırdaki
örtük öz-referans olarak yakından okumada). Hepsi tek tek açılıp hedef cümle
`auto-mod.md`'de gerçekten var mı diye doğrulandı: beşi doğru satırı gösteriyordu
(`:202-204`, `:283-287`, `:289`, `:214,216`, `:214`), dördü kaymıştı — preset 6
satırı `:209`→`:208`, HandBrakeCLI komutu `:209`→`:210`, `-g 300` kazanç cümlesi
`:250`→`:403-404`, öz-referans `:316`→`:311`. Bu doğrulama aşağıdaki çapa
biçiminin malzemesi oldu; tek başına satır numarası düzeltmesi olarak
**bırakılmadı** (aşağıya bakın).

**Hangi ağaçta ölçüldü, ve neden T0'ın verdiği `:223`/`:225` burada
doğrulanmadı.** Bu sözleşmenin tamamı `origin/T115-ci-ffmpeg` dalı üzerinde
(worktree taban commit `361b96e`, headSha karşılaştırması `0e122f2`) yapıldı.
T0'ın verdiği `:223`/`:225` `origin/T111-auto-mod` ucunda (o gün orada) doğruydu;
bu dalda değil — bu dalın `auto-mod.md`'sinde o iki satırda K3'ün devamı başka
cümleler var. **İki taraf da haklıydı, çünkü iki farklı ağaç ölçülüyordu:**
`T115` tabanlı bu worktree'de `:208`/`:210`, `T111-auto-mod` ucunda `:229`/`:231`.
Kök sebep aynı kaldı: T111 dalı `auto-mod.md` üzerinde hâlâ commit atıyor —
sözleşme sırasında T111'e tur 2 açıldığı öğrenildi, yani bu dosya **birleşme
anına kadar kararlı değil**. Bu bir borç değil, ölçüm sırasında bilinmesi
gereken bir gerçek: `docs/inceleme/handbrake-motoru.md` içindeki dokuz
`auto-mod.md` atfı satır numarasına değil **çapaya** çevrildi (bölüm başlığı
`§` + hedef cümlenin ilk birkaç kelimesi, satır numarası yalnız `şu an :N`
diye ikincil not olarak duruyor) — T111 tur 2 birleştiğinde satırlar yine
kayarsa çapa metni `grep`'le hâlâ bulunur, üçüncü bir "satır numarası düzeltme"
turu gerekmez. `handbrake-motoru.md`'deki dokuz atfın hepsi bu biçime çevrildi.

Bayatlama şöyle oluyor: yerleşim yeniden temellendirilirken (satır ekleme/çıkarma)
üstündeki referans cümle güncellenmiyor, ve atıf başka bir ekleme ile rastlantısal
doğruya dönebildiği için "sayı doğru görünüyor" testi tek başına güvenilir değil —
her seferinde hedef cümle açılıp okunmalı. Yakalama önerisi: `docs/inceleme/`
altındaki `dosya.md:N` atıflarını çıkarıp hedef dosyada o satırın hâlâ referans
metnindeki alıntıyı içerdiğini denetleyen küçük bir betik (CI'a değil, isteğe
bağlı bir `verify` adımına) eklenebilir — uygulanmadı, yalnız öneri.
