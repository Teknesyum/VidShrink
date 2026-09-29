# Varsayılan atama — AV ölçümü

Ölçülen dosya: PS-SFTA `SFTA.ps1` (master)
SHA256: `3eb6f6dee3fd8c91604042060b9d658f08ec85d3fd0a14769119dfd78bc30851`
Boyut: 23.897 B
Tarih: 2026-09-29

## VirusTotal

- Statik motorlar: **0/61** — Microsoft dahil hiçbiri malware saymadı → statik imza (bayrak b) temiz.
- Crowdsourced Sigma (davranışsal proxy): **2 MEDIUM, 1 LOW**
  - MEDIUM — "Change PowerShell Policies to an Insecure Level" (executionpolicy bypass). Gerçek: scripti koşmak için gereken desen (bayrak a).
  - MEDIUM — "Manipulation of User Computer or Group Security Principals Across AD". Sigma yanlış-pozitifi: SID/DomainSID API kullanımına ötüyor, gürültü.
  - LOW — "User with Privileges Logon". Jenerik, gürültü.

## Sonuç

Statik: temiz. Davranışsal: düşük/orta birkaç ping, biri yanlış-pozitif. Malware değil.
VT statiktir — SmartScreen itibarı ve VidShrink.exe→powershell zinciri buraya girmez;
onlar imzalama ve tasarımla (yönlendirme butonu) çözülür.

## Süre

`Set-FTA` uzantı başına ~0,6 sn (ilk çağrı ~0,96 sn); 60 uzantı ~36 sn. Maliyet pinli
SFTA.ps1'in içinde (her çağrıda Shell32.dll okuması, hash'in betikte hesabı, çağrı başına
`SHChangeNotify`); komut bu yüzden `Write-Progress` ile ilerleme gösterir.
