# Devir notu — 8 Eylul 2026, 02:00

Kullanici "durdur da sonra devam ederiz" dedi. Iki yapici tur ortasinda durduruldu;
isleri **kaybolmadi**, dallara commit'lendi ve itildi.

## Durum

`main` = `cf42e5d7`. Bugun birlesenler: T188 (iki tur), T191.

| Dal | Commit | Durum |
|---|---|---|
| `T189-tur2` | `6b23c47e` | Yapici "temizlik, commit, push" adimindayken durduruldu. Agac temiz. **Denetlenmedi.** |
| `T192-kare-kusurlari` | `1951e52c` | Derleme adiminda durduruldu. Yarim isi WIP commit'i olarak duruyor. **Denetlenmedi.** |

## Devam dendiginde kosulacak dort adim

1. **T189 tur 2'yi dogrula.** Sozlesme `contracts/T189.md` "## Tur 2" bolumu, K8-K11.
   Commit mesaji "ayirici artik ortada, onizleme karesi belirlenimli" diyor — **iddia,
   olcum degil.** K9 uc kosumluk sha256 tablosu istiyor; tablo yoksa denetci acilmadan
   once yapiciya tamamlatilir. Sonra `auditor` tipinde denetci.

2. **T192'yi bitir.** Yarim. **Bilinen sorun:** `MainWindow.axaml.cs` degistirilmis ama
   `owns` listesinde yok (`owns`ta `MainWindow.axaml` var, `.cs` yok). Ya `owns`
   genisletilir (hata sozlesmede ise T0 bunu yazili kabul eder) ya degisiklik geri alinir.

3. **Kareleri T0 yeniler.** T189 tur 2 ve T192 birlestikten sonra
   `dotnet run --project tools/VidShrink.Shot`. Bu, T189'un K5'inde "T0'a birakildi"
   diye isaretli.

4. **T190 README** (EN + TR, mermaid semalari, yeni kareler) → fable denetler →
   **0.3.1 kesilir**. Kullanicinin cumlesi: "0.3.1 surumunu de kes bunlar bitince".

## 0.3.1'e girecekler

- T176 (oynatici girdisi), T185 (oynatici sekmesi en sola), T188 (kabuk entegrasyonu),
  T191 (kabuk klasoru guncelleyiciyle gidiyor).
- `v0.3.0` etiketi `3a6351a7`'de; bunlarin hicbiri yayindaki ikilide yok.

## Kapanmayan borc — bildirilmesi gereken

Kabuk paketini isletim sistemine tanitan tek yer `Install-VidShrink.ps1`. Uygulamada ya
da baslaticida `Add-AppxPackage -Register` cagiran kod yok. T191 dosyalari indiriyor ama
kaydi yapmiyor; **asil kilit imzalama** — T188 uretimde `0x800B0100` olctu. 0.3.1 bunu
kapatmiyor ve kullaniciya bu haliyle soylendi.

## Raftakiler

- **Maks sikistirma modu.** Kullanici erteledi: "4-8 saat olmaz gece calismani
  istemiyorum sonra musait zamanda bakalim hatirlatta". **Hatirlatmak T0'in isi** —
  gunduz, kendiliginden. Hazirlik bitti; olculecek iki sey `docs/plan.md`'de.
- T186 (ayar arayuzu yeniden tasarimi), T187 (tasma teklifi, %3, dort secenek).
