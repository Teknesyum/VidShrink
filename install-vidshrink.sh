#!/bin/sh
set -eu

repository='Teknesyum/VidShrink'
install_root=${VIDSHRINK_INSTALL_ROOT:-"$HOME/.local/share/vidshrink"}
bin_directory="$HOME/.local/bin"
bundle_path="$HOME/Applications/VidShrink.app"

say() {
    printf '%s\n' "$1"
}

fail() {
    printf '%s\n' "$1" >&2
    exit 1
}

# Kullanıcıya söylenen ama çıktıya karışmaması gereken satırlar. runtime_identifier'ın
# stdout'u bir değişkene okunuyor; not oraya düşerse mimari adının parçası olur.
note() {
    printf '%s\n' "$1" >&2
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "$1 bulunamadı. Kurulum için gereklidir."
}

data_directory="$HOME/.local/share"
applications_directory="$data_directory/applications"
desktop_entry="$applications_directory/vidshrink.desktop"
mime_directory="$data_directory/mime"
mime_package="$mime_directory/packages/vidshrink.xml"

media_types='mp4 video/mp4
mkv video/x-matroska
mov video/quicktime
avi video/x-msvideo
webm video/webm
wmv video/x-ms-wmv
flv video/x-flv
m4v video/x-m4v
mpg video/mpeg
mpeg video/mpeg
ts video/mp2t
m2ts video/mp2t
3gp video/3gpp
ogv video/ogg
vob video/mpeg
asf video/x-ms-asf
rm application/vnd.rn-realmedia
rmvb application/vnd.rn-realmedia-vbr
divx video/vnd.avi
mxf application/mxf
f4v video/mp4
mts video/mp2t
dav video/x-dav
gif image/gif'

refresh_desktop_databases() {
    if command -v update-mime-database >/dev/null 2>&1 && [ -d "$mime_directory" ]; then
        update-mime-database "$mime_directory" >/dev/null 2>&1 || true
    fi
    if command -v update-desktop-database >/dev/null 2>&1 && [ -d "$applications_directory" ]; then
        update-desktop-database "$applications_directory" >/dev/null 2>&1 || true
    fi
}

exec_argument_escape() {
    printf '%s' "$1" | sed \
        -e 's/\\/\\\\\\\\/g' \
        -e 's/`/\\\\`/g' \
        -e 's/\$/\\\\$/g' \
        -e 's/"/\\\\"/g' \
        -e 's/%/%%/g'
}

write_desktop_entry() {
    executable=$1
    mime_list=$(printf '%s\n' "$media_types" | awk '{ print $2 }' | sort -u | tr '\n' ';')

    mkdir -p "$applications_directory" "$mime_directory/packages"
    icon_line=''
    icon_file="$(dirname "$executable")/VidShrink.png"
    if [ -f "$icon_file" ]; then
        icon_line="Icon=$icon_file"
    fi

    cat > "$mime_package" <<MIME
<?xml version="1.0" encoding="UTF-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
    <mime-type type="video/x-dav">
        <comment>DAV video</comment>
        <glob pattern="*.dav"/>
    </mime-type>
</mime-info>
MIME

    escaped_executable=$(exec_argument_escape "$executable")

    {
        printf '[Desktop Entry]\n'
        printf 'Type=Application\n'
        printf 'Name=VidShrink\n'
        printf 'Comment=Play and shrink videos\n'
        printf 'Exec="%s" %%F\n' "$escaped_executable"
        [ -z "$icon_line" ] || printf '%s\n' "$icon_line"
        printf 'Terminal=false\n'
        printf 'Categories=AudioVideo;Video;Player;\n'
        printf 'MimeType=%s\n' "$mime_list"
    } > "$desktop_entry"

    refresh_desktop_databases
}

remove_desktop_entry() {
    touched=''
    if [ -e "$desktop_entry" ]; then
        rm -f "$desktop_entry"
        removed="$removed$desktop_entry
"
        touched=1
    fi
    if [ -e "$mime_package" ]; then
        rm -f "$mime_package"
        removed="$removed$mime_package
"
        touched=1
    fi
    [ -z "$touched" ] || refresh_desktop_databases
}

# Kurulumun bıraktığı üç iz: uygulama paketi, düz kurulum dizini, PATH'teki kısayol.
# Kısayol yalnız buraya bakıyorsa siliniyor; kullanıcının kendi koyduğu bir vidshrink
# başka bir yeri gösteriyorsa ona dokunulmuyor.
uninstall() {
    removed=''

    if [ -e "$bundle_path" ]; then
        rm -rf "$bundle_path"
        removed="$removed$bundle_path
"
    fi

    if [ -e "$install_root" ]; then
        rm -rf "$install_root"
        removed="$removed$install_root
"
    fi

    remove_desktop_entry

    link="$bin_directory/vidshrink"
    if [ -L "$link" ]; then
        target=$(readlink "$link")
        case "$target" in
            "$bundle_path"/*|"$install_root"/*)
                rm -f "$link"
                removed="$removed$link
" ;;
        esac
    fi

    if [ -z "$removed" ]; then
        say 'Kaldırılacak bir kurulum bulunamadı.'
    else
        printf 'Silindi:\n%s' "$removed"
    fi
}

case "$install_root" in
    "$HOME/.local/share"/?*) : ;;
    *) fail "Güvenlik nedeniyle kurulum yolu ~/.local/share altında olmalıdır: $install_root" ;;
esac

case "${1:-}" in
    '') : ;;
    --uninstall) uninstall; exit 0 ;;
    --desktop-entry)
        [ -n "${2:-}" ] || fail 'Kullanım: --desktop-entry <çalıştırılabilir yolu>'
        write_desktop_entry "$2"
        say "Masaüstü kaydı yazıldı: $desktop_entry"
        exit 0
        ;;
    *) fail "Bilinmeyen seçenek: $1. Kaldırmak için --uninstall kullanın." ;;
esac

# Yayında dört hedef var: win-x64, osx-arm64, osx-x64, linux-x64. Başka bir mimaride
# yanlış arşivi sessizce kurmak yerine burada duruluyor.
#
# Boş okuma bundan ayrı bir durum. uname boş dönerse eski hâli boş değeri
# "desteklenmeyen mimari" sayıp reddediyordu — Windows kurucusunu düşüren tuzağın aynısı.
# Boş bir değer artık kullanıcıya basılmıyor, okunamadığı söyleniyor. Linux'ta yayın tek:
# okunamayan mimaride durmak yerine linux-x64 varsayılıp varsayıldığı söyleniyor. macOS'ta
# iki yayın var, arm64 ile x64 arasında varsayım yapılamaz; orada kurulum duruyor.
runtime_identifier() {
    system=$(uname -s 2>/dev/null || true)
    machine=$(uname -m 2>/dev/null || true)
    if [ -z "$machine" ]; then
        machine=$(uname -p 2>/dev/null || true)
    fi

    case "$system" in
        Darwin)
            case "$machine" in
                arm64|aarch64) printf 'osx-arm64\n' ;;
                x86_64|amd64) printf 'osx-x64\n' ;;
                "") fail "Mimari okunamadı: uname -m ve uname -p boş döndü. macOS'ta osx-arm64 ile osx-x64 arasında varsayım yapılamıyor; doğru arşivi https://github.com/Teknesyum/VidShrink/releases adresinden elle indirin." ;;
                *) fail "Bu mimari için yayın yok: $machine. macOS'ta yalnız osx-arm64 ve osx-x64 yayımlanıyor." ;;
            esac
            ;;
        Linux)
            case "$machine" in
                x86_64|amd64) printf 'linux-x64\n' ;;
                "")
                    note 'Mimari okunamadı; Linux tarafında yalnız linux-x64 yayımlandığı için linux-x64 varsayıldı.'
                    printf 'linux-x64\n'
                    ;;
                *) fail "Bu mimari için yayın yok: $machine. Linux'ta yalnız linux-x64 yayımlanıyor." ;;
            esac
            ;;
        "")
            fail 'İşletim sistemi okunamadı: uname -s boş döndü. Windows için Install-VidShrink.ps1 kullanın.'
            ;;
        *)
            fail "Desteklenmeyen işletim sistemi: $system. Windows için Install-VidShrink.ps1 kullanın."
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

libmpv_install_command() {
    if [ "$(uname -s)" = 'Darwin' ]; then
        printf 'brew install mpv\n'
    elif command -v apt-get >/dev/null 2>&1; then
        printf 'sudo apt install libmpv2\n'
    elif command -v dnf >/dev/null 2>&1; then
        printf 'sudo dnf install mpv-libs\n'
    elif command -v pacman >/dev/null 2>&1; then
        printf 'sudo pacman -S mpv\n'
    elif command -v zypper >/dev/null 2>&1; then
        printf 'sudo zypper install libmpv2\n'
    fi
}

has_libmpv() {
    if [ "$(uname -s)" = 'Darwin' ]; then
        for directory in /opt/homebrew/lib /usr/local/lib /opt/local/lib; do
            if [ -f "$directory/libmpv.2.dylib" ]; then return 0; fi
        done
        return 1
    fi

    for ldconfig in ldconfig /sbin/ldconfig /usr/sbin/ldconfig; do
        if command -v "$ldconfig" >/dev/null 2>&1 && "$ldconfig" -p 2>/dev/null | grep -q 'libmpv\.so\.2'; then
            return 0
        fi
    done

    for directory in /usr/lib /usr/lib64 /usr/lib/x86_64-linux-gnu /usr/local/lib; do
        if [ -f "$directory/libmpv.so.2" ]; then return 0; fi
    done
    return 1
}

require_libmpv() {
    if has_libmpv; then
        return 0
    fi

    say 'libmpv bulunamadı. Oynatıcı sekmesi onunla çalışır; VidShrink onu kendisi kurmaz.'
    install_command=$(libmpv_install_command)
    if [ -n "$install_command" ]; then
        say 'Şu komutu çalıştırıp kurulumu yeniden başlatın:'
        say ''
        say "    $install_command"
        say ''
    else
        say 'Paket yöneticinizle libmpv paketini (libmpv.so.2) kurup kurulumu yeniden başlatın.'
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

say 'VidShrink kurulumu hazırlanıyor...'
require_ffmpeg
require_libmpv

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

# Burada başlatıcı yok: kısayol doğrudan uygulamayı gösterir. macOS'ta güncellemeyi
# uygulamanın kendisi yapıyor (paketin tamamını takas ederek), Windows'ta başlatıcı.
staged_executable=''
for candidate in VidShrink VidShrink.App; do
    if [ -f "$stage_root/$candidate" ]; then
        staged_executable=$candidate
        break
    fi
done
[ -n "$staged_executable" ] || fail 'Kurulan VidShrink çalıştırılabiliri bulunamadı.'

mkdir -p "$bin_directory"

# macOS'ta kurulum uygulama paketinin kendisi; yük hem ~/.local/share hem paket içinde
# tutulsa yayın iki kez saklanmış olurdu. Paket yerelde üretildiği için karantina
# almıyor ve ad-hoc imzayla açılıyor — noterleme gerekmiyor.
#
# Paketleme betiği arşivin içinden geliyor. Taşımayan eski bir yayın kurulduğunda düz
# kuruluma düşülüyor: uygulama yine çalışır, yalnız Dock kimliği ve çift tık olmaz.
if [ "$(uname -s)" = 'Darwin' ] && [ -f "$stage_root/macos-app-bundle.sh" ]; then
    rm -rf "$install_root"
    mkdir -p "$HOME/Applications"
    sh "$stage_root/macos-app-bundle.sh" "$stage_root" "$staged_executable" "$version" "$bundle_path" || \
        fail 'Uygulama paketi üretilemedi.'
    installed_executable="$bundle_path/Contents/MacOS/VidShrink"
    say "VidShrink $version kuruldu: $bundle_path"
else
    if [ "$(uname -s)" = 'Darwin' ]; then
        note 'Bu yayın paketleme betiğini taşımıyor; uygulama paketi olmadan kuruldu.'
    fi
    rm -rf "$install_root"
    mkdir -p "$install_root"
    cp -R "$stage_root/." "$install_root/"
    installed_executable="$install_root/$staged_executable"
    chmod +x "$installed_executable"
    say "VidShrink $version kuruldu: $install_root"
    if [ "$(uname -s)" = 'Linux' ]; then
        write_desktop_entry "$installed_executable"
        say "Dosya yöneticisinde \"Birlikte aç\" listesine eklendi: $desktop_entry"
    fi
fi

ln -sf "$installed_executable" "$bin_directory/vidshrink"
# Paket kurulduysa uygulama kendini güncelleyebiliyor: paketin içindeki tek bir dosyayı
# değiştirmek imzayı bozacağı için birim paketin tamamı, yeni paket yanına hazırlanıp
# çıkışta takas ediliyor. Paketsiz kurulumda ve Linux'ta güncelleme bu betiği yeniden
# çalıştırmakla olur.
if [ "$installed_executable" = "$bundle_path/Contents/MacOS/VidShrink" ]; then
    say 'Yeni sürümleri uygulama kendisi kuruyor; Ayarlar altından kapatabilirsiniz.'
else
    say 'Güncellemek için bu komutu yeniden çalıştırın.'
fi
say 'Kaldırmak için: --uninstall'
case ":${PATH}:" in
    *":$bin_directory:"*) say 'Çalıştırmak için: vidshrink' ;;
    *) say "Çalıştırmak için: $bin_directory/vidshrink ($bin_directory henüz PATH içinde değil)" ;;
esac
