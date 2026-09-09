# Danisma 001 — UI Tasarimci

- Koltuk: `.calisma/koltuk-tasarimci.md` (agency.js show, 342 satir)
- Ajana verilen olgular: `.calisma/ui-olgular.md` (76 satir)
- Tarih: 2026-09-07
- Maliyet: ~38k token, 4 dk 12 sn

Asagisi ajanin donen cevabinin **tam metnidir**, kisaltilmadi.

---

`.calisma/koltuk-tasarimci.md` rol tanımını ve `.calisma/ui-olgular.md` olgularını okudum. Başka dosya açmadım, komut çalıştırmadım. Aşağıdaki kararlar yalnız o iki dosyaya dayanıyor; belirteç adlarını rolüyle anıyorum, sayı yazmıyorum.

---

# 1. Küçült sayfası — gruplama

**Karar:** Düz listeyi **dört bölge + durum-duyarlı görünürlük**e böl. Üç grup görünür, biri katlanır, eylemler sabit şeride iner.

**Gerekçe:** Bugün hepsi eşit ağırlıkta olduğu için hiçbiri birincil görünmüyor; kullanıcının "kocaman amaç paneli" şikâyeti aslında hiyerarşi yokluğunun belirtisi.

| Bölge | Başlık | İçerik | Ağırlık |
|---|---|---|---|
| A | (başlıksız) | Bırakma alanı + kaynak bilgi satırı | Bağlam |
| B | **Hedef** | Sayı kutusu + doldurma kipi + hazır yongalar | **Birincil** |
| C | **Kalite ve uyumluluk** | Kodek, HDR*, izinler, GPU | İkincil |
| D | **Gelişmiş** ▸ | CRF, ön ayar, mod, kodek kilidi, ses, alt sınırlar, kodlayıcı yolu | Katlı, varsayılan kapalı |
| — | (şerit) | Gözat · **Küçült** | Sabit alt eylem |

\* **HDR satırı kaynak HDR değilse hiç çizilmez.** Yer kaplamayan en iyi denetim, gösterilmeyen denetimdir.

**Gizlenecekler:** *Klasörde göster, kopyala, paylaş* iş bitmeden görünmez — bunlar ayar değil, sonuç eylemleridir; sonuç kartında doğarlar. *İptal* yalnız koşarken var, *Küçült*'ün yerini alır (iki buton yan yana durmaz).

**Kaynak bilgi satırı** (süre, çözünürlük, kare hızı, bit hızı, boyut, ses, kodek, HDR) dokuz ayrı satır değil, **tek sarmalı etiket şeridi** olsun — ikincil metin belirteci, orta noktayla ayrılmış. Dokuz satır dokuz karar gibi görünüyor, oysa hiçbiri tıklanabilir değil.

```
┌───────────────────────────────────────────────────────────┐
│  ⬓  Videoyu buraya bırak            [ Gözat ]             │
│     tatil.mp4 · 04:12 · 1920×1080 · 30 fps · 8,4 Mb/s     │
│     · 264 MB · AAC 2.0 · H.264 · SDR                      │
└───────────────────────────────────────────────────────────┘

  HEDEF
  ┌──────────────────┐  ┌───────────────┐
  │  25       MB  ▲▼ │  │ Hedef │ Tavan │      ← doldurma kipi
  └──────────────────┘  └───────────────┘
  ( 8 ) ( 25 ) ( 100 ) ( 128 ) ( 180 ) ( Yarısı ) ( WhatsApp )

  KALİTE VE UYUMLULUK
  Kodek       │ Otomatik │ Uyumlu │ En küçük │
  HDR         │ Koru │ SDR'a indir │            (yalnız HDR kaynakta)
  Çözünürlük düşürülebilir              (•——)
  Kare hızı düşürülebilir               (——•)
  Ekran kartı hızlandırma               (•——)

  ▸ Gelişmiş

──────────────────────────────────────────────────────────────
                                        [   Küçült   ]
```

**Sıra gerekçesi:** kullanıcı önce *neye* indireceğini söyler (Hedef), sonra *neyden ödün vereceğini* (Kalite). Ters sıra, henüz hedefi bilmeyen kişiye ödün sorusu sormak olur.

**Yan etki uyarısı:** sabit alt şerit ve HDR'ın koşullu gizlenmesi sayfa boyunu kısaltır; olgular "yerleşim ölçüleri teste pimli, sayfa boyu büyürse ölçüm kırmızı" diyor. Bu değişiklik boyu **küçültür**, ama pimler eşitlik bekliyorsa yine de kırmızıya döner — pimlerin yeniden temellendirilmesi işin parçası olarak planlansın.

---

# 2. İki-üç seçenekli ayarlar — bileşen seçimi

**Karar:** Tek bir **segmentli denetim** bileşeni; anahtar (switch) yalnız gerçek aç/kapa için.

| Ayar | Bileşen | Neden |
|---|---|---|
| **Kodek** (3) | Segmentli denetim | Üç eş-değerli isim; hiçbiri "kapalı" değil, üçü de bir seçim |
| **HDR** (2) | Segmentli denetim | "Koru / SDR'a indir" iki isimlendirilmiş durum. Anahtar olsaydı "HDR açık" diye okunur, oysa kapalı durumu HDR yokluğu değil *SDR'a dönüştürme eylemi* — anahtar yalan söyler |
| **Doldurma** (2) | Segmentli denetim, hedef kutusuna bitişik | Sayının anlamını değiştiriyor, bağımsız ayar değil (bkz. madde 3) |
| **Amaç** (2) | Ayrı denetim olarak **kaldırılır** (bkz. madde 3) | — |
| **İzinler** (2 ad.) | Anahtar | Gerçek aç/kapa: "izin var / izin yok". İkinci bir isim yok |
| **GPU hızlandırma** | Anahtar | Aynı gerekçe |

**İki ve üç seçenekli aynı bileşeni kullanmalı mı — evet.** Ayrı bileşen, kullanıcıya olmayan bir anlam farkı öğretir; segment sayısı görsel olarak zaten kendini söylüyor. Tutarlılık, mikro-optimizasyondan değerli.

**Türkçe uzunluğu — bileşenin kuralı:** segmentli denetim **satır içi etiket + segmentler** olarak değil, **üstte etiket, altta tam genişlik segment şeridi** olarak kurulsun. Etiket satır içi kalırsa "Ekran kartı hızlandırma" gibi uzun Türkçe etiket segmentleri sıkıştırır ve İngilizce'de sorunsuz görünen düzen Türkçe'de sarar — olgular bunun ölçümü daha önce kırmızıya düşürdüğünü söylüyor.

**Kesin sınır:** segmentli denetim **en çok 3 segment**. Dördüncüsü gerekirse açılır liste. Kodek üç seçenekle tam sınırda; ileride "en küçük (yavaş)" gibi bir dördüncü eklenirse bileşen değişmeli.

**Erişilebilirlik:** segmentli denetim bir radyo grubudur — ok tuşlarıyla gezilir, Sekme ile grubun tamamı tek durak olur; seçili segment yalnız dolgu ile değil, ayırt edici bir kenar/ağırlıkla da işaretlenir (renkle taşınan tek bilgi olmasın).

---

# 3. "Amaç" ve "Doldurma" ayrı denetim olmalı mı?

**Karar: Amaç kaldırılır, Doldurma hedef kutusuna gömülür.** İki denetim → sıfır bağımsız denetim.

**Doldurma:** "Hedef / Tavan" bağımsız bir tercih değil, yanındaki sayının **operatörüdür** — `≈25 MB` mı `≤25 MB` mi. Kendi başlığı ve kendi paneliyle durması, kullanıcıyı sayının kendisinden koparıyor. Sayı kutusunun sağına bitişik, iki segmentli bir kip olarak yapıştır; segment metinleri "Hedef"/"Tavan" kalsın, altında tek satır ikincil açıklama sayının okunuşunu yazsın ("yaklaşık 25 MB" / "en çok 25 MB").

```
┌──────────────────┐┌───────────────┐
│  25       MB  ▲▼ ││ Hedef │ Tavan │
└──────────────────┘└───────────────┘
   en çok 25 MB
```

**Amaç:** "Arşiv / Paylaşım" kullanıcının bir *niyet* beyanı ve zaten hazır yonga şeridinde örtük olarak var — "WhatsApp" yongası paylaşım niyetinin ta kendisi. Ayrı bir kocaman panel olarak durması, kullanıcının şikâyet ettiği boşluğu üreten şey.

**Öneri:** Amaç'ı yonga şeridinin **başına iki adlandırılmış ön ayar** olarak taşı — `( Arşiv ) ( Paylaşım ) │ ( 8 ) ( 25 ) …` — ince bir ayraçla boyut yongalarından ayrılsın. Ön ayara basmak hedefi/kipi doldurur, sonra kullanıcı elle değiştirebilir; değiştirince ön ayar seçimi kalkar. Böylece Amaç bir denetim değil, bir **kısayol** olur.

**Bilmediğim yer:** Amaç'ın motorda hedef boyut ve doldurma kipi dışında ne değiştirdiğini bilmiyorum (kodek seçimini, ses kbps'ini, çözünürlük iznini de sürüklüyor olabilir). Yalnız hedef+kip'i etkiliyorsa bu birleştirme kayıpsızdır; başka parametreleri de sürüklüyorsa ön ayar birden çok alanı doldurmalı ve **hangi alanları değiştirdiğini kullanıcıya göstermeli** (dolan alanlar kısa süreli vurgulanır). Bu bilgi olgularda yok, kod okumadım.

---

# 4. Taşma kesme teklifi

**Karar:** Modal değil, **sonuç kartının içinde açılan sayfa içi şerit**; varsayılan öneri + katlı "Diğer seçenekler"; yıkıcı olan iki adımlı.

**Neden modal değil:** render bitti, engellenen bir şey yok. Modal kullanıcının karar vermek için ihtiyaç duyduğu şeyi — hedef, çıkan boyut, önizleme — perdeler ve dört seçeneği eşit ağırlıkta bir sıraya dizerek "hangisi normal?" sorusunu doğurur. Şerit, kararı ölçümün yanında bırakır.

**Neden dördü birden değil:** dört eş buton, sık durumda (biraz aşmış, tekrar denersen olur) kullanıcıyı gereksiz bir tercih ağacına sokar; ikisi ise yıkıcıdır ve yanlışlıkla seçilebilir. Bir öneri + iki güvenli + katlı yıkıcı, doğru olanı ucuz, tehlikeli olanı bilinçli yapar.

```
┌─ ⚠ Hedefin üstünde ────────────────────────────────────────┐
│  Hedef 25 MB · Çıkan 26,1 MB · %4,4 aşım                   │
│                                                            │
│  [ Daha sıkı ayarla ve tekrar dene ]  ( Dosyayı böyle tut )│
│                                                            │
│  ▸ Diğer seçenekler                                        │
└────────────────────────────────────────────────────────────┘
```

Açıldığında:

```
│  ▾ Diğer seçenekler                                        │
│                                                            │
│   Videodan kırp — hedefe sığar, görüntü kalitesi korunur   │
│   │ Sondan │ Baştan │ Her ikisinden │                      │
│   %3 kesilir · yaklaşık 7 sn · süre 4:12 → 4:05            │
│              ( Kırp ve kaydet )        ← yıkıcı belirteç   │
│   ─────────────────────────────────────────────────────    │
│   ( Çıktıyı sil, vazgeç )              ← yıkıcı belirteç   │
```

**Yıkıcı olanı nasıl ayırt ederim — beş ayrı katman, tek başına renge yaslanmadan:**

1. **Konum:** katlı alanın içinde; kart açıldığında ekranda değil, bir jest gerektiriyor.
2. **Belirteç:** yıkıcı eylem rengi/kenarı (Theme'in tehlike belirteci) — ama tek işaret değil.
3. **Sonucu yazıyla söyler:** "%3 kesinti" soyut; "4:12 → 4:05, sondan ~7 sn" somut. Kayıp, sayı olarak butonun üstünde durur.
4. **Odak almaz:** Enter varsayılanı hep birincil öneridir; kırpma ve silme klavye varsayılanı olamaz.
5. **İki adım:** "Kırp ve kaydet"e basınca buton yerinde onaya döner ("Emin misin? Kırp") — ayrı bir diyalog açılmaz, ama tek tıkla olmaz.

**Ek karar:** kırpma önce **önizleme panelinde** gösterilsin — kesilecek aralık zaman şeridinde işaretlenir, kullanıcı "Kırp"a basmadan neyin gideceğini görür. Yıkıcı eylemin en iyi koruması onay kutusu değil, önizlemedir.

**Metin uyarısı:** dört seçeneğin etiketleri Türkçe'de uzun; butonlar sabit genişlikte değil, içeriğe göre büyüyen ve gerekirse dikey yığılan bir düzende olsun.

---

# 5. Önizleme paneli rozetleri

**Karar:** Üstte iki taraf etiketi köşelerde **sabit**; CRF rozeti işlenmiş tarafın etiketine bitişik, ikincil ağırlıkta; "Yaklaşık önizleme" metni tamamen kalkar.

```
┌──────────────────────────────────────────────────────────┐
│  ORİJİNAL                          İŞLENMİŞ · CRF 24     │
│                          ┃                               │
│                          ┃                               │
│         (orijinal)      ═╋═      (işlenmiş)              │
│                          ┃                               │
│                          ┃                               │
│                                                 %100     │
└──────────────────────────────────────────────────────────┘
```

**Hizalama:** sol etiket panelin üst-sol köşesine, sağ etiket üst-sağ köşesine yaslanır. Kullanıcının "orta panelin sağında ve solunda, üstte" isteği bununla karşılanıyor — perdenin solundaki her şey orijinal, sağındaki her şey işlenmiş.

**Perde hareket ederken sabit mi, kayar mı — sabit.** Gerekçe: sürükleme sırasında kayan metin okunmaz, perde uca yaklaşınca kırpılır ya da diğer etiketle çakışır; perdenin nerede olduğunu tutamacın kendisi zaten söylüyor. **Tek dinamik davranış:** perde bir tarafı tamamen kapattığında o tarafın etiketi soluklaşır — panelde görünmeyen şeyin etiketi tam ağırlıkta durmamalı.

**Ağırlık sırası:**
- **Taraf etiketleri** — birincil metin belirteci, orta ağırlık, büyük harf değil (Türkçe büyük harf İ/I sorunu ve okunurluk kaybı). Bunlar *ne baktığını* söyler, en güçlü olan bunlar.
- **CRF rozeti** — ikincil metin belirteci, tek aralıklı (monospace) yüz, yalnız işlenmiş tarafta. Teknik bir olgudur, taraf adının kimliğini paylaşmaz; "İŞLENMİŞ" ile arasında orta nokta ayracı, kendi kutusu olmasın — iki kutu iki eşit rozet gibi görünür.
- **Yakınlaştırma göstergesi** — üçüncül, alt köşede, tutamacın karşı tarafında; taban ölçekte gizli, değişince belirir.

**Neden CRF sağda:** CRF yalnız çıktıyı tanımlıyor; ortada ya da solda durursa iki tarafa birden aitmiş gibi okunur.

**Bilmediğim yer:** `LeftBadgeText` / `RightBadgeText` bugün hangi tarafa hangi metni koyuyor, olgularda yazmıyor. Ben **sol = orijinal, sağ = işlenmiş** öneriyorum (okuma yönü = önce/sonra); bugünkü eşleme tersse kod değil, kullanıcı beklentisi kazansın. Ayrıca CRF değerinin önizleme anında bilinip bilinmediğini (gerçek kodlama parametresi mi, tahmin mi) bilmiyorum — tahminse rozet bir tahmin işareti taşımalı, ama "Yaklaşık önizleme" cümlesi olarak değil.

---

# 6. Sekme sırası

**Karar:** Oynatıcı en sola konur (kullanıcının isteği), Küçült varsayılan seçili kalır (kullanıcının isteği), **çelişki bir ayraç ve iki gruplama ile çözülür** — sıra değiştirilmez.

```
┌──────────┬─┬─────────┬───────────┬─┬────────┬─────────┬──────────┐
│ ▷ Oynat  │ │ Küçült  │ Dönüştür  │ │ Ayarlar│ Hakkında│ Gelişmiş │
└──────────┴─┴═════════┴───────────┴─┴────────┴─────────┴──────────┘
   araç      ↑  iş akışı              ↑   yardımcı
           ayraç                    ayraç
```

**Yanıltır mı — kısmen, ve yalnız ilk açılışta.** "En soldaki varsayılandır" beklentisi adım dizisi okunan şeritlerde güçlüdür. Ama bu şerit bir sihirbaz değil; kullanıcı "Oynat"ı bir *kip*, "Küçült/Dönüştür"ü bir *iş* olarak okursa çelişki kalkar. Onu okutmanın yolu:

1. **Ayraç:** Oynatıcı ile Küçült arasına ince bir dikey ayraç. Oynatıcı böylece "birinci adım" değil, "ayrı bir mod" olarak okunur.
2. **İkonlu tek sekme:** yalnız Oynatıcı'da bir oynat ikonu bulunsun; diğerleri düz metin. Tek başına farklı görünen öğe, "sıradaki ilk" olarak sayılmaz.
3. **Güçlü seçim işareti:** seçili sekmenin göstergesi (alt çizgi + metin ağırlığı) tereddüde yer bırakmayacak kadar belirgin olsun; kullanıcı hangi sekmede olduğunu sormasın diye.

Üçü birlikteyken kalan yanılma payı, elde edilen tutarlılığa değer: mp4 dosyasını VidShrink ile açınca Oynatıcı seçili gelecek — yani **"sana bir video verildiğinde olan şey" en solda** duruyor. Bu, sırayı kendi içinde anlamlı kılıyor; kullanıcının isteği aslında tutarlı bir modelin ifadesi.

**Ayraç yetmezse plan B:** Oynatıcı'yı sekme şeridinden çıkar, şeridin solunda **ayrı bir mod düğmesi** yap (ikon + metin, sekme sırtı yok). O zaman "en soldaki sekme" tartışması biter — Küçült şeridin ilk sekmesi olur, Oynatıcı bir düğme. Yalnız bu, sekme klavye gezinmesini (Ctrl+Tab dizisi) ikiye böler; A planı bunu bozmadığı için önce onu denerim.

**Gelişmiş sekmesi:** koşullu görünüyor — görünüp kaybolan bir sekme şeridi sıçratır. En sağda kalması doğru; görünürlüğü değiştiğinde diğer sekmelerin yeri kaymamalı.

---

## Bilmediklerim (tahmin etmedim)

1. **Amaç**'ın motorda hedef+kip dışında hangi parametreleri sürüklediği.
2. Önizlemedeki **sol/sağ rozetin bugünkü içeriği** ve CRF değerinin önizleme anında gerçek mi tahmini mi olduğu.
3. **Gelişmiş sekmesi** ile Küçült sayfasındaki **Gelişmiş panel** aynı içerik mi, farklı mı — aynıysa biri kaldırılmalı, ama hangisi olduğunu olgular söylemiyor.
4. `Theme.axaml` içindeki belirteç **adları** — yukarıda belirteçleri rolüyle andım ("yıkıcı eylem rengi", "ikincil metin"); karşılığı olmayan bir rol varsa yeni belirteç üretmek yerine sorulmalı.
5. Yerleşim ölçülerini pimleyen testlerin **tam olarak neyi** pimlediği (sayfa boyu mu, tek tek denetim konumları mı) — bu, yukarıdaki değişikliklerin ölçüm maliyetini belirler.

---

## Senden istediklerim

1. **Sol/sağ eşlemesini onayla.** Önizlemede `ORİJİNAL` solda, `İŞLENMİŞ · CRF x` sağda olsun mu — bugünkü kod tersse hangisi kazanır?
2. **Amaç'ın kaldırılmasını onayla.** "Amaç ayrı denetim olmaktan çıksın, yonga şeridinin başına `( Arşiv ) ( Paylaşım )` ön ayarı olarak taşınsın" — kabul mü, yoksa Amaç motorda başka parametreleri de sürüklüyor mu?
3. **Gelişmiş çakışmasını netleştir.** `main.tab.advanced` sekmesi ile `main.advanced.title` paneli aynı içerik mi? Aynıysa hangisi kalsın?
4. **Sekme için A mı B mi.** A: ayraç + ikon (sıra aynı, `Oynat │ Küçült Dönüştür │ Ayarlar Hakkında Gelişmiş`). B: Oynatıcı şeritten çıkıp sol tarafta ayrı mod düğmesi olur.
5. **Pim maliyetini kabul et.** Yerleşim değişince ölçüm kırmızıya döner; pimlerin yeniden temellendirilmesi bu işin parçası olarak planlansın mı?