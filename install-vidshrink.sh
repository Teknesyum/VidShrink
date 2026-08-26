#!/bin/sh
set -eu

repository='Teknesyum/VidShrink'
install_root=${VIDSHRINK_INSTALL_ROOT:-"$HOME/.local/share/vidshrink"}
bin_directory="$HOME/.local/bin"

say() {
    printf '%s\n' "$1"
}

fail() {
    printf '%s\n' "$1" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "$1 bulunamadı. Kurulum için gereklidir."
}

# Yayında dört hedef var: win-x64, osx-arm64, osx-x64, linux-x64. Başka bir mimaride
# yanlış arşivi sessizce kurmak yerine burada duruluyor.
runtime_identifier() {
    machine=$(uname -m)
    case $(uname -s) in
        Darwin)
            case "$machine" in
                arm64|aarch64) printf 'osx-arm64\n' ;;
                x86_64) printf 'osx-x64\n' ;;
                *) fail "Bu mimari için yayın yok: $machine. macOS'ta yalnız osx-arm64 ve osx-x64 yayımlanıyor." ;;
            esac
            ;;
        Linux)
            case "$machine" in
                x86_64) printf 'linux-x64\n' ;;
                *) fail "Bu mimari için yayın yok: $machine. Linux'ta yalnız linux-x64 yayımlanıyor." ;;
            esac
            ;;
        *)
            fail "Desteklenmeyen işletim sistemi: $(uname -s). Windows için Install-VidShrink.ps1 kullanın."
            ;;
    esac
}

ffmpeg_install_command() {
    if [ "$(uname -s)" = 'Darwin' ]; then
        printf 'brew install ffmpeg\n'
    elif command -v apt-get >/dev/null 2>&1; then
        printf 'sudo apt install ffmpeg\n'
    elif command -v dnf >/dev/null 2>&1; then
        printf 'sudo dnf install ffmpeg\n'
    elif command -v pacman >/dev/null 2>&1; then
        printf 'sudo pacman -S ffmpeg\n'
    elif command -v zypper >/dev/null 2>&1; then
        printf 'sudo zypper install ffmpeg\n'
    fi
}

require_ffmpeg() {
    if command -v ffmpeg >/dev/null 2>&1 && command -v ffprobe >/dev/null 2>&1; then
        return 0
    fi

    say 'FFmpeg ve FFprobe bulunamadı. VidShrink bunları kendisi kurmaz.'
    install_command=$(ffmpeg_install_command)
    if [ -n "$install_command" ]; then
        say 'Şu komutu çalıştırıp kurulumu yeniden başlatın:'
        say ''
        say "    $install_command"
        say ''
    else
        say 'Paket yöneticinizle ffmpeg paketini kurup kurulumu yeniden başlatın.'
    fi
    exit 1
}

sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | cut -d' ' -f1
    else
        shasum -a 256 "$1" | cut -d' ' -f1
    fi
}

# checksums-<rid>.txt sha256sum biçimindedir: özet, iki boşluk, varlık adı.
expected_sha256() {
    awk -v name="$2" '$NF == name || $NF == "*" name { print $1; exit }' "$1"
}

assert_checksum() {
    expected=$(expected_sha256 "$checksums_file" "$1")
    [ -n "$expected" ] || fail "Sağlama listesinde $1 yok; indirilen dosya doğrulanamıyor."
    actual=$(sha256_of "$2")
    [ "$expected" = "$actual" ] || \
        fail "$1 sağlaması tutmuyor. Beklenen $expected, bulunan $actual. Kurulum durduruldu."
}

download_asset() {
    curl -fsSL "https://github.com/$repository/releases/download/$tag/$1" -o "$2" || \
        fail "Yayın varlığı indirilemedi: $1"
}

require_command uname
require_command curl
require_command unzip
require_command awk
require_command sed

runtime=$(runtime_identifier)
archive_name="vidshrink-$runtime.zip"
checksums_name="checksums-$runtime.txt"

case "$install_root" in
    "$HOME/.local/share"/?*) : ;;
    *) fail "Güvenlik nedeniyle kurulum yolu ~/.local/share altında olmalıdır: $install_root" ;;
esac

say 'VidShrink kurulumu hazırlanıyor...'
require_ffmpeg

work_root=$(mktemp -d 2>/dev/null || mktemp -d -t vidshrink-install)
trap 'rm -rf "$work_root"' EXIT INT TERM

stage_root="$work_root/stage"
mkdir -p "$stage_root"

say 'Son yayın aranıyor...'
release_json="$work_root/release.json"
curl -fsSL -H 'Accept: application/vnd.github+json' -H 'User-Agent: VidShrink-Installer' \
    "https://api.github.com/repos/$repository/releases/latest" -o "$release_json" || \
    fail 'Yayın bilgisi alınamadı.'

tag=$(sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$release_json" | head -n 1)
[ -n "$tag" ] || fail 'Yayın bilgisi okunamadı: etiket adı yok.'
version=${tag#v}
say "Kurulacak sürüm: $version"

for required in "$archive_name" "$checksums_name"; do
    grep -q "\"name\"[[:space:]]*:[[:space:]]*\"$required\"" "$release_json" || \
        fail "Yayın $tag bu varlığı taşımıyor: $required. Kurulum yapılmadı."
done

archive_file="$work_root/$archive_name"
checksums_file="$work_root/$checksums_name"

say 'Yayın paketi indiriliyor...'
download_asset "$checksums_name" "$checksums_file"
download_asset "$archive_name" "$archive_file"

say 'İndirilenler doğrulanıyor...'
assert_checksum "$archive_name" "$archive_file"

unzip -qo "$archive_file" -d "$stage_root"

# Kurulan sürümün işareti. Windows'ta güncelleyici bunu okuyup arşivin tamamını yeniden
# indirmekten kurtuluyor; burada uygulamanın kurulu sürümü bildirmesi için duruyor.
printf '%s' "$version" > "$stage_root/.update-version"

rm -rf "$install_root"
mkdir -p "$install_root" "$bin_directory"
cp -R "$stage_root/." "$install_root/"

# Burada başlatıcı yok: kendini güncelleme yalnız Windows'ta açık, kısayol doğrudan
# uygulamayı gösterir.
installed_executable="$install_root/VidShrink.App"
[ -f "$installed_executable" ] || fail 'Kurulan VidShrink.App bulunamadı.'
chmod +x "$installed_executable"
ln -sf "$installed_executable" "$bin_directory/vidshrink"

say "VidShrink $version kuruldu: $install_root"
# macOS ve Linux'ta uygulama kendini güncellemez: paket içindeki bir dosya değişince
# Gatekeeper imzası bozulur ve uygulama hiç açılmaz. Güncelleme bu betiği yeniden
# çalıştırmakla olur.
say 'Güncellemek için bu komutu yeniden çalıştırın.'
case ":${PATH}:" in
    *":$bin_directory:"*) say 'Çalıştırmak için: vidshrink' ;;
    *) say "Çalıştırmak için: $bin_directory/vidshrink ($bin_directory henüz PATH içinde değil)" ;;
esac
