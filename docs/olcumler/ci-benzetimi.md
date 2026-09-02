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
- **Failed (CI: 1, betik: 0) ve buna bağlı Passed (CI: 1162, betik: 1163) farklı —
  kimlik kimlik doğrulandı, tahmin edilmedi (tur 2).** CI logu (`gh run view
  33589639249 --log`) tek başarısız testi adıyla veriyor:
  `VidShrink.Tests.PerformanceCheckTests.IslemciZamaniSayaciDogruOkuyorMu`, sebep
  `[h264_nvenc @ ...] Cannot load nvcuda.dll`. Aynı testi bu makinede tek başına
  koşturdum (`dotnet test ... --filter FullyQualifiedName~PerformanceCheckTests.
  IslemciZamaniSayaciDogruOkuyorMu`, `.calisma/` altında değil doğrudan konsola,
  tam süit değil tek test): **Başarılı, 2 dk 3 sn**. Skipped 17=17 ve Total
  1180=1180 zaten birebir eşleştiği için, kümenin geri kalanında set farkı yok —
  Failed'daki tek fark bu bir test, kimlik doğrulamayla kapatıldı.

  **Kök sebep de ölçüldü, tahmin edilmedi.** Test kaynağı
  (`tests/VidShrink.Tests/PerformanceCheckTests.cs:710-711`) nvenc geçişini
  `EncoderCapabilities.Instance.HasEncoder("h264_nvenc")` ile koşullu çalıştırıyor.
  Bu kontrol ffmpeg derlemesinin nvenc'i **derlenmiş** listede tutup tutmadığına
  bakıyor, gerçek GPU/sürücü varlığına değil — CI'ın ffmpeg derlemesi de nvenc'i
  listeliyor (aynı derleme, T115 kurulumundan), ama CI runner'ında NVIDIA sürücüsü
  yok, `nvcuda.dll` çalışma anında yüklenemiyor ve encode adımı hata veriyor. Bu
  makinede gerçek bir GPU var, aynı ffmpeg derlemesiyle aynı encode adımı başarıyla
  çalışıyor — fark ffmpeg derlemesinden değil **donanımdan** geliyor. `HasEncoder`
  kontrolü ve test dosyası `tests/` altında, bu sözleşmenin owns'unda değil,
  değiştirilmedi.
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

  **T115'in ajanına doğrulama verisi (T0'ın isteğiyle).** Tur 2'de gate tam
  koşumu tekrarlanmadı (yalnız tek bir filtreli test koştu, aşağıya bakın), bu
  yüzden kod=66 tur 2'de yeniden alınmadı — aşağıdaki, tur 1'in tam koşumundan
  kalan doğrulama verisi: komut `"$PS" -NoProfile -ExecutionPolicy Bypass -File
  tools/kosum-kapisi/kosum-kapisi.ps1 -MinimumTotal 1134 -MaximumSkipped 30`
  (`$PS`=`powershell`, bu makinede `pwsh` yok). Çıktı (`.calisma/t118-kosum.log`,
  ham bayt denetimi Python'la yapıldı): `Başarılı!  - Başarısız:     0, Başarılı:
  1163, Atlanan:    17, Toplam:  1180, Süre: 22 m 19 s` satırı **evet, gerçekten
  Türkçe** üretildi ve dosyada doğru UTF-8 (`Ba\xc5\x9far\xc4\xb1s\xc4\xb1z` =
  "Başarısız") olarak duruyor; hemen ardından `kosum-kapisi.ps1` `KOSUM KAPISI
  DUSTU: kod=66 sart=Basarisiz/Failed ozeti yok.` bastı. Yani dosyaya yazılan
  bayt dizisi doğruydu ama kapının kendi iç `$text` değişkeninde regex'in aradığı
  "Başarısız"/"ş"/"ı" dizisi eşleşmedi — iki gözlem arasındaki fark (doğru dosya
  baytı, yanlış regex eşleşmesi) `kosum-kapisi.ps1`'in `dotnet test`'i `2>&1` ile
  canlı yakaladığı noktada (satır ~20-24) bir kod sayfası dönüşümü olduğuna işaret
  ediyor; kesin mekanizma (PowerShell 5.1 `[Console]::OutputEncoding` vs. dosyaya
  yazarken kullanılan encoding) bu sözleşmede adım adım izlenmedi.

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
  `-MinimumTotal=1134`, `-MaximumSkipped=30`, `pwsh` yok — aşağıdaki tur 3 notuna bakın).
- Bilinmeyen argüman (`--self-test` dışında) `DURDU` yazıp çıkış kodu 3 verir.
- **Parametreler OKUNUYOR, kopyalanmadı (tur 2, K3'ün cevabı).** `tools/ci-gibi-kos.sh`
  içinde sabit bir sayı yok: her koşumda `grep -E 'kosum-kapisi\.ps1' "$WORKFLOW" |
  tail -1` ile `.github/workflows/ci.yml`'deki güncel satır okunur, `-MinimumTotal`/
  `-MaximumSkipped` oradan `grep -oE` ile çıkarılır (bkz. betiğin ilk 6 satırı).
  `ci.yml` gate parametresini değiştirdiği gün betik bir sonraki koşumda otomatik
  izler, elle güncelleme gerekmez — üçüncü seçenek (kopyalanmış sabit) yok.
- **Donanım uyuşmazlığı sessiz geçilmiyor, görünür kılınıyor (tur 2, K2'nin cevabı).**
  Gate'ten sonra betik `ffmpeg -encoders`'ta `h264_nvenc` listelenip listelenmediğine
  ve küçük bir gerçek deneme kodlamasının (`testsrc2=320x240`, 0,1 sn, tam süit değil)
  başarılı olup olmadığına bakıyor. İkisi de doğruysa bu makinede GPU'nun **gerçekten**
  çalıştığı, CI runner'ında ise yalnız derlemede listeli olup `nvcuda.dll` yokluğuyla
  düştüğü — yani bu sınıftaki testlerin (bilinen örnek:
  `PerformanceCheckTests.IslemciZamaniSayaciDogruOkuyorMu`, `tests/` içinde
  `HasEncoder(".*nvenc")` ile korunan 2 doğrudan çağrı yeri bulundu — bu betiğin
  taradığı yalnız bu ikisi, listenin tam kapsamı **ölçülmedi**) CI'dan farklı
  sonuçlanabileceği bir uyarı olarak basılıyor. Betik farkı **kapatamıyor** (test
  dosyaları owns dışı), yalnız görünür kılıyor — koşum sonunda sessizce geçmiyor.
  İlk sürümde bu prob 64x64 boyutla yazılmıştı ve nvenc'in kendi minimum çözünürlük
  sınırına takılıp her zaman "çalışmıyor" diye yanlış rapor veriyordu; 320x240'a
  çıkarılıp standalone doğrulandıktan sonra teslim edildi.

## Tur 3: iki temsil borcu daha kapatıldı

**1) `pwsh` yokluğu artık ayrı, görünür bir bulgu.** Önceden `--self-test`
`command -v powershell || command -v pwsh` diyip ikisini eşitliyordu, "powershell/pwsh
PATH'te bulundu" tek satırıyla geçiyordu — bu makinede `pwsh` yok, yalnız Windows
PowerShell 5.1 var, ama bu fark görünmüyordu. Artık `pwsh` varsa "ayni surum ailesi"
diyor; yoksa `UYARI:` ile CI'ın `pwsh` (PowerShell 7) kullandığını, bu ortamın 5.1'e
düştüğünü ve `kod=66` gibi açıklanamayan kapı farklarının bu sürüm düşüşünden
kaynaklanabileceğini (kanıtlanmadı, aday) yazıyor — `KENDI-SINAMA` yine geçiyor
(fatal değil), ama sessiz değil. Gerçek koşumda (`ci-gibi-kos.sh` argümansız) `$PS`
seçimi sırasında aynı uyarı tekrar basılıyor, yalnız `--self-test`'e özel değil.

**2) `-MaximumSkipped` artık `-MinimumTotal` ile eş sıkılıkta.** Önceden format
değişir/satır kaybolursa yalnız `UYARI: ... sinirsiz kabul edilecek` yazıp
`--self-test` yine `exit 0` veriyordu, gerçek koşum da `-MaximumSkipped` olmadan
`kosum-kapisi.ps1`'i çağırıyordu — yani betik "CI ile hizalıyım" derken CI'dan daha
gevşek bir kapıyla koşabiliyordu. Artık `-MinimumTotal`'ın izlediği yol: `--self-test`
`DUSTU` deyip `ok=0` yapıyor, gerçek koşum `DURDU` deyip `exit 3` ile duruyor.
Doğrulama: `.calisma/` altında `-MaximumSkipped`i silinmiş bir `ci.yml` kopyası ve
`WORKFLOW` değişkeni o kopyaya çevrilmiş bir betik kopyasıyla iki yol da denendi —
`--self-test` `KENDI-SINAMA DUSTU` (exit 1), gerçek koşum `DURDU` (exit 3) verdi;
kopyalar teslimden önce silindi, `owns` dışına kalıcı iz bırakmadı.

**3) İlk satır artık GPU'yu temsil edemediğini de söylüyor.** `CI TEMSILI:` satırının
hemen altına, koşum ffmpeg-prob'una gitmeden önce, sabit bir `TEMSIL EDEMEDIGI:`
satırı eklendi — bu makinede GPU donanımı gerçek, CI runner'ında yok, betik bu
ekseni asla CI gibi kırmızıya çeviremez. Önceki UYARI (satır ~92-103) hâlâ duruyor
ve yalnız prob gerçekten GPU çalıştığını doğrularsa basılıyor; yeni satır ise
koşuldan bağımsız, her koşumda baştan açıklıyor — K3'ün "temsil ettiği kadar
edemediğini de söylesin" isteğinin karşılığı.

**Doğrulama:** `bash tools/ci-gibi-kos.sh --self-test` tur 3 sonunda yine `KENDI-SINAMA
GECTI` (exit 0) veriyor — yeni `pwsh` UYARI'sı `ok`u düşürmüyor, yalnız görünür
kılıyor; `-MaximumSkipped` gerçek `ci.yml`'de var olduğu için `DUSTU` yolu bu
makinede tetiklenmiyor, yalnız yukarıdaki `.calisma/` senaryosuyla ayrıca kanıtlandı.

## `handbrake-motoru.md` çapaları: main'e karşı yeniden doğrulandı, iki sapma bulundu

T0'ın verdiği "eski main → main'de şimdi" tablosu **beklenti** olarak kullanıldı,
kaynak olarak değil: `origin/main` (uç `2cd0361`) çekildi, dokuz çapanın her biri
kendi alıntı metniyle `auto-mod.md`'nin güncel haline karşı tek tek arandı. Yedisi
tabloyla birebir örtüştü (`:223-225`, `:320`, `:314-318`, `:229`, `:231`, `:235`,
`:237`). İki sapma bulundu:

- **`-g 300`/K6 çapası: tablo `:276` diyor, kendi ölçümüm `:448` buldu.** Çapanın
  alıntısı ("kalemlerin en büyüğü: `-g 300` dosyayı %24,5 küçültürken puanı
  yükseltiyor…") `auto-mod.md`'de yalnız `:448`'de, `## K6 — Sıradaki adım` başlığı
  altında geçiyor — çapanın kendi kayıtlı üst başlığıyla tutarlı. `:276` farklı bir
  cümleye ait: K4 tablosunun bir satırı ("Dosya %24,5 küçülürken…"), yalnız aynı
  "%24,5" rakamını paylaşıyor, alıntı metni farklı. T0'ın talimatı ("bu sayıları
  körlemesine yazma… kendi gözünle bul") gereği kendi ölçümüme güvenip `:448` yazdım;
  tablonun bu satırı muhtemelen iki benzer cümleyi karıştırmış.
- **Öz-referans çapası: tabloda hiç yok, içerik de kaymış (yalnız satır değil).**
  `handbrake-motoru.md`'nin kendi K4 bölümüne yaptığı öz-referans (önceki `:311`)
  T0'ın tablosunda hiç listelenmemişti. Hedef cümle önceden "Sonuç: hizalamanın payı
  negatif." derken, `auto-mod.md`'de şimdi (`:342`) "Sonuç: hizalamanın payı
  ortalamada negatif, p10'da değil." diyor — T111'in sonraki kilit düzeltmesi p10
  kuyruğunda **+0,135** ölçmüş, yani pozitif bir pay bulmuş. Yalnız satır numarasını
  kaydırmak bu değişikliği kaçırırdı: alıntı da, çevresindeki iddia da güncellendi
  (`handbrake-motoru.md` içinde "ortalama için hâlâ geçerli, p10'da tersi var"
  diye nüanslandı) — çapa biçiminin varlık sebebi tam olarak bu: satır numarası
  bayatlar, ama alıntıyı okumadan yalnız `grep`le "bulundu" demek içerik kaymasını
  yakalamaz.

## Ölçülmedi

- Betiğin çıktısı ile CI'ın çıktısındaki 1180 testin tamamı tek tek eşleştirilmedi;
  yalnız Failed'daki tek farkın kimliği doğrulandı (tur 2). Skipped/Total'ın
  birebir tutması set eşitliğinin dolaylı kanıtı, birebir liste karşılaştırması değil.
- `tests/` altında `HasEncoder(".*nvenc")` ile korunan tam çağrı sayısı ve bu
  sınıftaki testlerin tam listesi ölçülmedi; betiğin GPU-uyuşmazlık uyarısı yalnız
  2 doğrudan çağrı yerini buldu (`PerformanceCheckTests.cs`, `EncoderCapabilitiesTests.cs`),
  farklı bir kalıpla (değişken, dolaylı sarmalayıcı) yazılmış başka çağrı yerleri
  olabilir — betik bunları taramıyor, yalnız gate sonucunun kendisini etkileyen
  bilinen örneği belgeliyor.
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
