# AOT Dalgası — fable Yanıtı (birebir)

Soru: `docs/danisma/2026-09-18-aot-dalgasi-soru-fable.md`. Tarih: 2026-09-18.
Aşağısı fable'ın yanıtının birebir metnidir; kısaltılmadı, düzeltilmedi.

---

Gerekli bağlamın tamamı elimde. Doğruladıklarım: `Load(object)`'in çalışma anı gövdesi yalnızca fırlatıyor (derleyici araya giriyor), IL2026 yalnız Uri'li `Load` üstünde; `!AvaloniaResources` App.dll'de `Build:/Themes/Palette/...` metotlarıyla var ve üzerinde hiçbir trim koruması yok; libmpv köprüsü `delegate* unmanaged` + `UnmanagedCallersOnly` ile AOT'a hazır; kalkan `RegOverridePredefKey` ile HKCU'yu süreç içinde yönlendiriyor; başlatıcı ayrı bir .NET süreci ve uygulama süreci doğmadan 70 ms harcıyor. Cevap aşağıda.

---

# AOT Dalgası — fable Cevabı

Önce ölçüm-tahmin ayrımı: aşağıda "doğruladım" dediklerim derlemeden ya da koddan okundu; "tahmin" dediklerim sayı değil, sıralama için gerekçe. Ölçüm gibi sunulacak tek bir sayı bile yok; ölçüm düzeneği sizde.

## 1. 145,2 ms'lik blok ve 200 ms hedefi

**Bloğun içinde ne var.** `libmpv-hazir → app-init`, `AppBuilder.Setup` + `StartWithClassicDesktopLifetime`'ın `App.Initialize`'a kadar olan kısmı. Dört ayrı maliyet sınıfı üst üste:

1. **Yerel kütüphane yüklemesi**: libSkiaSharp, libHarfBuzzSharp, ANGLE'ın libEGL/libGLESv2'si, D3D11/DXGI. Onlarca MB'lık yerel imaj, sayfa haritalama + yeniden yerleştirme. AOT'tan **etkilenmez**; disk önbelleği ve OS yükleyicisi belirler.
2. **GPU aygıt kurulumu**: DXGI fabrika + D3D11 aygıt + EGL display/context. Sürücü kaynaklı; AOT'tan **etkilenmez**. H'nin "yazılım kare yolu" notu buna işaret ediyor.
3. **Tip yükleme ve metadata**: Avalonia.Base/Controls/Skia/Win32 + Fluent tema — statik yapıcılar, jenerik örnekleme, R2R sabitleme hücrelerinin ilk çözümü. R2R'de kod var ama tip sistemi çalışma anında kuruluyor. AOT bunu **büyük ölçüde** öne çeker (tipler imaja gömülü).
4. **Kalan JIT**: R2R'nin kapsamadığı jenerikler, `TieredPGO` için enstrümanlı yeniden derlemeler, dinamik yollar. AOT **sıfırlar**.

**Ne kadarı gider — tahmin, ölçüm değil.** Sınıf 1+2 bu makinede kaç ms, bilmiyorum; masaüstü sürücülerinde D3D11 aygıt + EGL için tipik 20–60 ms, yerel imaj yüklemesi sıcak kipte 10–30 ms aralığını anımsıyorum ama bunlar başka makinelerin sayıları. Kabaca söylemem gerekirse bloğun **yarıdan azı** AOT'a duyarlı; 70–100 ms'lik bir taban donanım/OS kalır. Bu "kabaca"yı veriye çevirmenin iki ucuz yolu var ve **ikisi de AOT derlemesi istemiyor**:

- `app-init` probuna `System.Runtime.JitInfo.GetCompilationTime()` ve `GetCompiledMethodCount()` yazdırın. Bu, 145 ms'in içindeki JIT payını süreç içinden, doğrudan verir. Bu sayı 15 ms ise AOT'un bu bloktaki tavanı (JIT + tip yükleme) küçük; 60 ms ise büyük. Tek satırlık prob, kalkan aynı, düzenek aynı.
- Aynı düzenekte `Win32RenderingMode.Software` (ya da ortam değişkeni `AVALONIA_...` yoksa `AppBuilder.With(new Win32PlatformOptions{RenderingMode=[Software]})`) ile bir çift koşumu: bloktan düşen miktar **GPU/sürücü payının** alt sınırı. Bu eşli fark AOT'un asla dokunamayacağı kısmı söyler.

**200 ms `ilk-kare` için savunulabilir mi: hayır.** Tabloya bakın: `cerceve → ilk-kare` 226 ms ve içinde AOT'un dokunamadığı kalemler var: Win32 pencere oluşturma + gösterme (`pencere-kuruldu → pencere-yuklendi` 39 ms), libmpv çekirdek + dosya açma (`motor-acildi` 35 ms), ilk kare çözme (17 ms). Bunlara sınıf 1+2'yi ekleyince yalnız donanım/OS tabanı **100–150 ms** ediyor (tahmin). 200 hedefi geriye yönetilen kod için 50–100 ms bırakır: çalışma zamanı + Avalonia + App.axaml + MainWindow XAML (`pencere-yapici → xaml` bugün tek başına 84 ms) hep beraber bu aralığa sığmaz; AOT XAML kurulumunu hızlandırır ama sıfırlamaz (nesne inşası, biçem eşleme, özellik sistemi çalışma anında koşar).

Savunulabilir hedefler:
- **`main`'e kadar**: evet. Bugün 124 ms ve 70'i uygulama süreci doğmadan geçiyor (`iz:baslatici` 61 + süreç doğumu). Başlatıcı Avalonia'sız, JSON'suz basit bir .NET süreci; **yalnız başlatıcıyı NativeAOT yapmak** düşük riskli, kalkan sorunu yok (kalkan uygulama sürecinde çalışır, ortam değişkeni çocuğa miras kalır; başlatıcı HKCU'ya yazmıyorsa — koda göre yazmıyor ama `Updater`/`UygulamaKlasoruKapisi`'ni siz doğrulayın). `main`'i 124'ten 50–60'a çekmesi beklenir; ölçüsü mevcut düzenekle alınır.
- **`cerceve`** için 150–200 civarı: AOT ile olası, ölçmeden söylenmez.
- **`ilk-kare`** için gerçekçi teorik sınır bu makinede **300–350 ms** (tahmin). H'deki 200, "AOT + yazılım kare yolu"nu birlikte varsayıyor ve yazılım kare yolunun ilk boyayı yavaşlattığını hesaba katmıyor.

Bir de düzenek notu: gürültü tabanı ±60 ms, 10 çiftte ~30 ms altı etkiler görünmez. AOT'un `ilk-kare` etkisi 100+ ms beklenir, görünür; başlatıcı AOT'unun `main` etkisi de görünür.

## 2. Anahtarlar: risk/kazanç sırası

Aşağıdan yukarı: önce düşük risk yüksek kazanç.

**1. `System.Globalization.UseNls=true` (yalnız Windows) — dene.** (b)'deki -24,9 ms'lik `app-init` kazancının kaynağı büyük olasılıkla `icu.dll`'in ilk kültür dokunuşunda yüklenmesi (Windows sistem ICU'su, birkaç MB). NLS kipi ICU'yu hiç yüklemez, kültür verisini kernel32'nin bellekte duran yerel tablolarından okur: `CurrentUICulture.Name` doğru döner, `ToUpper(tr)` `i→İ` yapar, 42 dilin sayı/tarih biçimi kalır. Risk: sıralama/karşılaştırma NLS ile ICU arasında ince farklar taşır (kültürel `string.Compare` ile sıralanan listeleriniz varsa sıra değişebilir), bazı normalizasyon davranışları farklı. Kazanç ölçülmedi — hipotez; runtimeconfig anahtarı, ölçüm düzeneği değişmiyor, bir çift koşumda cevabı alırsınız. macOS/Linux'a dokunmaz.

**2. `DOTNET_TieredPGO=0` ile bir çift koşum.** Sıfır kod, ortam değişkeni. TieredPGO açılışta sıcak metotları enstrümanlı katmandan geçirir; R2R kodu için etkisi küçük olmalı ama ölçülmemiş. Ürün kararı değil, teşhis: fark çıkmazsa kapatın konuyu.

**3. `PublishReadyToRunComposite` — açık kalsın.** Zaten ölçüldü. Trim ile birlikte çalışır (.NET 8 önce keser, sonra kompozit imajı kurar).

**4. `PublishTrimmed` partial — açılış için beklentiyi düşürün.** Kesme kodu hızlandırmaz; kazancı diskteki boyut ve daha küçük R2R imajı (daha az sayfa hatası). 235→229 dosya, 366→318 MB: açılışa yansıması olsa 0–20 ms bandında, gürültünün altında. Riski: çerçeve ve Avalonia içindeki (IsTrimmable) yansıma yollarının sessizce kopması; sizin derlemeleriniz partial'da bütün kalır. Bu yüzden `PaletteCatalog`'daki iki IL2026 **partial altında yanlış alarm**: `!AvaloniaResources` App.dll'de duruyor, App.dll kesilmiyor. Uyarı doğru, kopma yok. Trim'i açılış için değil, **AOT'a giden yolu temizlemek** için görün.

**5. `TrimmerRootAssembly` — araç değil yara bandı.** Bir derlemenin tamamını kökler; partial'da sizinkiler zaten köklü, Avalonia derlemelerine uygulamak kesmeyi iptal eder. Meşru tek kullanımı: **ölçüm yayınında** `Microsoft.Win32.Registry`'yi köklemek (bkz. 4. soru). Full trim'de `!AvaloniaResources` için `TrimmerRootDescriptor` tip düzeyinde daha doğru olurdu; ama 3. sorudaki dönüşüm bu ihtiyacı tümden kaldırır.

**6. Full trim.** Partial'a göre açılış kazancı yok, yalnız boyut; risk artar (sizin derlemeleriniz de kesilir). Yalnız NativeAOT öncülü olarak anlamlı; AOT zaten full trim'dir.

**7. `InvariantGlobalization` — hayır.** -43,5 ms 7/10 gerçek ama bedeli yazdığınız ikiden büyük: invariant kipte 42 dilin **sayı ve tarih biçimi** de gider ("1,5 MB" her yerde "1.5 MB" olur). `PredefinedCulturesOnly` çözüm değil: kültür verisini geri getirmez, yalnız invariant kipte hangi kültür adlarının kabul edileceğini kısıtlar. "Kültür verisini kendi tablomuza taşımak" 42 dil × (büyük harf tablosu + sayı/tarih kalıpları) demek; NLS varken bunun gerekçesi yok. Kültür dokunuşunu ilk kareden sonraya ertelemek (dil algısını `GetUserPreferredUILanguages` P/Invoke ile yapmak, Setup'ta zaten var) ICU yükünü yalnız öteler ve 44 çağrı noktasından biri ilk kareden önce koşarsa geri gelir; kırılgan.

**8. NativeAOT — en yüksek kazanç, en yüksek risk; 3. soruda.**

## 3. NativeAOT mümkün mü; `Load(object)` ve 26 palet

**`AvaloniaXamlLoader.Load(object)` AOT'da yansıma istemiyor — doğruladım.** Avalonia 12.1.2'nin `Avalonia.Markup.Xaml.dll`'inde:
- `Load(Uri, Uri)` ve `Load(IServiceProvider, Uri, Uri)` `RequiresUnreferencedCode` taşıyor ("dynamically loads referenced assembly with Avalonia resources"). IL2026'nızın kaynağı bu; `ResourceInclude.Loaded` bunu çağırıyor.
- `Load(object)` ve `Load(IServiceProvider, object)` **hiç öznitelik taşımıyor** ve çalışma anı gövdesi yalnızca "No precompiled XAML found for …, make sure to specify x:Class" fırlatıyor. Yani bu çağrı çalışma anında hiç koşmaz: XAML derleyicisi `x:Class`'lı her sınıfta çağrıyı üretilmiş `!XamlIlPopulateTrampoline`'a çevirir (App.dll'inizde bu trambolin var, doğruladım). Yansıma yok, uyarı yok, AOT'a uygun.
- Uri'li yol ise `CompiledAvaloniaXaml.!AvaloniaResources` tipindeki `Build:/Themes/Palette/Neon/Theme.axaml` metotlarını **ada göre** buluyor; App.dll'de bu tip ve metotlar var, üzerinde `DynamicDependency`/ILLink koruması **yok**. Partial'da dert değil (derleme bütün kalıyor); full trim/AOT'ta bu metotlar kesilebilir — bu durumda `ResourceInclude` derleme anında değil çalışma anında patlar.

**26 paletlik dönüşüm engeli kaldırır — ama 26×2 dosya gerekmez.** Her `Theme.axaml`'e bir öznitelik (`x:Class="VidShrink.App.Themes.Palette.Neon"`), artı **tek bir** `Palettes.cs` içinde 26 üç satırlık `partial class X : ResourceDictionary { public X() { AvaloniaXamlLoader.Load(this); } }`, artı `PaletteCatalog.Load(name)`'in 26 kollu bir `switch`'e (ya da `Dictionary<string, Func<ResourceDictionary>>`'ye) dönmesi. Satır 102'deki `merged[0] = new ResourceInclude(...)` yerine `merged[0] = incoming` yazılır; `Keys()` zaten `IResourceDictionary` kolunu tanıyor. Sonuç: `PaletteCatalog`'daki iki IL2026 gider, `avares://` çalışma anı araması biter, `PaletteTests`'in "liste klasörle aynı" ölçüsü aynen kalır. `IsLight` için bir sözlük kurup tek renk okumak yerine tip başına statik özellik daha temiz ama AOT için gerekli değil.

Not: `App.axaml`'deki beş `ResourceInclude` uyarı vermiyor; Avalonia'nın derleyicisi XAML içindeki sabit `Source`'ları derleme anında çözebiliyor (bunu derlemeden değil davranıştan çıkarıyorum — ölçtüğünüz 13,4 ms ve uyarı sayımı bununla tutarlı; kesin mekanizmayı doğrulamadım).

**Diğer engeller, sırayla:**
- **libmpv P/Invoke engel değil.** `DllImport` + `NativeLibrary.TryLoad` + `SetDllImportResolver` AOT'ta çalışır; geri çağrı `delegate* unmanaged[Cdecl]` ve `UnmanagedCallersOnly` ile yazılmış — tam olarak AOT'un istediği biçim. `Assembly.GetExecutingAssembly()` de sorunsuz. Dize parametreleri için `LibraryImport`'a geçmek gerekmez (ILC, `DllImport` sarmalayıcılarını derleme anında üretir); yalnız analizör önerisi (SYSLIB1054) çıkar.
- **`BuiltInComInteropSupport=true` — gerçek engel.** NativeAOT yerleşik COM birlikte çalışmasını desteklemez (yalnız `ComWrappers`). Bu bayrak ilk Avalonia commit'iyle gelmiş (`774b1871`), büyük olasılıkla şablondan. Avalonia 11+ Win32 arka ucu sürükle-bırak ve kabuk çağrılarında kendi MicroCom'unu kullanıyor; bayrağı kapatınca ne kırıldığını bir koşumda görün (sürükle-bırak, dosya iletişim kutuları). Kapanmıyorsa AOT yok.
- **`PresetLibrary` üç IL2026/IL3050.** `JsonStringEnumConverter(allowIntegerValues:false)` yerine .NET 8'in jenerik `JsonStringEnumConverter<TEnum>`'u; `allowIntegerValues:false` gerektiği için `sealed class Strict<T> : JsonStringEnumConverter<T> { public Strict() : base(null, false) {} }` türetip enum'a `[JsonConverter(typeof(Strict<...>))]` vermek yeterli. Açılış yolunda değil ama AOT derlemesi bunları uyarı değil hata sayar (IL3050).
- **Ortam**: `link.exe` PATH'te yok (H4). vcvars düzelmeden AOT bağlanmaz; CI'da ekleyip yerelde `Developer PowerShell`'den koşmak çözer.
- **Avalonia 12'nin AOT durumu**: 11.x resmi olarak trim ve NativeAOT destekliyordu; 12.1.2 için özel bir kırılma bilmiyorum, doğrulamadım.
- **Üç platform**: AOT her hedef OS'un kendi makinesinde derlenir; macOS/Linux yayını CI matrisinde ayrı iş olur. Windows'ta AOT açıp macOS'ta R2R bırakmak mümkün (RID koşullu), csproj'unuz zaten RID koşullu yapıya sahip.
- **Kalkan duvarı** — 4. soru.

Toplam hüküm: **mümkün**, engel listesi kısa ve hepsi mekanik; en belirsizi COM bayrağı. Ama "mümkün" ile "değer" farklı: 1. sorudaki `JitInfo` probu AOT'un `ilk-kare`'de kaç ms alabileceğinin tavanını AOT derlemeden söyler. Önce onu okuyun.

## 4. Ölçüm duvarı

Önce teşhis, sonra seçenekler.

**Trim'de hook neden `Main`'e varmadan ölüyor — yüksek olasılıklı teşhis.** `.hata` dosyası yok, yani `try` içine hiç girilmemiş. Bu, `Initialize`'ın **JIT'lenirken** düşmesi demek: gövdedeki bir üye çözülemiyor. Adaylar `RegistryKey.FromHandle`, `SafeRegistryHandle`, `RegistryValueOptions`, `GetValueKind`, `RegLoadAppKey`'in `out IntPtr`'si — uygulamanın kendisi bunları çağırmadığı için kesilmiş `Microsoft.Win32.Registry.dll`'de yoklar. Hook harici IL ama çerçeveyi kesilmiş uygulama klasöründen yüklüyor. Çözüm bir satır ve yalnız ölçüm yayınında: `<TrimmerRootAssembly Include="Microsoft.Win32.Registry" />` (ve gerekirse `System.Linq`) bir `-p:VidShrinkOlcumKok=true` koşuluna bağlı. Ölçülen ikili, sevk edilenden birkaç yüz KB'lık köklenmiş bir Registry.dll ile ayrılır; belgeye yazılır, eşli farkta sıfıra yakındır. **Trim için meşru ve en ucuz yol bu.** AOT için işe yaramaz: IL yüklenmez.

**AOT için üç meşru düzenek, artan maliyetle:**

**(a) Ürün içi HKCU kalkanı — H4'ün dediği, en sadık olan.** Hook'un 40 satırı `Program.Main`'in ilk satırına, `VIDSHRINK_OLCUM_KOVAN` kapısıyla. Ürün kodu yolu **sıfır** değişir; yalnız HKCU tutamağı yönlendirilir, bugün kalkanın yaptığının aynısı. P/Invoke'lar AOT'ta çalışır. İki dikkat: başlatıcı da bugün kalkanı ortamdan miras alıyor, kalkan uygulama içine girince **başlatıcıya da** girmeli (HKCU'ya dokunuyorsa); ve `RegOverridePredefKey` her kayıt defteri dokunuşundan önce koşmalı — `Main`'in ilk satırı bunu sağlar. Ürün, ölçüm gerekçeli bir kod yolu taşır; ortam değişkeni yoksa tek `if`'ten öteye geçmez. Kabul edilebilir.

**(b) Kayıt defterine dokunmayan açılış kolu (yazma atlama bayrağı).** Sorunuzun özü: bu hâlâ ürünün açılışı mı? Şöyle: sıcak kipte ürün HKCU'ya **yazmıyor**, okuyor — `FileAssociationSetup.Ensure` bir kez yazdıktan sonra yalnız karşılaştırır, `SetupShellMenu` ve `DefaultAppSuggestionBar.Wanted` okur. Kalkanın amacı zamanlama değil, ölçümün makineye yazmasını engellemek. Yalnız **yazma dalını** atlayıp okuma/karşılaştırmayı bırakan bir bayrak, sıcak kip yolunu korur; fark, her koşumda "yazılması gerek" kararına varılıp yazılmaması, yani birkaç µs. ±60 ms tabanın çok altında. **Meşru, tek şartla: bayrak çiftin iki tarafına da aynı uygulanır**; eşli fark yöntemiyle çarpıklık zaten sadeleşir. Zayıf yanı (a)'dan az sadık olması ve bayrağın ürüne sızıp dosya ilişkilendirmesini sessizce kapatabilmesi — zararsız ama izlenmesi gereken bir davranış. (a) varken (b)'yi seçmem.

**(c) Kod yazmadan: ayrı bir Windows kullanıcı hesabı.** Ölçüm atılabilir bir yerel hesapta koşar; HKCU o hesabın kovanı, gerçek kullanıcının kaydına hiç dokunulmaz. Ürün kodu değişmez, hook gerekmez, AOT/trim/IL fark etmez. Maliyet düzenekte: EkranSaati süreci zaten ayrı masaüstünde açıyor, `CreateProcessWithLogonW` ile başka kullanıcıda açması gerekir (parola yönetimi, profil ısınması, `Personalize` anahtarının o hesabın varsayılanı olması). Bugünkü tabanla eşlenmez (profil farklı), kendi içinde eşlenir. Windows Sandbox/VM de aynı sınıfta: mutlak sayı için değil, **çift içi fark** için meşru; vGPU olduğu için D3D bloğu farklıdır ama iki tarafta da aynıdır.

**Meşru olmayan**: kayıt defteri erişimini sistem düzeyinde kancalamak (AppInit_DLLs, detours) — sistem ayarı ve ölçtüğünüz makinede kalıcı iz.

Sıra önerim: trim için TrimmerRootAssembly'li hook (bugün), AOT için ürün içi kalkan (a) — başlatıcıya da. Başlatıcı AOT'u ise bunların hiçbirini beklemiyor: kalkan uygulama sürecinde kalıyor, başlatıcı HKCU'ya yazmıyorsa mevcut düzenek olduğu gibi çalışır.

---

Yok

Sanity: dört soru dört başlıkta cevaplandı; derlemeden doğrulanan iddialar (Load(object) gövdesi, RequiresUnreferencedCode yerleri, `!AvaloniaResources` korumasızlığı, libmpv geri çağrı biçimi, kalkan mekanizması, başlatıcının ayrı süreç oluşu) tahminlerden ("tahmin", "hipotez", "doğrulamadım") ayrı işaretlendi; hiçbir dosya değiştirilmedi.
