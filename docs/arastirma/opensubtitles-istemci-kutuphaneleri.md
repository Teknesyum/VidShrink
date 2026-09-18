# OpenSubtitles REST v1 — açık kaynak istemcilerden çıkarılan sözleşme

Araştırma ajanının ham bulgusu (2026-09-18). `api.opensubtitles.com`a hiç istek atılmadı;
hepsi `raw.githubusercontent.com` ve `api.github.com` üzerinden. P28 denetiminin girdisidir.

Resmî OpenAPI (Stoplight) şartnamesi birebir bir depoda duruyor:
`https://github.com/phdelodder/SubTools/blob/master/MultiSubDownloader/src/main/resources/opensubtitles/open-api.json`
(başlık `"OpenSubtitles API"`, sürüm `1.0.1`).

## Kullanılan depolar

| kütüphane | dosya |
|---|---|
| Jellyfin eklentisi (C#) | `jellyfin/jellyfin-plugin-opensubtitles` |
| bazarr sağlayıcı (py) | `morpheus65535/bazarr` `custom_libs/subliminal_patch/providers/opensubtitlescom.py` |
| subliminal sağlayıcı (py) | `Diaoul/subliminal` `src/subliminal/providers/opensubtitlescom.py` |
| python `opensubtitlescom` | `dusking/opensubtitles-com` |
| node `opensubtitles.com` | `vankasteelj/opensubtitles.com` |
| resmî VLSub (Lua) | `opensubtitles/vlsub-opensubtitles-com` |
| Go istemci fikstürleri | `TheForgotten69/go-opensubtitles` |
| VCR kaseti (gerçek trafik) | `Diaoul/subliminal` `tests/cassettes/opensubtitlescom/test_download_subtitle.yaml` |

## 1. Taban adres ve vip-api

```json
[{"url": "https://api.opensubtitles.com/api/v1", "description": "Default server"},
 {"url": "https://vip-api.opensubtitles.com/api/v1", "description": "VIP server"}]
```

Şartnamenin `/login` açıklaması: "Further API requests must continue on returned `base_url`
host... If `base_url` equals `vip-api.opensubtitles.com` make sure you always send with every
request JWT token (if available), otherwise request might fail with 4xx code."

`/login` 200 şemasında `base_url` enum `["api.opensubtitles.com", "vip-api.opensubtitles.com"]`,
varsayılan `api.opensubtitles.com`. Bunu doğru uygulayan tek istemci bazarr:

```python
self.server_hostname = r.json()['base_url']
finally:
    if self.server_hostname.startswith('vip'):
        self.session.headers.update({'Authorization': 'Bearer ' + self.token})
    else:
        self.session.headers.pop('Authorization', None)
```

Jellyfin `base_url`u tamamen yok sayıyor. Token ömrü: bazarr 12 sa, subliminal 24 sa,
Jellyfin JWT `exp` alanını okuyor.

## 2. Başlıklar

```json
"Api-Key": {"type":"apiKey","name":"Api-Key","in":"header"},
"Bearer":  {"type":"http","scheme":"bearer","bearerFormat":"JWT"}
```

`User-Agent` her uç noktada bildirilmiş bir **parametre**, açıklaması `<<{{APP_NAME}} v{{APP_VERSION}}>>`.

Gerçek tel kaydı (subliminal kaseti, POST /download):

```
Accept: */*
Accept-Encoding: gzip, deflate
Api-Key: <subliminal-anahtari-maskelendi>
Authorization: Bearer <subliminal-belirteci-maskelendi>
Content-Type: application/json
User-Agent: Subliminal v2.2
```

Sunucunun kabul ettiği küme:
`Access-Control-Allow-Headers: Origin, Authorization, Accept, Api-Key, Content-Type, X-User-Agent`

Tuzak: python `opensubtitlescom` anahtarı **`API-Key`** yazıyor ve `authorization`a `Bearer `
ön ekini koymuyor.

## 3. GET /subtitles parametreleri

```
ai_translated, episode_number, foreign_parts_only, hearing_impaired, id, imdb_id,
languages, machine_translated, moviehash, moviehash_match, order_by, order_direction,
page, parent_feature_id, parent_imdb_id, parent_tmdb_id, query, season_number,
tmdb_id, trusted_sources, type, user_id, year
```

Varsayılanlar: `type=all`, `hearing_impaired=include`, `foreign_parts_only=include`,
`trusted_sources=include`, `machine_translated=exclude`, `ai_translated=include`,
`moviehash_match=include`.

`languages`: virgülle ayrık, **alfabetik sıralı**, küçük harf; kodlar OpenSubtitles kümesi
(çoğu ISO 639-1, `pt-br`, `zh-cn`, `es-mx` gibi bölgesel varyantlarla).

```python
criterion.update({'languages': ','.join(sorted(lang.opensubtitlescom for lang in languages))})
```

**Yönlendirmeden kaçınma** tekrar eden tema. Şartname: "Avoid http redirection by sending
request parameters sorted and without default values, and send all queries in lowercase.
Remove leading zeroes in ID parameters". Kaset bunu kanıtlıyor: `...&page=1&query=man+of+steel`
→ **301**, varsayılan `page=1` atılınca → 200.

```python
params = dict(sorted(params.items())) if params else {}
params = {k.lower(): (v.lower() if isinstance(v, str) else v) for k, v in params.items()}
```

Jellyfin hem anahtarı hem değeri küçültüp sıralıyor. bazarr kimlikleri temizliyor:
`external_id.lower().lstrip('tt').lstrip('0')`.

## 4. Yanıt modeli

`attributes` zorunlu alanlar: `subtitle_id, language, download_count, new_download_count,
from_trusted, foreign_parts_only, ai_translated, machine_translated, upload_date,
feature_details, url, files`. `files[]` = `{file_id (zorunlu), cd_number, file_name (zorunlu)}`.

`moviehash_match` şemada **yok**; şartname: "If a `moviehash` is sent with a request, a
`moviehash_match` boolean field will be added to the response. The matching subtitles will
always come first in the response."

Telde görülen ama şemada olmayanlar: `slug`, `nb_cd`, `legacy_uploader_id`, `per_page`.

Zarf: `total_pages`, `total_count`, `per_page`, `page`, `data`.

## 5. POST /download

```json
"required": ["file_id"],
"properties": {"file_id": ..., "sub_format": ..., "file_name": ...,
  "in_fps": ..., "out_fps": ..., "timeshift": ..., "force_download": ...}
```

Yanıt (2025 kaseti):

```json
{"link":"https://www.opensubtitles.com/download/163EEDF9.../subfile/....srt",
 "file_name":"man.of.steel.2013.720p.bluray.x264-felony....srt",
 "requests":1,"remaining":19,
 "message":"Your quota will be renewed in 0 hours and 28 minutes (2025-02-13 23:59:59 UTC) ts=1739489491 ",
 "reset_time":"0 hours and 28 minutes","reset_time_utc":"2025-02-13T23:59:59.999Z",
 "uk":"count_uid_699022","uid":699022,"ts":1739489491}
```

**Bearer zorunlu mu?** Şartname: `security: [{"Bearer": []}, {"Api-Key": []}]` ve
"VERY IMPORTANT: In HTTP request must be both headers: Api-Key and Authorization".
Resmî VLSub karar noktasını en net gösteriyor: **arama** anonim çalışıyor
(`"No credentials - using anonymous access"`, `token = ""`), **indirme** anonimde sert
düşüyor (`"Authentication required for download"`). Yani: arama Api-Key ile, indirme Bearer ister.

Dönen `link` düz GET ile, kimliksiz indiriliyor.

## 6. Hata ve bağlantı ömrü

Şartname: indirme adresi **3 saatten** uzun kullanılamaz, önbelleğe alınmaz; indirme sayacı
bu çağrıda artar, dosyanın kendi indirilişinde değil; dosya hep UTF-8.

bazarr'ın durum haritası en eksiksizi:

```python
400 ConfigurationError | 401 token sifirla, bir kez yeniden giris | 403 ProviderError
406 DownloadLimitExceeded (requests/remaining/reset_time govdeden)
410 "Download link has expired" | 429 TooManyRequests | 502 APIThrottled (15 sn uyu, bir kez yenile)
500-599 ProviderError
```

subliminal'in haritası kısmen **eski** (406'yı kota değil oturum sayıyor); kotayı gövdeden
okuyor: `remaining <= 0` → `DownloadLimitReached`. Jellyfin 406'yı yalnız `Remaining <= 0`
iken kota sayıyor, 401'de JWT'yi atıp yeniden deniyor, 429'u `Retry-After`a uyarak 5 kez,
502'yi 500 ms sonra yineliyor.

Hız sınırı: şartname `/login` için "1 request per 1 second"; gerçek yanıt başlıkları
`RateLimit-Limit: 5`, `X-RateLimit-Limit-Second: 5`. Jellyfin istemci tarafında
`PermitLimit = 5, Window = 1 sn` ile aynasını kuruyor.

## 7. Kota sayıları

Şartname örneği (ücretsiz hesap): `allowed_downloads: 100`, `level: "Sub leecher"`, `vip: false`
— bunlar şartname **örneği**, güncel garanti değil. `opensubtitles-dev/opensubs-cli` README'si
"20 downloads per day with a free account" diyor; 2025 kaseti `"remaining":19` ile bunu
doğruluyor (izin 20).

**DOĞRULANMADI:** anonim (girişsiz) indirme kotasının tam sayısı — hiçbir istemci sabitlemiyor.
**DOĞRULANMADI:** VIP izninin tam sayısı.

## Hedef listesine iki düzeltme

- **Stremio `opensubtitles-v3` bu API değil.** `https://opensubtitles-v3.strem.io/subtitles/`
  adresine, kimliksiz, `movie/tt0770828.json` biçiminde konuşuyor ve eski `.org` derleminden
  doğrudan dosya adresleri döndürüyor. REST v1 isteniyorsa yanlış sağlayıcı.
- **`pyopensubtitles` / `agonzalezro/python-opensubtitles` eski XML-RPC `.org` API'si** — v1 değil.
  Bu API'nin pypi karşılığı `opensubtitlescom` = `dusking/opensubtitles-com`. VLSub'da da ölü
  bir XML-RPC bloğu (`"406 No session"`) duruyor, v1 yolu değil.
