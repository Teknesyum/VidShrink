# Kabuk Teslimi Olcumu

T191. Makine: Windows 11 Pro 10.0.22631, x64. Tarih: 2026-09-08.
Butun bloklar **ham cikti**; ozet degil. Olculmeyen sey icin "olcemedim" yazili.

## Ozet — tek cumleyle

`shell/` klasoru yayin paketinin icinde zaten var; eksik olan sey manifestoydu.
Manifestoya `shell` adinda **ikinci bir ust dizi** eklendi: satirlari kurulum kokune
goreli, baslaticinin arsivinden iniyor, ama baslaticinin yan ad + gunluk + cikista gecis
dansina girmiyor — cunku bu dosyalari calisan surec tutmuyor, uzerlerine dogrudan
yaziliyor. `files` dizisinin sekli degismedi ve olculdu: 0.2.5 ile 0.3.0 istemcileri yeni
manifestoyu eskisiyle ayni okuyor.

---

## 1. Baslangic durumu — guncelleyici `launcher` dizisini bugun nasil tuketiyor

Ilgili satirlar, `main` uzerindeki halleriyle.

`src/VidShrink.Launcher/Updater.cs` (T191 oncesi):

- `48` — `UpdateCheck.ParseManifest(json)`; manifesto cozumleniyor.
- `56` — `UpdateCheck.Diff(appDirectory, manifest, cache)`; **uygulama klasoru** ile
  `files` dizisi karsilastiriliyor, inecek uygulama dosyalari cikiyor.
- `57` — `UpdateCheck.Diff(baseDirectory, manifest.Launcher, cache)`; **kurulum koku** ile
  `launcher` dizisi karsilastiriliyor.
- `59` — `changed.Count == 0 && launcherChanged.Count == 0` ise hicbir sey indirilmiyor.
- `73-76` — degisen uygulama dosyalari `vidshrink-<rid>.zip` icinden araliksal istekle
  cekilip yan klasore yaziliyor.
- `80-83` — degisen `launcher` satirlari `vidshrink-launcher-<rid>.zip` icinden cekiliyor.
- `94` — `UpdateRollout.Apply(...)`.

`src/VidShrink.Core/UpdateCheck.cs` (T191 oncesi):

- `282-306` `ParseManifest` — okunan ust alanlar yalnizca `version`, `commit`, `built`,
  `rid`, `files`, `launcher`. Baska hicbir ust alan okunmuyor.
- `350-367` `Diff(directory, files, cache)` — verilen dizinle verilen listeyi karsilastirir.
  Yol ayraci `/` oldugu icin `shell/x.dll` gibi klasor iceren bir satiri **zaten
  destekliyor**; `LocalPath` (satir 369) ayraci cevirip birlestirir.
- `1209-1236` `UpdateRollout.Apply` — once uygulama dosyalari yerlesir, sonra baslatici
  kurulur.

Sonuc: guncelleyici **yalnizca manifestodaki satirlari** takas eder. Yayin arsivinde
duran ama manifestoda sayilmayan bir dosya, guncelleyiciyle gelen kuruluma hicbir zaman
ulasmaz. `shell/` tam olarak bu durumdaydi.

## 2. Klasor iceren bir satirla ne olur

Uc yol vardi. Secilen ucuncusu.

**(a) `files` dizisine `shell/**` eklemek.** Elenmesinin sebebi `release.yml:203-213`
yorumunda yazili ve gecerli: `files` satirlari **uygulama arsivine** (`vidshrink-<rid>.zip`)
goreli. Kurulu her eski guncelleyici `shell/...` satirini o arsivde arar, bulamaz,
`StageFileAsync` icindeki `FileNotFoundException`'i alir ve **guncellemenin tamamindan**
vazgecer. `files` dizisinin sekli degismez kuralinin kaynagi budur.

**(b) `launcher` dizisine `shell/**` eklemek.** Makine buna teknik olarak hazir: `Target`,
`Incoming`, `StagePath` hepsi goreli yol uzerinden calisiyor ve alt klasoru destekliyor.
Elenmesinin sebebi davranissal: `launcher` dizisi cikista gecis yapan yola bagli.
`LauncherUpdate.Commit` (UpdateCheck.cs:1212-1218) bekleyen her satir icin sirayla
`Replace` cagirir ve **ilk basarisizlikta `return false`** der; gunluk silinmez, surum
isareti yazilmaz. Kabuk uzantisi DLL'ini o an `explorer.exe` tutuyorsa `File.Move` 30
saniye boyunca doner, dusar ve **baslaticinin kendi takasi da o turda oturmaz**. Yani
kabuk klasorundeki bir kilit, baslatici guncellemesini rehin alirdi. Olcmedim: gercek bir
`explorer.exe` kilidi altinda bu senaryoyu kosturmadim; gerekce kod okumasindan cikti.

**(c) Ayri bir `shell` ust dizisi.** Secilen yol. Bu dosyalari calisan surec tutmuyor, o
yuzden yan ad, gunluk ve cikista gecis gerekmiyor: dogrulanan dosya dogrudan uzerine
yazilir (`ShellUpdate.Apply`). Kopyalama duserse yutulur; `ShellUpdate.Installed` yanlis
doner, `AlreadyCurrent` kapisi acik kalir ve is sonraki acilisa kalir. Baslaticinin takasi
etkilenmez.

## 3. Eski istemci yeni manifestoyu okudugunda ne yapar — **calistirilmis olcum**

`v0.2.5` ve `v0.3.0` etiketlerindeki `UpdateCheck.cs` git'ten cikarilip kendi konsol
projesinde derlendi ve yeni manifesto (asagidaki 5. bolumun ciktisi) o surumlerin kendi
`ParseManifest`'ine verildi.

```
===== v0.2.5 istemcisi yeni manifesti okuyor =====
version=0.3.0 rid=win-x64
files=234
launcher=1 -> VidShrink.exe
files icinde shell/ ile baslayan satir: 0
launcher icinde shell/ ile baslayan satir: 0
bu surumun ReleaseManifest kaydinda Shell alani var mi: False

===== v0.3.0 istemcisi yeni manifesti okuyor =====
version=0.3.0 rid=win-x64
files=234
launcher=1 -> VidShrink.exe
files icinde shell/ ile baslayan satir: 0
launcher icinde shell/ ile baslayan satir: 0
bu surumun ReleaseManifest kaydinda Shell alani var mi: False
```

Kaynak tarafinda ayni sey:

```
===== v0.2.5 =====
-- ReleaseManifest kaydinin alanlari
public sealed record ReleaseManifest(
    string Version,
    string Commit,
    DateTimeOffset Built,
    string Rid,
    IReadOnlyList<ManifestFile> Files)
{
    /// <summary>
    /// Başlatıcının dosyaları. <see cref="Files"/> uygulama klasörüne göreli, bu liste
    /// kurulum köküne göreli; ikisi ayrı alanda çünkü ayrı arşivlerden iniyorlar ve ayrı
    /// yerlere yazılıyorlar. Alanı tanımayan eski bir güncelleyici burayı hiç görmez ve
    /// eskisi gibi yalnız <c>app/</c> klasörünü günceller.
    /// </summary>
    public IReadOnlyList<ManifestFile> Launcher { get; init; } = Array.Empty<ManifestFile>();
}
-- manifestten okunan ust alanlar (ReadFileList cagrilari)
293:                ReadFileList(root, "files"))
295:                Launcher = ReadFileList(root, "launcher")
-- TryGetProperty ile okunan ust alanlar
TryGetProperty("built"
TryGetProperty("commit"
TryGetProperty("rid"

===== v0.3.0 =====
-- ReleaseManifest kaydinin alanlari
public sealed record ReleaseManifest(
    string Version,
    string Commit,
    DateTimeOffset Built,
    string Rid,
    IReadOnlyList<ManifestFile> Files)
{
    /// <summary>
    /// Başlatıcının dosyaları. <see cref="Files"/> uygulama klasörüne göreli, bu liste
    /// kurulum köküne göreli; ikisi ayrı alanda çünkü ayrı arşivlerden iniyorlar ve ayrı
    /// yerlere yazılıyorlar. Alanı tanımayan eski bir güncelleyici burayı hiç görmez ve
    /// eskisi gibi yalnız <c>app/</c> klasörünü günceller.
    /// </summary>
    public IReadOnlyList<ManifestFile> Launcher { get; init; } = Array.Empty<ManifestFile>();
}
-- manifestten okunan ust alanlar (ReadFileList cagrilari)
298:                ReadFileList(root, "files"))
300:                Launcher = ReadFileList(root, "launcher")
-- TryGetProperty ile okunan ust alanlar
TryGetProperty("built"
TryGetProperty("commit"
TryGetProperty("rid"
```

Iki surum de `shell` alanini hic gormuyor: `files` 234 satir, `launcher` tek satir,
ikisinde de `shell/` onekli satir yok. Yani 0.2.5 ve 0.3.0 kurulumlari yeni manifestoyu
**bugunku davranislariyla** okur — guncellemeden vazgecmez, fazladan dosya indirmez.

Bunun bedeli olculdu ve soylenmesi gerekiyor: 0.3.0 istemcisi `shell` alanini gormedigi
icin kabuk klasorunu **kendi turunda almaz**. O tur baslaticiyi 0.3.1'e tasir; kabuk
klasoru bir sonraki acilista, artik 0.3.1 baslaticisi kostugu icin yerlesir. Duzeltme iki
acilis surer, bir acilis degil.

## 4. Kod degisikligi

- `ReleaseManifest.Shell` — yeni alan, varsayilani bos dizi.
- `ParseManifest` — `ReadFileList(root, "shell")`.
- `ShellUpdate` — yeni sinif: `StagePath`, `Target`, `Installed`, `Apply`.
- `UpdateCheck.AlreadyCurrent(base, app, manifest)` — sonuna `&& ShellUpdate.Installed(...)`
  eklendi. Surum isareti bu soruyu cevaplayamaz: isaret uygulamanin ve baslaticinin,
  kabuk klasorunun yerlesmesi ayri bir adim ve dusebiliyor. Kapi olmasaydi bir kere
  dusen kopyalama bir daha hic denenmezdi.
- `UpdateRollout.Apply` — istege bagli `shellFiles` parametresi. Kabuk adimi uygulama
  adimindan **once** kosuyor, cunku `UpdateStage.Apply` sonunda yan klasorun tamamini
  siliyor; sonrasinda kopyalanacak dosya kalmazdi.
- `Updater.RunAsync` — `shellChanged` hesaplaniyor, baslatici arsivi **tek kez** acilip
  hem `launcher` hem `shell` satirlari oradan cekiliyor.
- `release.yml` — `jq` katlamasina ikinci bir atama eklendi.

## 5. K4 — degisen `jq` ifadesi gercek bir manifesto ornegi uzerinde

Girdi gercek: `v0.3.0` yayinindan indirilen `manifest-win-x64.json` ve
`vidshrink-launcher-win-x64.zip`. Baslatici manifestosu, CI'daki komutun aynisiyla
(`vidshrink-manifest write publish-launcher`) uretildi. `jq` surumu ciktida.

```
== 1. Baslatici arsivi aciliyor (v0.3.0 gercek varlik)
VidShrink.Core.pdb
VidShrink.exe
VidShrink.pdb
shell/AppxManifest.template.xml
shell/Assets/Square150x150Logo.png
shell/Assets/Square44x44Logo.png
shell/Assets/StoreLogo.png
shell/VidShrink.ShellExtension.dll

== 2. Manifest araci publish-launcher uzerinde (CI'daki komutun aynisi)
C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T191/.calisma/T191/launcher-manifest.json: 8 dosya, 65.08 MB
VidShrink.Core.pdb 82456
VidShrink.exe 68001772
VidShrink.pdb 18696
shell/AppxManifest.template.xml 2428
shell/Assets/Square150x150Logo.png 14281
shell/Assets/Square44x44Logo.png 2251
shell/Assets/StoreLogo.png 2689
shell/VidShrink.ShellExtension.dll 120832

== 3. ESKI jq ifadesi (main'deki hali)
{
  "launcher": [
    {
      "path": "VidShrink.exe",
      "sha256": "a3c95d901c3c6c1b1e5e7e28e536750382f6281f3caca4464f1ec598911c6964",
      "size": 68001772
    }
  ],
  "shell": null
}

== 4. YENI jq ifadesi (release.yml'ye giren hali)
{
  "launcher": [
    {
      "path": "VidShrink.exe",
      "sha256": "a3c95d901c3c6c1b1e5e7e28e536750382f6281f3caca4464f1ec598911c6964",
      "size": 68001772
    }
  ],
  "shell": [
    {
      "path": "shell/AppxManifest.template.xml",
      "sha256": "e46bc4ff22c2d6694afe1c62f529f39fdf54c458a71b9aa12e7bf0f54991adcd",
      "size": 2428
    },
    {
      "path": "shell/Assets/Square150x150Logo.png",
      "sha256": "ce8fa49a3c9cf7ee194753a62cada66bffae42aad67cb6a954005a7b5b5359c6",
      "size": 14281
    },
    {
      "path": "shell/Assets/Square44x44Logo.png",
      "sha256": "8899b91bd9eea1c583b88dddfee80bfc1903bcd0f89a9b459f449edfbc37da93",
      "size": 2251
    },
    {
      "path": "shell/Assets/StoreLogo.png",
      "sha256": "2509e746a2f19b4b90a21d8ebff22d2e2f8cb750f848c14ec6ec5f74fe52a452",
      "size": 2689
    },
    {
      "path": "shell/VidShrink.ShellExtension.dll",
      "sha256": "fd89a18eef8f83b21f3441a2442d3341dea5b7aabf957f14dc02d96a2e2b4aab",
      "size": 120832
    }
  ]
}

== 5. files dizisinin sekli degismedi mi
eski files=234
yeni files=234
files dizileri ayni mi: true
yeni ust alanlar: ["built","commit","files","launcher","rid","shell","version"]
== 6. release.yml gercek satirlar
230:              '.launcher = ($launcher[0].files | map(select(.path == "VidShrink.exe")))
231:               | .shell = ($launcher[0].files | map(select(.path | startswith("shell/"))))' \
234:            jq -r '.launcher[] | "manifest launcher: \(.path) \(.size)"' publish/${{ matrix.rid }}/manifest.json
235:            jq -r '.shell[] | "manifest shell: \(.path) \(.size)"' publish/${{ matrix.rid }}/manifest.json
```

Okunanlar: `.shell` eski ifadede `null`, yeni ifadede bes satir. `files` dizisi bayt bayt
ayni (`files dizileri ayni mi: true`, 234 satir). `.pdb` dosyalari iki dizinin de disinda
kaliyor. Ust alan listesine yalnizca `shell` eklendi.

Olcemedim: gercek bir CI kosumu yapmadim. Yukaridaki blok `release.yml`'nin `jq`
ifadesinin bu makinede, gercek yayin varliklari uzerinde kosturulmus halidir; is akisinin
kendisi GitHub Actions uzerinde kosmadi.

## 6. K2 ve K3 — testler ve mutasyon

Dar filtre yesil (58 test, 3'u kurulu baslatici gosterilmedigi icin atlandi):

```
$ dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj --filter "FullyQualifiedName~UpdaterTests"
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.34]     VidShrink.Tests.UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName [SKIP]
  Atlandı VidShrink.Tests.UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName [1 ms]
[xUnit.net 00:00:15.14]     VidShrink.Tests.UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout [SKIP]
  Atlandı VidShrink.Tests.UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout [1 ms]
[xUnit.net 00:00:16.91]     VidShrink.Tests.UpdaterTests.SwitchedOffLauncherMakesNoNetworkRequestAtAll [SKIP]
  Atlandı VidShrink.Tests.UpdaterTests.SwitchedOffLauncherMakesNoNetworkRequestAtAll [1 ms]
Başarılı!  - Başarısız:     0, Başarılı:    55, Atlanan:     3, Toplam:    58, Süre: 10 s - VidShrink.Tests.dll (net8.0)
cikis kodu: 0
```

Yeni testler:

| Test | Ne olcuyor |
| --- | --- |
| `AnUpdateInstallsTheShellFolderIntoAnInstallThatNeverHadOne` | Kurulum kokunde `shell/` hic yokken bir tur kosuyor; klasorun yerlestigi ve ozetlerin tuttugu olculuyor |
| `AMissingShellFolderKeepsTheUpdateGateOpen` | Surum isareti yazili olsa bile eksik `shell/` "zaten guncel" saydirmiyor |
| `TheManifestCountsTheShellFolderInItsOwnField` | `shell` ust alani okunuyor; `files` ve `launcher` dizilerine `shell/` onekli satir sizmiyor |
| `AManifestWithoutAShellFieldIsStillRead` | Alani olmayan manifesto eskisi gibi okunuyor |

Duzeltme dort ayri noktadan geri alindi; her biri farkli bir testi kirmizi yapti:

```
======================================================================
M1 UpdateRollout.Apply icindeki ShellUpdate.Apply cagrisi silindi
======================================================================
Toplam 1 test dosyası belirtilen desenle eşleşti.
  Başarısız VidShrink.Tests.UpdaterTests.AnUpdateInstallsTheShellFolderIntoAnInstallThatNeverHadOne [6 ms]
  Hata İletisi:
Başarısız! - Başarısız:     1, Başarılı:    54, Atlanan:     3, Toplam:    58, Süre: 10 s - VidShrink.Tests.dll (net8.0)
cikis kodu: 1
======================================================================
M2 AlreadyCurrent icindeki ShellUpdate.Installed kapisi silindi
======================================================================
Toplam 1 test dosyası belirtilen desenle eşleşti.
  Başarısız VidShrink.Tests.UpdaterTests.AMissingShellFolderKeepsTheUpdateGateOpen [1 ms]
  Hata İletisi:
   Assert.False() Failure
Başarısız! - Başarısız:     1, Başarılı:    54, Atlanan:     3, Toplam:    58, Süre: 10 s - VidShrink.Tests.dll (net8.0)
cikis kodu: 1
======================================================================
M3 ParseManifest artik shell alanini okumuyor
======================================================================
Toplam 1 test dosyası belirtilen desenle eşleşti.
  Başarısız VidShrink.Tests.UpdaterTests.TheManifestCountsTheShellFolderInItsOwnField [1 ms]
  Hata İletisi:
   Assert.Single() Failure: The collection was empty
Başarısız! - Başarısız:     1, Başarılı:    54, Atlanan:     3, Toplam:    58, Süre: 10 s - VidShrink.Tests.dll (net8.0)
cikis kodu: 1
======================================================================
kaynak geri alindi
M4 release.yml'deki .shell katlamasi silindi
Toplam 1 test dosyası belirtilen desenle eşleşti.
  Başarısız VidShrink.Tests.UpdaterTests.TheWorkflowFoldsTheLauncherIntoTheManifest [4 ms]
   Assert.Contains() Failure: Sub-string not found
     at VidShrink.Tests.UpdaterTests.TheWorkflowFoldsTheLauncherIntoTheManifest() in C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T191\tests\VidShrink.Tests\UpdaterTests.cs:line 422
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 4 ms - VidShrink.Tests.dll (net8.0)
cikis kodu: 1
release.yml geri alindi
```

Kaynak her turdan sonra geri yuklendi; son durumda `git diff` yalnizca teslim edilen
degisikligi gosteriyor.

## 7. Kapsam disi kalan bosluk — dosyalar iniyor, kayit olmuyor

T191 dosyalarin teslimini duzeltir, kaydi degil. Kabuk paketini isletim sistemine
tanitan tek yer `Install-VidShrink.ps1` icindeki `Write-Windows11ShellMenu`
`AppxManifest.xml`i uretir ve `Add-AppxPackage -Register` cagirir. Uygulamada ya da
baslaticida bu isi yapan hicbir kod yok:

```
$ grep -rn "Install-VidShrink\|Appx\|RegisterShell\|ShellMenu" --include=*.cs src/
src/VidShrink.Core/UpdateCheck.cs:62:/// ile <c>Install-VidShrink.ps1</c>. Ayrıştıkları sürüm bir kullanıcının kurulumunu düşürdü —
src/VidShrink.Core/UpdateCheck.cs:233:            return "irm https://raw.githubusercontent.com/Teknesyum/VidShrink/main/Install-VidShrink.ps1 | iex";

$ grep -n "Add-AppxPackage -Register\|function Write-Windows11ShellMenu" Install-VidShrink.ps1
216:function Write-Windows11ShellMenu([string]$Root, [string]$InstallDirectory) {
238:    Add-AppxPackage -Register $manifestPath -ExternalLocation $InstallDirectory -ErrorAction Stop
```

Uygulamada ve baslaticida cikan iki satir da yorum/metin; kayit cagiran kod yok.

Yani guncelleyiciyle gelen kurulumda `shell/` klasoru artik yerlesiyor, ama paket kaydi
ancak kullanici kurulum betigini yeniden kosturdugunda olusuyor. Sag menunun kendiliginden
gorunmesi icin ayri bir is gerekiyor. Olcemedim: kayitli bir makinede yalnizca DLL
degistiginde menunun calismaya devam edip etmedigini denemedim.
