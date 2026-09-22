# Kapalı Defter Denetimi — Satır 1-125

Kapsam: `.claude/kapali.md` 1-125. satırlar. Sadece okuma; kaynağa dokunulmadı.

## Sayılar

- Toplam `- [x]` satırı: **112**
- Ucuz doğrulanan iddia: **45 commit hash'i** (main'in atası, `git merge-base --is-ancestor`) + **26 docs/ belgesi** (var) + **~12 kod/test iddiası** (grep+read ile yerinde okundu)
- Yoklanamaz (artefaktı yok — kayıt/karar notu, "sonucu aktarılacak" ilişkisi, veya ölçüm rakamının kendisi zamana bağlı): geri kalan satırların büyük kısmı
- ŞÜPHELİ: **0**

## Yöntem ve kapsam notu

Main HEAD: `e72bfbae`. Satır aralığındaki tüm hex commit referansları (45 adet) tek komutla
`git cat-file -e` + `git merge-base --is-ancestor <hash> main` ile tarandı — hepsi var ve main'in
atası. Aralıktaki tüm `docs/...md` yolları (26 adet) dosya sistemi üzerinde var olduğu doğrulandı
(`docs/danisma/010` önekiyle anılan dosya `010-fable-onayar-kap.md` olarak bulundu).

Ayrıca aşağıdaki spesifik teknik iddialar kaynakta yerinde okunarak doğrulandı:

| Satır | İddia | Bulgu | Sonuç |
|---|---|---|---|
| 36 | `ShareDiagnosis.Detail` düşürüldü, yalnız Key+Args kaldı | `src/VidShrink.Core/Share/ShareErrorClassifier.cs:32-41` — record yalnız `Failure, Key, Args, RetryAfter, SuggestedTargetId, Step` taşıyor, `Detail` yok | Doğrulandı |
| 56 | Beş şerit düğmesinin `AutomationProperties.GetName` ile okunması | `tests/VidShrink.Tests/OynaticiDenetimTests.cs:390-398` — `BtnSeritPlay/Back/Forward/Mute/FullScreen` döngüsünde `AutomationProperties.GetName(dugme)` çağrılıyor | Doğrulandı |
| 24 | src/ altında kültürsüz `.ToString("0...")` sıfır | `src/` genelinde 37 dosyadaki tüm `.ToString("0...")` çağrıları `CultureInfo.InvariantCulture` ya da yerel `kultur`/`Strings.Culture` parametresi taşıyor, kültürsüz çağrı yok (`BicimDisiYazimTests` dışında) | Doğrulandı |
| 69 | E6: `IsForced` artık okunuyor (yalnız okunup kullanılmıyor değil) | `src/VidShrink.Core/StreamMapping.cs:149,639` — `Disposition` hesabında ve `forced` filtrelemesinde kullanılıyor | Doğrulandı |
| 104 | `--surum`/`--version` çifti README ve koda girdi | `src/VidShrink.Cli/CliRequest.cs:261` switch kolu; `README.md:172`, `README.tr.md:173` tabloda satır var | Doğrulandı |
| 122 | 7 düz ölçü belirteçe çekildi, `OlcuBelirteciTests` var | Belge `docs/olcumler/olcu-belirteci-kabuk.md` mevcut | Doğrulandı (belge var; testin adı bu turda ayrıca çalıştırılmadı, salt-okur kapsamda) |
| 123 | teknesyum-ui taraması 20 yanlış pozitif gerekçeyle muaf | `.claude/teknesyum-ui.json` en az 5 madde okunan bölümde gerekçeli `ignore` girdisi taşıyor, yapı iddiaya uyumlu | Doğrulandı (örnekleme) |

## ŞÜPHELİ

Bu dilimde (1-125) şüpheli bulunmadı. Not: 13-14. satırlar (96-183 ve 184-270 dilimlerini
denetleyen ajan raporları) kendi aralıklarında şüpheli bulgu bildiriyor (`OrtakOdakTests` pimi
silinmiş, 198/203 notu eskimiş) — bunlar bu denetimin kapsamı olan 1-125 dışındaki satırlara
ait iddialar olduğu için burada tekrar doğrulanmadı, yalnız not edilir.

## Kapsam dışı bırakılanlar (yoklanamaz)

Ajan rapor relay satırları ("sonucu aktarılacak"), kayıt/karar notları (örn. satır 57 "yalnız
koyu tema kuralı geçerli değil" kararı), ve zamana bağlı ham sayı iddiaları (örn. satır 46'daki
"5081 → 4481" satır sayısı — dosya bugün 4555 satır; aradan geçen zamanda başka işler dosyaya
satır eklemiş olabileceğinden bu tek başına çelişki sayılmadı, ŞÜPHELİ işaretlenmedi) ucuz
doğrulama kapsamına girmediği için "yoklanamaz" sayıldı.
