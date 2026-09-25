# Danışma 014: Soru

claude-fable-5-1, 5.337 çıktı token, 112 sn. Girdi: [014-fable-sure-neden-uzun-girdi.md](014-fable-sure-neden-uzun-girdi.md). Cevap olduğu gibi:

---

## 1) Kullanıcıya dürüst açıklama (3-4 satır)

Süre "ufacık değişiklik"e değil, değişikliğin geçmek zorunda olduğu **sabit kapıya** gidiyor. Bugünkü ölçülmüş taban: yerel dokunulan-alan testi ~4 dk + main CI 13 dk + yayın ~3,6 dk ≈ **21 dk**; bu, hiçbir şey kırılmazsa. 0.9.3'te 30-45 dk olmasının nedeni bir kez kırmızı görmekti: her kırmızı-düzelt-it döngüsü tam 13 dk daha ekler, üstüne yayın o gün testleri ikinci kez (12,4 dk) koştu — o son kalem bugün kapandı.

CI'ın 13 dakikası da testin gerçek maliyeti değil, **en yavaş parçanın** maliyeti: 36120718858'de ana-kalan 13,0 dk, ana-ow 10,9 dk, ana-kp 10,4 dk sürerken ana-gad 4,5 dk, yerleşimler 3-5 dk'da bitti. Kurulum + derleme parça başına yalnız 1,7 dk (ffmpeg 4 sn, libmpv 3 sn, build 83 sn); ana-kalan'ın 11,2 dk'sı saf test koşumu (2200 test).

## 2) Plan doğru mu, risk ne, daha iyisi var mı

**Plan (alan matrisi) işe yarar ama en etkili ve en güvenli kısaltma değil.** İki nedenle:

- **Risk gerçek ve iki yüzlü.** (a) Sınıflandırma "değişen dosya" üzerinden yapılıyor, ama kırılma değişen dosyada değil onu **çağıran** yerde çıkar: `Core/Setup` içinde bir imza değişince `App` derlenir (build tam, o yakalanır) ama App'in davranış testleri koşmaz. (b) Süzgeç sınıf adına bakıyor; kurucu koduna dokunan ama adı listede olmayan bir test sınıfı (bellekte örneği var: `BiciminTests` süzgeci `BaslikKapsamiTests`'i koşturmadı, CI kırmızı) sessizce dışarıda kalır ve boş süzgeç yeşil döner. Bu ikinciyi ancak "kurucu parçası en az N test koştu" pimi ve kaynak okuyan bir kapsam testi tutar — yani planın kendisi yeni bir bakım yükü getiriyor.
- **Kazanç sınırlı.** Alan modu tek parçayı 1,7 + ~3,5 ≈ 5-6 dk'ya indiriyor; aşağıdaki risksiz yol aynı yere **her commit için** iniyor.

**Önerdiğim sıra:**

1. **Önce parça dengesi (risksiz, en büyük kazanç).** Üç büyük parça 10-13 dk, gerisi 3-5 dk. ana-kalan (2200 test), ana-kp ve ana-ow'u ikişer-üçer parçaya bölünce duvar saati ~13 dk'dan **~6-7 dk**'ya iner; parça başına 1,7 dk kurulum bedeli var, 8→13 parça bunu karşılar. Harfle bölme yönteminiz zaten kurulu, dördüncü parça tümleyen — aynı desen. Hiçbir test atlanmaz, sınıflandırma hatası yok. Tek şart: bölmeden önce sınıf başına süreyi trx'ten okumak; sayıya (2200) değil süreye göre bölün (memory: ölçüm sürüm ofseti / paralel koşum kararsızlığı burada geçerli değil, tek sefer okunur).

2. **Alan modunu yalnız dalda, main'de her zaman tam.** Sorudaki "main'de tam, dalda alan" seçeneği doğru olan bu: 30-45 dk'yı üreten şey **yineleme döngüsü**dür (kırmızı → düzelt → tekrar it), o döngü dalda döner; alan modu dalda döngüyü 5-6 dk'ya indirir, main'in tam koşumu güvenliği korur. Yayın zaten main yeşiline bağlı, o yüzden main'de alan modu yayın yolunu **kısaltmaz** da — 1. adım kısaltır. Böylece riskli süzgeç yalnız ön elemede kalır, teslimin kapısı olmaz.

3. **Küçük bir tuzak: README'yi ayrı commit'e almayın.** `paths-ignore` `**/*.md`'yi dışladığı için README-only commit'te CI hiç açılmaz; ad70e5d0'ın "aynı commit'in CI yeşili varsa yayın testleri atlar" kısa yolu o commit'te yeşil bulamaz ve yayın 12,4 dk'lık tam süite düşer. Ya kurucu değişikliğiyle aynı commit'te tutun (bugünkü gibi), ya da yayının "CI yeşili yok" durumunda README-only farkı tanıyıp testsiz geçmesine izin verin — ikincisi `git diff --name-only` ile tek satır.

**Özet hüküm:** plan yanlış değil ama yanlış yere konmuş. Parça dengesi (main dahil, risksiz, ~13→6-7 dk) + alan modu yalnız dalda (yineleme, ~5 dk) + README tuzağı kapatılırsa minör yayın toplamı ~21 dk'dan **~12-14 dk**'ya iner, hem de kurucu dışı kırılma main'de görünmeye devam eder. Majörde tam süit + kosum-kapisi olduğu gibi kalır.
