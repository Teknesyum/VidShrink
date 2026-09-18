# P28 Altyazı İndirme — Mutasyon Dökümü

Her satır: mutasyon uygulandı, Release `-warnaserror` derlendi, dokunulan filtre koşuldu, ham çıktı alındı, mutasyon geri alındı.

```

===== M01 blok boyu 64KB->32KB [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/MovieHash.cs
public const int ChunkBytes = 64 * 1024;
  ->
public const int ChunkBytes = 32 * 1024;
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.BlokUclardanTamAltmisDortKibOkunur [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                          ↓ (pos 15)
Expected: "0000000000030003"
Actual:   "0000000000030000"
                          ↑ (pos 15)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.BlokUclardanTamAltmisDortKibOkunur() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 254
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M02 boy toplama girmiyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/MovieHash.cs
var sum = unchecked((ulong)length);
  ->
var sum = 0UL;
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.BlokUclardanTamAltmisDortKibOkunur [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                      ↓ (pos 11)
Expected: "0000000000030003"
Actual:   "0000000000000003"
                      ↑ (pos 11)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.BlokUclardanTamAltmisDortKibOkunur() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 254
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IkiUcBlokIcerigeGoreToplanir [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                      ↓ (pos 11)
Expected: "0000000000046000"
Actual:   "0000000000006000"
                      ↑ (pos 11)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.IkiUcBlokIcerigeGoreToplanir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 174
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.SifirDolusuDosyaninHashiBoyutunKendisi [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                      ↓ (pos 11)
Expected: "0000000000040000"
Actual:   "0000000000000000"
                      ↑ (pos 11)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.SifirDolusuDosyaninHashiBoyutunKendisi() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 145
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.ToplamaAltmisDortBitteSarilir [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
           ↓ (pos 0)
Expected: "000000000001ffff"
Actual:   "ffffffffffffffff"
           ↑ (pos 0)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.ToplamaAltmisDortBitteSarilir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 224
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     4, Başarılı:    45, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M03 kucuk dosya esigi kalkti [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/MovieHash.cs
if (length < MinimumBytes) return null;
  ->
if (length < 0) return null;
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.KucukDosyaHashUretmez [1 ms]
  Hata İletisi:
   Assert.Null() Failure: Value is not null
Expected: null
Actual:   "000000000001ffff"
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.KucukDosyaHashUretmez() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 268
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M04 son blok yerine ilk blok iki kez [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/MovieHash.cs
stream.Seek(length - ChunkBytes, SeekOrigin.Begin);
  ->
stream.Seek(0, SeekOrigin.Begin);
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.BlokUclardanTamAltmisDortKibOkunur [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                          ↓ (pos 15)
Expected: "0000000000030003"
Actual:   "0000000000030002"
                          ↑ (pos 15)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.BlokUclardanTamAltmisDortKibOkunur() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 254
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IkiUcBlokIcerigeGoreToplanir [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                       ↓ (pos 12)
Expected: "0000000000046000"
Actual:   "0000000000044000"
                       ↑ (pos 12)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.IkiUcBlokIcerigeGoreToplanir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 174
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.ToplamaAltmisDortBitteSarilir [< 1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                          ↓ (pos 15)
Expected: "000000000001ffff"
Actual:   "000000000001fffe"
                          ↑ (pos 15)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.ToplamaAltmisDortBitteSarilir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 224
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     3, Başarılı:    46, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M05 dil parametresi gonderilmiyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
return codes.Count == 0 ? "" : "languages=" + string.Join(",", codes) + "&";
  ->
return "";
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.AramaIstegiHashVeDiliTasir [1 ms]
  Hata İletisi:
   Assert.Contains() Failure: Sub-string not found
String:    "https://api.opensubtitles.com/api/v1/subt"···
Not found: "languages=en,tr"
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.AramaIstegiHashVeDiliTasir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 302
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M06 hash aramasi atlaniyor, hep adla [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
"moviehash=" + query.MovieHash.ToLowerInvariant()
  ->
"query=" + Escape(query.Name)
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.AramaIstegiHashVeDiliTasir [2 ms]
  Hata İletisi:
   Assert.Contains() Failure: Sub-string not found
String:    "https://api.opensubtitles.com/api/v1/subt"···
Not found: "moviehash=abc123def4567890"
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.AramaIstegiHashVeDiliTasir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 301
--- End of stack trace from previous location ---
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.HashTutmazsaAdlaAranir [< 1 ms]
  Hata İletisi:
   Assert.Contains() Failure: Sub-string not found
String:    "https://api.opensubtitles.com/api/v1/subt"···
Not found: "moviehash="
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.HashTutmazsaAdlaAranir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 342
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     2, Başarılı:    47, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M07 bosluk %20 olarak kodlaniyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
.Replace("%20", "+", StringComparison.Ordinal);
  ->
;
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.HashTutmazsaAdlaAranir [1 ms]
  Hata İletisi:
   Assert.Contains() Failure: Sub-string not found
String:    "https://api.opensubtitles.com/api/v1/subt"···
Not found: "query=film+adi"
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.HashTutmazsaAdlaAranir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 344
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M08 hash eslesmesi siralamada onde degil [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
.OrderByDescending(candidate => candidate.HashMatch)
  ->
.OrderByDescending(candidate => false)
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.SiralamaHashiSonraYeglenenDiliSonraIndirmeSayisiniAlir [11 ms]
  Hata İletisi:
   Assert.Equal() Failure: Collections differ
                      ↓ (pos 0)
Expected: long[]     [4, 3, 2, 1]
Actual:   List<long> [3, 2, 1, 4]
                      ↑ (pos 0)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.SiralamaHashiSonraYeglenenDiliSonraIndirmeSayisiniAlir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 366
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M09 yanindaki dosya eziliyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
while (File.Exists(candidate))
  ->
while (File.Exists(candidate) && counter < 0)
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.VarOlanAltyaziEzilmez [2 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                                                     ↓ (pos 106)
Expected: ···"isma\\p28-altyazi\\indir-ezme\\Film.tr.2.srt"
Actual:   ···"alisma\\p28-altyazi\\indir-ezme\\Film.tr.srt"
                                                       ↑ (pos 106)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.VarOlanAltyaziEzilmez() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 410
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M10 401 hep anahtar reddi sayiliyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
return SubtitleOutcome.NeedAccount;
  ->
return trouble;
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IndirmedeKotaGovdeliDortYuzBirKotaSayilir [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Values differ
Expected: NeedAccount
Actual:   BadKey
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.IndirmedeKotaGovdeliDortYuzBirKotaSayilir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 480
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M11 imzali baglantiya da anahtar gidiyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
            fetch.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
  ->
            fetch.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            fetch.Headers.TryAddWithoutValidation("Api-Key", _apiKey);
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.ImzaliBaglantiyaAnahtarGonderilmez [3 ms]
  Hata İletisi:
   Assert.DoesNotContain() Failure: Sub-string found
                                      ↓ (pos 29)
String: ···"r-Agent=VidShrink,v15.0.0;Api-Key=ANAHTAR"
Found:  "Api-Key"
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.ImzaliBaglantiyaAnahtarGonderilmez() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 503
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    48, Atlanan:     0, Toplam:    49, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M12 kimlik paylasim bicimiyle gidiyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
internal static string UserAgent { get; } = ShareIdentity.ProductName + " v" + Version();
  ->
internal static string UserAgent { get; } = ShareIdentity.UserAgent;
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.KimlikBasligiSaglayicininIstedigiBicimde [1 ms]
  Hata İletisi:
   Assert.Matches() Failure: Pattern not found in value
Regex: "^VidShrink v\\d+\\.\\d+\\.\\d+$"
Value: "VidShrink/15.0.0 (+https://github.com/Teknesyum/Vi"···
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.KimlikBasligiSaglayicininIstedigiBicimde() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 323
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M13 429 gunluk kotaya kaydiriliyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
HttpStatusCode.TooManyRequests => SubtitleOutcome.RateLimited,
  ->
HttpStatusCode.TooManyRequests => SubtitleOutcome.QuotaExceeded,
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.SinirKodlariAyriKollaraDuser(kod: TooManyRequests, beklenen: RateLimited) [< 1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Values differ
Expected: RateLimited
Actual:   QuotaExceeded
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.SinirKodlariAyriKollaraDuser(HttpStatusCode kod, SubtitleOutcome beklenen) in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 457
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M14 tum hata kollari tek bildirime dusuyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Subtitles.cs
SubtitleOutcome.NoResult => "player.subtitle.download.noresult",
  ->
SubtitleOutcome.NoResult => "player.subtitle.download.offline",
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(anahtar: "player.subtitle.download.noresult") [10 ms]
  Hata İletisi:
   player.subtitle.download.noresult uretim kaynaginda hic okunmuyor
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(String anahtar) in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 947
   at InvokeStub_AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(Object, Span`1)
   at System.Reflection.MethodBaseInvoker.InvokeWithOneArg(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.HerHataKoluAyriBildirimAnahtarinaDuser [2 ms]
  Hata İletisi:
   Assert.Equal() Failure: Values differ
Expected: 8
Actual:   7
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.HerHataKoluAyriBildirimAnahtarinaDuser() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 612
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.SaglayiciKollariOynaticidaAyriBildirimUretir [269 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                                    ↓ (pos 25)
Expected: "player.subtitle.download.noresult"
Actual:   "player.subtitle.download.offline"
                                    ↑ (pos 25)
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.SaglayiciKollariOynaticidaAyriBildirimUretir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 659
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     3, Başarılı:    48, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M15 bildirim anahtari kaynakta okunmuyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Subtitles.cs
SubtitleOutcome.NeedAccount => "player.subtitle.download.needaccount",
  ->
SubtitleOutcome.NeedAccount => "player.subtitle.download.badkey",
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(anahtar: "player.subtitle.download.needaccount") [12 ms]
  Hata İletisi:
   player.subtitle.download.needaccount uretim kaynaginda hic okunmuyor
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(String anahtar) in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 947
   at InvokeStub_AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(Object, Span`1)
   at System.Reflection.MethodBaseInvoker.InvokeWithOneArg(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.HerHataKoluAyriBildirimAnahtarinaDuser [2 ms]
  Hata İletisi:
   Assert.Equal() Failure: Values differ
Expected: 8
Actual:   7
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.HerHataKoluAyriBildirimAnahtarinaDuser() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 612
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     2, Başarılı:    49, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M16 anahtarsizken de indirme satiri ciziliyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Tracks.cs
items.Add(SubtitleDownloadReady
  ->
items.Add(true
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.AnahtarYokkenMenuAnahtarAlSatiriniGosterir [781 ms]
  Hata İletisi:
   Assert.Contains() Failure: Item not found in collection
Collection: ["Off", "Load subtitle file…", "Download subtitle…", "Next subtitle", "Subtitle delay +0,5 s", ···]
Not found:  "Download subtitle — get an API key…"
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.AnahtarYokkenMenuAnahtarAlSatiriniGosterir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 694
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M17 anahtar ayara yazilmiyor [KIRMIZI] =====
dosya: src/VidShrink.App/AppSettings.cs
root["openSubtitlesApiKey"] = OpenSubtitlesApiKey;
  ->
root["openSubtitlesApiKey"] = "";
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.AnahtarAyarDosyasinaYazilipGeriOkunur [3 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
           ↓ (pos 0)
Expected: "kullanici-anahtari-123"
Actual:   ""
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.AnahtarAyarDosyasinaYazilipGeriOkunur() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 824
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M18 anahtar ayardan geri okunmuyor [KIRMIZI] =====
dosya: src/VidShrink.App/AppSettings.cs
ReadString(root, "openSubtitlesApiKey", value => settings.OpenSubtitlesApiKey = value);
  ->
ReadString(root, "openSubtitlesApiKey", value => { });
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.AnahtarAyarDosyasinaYazilipGeriOkunur [3 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
           ↓ (pos 0)
Expected: "kullanici-anahtari-123"
Actual:   ""
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.AnahtarAyarDosyasinaYazilipGeriOkunur() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 824
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M19 406 istek sinirina kaydiriliyor [KIRMIZI] =====
dosya: src/VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs
HttpStatusCode.NotAcceptable => SubtitleOutcome.QuotaExceeded,
  ->
HttpStatusCode.NotAcceptable => SubtitleOutcome.RateLimited,
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.SinirKodlariAyriKollaraDuser(kod: NotAcceptable, beklenen: QuotaExceeded) [2 ms]
  Hata İletisi:
   Assert.Equal() Failure: Values differ
Expected: QuotaExceeded
Actual:   RateLimited
  Yığın İzleme:
     at VidShrink.Tests.AltyaziIndirmeTests.SinirKodlariAyriKollaraDuser(HttpStatusCode kod, SubtitleOutcome beklenen) in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-p28\tests\VidShrink.Tests\AltyaziIndirmeTests.cs:line 457
--- End of stack trace from previous location ---
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== OZET =====
M01 blok boyu 64KB->32KB                          KIRMIZI          1
M02 boy toplama girmiyor                          KIRMIZI          4
M03 kucuk dosya esigi kalkti                      KIRMIZI          1
M04 son blok yerine ilk blok iki kez              KIRMIZI          3
M05 dil parametresi gonderilmiyor                 KIRMIZI          1
M06 hash aramasi atlaniyor, hep adla              KIRMIZI          2
M07 bosluk %20 olarak kodlaniyor                  KIRMIZI          1
M08 hash eslesmesi siralamada onde degil          KIRMIZI          1
M09 yanindaki dosya eziliyor                      KIRMIZI          1
M10 401 hep anahtar reddi sayiliyor               KIRMIZI          1
M11 imzali baglantiya da anahtar gidiyor          KIRMIZI          1
M12 kimlik paylasim bicimiyle gidiyor             KIRMIZI          1
M13 429 gunluk kotaya kaydiriliyor                KIRMIZI          1
M14 tum hata kollari tek bildirime dusuyor        KIRMIZI          3
M15 bildirim anahtari kaynakta okunmuyor          KIRMIZI          2
M16 anahtarsizken de indirme satiri ciziliyor     KIRMIZI          1
M17 anahtar ayara yazilmiyor                      KIRMIZI          1
M18 anahtar ayardan geri okunmuyor                KIRMIZI          1
M19 406 istek sinirina kaydiriliyor               KIRMIZI          1

===== M20 yandaki altyazi yuklenemedi metni kaynakta okunmuyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Tracks.cs
_trackNotice = added ? null : "player.subtitle.loadfailed";
  ->
_trackNotice = added ? null : "player.subtitle.download.offline";
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(anahtar: "player.subtitle.loadfailed") [12 ms]
   player.subtitle.loadfailed uretim kaynaginda hic okunmuyor
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 3 s - VidShrink.Tests.dll (net8.0)

===== M21 kisayol ipucu metni arayuzden dusuyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerShortcutsPanel.axaml
Text="{loc:Text settings.player-shortcuts.hint}"
  ->
Text="{loc:Text settings.player-shortcuts.title}"
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(anahtar: "settings.player-shortcuts.hint") [23 ms]
   settings.player-shortcuts.hint uretim kaynaginda hic okunmuyor
Başarısız! - Başarısız:     1, Başarılı:    50, Atlanan:     0, Toplam:    51, Süre: 3 s - VidShrink.Tests.dll (net8.0)

===== M22 indirme satiri menuye geri konuyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Tracks.cs
items.Add(Plain(Strings.Get("player.subtitle.load"), () => _ = PickSubtitleAsync()));
  ->
items.Add(Plain(Strings.Get("player.subtitle.load"), () => _ = PickSubtitleAsync()));
        items.Add(SubtitleDownload
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IndirmeSatiriBuSurumdeMenudeYok [281 ms]
   Assert.DoesNotContain() Failure: Item found in collection
Collection: ["Off", "Load subtitle file…", "Download subtitle — get an API key…", "Next subtitle", "Subtitle delay +0,5 s", ···]
Found:      "Download subtitle — get an API key…"
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.MenuMetinleriPYirmiDokuzaBekliyor [83 ms]
   Assert.Empty() Failure: Collection was not empty
Collection: ["player.subtitle.download", "player.subtitle.download.getkey"]
Başarısız! - Başarısız:     2, Başarılı:    48, Atlanan:     0, Toplam:    50, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M23 menu hic cizilmiyor (bos liste) [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Tracks.cs
items.Add(Plain(Strings.Get("player.subtitle.load"), () => _ = PickSubtitleAsync()));
  ->
(satir silindi)
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IndirmeSatiriBuSurumdeMenudeYok [260 ms]
   Assert.DoesNotContain() Failure: Item found in collection
Collection: ["Off", "Download subtitle — get an API key…", "Next subtitle", "Subtitle delay +0,5 s", "Subtitle delay −0,5 s", ···]
Found:      "Download subtitle — get an API key…"
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.MenuMetinleriPYirmiDokuzaBekliyor [94 ms]
   Assert.Empty() Failure: Collection was not empty
Collection: ["player.subtitle.download", "player.subtitle.download.getkey"]
Başarısız! - Başarısız:     2, Başarılı:    48, Atlanan:     0, Toplam:    50, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M23 menunun altyazi yukle satiri silindi [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Tracks.cs
(satir silindi)
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IndirmeSatiriBuSurumdeMenudeYok [246 ms]
   Assert.DoesNotContain() Failure: Item found in collection
Collection: ["Off", "Download subtitle — get an API key…", "Next subtitle", "Subtitle delay +0,5 s", "Subtitle delay −0,5 s", ···]
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.MenuMetinleriPYirmiDokuzaBekliyor [87 ms]
   Assert.Empty() Failure: Collection was not empty
Collection: ["player.subtitle.download", "player.subtitle.download.getkey"]
Başarısız! - Başarısız:     2, Başarılı:    48, Atlanan:     0, Toplam:    50, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M23 menunun "altyazi dosyasi yukle" satiri siliniyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Tracks.cs
-        items.Add(Plain(Strings.Get("player.subtitle.load"), ...));
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IndirmeSatiriBuSurumdeMenudeYok [289 ms]
   Assert.Contains() Failure: Item not found in collection
Collection: ["Off", "Next subtitle", "Subtitle delay +0,5 s", "Subtitle delay −0,5 s", "Reset subtitle delay", ···]
Not found:  "Load subtitle file…"
Başarısız! - Başarısız:     1, Başarılı:    49, Atlanan:     0, Toplam:    50, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M22 indirme satiri menuye geri konuyor [KIRMIZI] =====
dosya: src/VidShrink.App/Playback/PlayerView.Tracks.cs
----- ham cikti -----
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.IndirmeSatiriBuSurumdeMenudeYok [256 ms]
   Assert.DoesNotContain() Failure: Item found in collection
Collection: ["Off", "Load subtitle file…", "Download subtitle — get an API key…", "Next subtitle", "Subtitle delay +0,5 s", ···]
Found:      "Download subtitle — get an API key…"
  Başarısız VidShrink.Tests.AltyaziIndirmeTests.MenuMetinleriPYirmiDokuzaBekliyor [78 ms]
   Assert.Empty() Failure: Collection was not empty
Collection: ["player.subtitle.download", "player.subtitle.download.getkey"]
Başarısız! - Başarısız:     2, Başarılı:    48, Atlanan:     0, Toplam:    50, Süre: 2 s - VidShrink.Tests.dll (net8.0)

===== M24 yonerge ayiklamasi kalkiyor (ad alani uye okumasi sayiliyor) [KIRMIZI] =====
dosya: tests/VidShrink.Tests/OluUyeTests.cs
----- ham cikti -----
  Başarısız VidShrink.Tests.OluUyeTests.OluOzellikYuzeyiPimlenenKume [2 s]
   Assert.Equal() Failure: Collections differ
Expected: [···, "StrategyAdvice.SuggestedPreference  yalniz-disarid"···, "StreamPlan.Attachments  hic-gorunmeyen", "StreamPlan.Subtitles  yalniz-disarida", "SubtitleTrack.Image  hic-gorunmeyen", "ThresholdRule.Slope  hic-gorunmeyen", ···]
Actual:   [···, "StrategyAdvice.SuggestedPreference  yalniz-disarid"···, "StreamPlan.Attachments  hic-gorunmeyen", "SubtitleTrack.Image  hic-gorunmeyen", "ThresholdRule.Slope  hic-gorunmeyen", "TimeEstimate.ExpectedSeconds  yalniz-disarida", ···]
Başarısız! - Başarısız:     1, Başarılı:    14, Atlanan:     0, Toplam:    15, Süre: 6 s - VidShrink.Tests.dll (net8.0)
```
