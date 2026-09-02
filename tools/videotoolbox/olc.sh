#!/bin/sh
# VideoToolbox olcumu (macOS). Uc kol, ayni hedef bit hizi, her kolda ses atiliyor.
#
# Havuz kusuru: parca-1'de ses yok, parca-2/3'te AAC var ve sureler birebir esit degil.
# Ses butceden yerse parcalar arasi kiyas haksiz olur, o yuzden once her parcanin
# video-only kopyasi cikariliyor (-c:v copy, bit birebir ayni) ve hem kodlama girdisi
# hem VMAF referansi o kopya oluyor. Boylece uc kol da ayni baytlari goruyor.
#
# Kullanim:  tools/videotoolbox/olc.sh [parca-1 parca-2 ...]
# Ayarlar:   BIT=5500  (kbit/s, uc kolda ayni)   C=<calisma dizini>
set -u

KOK=$(cd "$(dirname "$0")/../.." && pwd)
K=${K:-"$KOK/.calisma/kaynak"}
C=${C:-"$KOK/.calisma/vt"}
BENCH=${BENCH:-"$KOK/tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench"}
BIT=${BIT:-5500}
TSV="$C/olcumler.tsv"

mkdir -p "$C/cikti" "$C/olcum" "$C/gunluk"
[ -f "$TSV" ] || printf 'parca\tkodek\tbit_k\tbayt\tsure_sn\tpix_fmt\tvmaf_ort\tvmaf_p10\tvmaf_harmonik\tvmaf_min\n' > "$TSV"

# Kodlanan kolun ffmpeg argumanlari. Uc kolda da tek gecis ABR: VideoToolbox iki gecis
# desteklemiyor, dolayisiyla esit hiz denetimi ancak boyle kuruluyor.
kol_arg() {
    case "$1" in
        libx265)            printf -- '-c:v libx265 -preset slow -pix_fmt yuv420p10le -tag:v hvc1' ;;
        hevc_videotoolbox)  printf -- '-c:v hevc_videotoolbox -profile:v main10 -pix_fmt p010le -tag:v hvc1' ;;
        h264_videotoolbox)  printf -- '-c:v h264_videotoolbox -pix_fmt yuv420p' ;;
        *) echo "bilinmeyen kol: $1" >&2; exit 1 ;;
    esac
}

referans() {
    r="$C/cikti/$1-video.mkv"
    if [ ! -f "$r" ]; then
        ffmpeg -hide_banner -loglevel error -nostdin -y -i "$K/$1.mkv" \
            -an -sn -dn -c:v copy "$r" || { echo "referans cikarilamadi: $1" >&2; exit 1; }
    fi
    printf '%s' "$r"
}

alan() { ffprobe -v error -select_streams v:0 -show_entries stream="$2" -of default=nw=1:nk=1 "$1"; }

kos() {
    parca=$1; kod=$2
    out="$C/cikti/$parca-$kod.mp4"
    olcum="$C/olcum/$parca-$kod.json"
    ref=$(referans "$parca")

    if grep -q "^$parca	$kod	" "$TSV" 2>/dev/null; then echo "atlandi $parca $kod"; return 0; fi

    echo "=== $parca $kod $(date +%H:%M:%S)"
    rm -f "$out"
    # Duvar saati yalniz kodlamayi kapsiyor; olcum ve yoklama disarida.
    /usr/bin/time -p ffmpeg -hide_banner -loglevel error -nostdin -y -i "$ref" \
        -an -sn -dn $(kol_arg "$kod") -b:v "${BIT}k" "$out" \
        2> "$C/gunluk/$parca-$kod.time" || { echo "KODLAMA HATASI $parca $kod" >&2; cat "$C/gunluk/$parca-$kod.time" >&2; return 1; }

    sure=$(awk '$1=="real"{print $2}' "$C/gunluk/$parca-$kod.time")
    bayt=$(wc -c < "$out" | tr -d ' ')
    pix=$(alan "$out" pix_fmt)

    "$BENCH" measure "$ref" "$out" > "$olcum" 2>> "$C/gunluk/$parca-$kod.olcum.log" \
        || { echo "OLCUM HATASI $parca $kod" >&2; return 1; }

    oku() { sed -n 's/.*"'"$1"'"[[:space:]]*:[[:space:]]*\([0-9.eE+-]*\).*/\1/p' "$olcum" | head -1; }
    printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
        "$parca" "$kod" "$BIT" "$bayt" "$sure" "$pix" \
        "$(oku VmafNegMean)" "$(oku VmafNegP10)" "$(oku VmafNegHarmonic)" "$(oku VmafNegMin)" >> "$TSV"
    echo "    $bayt bayt, ${sure}s, p10 $(oku VmafNegP10)"
}

parcalar=${*:-"parca-1 parca-2 parca-3"}
for p in $parcalar; do
    for kod in libx265 hevc_videotoolbox h264_videotoolbox; do
        kos "$p" "$kod"
    done
done
echo "BITTI $(date +%H:%M:%S) -> $TSV"
