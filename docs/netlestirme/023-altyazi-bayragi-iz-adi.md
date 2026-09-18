# 023 — Altyazı Varsayılan Bayrağı ve İz Adı: Fable'ın Yanıtı

Girdi: [023-altyazi-bayragi-iz-adi-girdi.md](023-altyazi-bayragi-iz-adi-girdi.md).
Ölçüm: [../olcumler/e6-altyazi-bayragi-iz-adi.md](../olcumler/e6-altyazi-bayragi-iz-adi.md).
Yanıt birebir:

---

**1. Düzeltilmeli; iz adı kaynağınkiyle aynen taşınsın.** Kaynağın bilgisini kayıpsız taşımak küçültme kolunun zaten savunduğu ilkedir; MKV'de korunup MP4'te düşmesi kullanıcıya açıklanamayan bir tutarsızlık. Oynatıcı azlığı gerekçe değil: mp4 `hdlr`/`udta` başlığını VLC, mpv, Plex ve Apple oynatıcıları okur. Ses için `-metadata:s:a:0 title=`, altyazı için `-metadata:s:s:N title=` — kaynak başlık boşsa hiç yazma, uydurma başlık üretme.

**2. Düzeltilmeli; kural "kaynağın `IsDefault`'u izlenir, hiçbir bilgi yoksa altyazı varsayılan olmaz" olsun.** MKV çıktısı zaten bu davranışı veriyor, MP4 kararın kendisi değil muxer'ın boşluğu; `-disposition:s:N 0` (ya da kaynak default ise `default`) ile aynı satıra çekilir. "Forced olan tek altyazı varsayılan olsun" kuralı forced'ın anlamına ters: forced iz oynatıcı tarafından zaten dil eşleşince otomatik gösterilir, `default` üstüne binince kullanıcı kapatamaz. `forced` bayrağını `-disposition:s:N forced` olarak ayrıca taşı; bugün korunması muxer'a bağlı, açık yazılınca güvenceye alınır.

**3. Yüzey açılmasın; karar kaynaktan türesin.** İki soruda da kaynak yeterli bilgiyi taşıyor (title, default, forced) ve doğru davranış "aynen taşı"; kullanıcıya sorulacak gerçek bir tercih yok. Kutu açmak, ses izi dil yeğlemesiyle seçilirken altyazı için ayrı bir karar yüzeyi demek — kolun sadeliğini bozar. Bir gün "MP4'te varsayılan altyazıyı X yap" isteği gelirse o zaman düşünülür; bugünkü ölçüm bunu göstermiyor.
