 Danisma 001 — UI Tasarimci# Danisma 008 — UI Tasarimci
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci- Koltuk: `.calisma/koltuk-tasarimci.md` (agency.js show, 342 satir)
 Danisma 001 — UI Tasarimci- Ajana verilen olgular: `.calisma/ui-olgular.md` (76 satir)
 Danisma 001 — UI Tasarimci- Tarih: 2026-09-07
 Danisma 001 — UI Tasarimci- Maliyet: ~38k token, 4 dk 12 sn
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI TasarimciAsagisi ajanin donen cevabinin **tam metnidir**, kisaltilmadi.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci`.calisma/koltuk-tasarimci.md` rol tanımını ve `.calisma/ui-olgular.md` olgularını okudum. Başka dosya açmadım, komut çalıştırmadım. Aşağıdaki kararlar yalnız o iki dosyaya dayanıyor; belirteç adlarını rolüyle anıyorum, sayı yazmıyorum.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci# 1. Küçült sayfası — gruplama
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Karar:** Düz listeyi **dört bölge + durum-duyarlı görünürlük**e böl. Üç grup görünür, biri katlanır, eylemler sabit şeride iner.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Gerekçe:** Bugün hepsi eşit ağırlıkta olduğu için hiçbiri birincil görünmüyor; kullanıcının "kocaman amaç paneli" şikâyeti aslında hiyerarşi yokluğunun belirtisi.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci| Bölge | Başlık | İçerik | Ağırlık |
 Danisma 001 — UI Tasarimci|---|---|---|---|
 Danisma 001 — UI Tasarimci| A | (başlıksız) | Bırakma alanı + kaynak bilgi satırı | Bağlam |
 Danisma 001 — UI Tasarimci| B | **Hedef** | Sayı kutusu + doldurma kipi + hazır yongalar | **Birincil** |
 Danisma 001 — UI Tasarimci| C | **Kalite ve uyumluluk** | Kodek, HDR*, izinler, GPU | İkincil |
 Danisma 001 — UI Tasarimci| D | **Gelişmiş** ▸ | CRF, ön ayar, mod, kodek kilidi, ses, alt sınırlar, kodlayıcı yolu | Katlı, varsayılan kapalı |
 Danisma 001 — UI Tasarimci| — | (şerit) | Gözat · **Küçült** | Sabit alt eylem |
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci\* **HDR satırı kaynak HDR değilse hiç çizilmez.** Yer kaplamayan en iyi denetim, gösterilmeyen denetimdir.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Gizlenecekler:** *Klasörde göster, kopyala, paylaş* iş bitmeden görünmez — bunlar ayar değil, sonuç eylemleridir; sonuç kartında doğarlar. *İptal* yalnız koşarken var, *Küçült*'ün yerini alır (iki buton yan yana durmaz).
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Kaynak bilgi satırı** (süre, çözünürlük, kare hızı, bit hızı, boyut, ses, kodek, HDR) dokuz ayrı satır değil, **tek sarmalı etiket şeridi** olsun — ikincil metin belirteci, orta noktayla ayrılmış. Dokuz satır dokuz karar gibi görünüyor, oysa hiçbiri tıklanabilir değil.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci┌───────────────────────────────────────────────────────────┐
 Danisma 001 — UI Tasarimci│  ⬓  Videoyu buraya bırak            [ Gözat ]             │
 Danisma 001 — UI Tasarimci│     tatil.mp4 · 04:12 · 1920×1080 · 30 fps · 8,4 Mb/s     │
 Danisma 001 — UI Tasarimci│     · 264 MB · AAC 2.0 · H.264 · SDR                      │
 Danisma 001 — UI Tasarimci└───────────────────────────────────────────────────────────┘
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci  HEDEF
 Danisma 001 — UI Tasarimci  ┌──────────────────┐  ┌───────────────┐
 Danisma 001 — UI Tasarimci  │  25       MB  ▲▼ │  │ Hedef │ Tavan │      ← doldurma kipi
 Danisma 001 — UI Tasarimci  └──────────────────┘  └───────────────┘
 Danisma 001 — UI Tasarimci  ( 8 ) ( 25 ) ( 100 ) ( 128 ) ( 180 ) ( Yarısı ) ( WhatsApp )
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci  KALİTE VE UYUMLULUK
 Danisma 001 — UI Tasarimci  Kodek       │ Otomatik │ Uyumlu │ En küçük │
 Danisma 001 — UI Tasarimci  HDR         │ Koru │ SDR'a indir │            (yalnız HDR kaynakta)
 Danisma 001 — UI Tasarimci  Çözünürlük düşürülebilir              (•——)
 Danisma 001 — UI Tasarimci  Kare hızı düşürülebilir               (——•)
 Danisma 001 — UI Tasarimci  Ekran kartı hızlandırma               (•——)
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci  ▸ Gelişmiş
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci──────────────────────────────────────────────────────────────
 Danisma 001 — UI Tasarimci                                        [   Küçült   ]
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Sıra gerekçesi:** kullanıcı önce *neye* indireceğini söyler (Hedef), sonra *neyden ödün vereceğini* (Kalite). Ters sıra, henüz hedefi bilmeyen kişiye ödün sorusu sormak olur.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Yan etki uyarısı:** sabit alt şerit ve HDR'ın koşullu gizlenmesi sayfa boyunu kısaltır; olgular "yerleşim ölçüleri teste pimli, sayfa boyu büyürse ölçüm kırmızı" diyor. Bu değişiklik boyu **küçültür**, ama pimler eşitlik bekliyorsa yine de kırmızıya döner — pimlerin yeniden temellendirilmesi işin parçası olarak planlansın.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci# 2. İki-üç seçenekli ayarlar — bileşen seçimi
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Karar:** Tek bir **segmentli denetim** bileşeni; anahtar (switch) yalnız gerçek aç/kapa için.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci| Ayar | Bileşen | Neden |
 Danisma 001 — UI Tasarimci|---|---|---|
 Danisma 001 — UI Tasarimci| **Kodek** (3) | Segmentli denetim | Üç eş-değerli isim; hiçbiri "kapalı" değil, üçü de bir seçim |
 Danisma 001 — UI Tasarimci| **HDR** (2) | Segmentli denetim | "Koru / SDR'a indir" iki isimlendirilmiş durum. Anahtar olsaydı "HDR açık" diye okunur, oysa kapalı durumu HDR yokluğu değil *SDR'a dönüştürme eylemi* — anahtar yalan söyler |
 Danisma 001 — UI Tasarimci| **Doldurma** (2) | Segmentli denetim, hedef kutusuna bitişik | Sayının anlamını değiştiriyor, bağımsız ayar değil (bkz. madde 3) |
 Danisma 001 — UI Tasarimci| **Amaç** (2) | Ayrı denetim olarak **kaldırılır** (bkz. madde 3) | — |
 Danisma 001 — UI Tasarimci| **İzinler** (2 ad.) | Anahtar | Gerçek aç/kapa: "izin var / izin yok". İkinci bir isim yok |
 Danisma 001 — UI Tasarimci| **GPU hızlandırma** | Anahtar | Aynı gerekçe |
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**İki ve üç seçenekli aynı bileşeni kullanmalı mı — evet.** Ayrı bileşen, kullanıcıya olmayan bir anlam farkı öğretir; segment sayısı görsel olarak zaten kendini söylüyor. Tutarlılık, mikro-optimizasyondan değerli.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Türkçe uzunluğu — bileşenin kuralı:** segmentli denetim **satır içi etiket + segmentler** olarak değil, **üstte etiket, altta tam genişlik segment şeridi** olarak kurulsun. Etiket satır içi kalırsa "Ekran kartı hızlandırma" gibi uzun Türkçe etiket segmentleri sıkıştırır ve İngilizce'de sorunsuz görünen düzen Türkçe'de sarar — olgular bunun ölçümü daha önce kırmızıya düşürdüğünü söylüyor.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Kesin sınır:** segmentli denetim **en çok 3 segment**. Dördüncüsü gerekirse açılır liste. Kodek üç seçenekle tam sınırda; ileride "en küçük (yavaş)" gibi bir dördüncü eklenirse bileşen değişmeli.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Erişilebilirlik:** segmentli denetim bir radyo grubudur — ok tuşlarıyla gezilir, Sekme ile grubun tamamı tek durak olur; seçili segment yalnız dolgu ile değil, ayırt edici bir kenar/ağırlıkla da işaretlenir (renkle taşınan tek bilgi olmasın).
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci# 3. "Amaç" ve "Doldurma" ayrı denetim olmalı mı?
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Karar: Amaç kaldırılır, Doldurma hedef kutusuna gömülür.** İki denetim → sıfır bağımsız denetim.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Doldurma:** "Hedef / Tavan" bağımsız bir tercih değil, yanındaki sayının **operatörüdür** — `≈25 MB` mı `≤25 MB` mi. Kendi başlığı ve kendi paneliyle durması, kullanıcıyı sayının kendisinden koparıyor. Sayı kutusunun sağına bitişik, iki segmentli bir kip olarak yapıştır; segment metinleri "Hedef"/"Tavan" kalsın, altında tek satır ikincil açıklama sayının okunuşunu yazsın ("yaklaşık 25 MB" / "en çok 25 MB").
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci┌──────────────────┐┌───────────────┐
 Danisma 001 — UI Tasarimci│  25       MB  ▲▼ ││ Hedef │ Tavan │
 Danisma 001 — UI Tasarimci└──────────────────┘└───────────────┘
 Danisma 001 — UI Tasarimci   en çok 25 MB
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Amaç:** "Arşiv / Paylaşım" kullanıcının bir *niyet* beyanı ve zaten hazır yonga şeridinde örtük olarak var — "WhatsApp" yongası paylaşım niyetinin ta kendisi. Ayrı bir kocaman panel olarak durması, kullanıcının şikâyet ettiği boşluğu üreten şey.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Öneri:** Amaç'ı yonga şeridinin **başına iki adlandırılmış ön ayar** olarak taşı — `( Arşiv ) ( Paylaşım ) │ ( 8 ) ( 25 ) …` — ince bir ayraçla boyut yongalarından ayrılsın. Ön ayara basmak hedefi/kipi doldurur, sonra kullanıcı elle değiştirebilir; değiştirince ön ayar seçimi kalkar. Böylece Amaç bir denetim değil, bir **kısayol** olur.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Bilmediğim yer:** Amaç'ın motorda hedef boyut ve doldurma kipi dışında ne değiştirdiğini bilmiyorum (kodek seçimini, ses kbps'ini, çözünürlük iznini de sürüklüyor olabilir). Yalnız hedef+kip'i etkiliyorsa bu birleştirme kayıpsızdır; başka parametreleri de sürüklüyorsa ön ayar birden çok alanı doldurmalı ve **hangi alanları değiştirdiğini kullanıcıya göstermeli** (dolan alanlar kısa süreli vurgulanır). Bu bilgi olgularda yok, kod okumadım.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci# 4. Taşma kesme teklifi
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Karar:** Modal değil, **sonuç kartının içinde açılan sayfa içi şerit**; varsayılan öneri + katlı "Diğer seçenekler"; yıkıcı olan iki adımlı.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Neden modal değil:** render bitti, engellenen bir şey yok. Modal kullanıcının karar vermek için ihtiyaç duyduğu şeyi — hedef, çıkan boyut, önizleme — perdeler ve dört seçeneği eşit ağırlıkta bir sıraya dizerek "hangisi normal?" sorusunu doğurur. Şerit, kararı ölçümün yanında bırakır.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Neden dördü birden değil:** dört eş buton, sık durumda (biraz aşmış, tekrar denersen olur) kullanıcıyı gereksiz bir tercih ağacına sokar; ikisi ise yıkıcıdır ve yanlışlıkla seçilebilir. Bir öneri + iki güvenli + katlı yıkıcı, doğru olanı ucuz, tehlikeli olanı bilinçli yapar.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci┌─ ⚠ Hedefin üstünde ────────────────────────────────────────┐
 Danisma 001 — UI Tasarimci│  Hedef 25 MB · Çıkan 26,1 MB · %4,4 aşım                   │
 Danisma 001 — UI Tasarimci│                                                            │
 Danisma 001 — UI Tasarimci│  [ Daha sıkı ayarla ve tekrar dene ]  ( Dosyayı böyle tut )│
 Danisma 001 — UI Tasarimci│                                                            │
 Danisma 001 — UI Tasarimci│  ▸ Diğer seçenekler                                        │
 Danisma 001 — UI Tasarimci└────────────────────────────────────────────────────────────┘
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI TasarimciAçıldığında:
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci│  ▾ Diğer seçenekler                                        │
 Danisma 001 — UI Tasarimci│                                                            │
 Danisma 001 — UI Tasarimci│   Videodan kırp — hedefe sığar, görüntü kalitesi korunur   │
 Danisma 001 — UI Tasarimci│   │ Sondan │ Baştan │ Her ikisinden │                      │
 Danisma 001 — UI Tasarimci│   %3 kesilir · yaklaşık 7 sn · süre 4:12 → 4:05            │
 Danisma 001 — UI Tasarimci│              ( Kırp ve kaydet )        ← yıkıcı belirteç   │
 Danisma 001 — UI Tasarimci│   ─────────────────────────────────────────────────────    │
 Danisma 001 — UI Tasarimci│   ( Çıktıyı sil, vazgeç )              ← yıkıcı belirteç   │
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Yıkıcı olanı nasıl ayırt ederim — beş ayrı katman, tek başına renge yaslanmadan:**
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci1. **Konum:** katlı alanın içinde; kart açıldığında ekranda değil, bir jest gerektiriyor.
 Danisma 001 — UI Tasarimci2. **Belirteç:** yıkıcı eylem rengi/kenarı (Theme'in tehlike belirteci) — ama tek işaret değil.
 Danisma 001 — UI Tasarimci3. **Sonucu yazıyla söyler:** "%3 kesinti" soyut; "4:12 → 4:05, sondan ~7 sn" somut. Kayıp, sayı olarak butonun üstünde durur.
 Danisma 001 — UI Tasarimci4. **Odak almaz:** Enter varsayılanı hep birincil öneridir; kırpma ve silme klavye varsayılanı olamaz.
 Danisma 001 — UI Tasarimci5. **İki adım:** "Kırp ve kaydet"e basınca buton yerinde onaya döner ("Emin misin? Kırp") — ayrı bir diyalog açılmaz, ama tek tıkla olmaz.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Ek karar:** kırpma önce **önizleme panelinde** gösterilsin — kesilecek aralık zaman şeridinde işaretlenir, kullanıcı "Kırp"a basmadan neyin gideceğini görür. Yıkıcı eylemin en iyi koruması onay kutusu değil, önizlemedir.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Metin uyarısı:** dört seçeneğin etiketleri Türkçe'de uzun; butonlar sabit genişlikte değil, içeriğe göre büyüyen ve gerekirse dikey yığılan bir düzende olsun.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci# 5. Önizleme paneli rozetleri
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Karar:** Üstte iki taraf etiketi köşelerde **sabit**; CRF rozeti işlenmiş tarafın etiketine bitişik, ikincil ağırlıkta; "Yaklaşık önizleme" metni tamamen kalkar.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci┌──────────────────────────────────────────────────────────┐
 Danisma 001 — UI Tasarimci│  ORİJİNAL                          İŞLENMİŞ · CRF 24     │
 Danisma 001 — UI Tasarimci│                          ┃                               │
 Danisma 001 — UI Tasarimci│                          ┃                               │
 Danisma 001 — UI Tasarimci│         (orijinal)      ═╋═      (işlenmiş)              │
 Danisma 001 — UI Tasarimci│                          ┃                               │
 Danisma 001 — UI Tasarimci│                          ┃                               │
 Danisma 001 — UI Tasarimci│                                                 %100     │
 Danisma 001 — UI Tasarimci└──────────────────────────────────────────────────────────┘
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Hizalama:** sol etiket panelin üst-sol köşesine, sağ etiket üst-sağ köşesine yaslanır. Kullanıcının "orta panelin sağında ve solunda, üstte" isteği bununla karşılanıyor — perdenin solundaki her şey orijinal, sağındaki her şey işlenmiş.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Perde hareket ederken sabit mi, kayar mı — sabit.** Gerekçe: sürükleme sırasında kayan metin okunmaz, perde uca yaklaşınca kırpılır ya da diğer etiketle çakışır; perdenin nerede olduğunu tutamacın kendisi zaten söylüyor. **Tek dinamik davranış:** perde bir tarafı tamamen kapattığında o tarafın etiketi soluklaşır — panelde görünmeyen şeyin etiketi tam ağırlıkta durmamalı.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Ağırlık sırası:**
 Danisma 001 — UI Tasarimci- **Taraf etiketleri** — birincil metin belirteci, orta ağırlık, büyük harf değil (Türkçe büyük harf İ/I sorunu ve okunurluk kaybı). Bunlar *ne baktığını* söyler, en güçlü olan bunlar.
 Danisma 001 — UI Tasarimci- **CRF rozeti** — ikincil metin belirteci, tek aralıklı (monospace) yüz, yalnız işlenmiş tarafta. Teknik bir olgudur, taraf adının kimliğini paylaşmaz; "İŞLENMİŞ" ile arasında orta nokta ayracı, kendi kutusu olmasın — iki kutu iki eşit rozet gibi görünür.
 Danisma 001 — UI Tasarimci- **Yakınlaştırma göstergesi** — üçüncül, alt köşede, tutamacın karşı tarafında; taban ölçekte gizli, değişince belirir.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Neden CRF sağda:** CRF yalnız çıktıyı tanımlıyor; ortada ya da solda durursa iki tarafa birden aitmiş gibi okunur.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Bilmediğim yer:** `LeftBadgeText` / `RightBadgeText` bugün hangi tarafa hangi metni koyuyor, olgularda yazmıyor. Ben **sol = orijinal, sağ = işlenmiş** öneriyorum (okuma yönü = önce/sonra); bugünkü eşleme tersse kod değil, kullanıcı beklentisi kazansın. Ayrıca CRF değerinin önizleme anında bilinip bilinmediğini (gerçek kodlama parametresi mi, tahmin mi) bilmiyorum — tahminse rozet bir tahmin işareti taşımalı, ama "Yaklaşık önizleme" cümlesi olarak değil.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci# 6. Sekme sırası
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Karar:** Oynatıcı en sola konur (kullanıcının isteği), Küçült varsayılan seçili kalır (kullanıcının isteği), **çelişki bir ayraç ve iki gruplama ile çözülür** — sıra değiştirilmez.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci┌──────────┬─┬─────────┬───────────┬─┬────────┬─────────┬──────────┐
 Danisma 001 — UI Tasarimci│ ▷ Oynat  │ │ Küçült  │ Dönüştür  │ │ Ayarlar│ Hakkında│ Gelişmiş │
 Danisma 001 — UI Tasarimci└──────────┴─┴═════════┴───────────┴─┴────────┴─────────┴──────────┘
 Danisma 001 — UI Tasarimci   araç      ↑  iş akışı              ↑   yardımcı
 Danisma 001 — UI Tasarimci           ayraç                    ayraç
 Danisma 001 — UI Tasarimci```
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Yanıltır mı — kısmen, ve yalnız ilk açılışta.** "En soldaki varsayılandır" beklentisi adım dizisi okunan şeritlerde güçlüdür. Ama bu şerit bir sihirbaz değil; kullanıcı "Oynat"ı bir *kip*, "Küçült/Dönüştür"ü bir *iş* olarak okursa çelişki kalkar. Onu okutmanın yolu:
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci1. **Ayraç:** Oynatıcı ile Küçült arasına ince bir dikey ayraç. Oynatıcı böylece "birinci adım" değil, "ayrı bir mod" olarak okunur.
 Danisma 001 — UI Tasarimci2. **İkonlu tek sekme:** yalnız Oynatıcı'da bir oynat ikonu bulunsun; diğerleri düz metin. Tek başına farklı görünen öğe, "sıradaki ilk" olarak sayılmaz.
 Danisma 001 — UI Tasarimci3. **Güçlü seçim işareti:** seçili sekmenin göstergesi (alt çizgi + metin ağırlığı) tereddüde yer bırakmayacak kadar belirgin olsun; kullanıcı hangi sekmede olduğunu sormasın diye.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI TasarimciÜçü birlikteyken kalan yanılma payı, elde edilen tutarlılığa değer: mp4 dosyasını VidShrink ile açınca Oynatıcı seçili gelecek — yani **"sana bir video verildiğinde olan şey" en solda** duruyor. Bu, sırayı kendi içinde anlamlı kılıyor; kullanıcının isteği aslında tutarlı bir modelin ifadesi.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Ayraç yetmezse plan B:** Oynatıcı'yı sekme şeridinden çıkar, şeridin solunda **ayrı bir mod düğmesi** yap (ikon + metin, sekme sırtı yok). O zaman "en soldaki sekme" tartışması biter — Küçült şeridin ilk sekmesi olur, Oynatıcı bir düğme. Yalnız bu, sekme klavye gezinmesini (Ctrl+Tab dizisi) ikiye böler; A planı bunu bozmadığı için önce onu denerim.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci**Gelişmiş sekmesi:** koşullu görünüyor — görünüp kaybolan bir sekme şeridi sıçratır. En sağda kalması doğru; görünürlüğü değiştiğinde diğer sekmelerin yeri kaymamalı.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci## Bilmediklerim (tahmin etmedim)
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci1. **Amaç**'ın motorda hedef+kip dışında hangi parametreleri sürüklediği.
 Danisma 001 — UI Tasarimci2. Önizlemedeki **sol/sağ rozetin bugünkü içeriği** ve CRF değerinin önizleme anında gerçek mi tahmini mi olduğu.
 Danisma 001 — UI Tasarimci3. **Gelişmiş sekmesi** ile Küçült sayfasındaki **Gelişmiş panel** aynı içerik mi, farklı mı — aynıysa biri kaldırılmalı, ama hangisi olduğunu olgular söylemiyor.
 Danisma 001 — UI Tasarimci4. `Theme.axaml` içindeki belirteç **adları** — yukarıda belirteçleri rolüyle andım ("yıkıcı eylem rengi", "ikincil metin"); karşılığı olmayan bir rol varsa yeni belirteç üretmek yerine sorulmalı.
 Danisma 001 — UI Tasarimci5. Yerleşim ölçülerini pimleyen testlerin **tam olarak neyi** pimlediği (sayfa boyu mu, tek tek denetim konumları mı) — bu, yukarıdaki değişikliklerin ölçüm maliyetini belirler.
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci---
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci## Senden istediklerim
 Danisma 001 — UI Tasarimci
 Danisma 001 — UI Tasarimci1. **Sol/sağ eşlemesini onayla.** Önizlemede `ORİJİNAL` solda, `İŞLENMİŞ · CRF x` sağda olsun mu — bugünkü kod tersse hangisi kazanır?
 Danisma 001 — UI Tasarimci2. **Amaç'ın kaldırılmasını onayla.** "Amaç ayrı denetim olmaktan çıksın, yonga şeridinin başına `( Arşiv ) ( Paylaşım )` ön ayarı olarak taşınsın" — kabul mü, yoksa Amaç motorda başka parametreleri de sürüklüyor mu?
 Danisma 001 — UI Tasarimci3. **Gelişmiş çakışmasını netleştir.** `main.tab.advanced` sekmesi ile `main.advanced.title` paneli aynı içerik mi? Aynıysa hangisi kalsın?
 Danisma 001 — UI Tasarimci4. **Sekme için A mı B mi.** A: ayraç + ikon (sıra aynı, `Oynat │ Küçült Dönüştür │ Ayarlar Hakkında Gelişmiş`). B: Oynatıcı şeritten çıkıp sol tarafta ayrı mod düğmesi olur.
 Danisma 001 — UI Tasarimci5. **Pim maliyetini kabul et.** Yerleşim değişince ölçüm kırmızıya döner; pimlerin yeniden temellendirilmesi bu işin parçası olarak planlansın mı?