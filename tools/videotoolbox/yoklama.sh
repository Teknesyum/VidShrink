#!/bin/sh
# EncoderCapabilities yoklamasinin bu makinedeki suresi.
#
# Uretim kodu degistirilmedi: asagidaki uc komut EncoderCapabilities.cs'in kendi
# calistirdiklarinin birebir kopyasi.
#   Load()                  -> RunCapture: -encoders / -filters / -version
#   RunProbe(codec)         -> deneme kodlamasi, tek kare
#   RunProbe(codec,pixfmt)  -> HDR10 piksel bicimi yoklamasi
# Olculen sey C# tarafinda Stopwatch'in olctugu ile ayni: sureci baslat, cikisini bekle.
#
# Kullanim: tools/videotoolbox/yoklama.sh [tekrar]     (varsayilan 12)
set -u

KOK=$(cd "$(dirname "$0")/../.." && pwd)
C=${C:-"$KOK/.calisma/vt"}
N=${1:-12}
TSV="$C/yoklama.tsv"
mkdir -p "$C"
printf 'yoklama\tkodek\ttekrar\tms\tcikis\n' > "$TSV"

NUL=/dev/null

# Tek kosumun suresini ms olarak basar. /usr/bin/time -p saniyeyi iki hane veriyor.
sure_ms() {
    t=$( { /usr/bin/time -p "$@" >$NUL 2>$NUL; } 2>&1 | awk '$1=="real"{print $2}' )
    printf '%s' "$t" | awk '{printf "%d", $1 * 1000}'
}

kaydet() { printf '%s\t%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4" "$5" >> "$TSV"; }

i=1
while [ "$i" -le "$N" ]; do
    # 1. Acilis okumasi: Load() uc capture kosuyor, ucunun toplami acilista odeniyor.
    for arg in -encoders -filters -version; do
        ms=$(sure_ms ffmpeg -hide_banner $arg); rc=$?
        kaydet "load$arg" "-" "$i" "$ms" "$rc"
    done

    # 2. Deneme kodlamasi. Bu makinede listelenen kodlayicilar.
    for kod in libx265 libx264 hevc_videotoolbox h264_videotoolbox; do
        ms=$(sure_ms ffmpeg -hide_banner -loglevel error \
            -f lavfi -i "testsrc2=size=256x256:rate=30:duration=0.1" \
            -c:v "$kod" -frames:v 1 -f null $NUL); rc=$?
        kaydet "probe" "$kod" "$i" "$ms" "$rc"
    done

    # 3. HDR10 piksel bicimi yoklamasi. Kodlayici basina en fazla iki kosum:
    #    p010le kabul edilirse ikincisi hic kosmuyor.
    for kod in libx265 hevc_videotoolbox h264_videotoolbox; do
        for pf in p010le yuv420p10le; do
            ms=$(sure_ms ffmpeg -hide_banner -loglevel warning \
                -f lavfi -i "testsrc2=size=256x256:rate=30:duration=0.1" \
                -vf "format=$pf" -c:v "$kod" -pix_fmt "$pf" \
                -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc \
                -frames:v 1 -f null $NUL); rc=$?
            kaydet "hdr10-$pf" "$kod" "$i" "$ms" "$rc"
        done
    done
    i=$((i + 1))
done

echo "BITTI -> $TSV"
awk -F'\t' 'NR>1 { k=$1"\t"$2; v[k]=v[k]" "$4 }
END { for (k in v) { n=split(v[k],a," "); asort_n=n;
        for(x=1;x<n;x++) for(y=1;y<=n-x;y++) if(a[y]+0>a[y+1]+0){t=a[y];a[y]=a[y+1];a[y+1]=t}
        med = (n%2) ? a[(n+1)/2] : (a[n/2]+a[n/2+1])/2
        printf "%-28s n=%d  min=%s  medyan=%s  maks=%s\n", k, n, a[1], med, a[n] } }' "$TSV" | sort
