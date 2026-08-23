#!/bin/sh
set -eu

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

runtime_identifier() {
    machine=$(uname -m)
    case $(uname -s) in
        Darwin)
            case "$machine" in
                arm64|aarch64) printf 'osx-arm64\n' ;;
                *) printf 'osx-x64\n' ;;
            esac
            ;;
        Linux)
            case "$machine" in
                aarch64|arm64) printf 'linux-arm64\n' ;;
                armv7l|armv7) printf 'linux-arm\n' ;;
                *) printf 'linux-x64\n' ;;
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

find_dotnet_sdk8() {
    for candidate in "$(command -v dotnet || true)" "$HOME/.dotnet/dotnet" '/usr/local/share/dotnet/dotnet' '/usr/share/dotnet/dotnet'; do
        [ -n "$candidate" ] || continue
        [ -x "$candidate" ] || continue
        if "$candidate" --list-sdks 2>/dev/null | grep -q '^8\.'; then
            printf '%s\n' "$candidate"
            return 0
        fi
    done
    return 1
}

# `dotnet` bir sembolik bag olabilir: /usr/bin/dotnet -> /usr/share/dotnet/dotnet.
# O durumda dirname yanlis kok verir. Koku ikilinin yolundan degil, dotnet'in kendi
# bildirdigi SDK klasorunden turet: "8.0.424 [/usr/share/dotnet/sdk]" -> /usr/share/dotnet
dotnet_root_of() {
    sdk_dir=$("$1" --list-sdks 2>/dev/null | sed -n 's/.*\[\(.*\)\]$/\1/p' | head -n 1)
    if [ -n "$sdk_dir" ]; then
        dirname "$sdk_dir"
    else
        dirname "$1"
    fi
}

install_dotnet_sdk8() {
    install_directory="$HOME/.dotnet"
    bootstrapper="$work_root/dotnet-install.sh"
    curl -fsSL 'https://dot.net/v1/dotnet-install.sh' -o "$bootstrapper"
    sh "$bootstrapper" --channel '8.0' --install-dir "$install_directory" --no-path >&2

    executable="$install_directory/dotnet"
    [ -x "$executable" ] || fail '.NET 8 SDK kurulumdan sonra bulunamadı.'
    printf '%s\n' "$executable"
}

require_command uname
require_command curl
require_command tar

runtime=$(runtime_identifier)

case "$install_root" in
    "$HOME/.local/share"/?*) : ;;
    *) fail "Güvenlik nedeniyle kurulum yolu ~/.local/share altında olmalıdır: $install_root" ;;
esac

say 'VidShrink kurulumu hazırlanıyor...'
require_ffmpeg

work_root=$(mktemp -d 2>/dev/null || mktemp -d -t vidshrink-install)
trap 'rm -rf "$work_root"' EXIT INT TERM

extract_root="$work_root/source"
publish_root="$work_root/publish"
mkdir -p "$extract_root" "$publish_root"

if dotnet=$(find_dotnet_sdk8); then
    :
else
    say '.NET 8 SDK yükleniyor (kullanıcı dizini, yönetici gerekmez)...'
    dotnet=$(install_dotnet_sdk8)
fi
DOTNET_ROOT=$(dotnet_root_of "$dotnet")
export DOTNET_ROOT
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

say 'Güncel VidShrink kaynakları indiriliyor...'
curl -fsSL 'https://github.com/Teknesyum/VidShrink/archive/refs/heads/main.tar.gz' -o "$work_root/source.tar.gz"
tar -xzf "$work_root/source.tar.gz" -C "$extract_root"

source_root=''
for directory in "$extract_root"/*/; do
    [ -d "$directory" ] || continue
    source_root=${directory%/}
    break
done
[ -n "$source_root" ] || fail 'İndirilen kaynak paketi açılamadı.'

say "VidShrink Release sürümü yayımlanıyor ($runtime)..."
"$dotnet" publish "$source_root/src/VidShrink.App/VidShrink.App.csproj" \
    --configuration Release --runtime "$runtime" --self-contained true \
    -p:PublishSingleFile=false --output "$publish_root" \
    || fail 'VidShrink derlenemedi.'

rm -rf "$install_root"
mkdir -p "$install_root" "$bin_directory"
cp -R "$publish_root/." "$install_root/"

installed_executable="$install_root/VidShrink.App"
[ -f "$installed_executable" ] || fail 'Kurulan VidShrink.App bulunamadı.'
chmod +x "$installed_executable"
ln -sf "$installed_executable" "$bin_directory/vidshrink"

say "VidShrink kuruldu: $install_root"
case ":${PATH}:" in
    *":$bin_directory:"*) say 'Çalıştırmak için: vidshrink' ;;
    *) say "Çalıştırmak için: $bin_directory/vidshrink ($bin_directory henüz PATH içinde değil)" ;;
esac
