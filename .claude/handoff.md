# Devir — 2026-09-09

Dal: `claude/tema-paleti` (origin'e itildi, uç `097b141`).
`main`e göre 55 commit ileri, 16 commit geri. PR açılmadı.

Sıradaki turda önce `git pull`, sonra aşağıdaki "Sıradaki iş" listesinden sür.

## Ne bitti

Plan `docs/plan.md`'nin üç adımı da kapandı:

- **A — Dil seçici.** Üst şeritte yalnız `Strings.ShortcutLanguages` (en, tr) + ayar
  tekerleği; tam liste ayarlardaki açılır kutuda. (`4dc3477`, `c4e6b1e`)
- **B — Yirmi tema.** Palet dosyaları `Themes/Palette/` altında, ayardan seçiliyor,
  çalışırken değişiyor. (`acb23db`, `aecc1b2`, `368777d`, `fca9ea2`)
- **C — Diller.** 40 yeni dil indi; depoda **42 dil klasörü** var
  (`src/VidShrink.App/Locales/`), her biri dört dosya ve **488 anahtar**.
  Denetim: `python tools/dil-denetim.py` → hepsi
  `TAMAM anahtar 488/488 eksik 0 fazla 0 yertutucu 0`.
  Kanıt ve maliyet: `docs/olcumler/diller.md` (dil başına ~93 bin belirteç,
  kırk dil ~3,7 milyon).
- **Kalan iki dilli dikiş** kapandı: motorda İngilizce/Türkçe gömülü cümle yok. (`68d8e3a`)
- **Sağdan sola.** `Strings.RightToLeftLanguages` = ar, fa, he, ur.
  `MainWindow` kurulumda ve her dil değişiminde, `ShrinkJobWindow` açılışta
  `FlowDirection`'ı buna bağlıyor. (`097b141`)

## Testler

```
dotnet test --filter "FullyQualifiedName~LocalizationTests|FullyQualifiedName~LanguageTests"
Başarılı!  - Başarısız: 0, Başarılı: 138, Atlanan: 0, Toplam: 138, Süre: 20 s
```

Tam koşum (`dotnet test`, ~20 dk): 1935 başarılı / 18 atlanan / **1 başarısız**.
Tek kırmızı: `VidShrink.Tests.OynaticiBoruTests_DecoderPipe.Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir`
(`OynaticiBoruTests.cs:379`, `Process.get_HasExited()` → "Erişim engellendi").
**Bu kırmızı dil işinden önce de vardı**, bu daldaki değişikliklerden değil.

## Sıradaki iş

1. **`TipOverflowTests` yalnız EN/TR ölçüyor.** 42 dilde balon/ipucu taşması var mı
   bakılmadı. Ölçüyü genişlet ya da uzun metinleri kısalt.
2. **`KillTree` kırmızısı.** Yukarıdaki tek başarısız test; ayrı iş, dil işiyle ilgisiz.
3. **Dalın geleceği.** `main`in 16 commit gerisinde. Birleştirme kararı T0'ın:
   ya `main`den rebase, ya PR. Bu dal `main`e kendiliğinden birleşmez.
4. Motor yol haritası (`docs/tasks/yol-haritasi.md`) P0'dan itibaren dokunulmadı.

## Çalışma kalıntısı

`.calisma/` (git dışı, ~6 GB) eski ölçüm turlarını taşıyor: `T158 T171 T176 T189*
T192* gorunum kaynak kaynak-genis t57 t58 t60 t61 t63 test-ciktilari`.
Dil işi buraya kalıcı hiçbir şey bırakmadı; boş klasörler silindi.
Rapora giren sayı `docs/olcumler/diller.md`'ye, düzenek `tools/dil-denetim.py`'ye taşındı.
`.calisma/`yi boşaltmak kullanıcının kararı.

## Kurallar (bu depoda unutulmasın)

- Kod yorumu yazma. Renk/ölçü uydurma.
- `main`e yalnız T0 birleştirir; kendi dalında çalış.
- Alt ajana arka plan komutu verme — ajanın turunu öldürüyor.
- Kabuk Windows PowerShell 5.1: `&&`, `||`, ternary yok.
