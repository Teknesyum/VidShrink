# Kodek matrisi ölçüm düzeneği

Auto modunun kodek ve çözünürlük kararlarını kalibre eden bash betikleri. Hepsi
proje kökünden koşar, çıktıyı `.calisma/kodek-matris/` altına yazar.

| betik | ne ölçer |
|---|---|
| `kos-matris.sh` | kodek × içerik × hedef matrisi (x264 / x265 / AV1) |
| `kos-vmaf.sh` | matris çıktılarının VMAF-NEG harmonik ortalaması |
| `kos-rc.sh` | variance-boost yalıtımı: preset 4/6, VB açık/kapalı, sabit hedef |
| `kos-vmaf-rc.sh` | `kos-rc.sh` çıktılarının VMAF'ı |
| `kos-taban.sh` | CRF 63 azami-q çıktısı — parametre kümesinin bit talebi |
| `kos-kucultme.sh` | tek değişken çözünürlük, kodek ve VB sabit |
| `kos-tavan.sh` | çözünürlük tavanı: indir → geri çık → VMAF, kodlama yok |

Sonuçlar `docs/olcumler/kodek-matris.md`, ham günlükler
`docs/olcumler/kodek-matris-ham/`. Danışma yazışması `docs/danisma/001..003`.

**Kaynaklar `.calisma/kaynak/` ve `.calisma/kaynak-genis/` altında ve git'e girmez.**
Betikler o yolları sabit yazıyor; başka makinede koşmadan önce kaynakları yerleştir.

ffmpeg çağrıları **sıralı** koşar. İki kodlamayı aynı anda başlatma — süre ve kalite
sayılarının ikisi de bozulur.
