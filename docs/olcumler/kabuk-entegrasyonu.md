# Kabuk Entegrasyonu Olcumleri

T188. Makine: Windows 11 Pro 10.0.22631, x64. Tarih: 2026-09-07.
Bu belge sag menunun neden calismadigini ve varsayilan program kaydinin nereye kadar
gidebildigini olculmus haliyle tutar. Butun bloklar **ham cikti**; ozet degil.

## Ozet — tek cumleyle

Kabuk uzantisinin kendisi saglam: derleniyor, kayit oluyor, paketli COM uzerinden
aktive oluyor ve basligini donduruyor. Calismayan sey **teslimat**: kurulu agacta
`shell/` klasoru hic yok, dolayisiyla kurulum betigi menuyu yazmadan sessizce donuyor.
Uretim yolu ise imza istiyor; imzasiz `.msix` kurulumu `0x800B0100` ile reddediliyor.

---

## 1. Makinenin baslangic durumu — menu gercekten yok

Kurulu agac, paket kaydi, isletim sistemi surumu ve klasik sag tik anahtarlari.
```
## 1. Kurulu VidShrink
InstallRoot: C:\Users\Administrator\AppData\Local\Programs\VidShrink
Exists: True

Mode  Length   Name
----  ------   ----
d----          app
d----          tools
-a--- 5        .launcher-version
-a--- 66000    VidShrink.Core.pdb
-a--- 68001772 VidShrink.exe
-a--- 18692    VidShrink.pdb


shell\ exists: False

## 2. Get-AppxPackage -Name Teknesyum.VidShrink.Shell
(bos - paket kayitli degil)

## 3. OS surumu

Caption                  Version    BuildNumber
-------                  -------    -----------
Microsoft Windows 11 Pro 10.0.22631 22631



## 4. Klasik sag tik anahtarlari (HKCU SystemFileAssociations)
HKCU:\Software\Classes\SystemFileAssociations\.mp4\shell : False
HKCU:\Software\Classes\SystemFileAssociations\.mkv\shell : False
HKCU:\Software\Classes\SystemFileAssociations\.mov\shell : False
HKCU:\Software\Classes\SystemFileAssociations\.avi\shell : False
HKCU:\Software\Classes\SystemFileAssociations\.webm\shell : False
```
Uc sey birden okunuyor: kurulu agacta `shell\` **yok**, `Teknesyum.VidShrink.Shell`
paketi **kayitli degil**, klasik `SystemFileAssociations\.<uzanti>\shell` anahtarlarinin
**hicbiri yok**. Yani bu makinede iki menu turunden de hicbiri mevcut degil.

### Nedeni — teslimat zinciri

`Install-VidShrink.ps1` icindeki `Write-Windows11ShellMenu`, `shell/AppxManifest.template.xml`
ya da `shell/VidShrink.ShellExtension.dll` bulunmazsa `false` donuyor ve yalniz sari bir
satir yaziyor: "Bu yayin Windows 11 kabuk paketini tasimiyor; klasik menu yazildi."

`.github/workflows/release.yml` icinde `shell-extension` isi DLL'i uretip
`publish-launcher/shell` altina koyuyor; ancak baslatici surum bildirimi birlestirilirken
dizi `map(select(.path == "VidShrink.exe"))` ile suzuluyor. Bu yuzden **kendini gunceleyen
bir kurulum `shell/` klasorunu hic almiyor**. Bu makinedeki 0.3.0 kurulumu tam olarak bu
durumda.

---

## 2. Yapi zinciri var mi

```
vswhere: C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe exists=True
--- vswhere -latest -products * ---

--- vswhere -find MSBuild ---
C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe

--- vswhere -requires VC.Tools.x86.x64 ---
C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools

--- msbuild in PATH ---

--- makeappx / signtool ---
makeappx.exe : C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\arm64\makeappx.exe; C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe; C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x86\makeappx.exe
signtool.exe : C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\arm\signtool.exe; C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\arm64\signtool.exe; C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe
```
## 3. Uzanti derleniyor mu

```
EXITCODE=0

  VidShrink.ShellExtension.cpp
     C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\src\VidShrink.ShellExtension\x64\Release\VidShrink.ShellExtension.lib kitaplığı ve C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\src\VidShrink.ShellExtension\x64\Release\VidShrink.ShellExtension.exp nesnesi oluşturuluyor
  Kod üretiliyor
  Previous IPDB not found, fall back to full compilation.
  All 32 functions were compiled because no usable IPDB/IOBJ from previous compilation was found.
  Kodun üretilmesi tamamlandı
  VidShrink.ShellExtension.vcxproj -> C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\src\VidShrink.ShellExtension\x64\Release\VidShrink.ShellExtension.dll
```
DLL uretiliyor, cikis kodu 0. Derleme tarafinda kusur yok.

---

## 4. Seyrek paket kaydi (gelistirici yolu)

```
stage: C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket

FullName
--------                                                                                                                
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell                           
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\VidShrink.exe                   
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\Assets                    
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\AppxManifest.template.xml 
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\AppxManifest.xml          
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\VidShrink.ShellExtension.…
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\Assets\Square150x150Logo.…
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\Assets\Square44x44Logo.png
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\Assets\StoreLogo.png      


## Add-AppxPackage -Register
KOMUT: Add-AppxPackage -Register 'C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\AppxManifest.xml' -ExternalLocation 'C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket'
SONUC: basarili
```
```
## Get-AppxPackage -Name Teknesyum.VidShrink.Shell

Name              : Teknesyum.VidShrink.Shell
PackageFullName   : Teknesyum.VidShrink.Shell_1.0.0.0_x64__vdfzgcscextka
InstallLocation   : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell
SignatureKind     : None
Status            : Ok
IsDevelopmentMode : True


## Gelistirici kipi anahtarlari
HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock exists=True

AllowDevelopmentWithoutDevLicense : 1
AllowAllTrustedApps               : 1


## Paketli COM sunucusu kaydi (HKCU ClassesRegistry)
Klasik CLSID kaydi: (yok - paketli COM klasik CLSID agacina yazmaz)
## COM aktivasyonu denemesi
CreateInstance: OK -> System.__ComObject
```
`Add-AppxPackage -Register` **basarili**. Dikkat: `SignatureKind : None` ve
`IsDevelopmentMode : True`. Bu yol yalniz `AllowDevelopmentWithoutDevLicense = 1` oldugu
icin acildi. **Gelistirici kipi kapali bir makinede ne olacagini olcemedim** — bu makinede
anahtar acik ve kapatip yeniden olcmek kullanicinin ayarini degistirmek olurdu.

---

## 5. COM aktivasyonu — once yanlis olctum, sonra duzelttim

Ilk sonda `E_NOINTERFACE` verdi:

```
## build

Oluşturma başarılı oldu.
    0 Uyarı
    0 Hata

Geçen Süre 00:00:10.16

## sonda (invoke yok)
CoCreateInstance: OK
QueryInterface(IExplorerCommand): HATA 0x80004002 Unable to cast COM object of type 'System.__ComObject' to interface type 'IExplorerCommand'. This operation failed because the QueryInterface call on the COM component for the interface with IID '{A88826F8-186F-4987-AADE-EA0CEF8FBFE8}' failed due to the following error: Böyle bir arabirim desteklenmiyor (0x80004002 (E_NOINTERFACE)).
```
```
## IExplorerCommand {A88826F8-186F-4987-AADE-EA0CEF8FBFE8} proxy/stub kaydi
HKLM:\SOFTWARE\Classes\Interface\{A88826F8-186F-4987-AADE-EA0CEF8FBFE8} exists=True
ProxyStubClsid32
  ProxyStubClsid32 = {95E15D0A-66E6-93D9-C53C-76E6219D3341}
HKCU:\SOFTWARE\Classes\Interface\{A88826F8-186F-4987-AADE-EA0CEF8FBFE8} exists=False

## PackagedCom kaydi
HKCU:\SOFTWARE\Classes\ActivatableClasses\Package\Teknesyum.VidShrink.Shell_1.0.0.0_x64__vdfzgcscextka exists=False
HKCU:\SOFTWARE\Classes\PackagedCom\Package\Teknesyum.VidShrink.Shell_1.0.0.0_x64__vdfzgcscextka exists=True

HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PackagedCom\Package\Teknesyum.VidShrink.Shell_1.0.0.0_x64__vdfzgcscextka exists=False

## Paketin dogrulanmasi
Teknesyum.VidShrink.Shell_1.0.0.0_x64__vdfzgcscextka
```
```
## build

Oluşturma başarılı oldu.
    0 Uyarı
    0 Hata

Geçen Süre 00:00:01.54

## in-proc sonda
KOMUT: sonda.dll <dll> --inproc
LoadLibrary: OK C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\VidShrink.ShellExtension.dll
GetProcAddress(DllGetClassObject): OK
DllGetClassObject: 0x00000000
IClassFactory.CreateInstance(IExplorerCommand): 0x80004002
```
Bu uc olcum **yanlisti**. Yonetilen sondada `IExplorerCommand` icin elle yazdigim IID
`{A88826F8-186F-4987-AADE-EA0CEF8FBFE8}` idi; bu deger yanlis. Gercek IID'yi C++ sondasi
baglayicidan okuyup yazdirdi:

```
## msbuild sonda.vcxproj

  sonda.cpp
  sonda.vcxproj -> C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\sondacpp\x64\Release\sonda.exe

## KOMUT: sonda.exe <dll>
IID_IExplorerCommand (baglayicidan) = {A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9}
__uuidof(IExplorerCommand) = {A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9}
LoadLibrary: OK
DllGetClassObject: 0x00000000
CreateInstance(IID_IUnknown): 0x00000000
QueryInterface(IID_IExplorerCommand): 0x00000000
GetTitle: 0x00000000 title=Bu Videoyu VidShrink ile Aç
GetState: 0x00000000 state=0
```
`IID_IExplorerCommand = {A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9}`. Dogru IID ile hem
in-proc hem paketli COM yolu temiz:

```
## build

  sonda.cpp
  sonda.vcxproj -> C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\sondacpp\x64\Release\sonda.exe

## KOMUT: sonda.exe --inproc <dll> <mp4>
IID_IExplorerCommand = {A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9}
SHCreateItemFromParsingName(C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\deneme.mp4): 0x00000000
SHCreateShellItemArrayFromShellItem: 0x00000000
LoadLibrary: OK
DllGetClassObject: 0x00000000
IClassFactory.CreateInstance(IExplorerCommand): 0x00000000
GetTitle: 0x00000000 title=Bu Videoyu VidShrink ile Aç
GetIcon: 0x00000000 icon=C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\VidShrink.exe
GetState: 0x00000000 state=0

## KOMUT: sonda.exe --com - <mp4>   (paketli COM aktivasyonu)
IID_IExplorerCommand = {A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9}
SHCreateItemFromParsingName(C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\deneme.mp4): 0x00000000
SHCreateShellItemArrayFromShellItem: 0x00000000
CoCreateInstance(IExplorerCommand): 0x00000000
GetTitle: 0x00000000 title=Bu Videoyu VidShrink ile Aç
GetIcon: 0x00000000 icon=C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\VidShrink.exe
GetState: 0x00000000 state=0
```
Her iki yolda da `0x00000000`; `GetTitle` "Bu Videoyu VidShrink ile Ac" donuyor,
`GetIcon` baslatici yolunu donuyor, `GetState` 0 (ECS_ENABLED). **Uzanti bozuk degil.**

Olcemedigim tek sey: Explorer'in kendi sag menusunde girdinin gorunmesi ve tiklaninca
dosyayi acmasi. Bunun icin paketin kurulu agactan (`%LOCALAPPDATA%\Programs\VidShrink\shell`)
kayitli olmasi gerekiyordu; oradaki klasor yok, `.calisma/` altindan kayit ettigim paketi
kullanicinin gercek Explorer'inda birakmak dogru olmazdi. **Explorer'da gorsel dogrulama
yapilmadi — olcemedim.**

---

## 6. Uretim yolu — imza siniri

```
## makeappx pack (sparse paket, imzasiz)
KOMUT: makeappx.exe pack /d "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell" /p "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\vidshrink-shell.msix" /nv /o
Microsoft (R) MakeAppx Tool

Copyright (C) 2013 Microsoft.  All rights reserved.



The path (/p) parameter is: "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\vidshrink-shell.msix"

The content directory (/d) parameter is: "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell"

Enumerating files from directory "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell"

Packing 6 file(s) in "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell" (content directory) to "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\vidshrink-shell.msix" (output file name).

Memory limit defaulting to 34146576384 bytes.

Using "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\AppxManifest.xml" as the manifest for the package.

Processing "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\AppxManifest.template.xml" as a payload file.  Its path in the package will be "AppxManifest.template.xml".

Processing "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\VidShrink.ShellExtension.dll" as a payload file.  Its path in the package will be "VidShrink.ShellExtension.dll".

Processing "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\Assets\StoreLogo.png" as a payload file.  Its path in the package will be "Assets\StoreLogo.png".

Processing "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\Assets\Square150x150Logo.png" as a payload file.  Its path in the package will be "Assets\Square150x150Logo.png".

Processing "\\?\C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket\shell\Assets\Square44x44Logo.png" as a payload file.  Its path in the package will be "Assets\Square44x44Logo.png".

Package creation succeeded.



## Imzasiz .msix kurulum denemesi (uretim yolu)
KOMUT: Add-AppxPackage -Path "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\vidshrink-shell.msix" -ExternalLocation "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T188\.calisma\paket"
SONUC: HATA
Add-AppxPackage: Deployment failed with HRESULT: 0x80073CF0, Paket açılamadı.  error 0x800B0100: The app package must be digitally
signed for signature validation.  NOTE: For additional information, look for [ActivityId]
62d1e45c-3b70-0002-79e0-5473703bdd01 in the Event Log or use the command line Get-AppPackageLog -ActivityID
62d1e45c-3b70-0002-79e0-5473703bdd01
```
Paket olusuyor ama kurulmuyor:
`0x80073CF0 ... error 0x800B0100: The app package must be digitally signed for signature
validation.`

**Sag menunun uretimde calismasinin onundeki engel budur: kod imzalama.** Gelistirici
kipi ile `-Register` calisiyor; son kullanicida `-Register` yolu yok, `.msix` gerekiyor
ve `.msix` imza istiyor.

---

## 7. Varsayilan program kaydi — nereye kadar gidilebiliyor

`FileAssociation.Register` calistirildi (hedef: kurulu `VidShrink.exe`).

```
## Hedef calistirilabilir
C:\Users\Administrator\AppData\Local\Programs\VidShrink\VidShrink.exe exists=True

## Kayit oncesi HKCU durumu
  .mp4 OpenWithProgids var=True VidShrink kaydi=False
  .mkv OpenWithProgids var=True VidShrink kaydi=False
  .mov OpenWithProgids var=True VidShrink kaydi=False
  .avi OpenWithProgids var=True VidShrink kaydi=False
  .webm OpenWithProgids var=True VidShrink kaydi=False
HKCU:\Software\Classes\Teknesyum.VidShrink.Video exists=False

## KOMUT: FileAssociation.Register(exe)  [VidShrink.App.dll, yansima ile]
Olusan/yazilan anahtar sayisi: 0
```
`Register` **basarisiz satirlarin** listesini dondurur; 0 uzunluk = butun satirlar yazildi.

Olusan anahtarlarin dokumu:

```
## Kayit sonrasi HKCU dokumu

KEY HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\DefaultIcon
KEY HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\shell
KEY HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\shell\open
KEY HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\shell\open\command
KEY HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video
   HKCU:\Software\Classes\Teknesyum.VidShrink.Video  ->  [(default)] = VidShrink
   HKCU:\Software\Classes\Teknesyum.VidShrink.Video  ->  [FriendlyTypeName] = VidShrink
   HKCU:\Software\Classes\Teknesyum.VidShrink.Video  ->  (Default) = VidShrink
   HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\DefaultIcon  ->  [(default)] = C:\Users\Administrator\AppData\Local\Programs\VidShrink\VidShrink.exe,0
   HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\DefaultIcon  ->  (Default) = C:\Users\Administrator\AppData\Local\Programs\VidShrink\VidShrink.exe,0
   HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\shell\open\command  ->  [(default)] = "C:\Users\Administrator\AppData\Local\Programs\VidShrink\VidShrink.exe" "%1"
   HKEY_CURRENT_USER\Software\Classes\Teknesyum.VidShrink.Video\shell\open\command  ->  (Default) = "C:\Users\Administrator\AppData\Local\Programs\VidShrink\VidShrink.exe" "%1"

KEY HKEY_CURRENT_USER\Software\Classes\Applications\VidShrink.exe\shell
KEY HKEY_CURRENT_USER\Software\Classes\Applications\VidShrink.exe\shell\open
KEY HKEY_CURRENT_USER\Software\Classes\Applications\VidShrink.exe\shell\open\command
KEY HKEY_CURRENT_USER\Software\Classes\Applications\VidShrink.exe
   HKEY_CURRENT_USER\Software\Classes\Applications\VidShrink.exe\shell\open\command  ->  [(default)] = "C:\Users\Administrator\AppData\Local\Programs\VidShrink\VidShrink.exe" "%1"
   HKEY_CURRENT_USER\Software\Classes\Applications\VidShrink.exe\shell\open\command  ->  (Default) = "C:\Users\Administrator\AppData\Local\Programs\VidShrink\VidShrink.exe" "%1"

## OpenWithProgids satirlari (tum medya uzantilari)
HKEY_CURRENT_USER\Software\Classes\.3gp\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.asf\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.avi\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.dav\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.divx\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.f4v\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.flv\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.gif\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.m2ts\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.m4v\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.mkv\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.mov\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.mp4\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.mpeg\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.mpg\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.mts\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.mxf\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.ogv\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.rm\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.rmvb\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.ts\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.vob\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.webm\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
HKEY_CURRENT_USER\Software\Classes\.wmv\OpenWithProgids  ->  [Teknesyum.VidShrink.Video] tur=None deger=(bos)
```
Yirmi dort medya uzantisinin `OpenWithProgids` listesine ProgID adi bos degerle girdi;
sozlesmede adi gecen `.mp4 .mkv .mov .avi .webm` bunlarin icinde.

### Sinir — varsayilan yapilamaz

```
## AssocQueryStringW ile etkin varsayilan (UserChoice okunmuyor, yazilmiyor)
  .mp4 -> 
  .mkv -> 
  .mov -> 
  .avi -> 
  .webm -> C:\Windows\system32\OpenWith.exe

## UserChoice anahtarinin dokunulmamis hali (yalniz okundu)
  .mp4 ProgId=AppXqj98qxeaynz6dv4459ayz6bnqxbyaqcs LastWrite=(okunamaz)
  .mkv ProgId=AppXqj98qxeaynz6dv4459ayz6bnqxbyaqcs LastWrite=(okunamaz)
  .mov ProgId=AppXqj98qxeaynz6dv4459ayz6bnqxbyaqcs LastWrite=(okunamaz)
  .avi ProgId=AppXqj98qxeaynz6dv4459ayz6bnqxbyaqcs LastWrite=(okunamaz)
  .webm ProgId=GOMPlayerPlus.webm LastWrite=(okunamaz)
```
Iki sey okunuyor:

`UserChoice` **degismedi**: `.mp4 .mkv .mov .avi` hala `AppXqj98qxeaynz6dv4459ayz6bnqxbyaqcs`
(Filmler ve TV), `.webm` hala `GOMPlayerPlus.webm`. Kayit kullanicinin secimine dokunmadi.

`AssocQueryStringW(ASSOCSTR_EXECUTABLE)` AppX isleyicileri icin **bos string** donuyor.
Yani "su an varsayilan kim" sorusu bir AppX isleyicisi icin bu API ile yanitlanamiyor.
`IsDefault` bu durumda `false` uretir; oneri seridi gosterilir. Yon dogru — ama nedeni
"VidShrink varsayilan degil" degil, "isleyici okunamadi". Bu, ayrimi olculmus bir
sinirdir; sessizce dogru sonuc veriyor.

`UserChoice` yazma denemesi **yapilmadi** (sozlesme K5 ve kullanicinin acik talimati).

---

## 8. Yarim kalanlar

1. **Imzalama.** Uretimde sag menu icin `.msix` imzalanmali. Imzasiz kurulum
   `0x800B0100` ile reddediliyor.
2. **Teslimat.** `release.yml` icindeki `map(select(.path == "VidShrink.exe"))` suzgeci
   `shell/` klasorunu otomatik guncellemeden disari birakiyor.
3. **Gelistirici kipi kapali makine.** Olcemedim.
4. **Explorer'da gorsel dogrulama.** Olcemedim.

---

## 9. Tur 2 — olu kodun canlandirilmasi (7 Eylul 2026)

Tur 1'de yazilan iki parca da uretimde hicbir yerden cagrilmiyordu. Tur 2'de baglandi.

**Serit.** `MainWindow.OnWindowLoaded` icinde `ShowDefaultAppSuggestion()` cagriliyor
(`src/VidShrink.App/MainWindow.axaml.cs`). Once yapiciya konmustu; oradan tam suit alti
yerlesim pimini kirdi (`WindowLayoutTests` uc kol, `AyarYuzeyiTests`, `QualityTargetUiTests`,
`PerformanceCheckTests`). Sebep dogru olculdu: pimler pencereyi hic gostermeden kuruyor,
serit bildirim satirina fazladan yukseklik ekliyor ve sayfa kayiyordu. `OnWindowLoaded`
bassiz kosumda hic ates almadigi icin pim etkilenmiyor, gercek kullanici acilista seridi
goruyor. Serit XAML'e yazilmadi — `MainWindow.axaml`
bu sozlesmenin `owns` listesinde degil — bunun yerine var olan bildirim yigini
`AppliedNotice.Parent` uzerinden bulunup sonuna ekleniyor. Yigin, `AppliedNotice` ve
`UpdateNotice` bildirimlerini tasiyan `Grid.Row="1"` altindaki `StackPanel`.

Kosul uc parcali ve karari `DefaultAppSuggestion.ShouldShow` veriyor: Windows,
varsayilan degil, daha once reddedilmemis. Ret `settings.json` icindeki
`defaultAppSuggestionDismissed` anahtarina yaziliyor; bir sonraki acilista kosul tutmaz
ve serit hic uretilmez.

**Kayit.** `App.OnFrameworkInitializationCompleted` icinde `RegisterFileTypes()`
cagriliyor (`src/VidShrink.App/App.axaml.cs`). Uygulama nasil acilirsa acilsin — ana
pencere ya da kabuk istegi — bu yol kosuyor. Cagri masaustu omru kolunun icinde: omur
kurulmadan calisan bir konak kayit defterine dokunmuyor. Olculdu — 21 testlik kosumdan
sonra gercek `HKCU:\Software\Classes\Applications` altinda `testhost.exe` yok ve gercek
`settings.json` icinde `fileAssociationRegisteredFor` anahtari yok. Kayit her acilista tekrarlanmiyor:
`FileAssociationSetup` yazilan calistirilabilirin yolunu `settings.json` icindeki
`fileAssociationRegisteredFor` anahtarina not dusuyor ve ayni yol gorulurse
`FileAssociation.Register` hic cagrilmiyor. Yol degisirse (tasinan kurulum, baska
klasore kurulan yeni surum) bir kez daha yaziliyor; eski komut satiri artik olmayan bir
dosyayi gosterecegi icin bu tazeleme gerekli.

Yazma basarisiz olursa not dusulmuyor: sonraki acilis yeniden deniyor.

**Olculen.** `dotnet test --filter FullyQualifiedName~KabukEntegrasyonTests` 21 test,
hepsi yesil. Filtresiz tam suit: 1921 test, 1903 basarili, 0 basarisiz, 18 atlanan,
21 dk 17 sn. Serit davranisi bassiz Avalonia konagi ile olculdu: kurulan seritte iki
dugme var, "bir daha sorma" dugmesine basilinca `IsVisible` yanlisa donuyor ve ret
gecici `settings.json` dosyasinda kalici oluyor.

**Olcemedim.** Gercek Explorer'da seridin ekranda gorunusu ve gercek `HKCU` agacina
uretim yolundan yazilan kayit — bu turda gercek kayit defterine yazan hicbir sey
kosturulmadi.

