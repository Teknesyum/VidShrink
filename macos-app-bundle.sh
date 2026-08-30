#!/bin/sh
set -eu

# Yayın çıktısını bir macOS uygulama paketine sarar ve ad-hoc imzalar.
#
#     macos-app-bundle.sh <yayın dizini> <çalıştırılabilir adı> <sürüm> <paket yolu>
#
# Paket neden gerekli: düz bir Unix ikilisinin Dock kimliği yoktur, menü çubuğunda
# "Avalonia Application" yazar ve Finder'dan çift tıklanmaz. Neden yerelde üretiliyor:
# yerelde üretilen paket karantina özniteliği almaz, bu yüzden noterlenmemiş ad-hoc
# imza ile açılır. İndirilmiş bir pakette aynısı Gatekeeper'a takılırdı.
#
# install-vidshrink.sh bunu yayın arşivinin içinden çağırır; arşive VidShrink.App.csproj
# koyar, böylece betik her zaman sardığı yayınla aynı sürümdendir.

[ $# -eq 4 ] || { printf 'Kullanım: %s <yayın dizini> <çalıştırılabilir adı> <sürüm> <paket yolu>\n' "$0" >&2; exit 2; }

payload=$1
executable=$2
version=$3
bundle=$4

[ -d "$payload" ] || { printf 'Yayın dizini yok: %s\n' "$payload" >&2; exit 1; }
[ -f "$payload/$executable" ] || { printf 'Çalıştırılabilir yok: %s\n' "$payload/$executable" >&2; exit 1; }

# Paketin içindeki ad her zaman VidShrink. Adı .App ile biten bir dosyayı çekirdek
# paket sanıp öldürüyor (docs/macos-ilk-kosum.md); yerel başlatıcı beklediği derleme
# adını kendi içinde taşıdığı için yeniden adlandırma onu bozmuyor.
host='VidShrink'

rm -rf "$bundle"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"

cp -R "$payload/." "$bundle/Contents/MacOS/"
[ "$executable" = "$host" ] || mv "$bundle/Contents/MacOS/$executable" "$bundle/Contents/MacOS/$host"
chmod +x "$bundle/Contents/MacOS/$host"

# Simge uygulamanın kendi görselinden üretiliyor; depoda .icns yok ve dışarıdan görsel
# getirilmiyor. sips ile iconutil her macOS'ta kurulu. Görsel yayında yoksa paket
# simgesiz kalır — eksik simge kurulumu durdurmaz.
icon_source="$payload/VidShrink.png"
if [ -f "$icon_source" ] && command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1; then
    iconset=$(mktemp -d)/VidShrink.iconset
    mkdir -p "$iconset"
    for size in 16 32 128 256 512; do
        sips -z $size $size "$icon_source" --out "$iconset/icon_${size}x${size}.png" >/dev/null 2>&1
        sips -z $((size * 2)) $((size * 2)) "$icon_source" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null 2>&1
    done
    iconutil -c icns "$iconset" -o "$bundle/Contents/Resources/$host.icns"
    rm -rf "$(dirname "$iconset")"
fi

# Sürüm iki alana da olduğu gibi yazılıyor; çağıran onu yayın etiketinden okuyor,
# etiket de Directory.Build.props'taki <Version> ile eşitleniyor (release.yml).
cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>VidShrink</string>
    <key>CFBundleDisplayName</key>
    <string>VidShrink</string>
    <key>CFBundleIdentifier</key>
    <string>com.teknesyum.vidshrink</string>
    <key>CFBundleExecutable</key>
    <string>$host</string>
    <key>CFBundleIconFile</key>
    <string>$host</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$version</string>
    <key>CFBundleVersion</key>
    <string>$version</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

# İmza en sonda: Contents/ mühürleniyor, mühürden sonra pakete dosya eklenirse ya da
# içindeki bir dosya değişirse paket hiç açılmaz.
if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$bundle" >/dev/null 2>&1 || \
        { printf 'Paket imzalanamadı: %s\n' "$bundle" >&2; exit 1; }
fi
