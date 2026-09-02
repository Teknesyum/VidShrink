#!/bin/sh
# Tek yerlesimi kodlar ve VMAF-NEG olcer. Bir satir TSV basar.
# Kullanim: olc.sh <kaynak> <etiket> <W> <H> <fps> <kbit> <kodlayici> <cikti-dizini>
set -e
SRC=$1; TAG=$2; W=$3; H=$4; FPS=$5; KBIT=$6; ENC=$7; OUT=$8
THREADS=${VS_THREADS:-4}
NAME="${TAG}_${W}x${H}@${FPS}_${KBIT}k_${ENC}"
VID="$OUT/enc/$NAME.mkv"
LOG="$OUT/vmaf/$NAME.json"
mkdir -p "$OUT/enc" "$OUT/vmaf"

SRCFPS=$(ffprobe -v error -select_streams v:0 -show_entries stream=r_frame_rate -of csv=p=0 "$SRC" | awk -F/ '{printf "%.4f", $1/$2}')
TRC=$(ffprobe -v error -select_streams v:0 -show_entries stream=color_transfer -of csv=p=0 "$SRC")
SW=$(ffprobe -v error -select_streams v:0 -show_entries stream=width -of csv=p=0 "$SRC")
SH=$(ffprobe -v error -select_streams v:0 -show_entries stream=height -of csv=p=0 "$SRC")
GOP=$(awk -v f="$FPS" 'BEGIN{printf "%d", (f*2)+0.5}')

if [ "$TRC" = "smpte2084" ]; then
  CTAG="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
  HWFMT=p010le; SWFMT=yuv420p10le; VFMT=yuv420p10le
else
  CTAG="-color_primaries bt709 -color_trc bt709 -colorspace bt709"
  HWFMT=nv12; SWFMT=yuv420p; VFMT=yuv420p
fi
VF="scale=$W:$H:flags=bicubic,fps=$FPS"

case "$ENC" in
  av1_nvenc|hevc_nvenc)
    ffmpeg -v error -y -i "$SRC" -an -sn -map 0:v:0 -vf "$VF" -pix_fmt $HWFMT -c:v $ENC -preset p5 -rc vbr -multipass fullres -b:v ${KBIT}k -maxrate $((KBIT*2))k -bufsize $((KBIT*4))k -g $GOP $CTAG "$VID" 2>/dev/null
    ;;
  libsvtav1)
    PLOG="$OUT/enc/$NAME.pass"
    ffmpeg -v error -y -i "$SRC" -an -sn -map 0:v:0 -vf "$VF" -pix_fmt $SWFMT -c:v libsvtav1 -preset 6 -b:v ${KBIT}k -g $GOP -svtav1-params "lp=$THREADS:pin=0" -pass 1 -passlogfile "$PLOG" -f null - 2>/dev/null
    ffmpeg -v error -y -i "$SRC" -an -sn -map 0:v:0 -vf "$VF" -pix_fmt $SWFMT -c:v libsvtav1 -preset 6 -b:v ${KBIT}k -g $GOP -svtav1-params "lp=$THREADS:pin=0" -pass 2 -passlogfile "$PLOG" $CTAG "$VID" 2>/dev/null
    rm -f "$PLOG"-0.log "$PLOG"-0.log.mbtree
    ;;
  *) echo "bilinmeyen kodlayici: $ENC" >&2; exit 2 ;;
esac

BYTES=$(stat -c %s "$VID")
FRAMES=$(ffprobe -v error -select_streams v:0 -count_frames -show_entries stream=nb_read_frames -of csv=p=0 "$VID")
DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$VID")
KBPS=$(awk -v b="$BYTES" -v d="$DUR" 'BEGIN{printf "%.1f", b*8/1000/d}')
BPPF=$(awk -v b="$BYTES" -v d="$DUR" -v w="$W" -v h="$H" -v f="$FPS" 'BEGIN{printf "%.6f", (b*8/d)/(w*h*f)}')

LAVFI="[0:v]scale=$SW:$SH:flags=bicubic,fps=$SRCFPS,format=$VFMT,setpts=PTS-STARTPTS[dist];[1:v]format=$VFMT,setpts=PTS-STARTPTS[ref];[dist][ref]libvmaf=model=version=vmaf_v0.6.1neg:log_path=$LOG:log_fmt=json:n_threads=$THREADS"
ffmpeg -v error -y -i "$VID" -i "$SRC" -lavfi "$LAVFI" -an -f null - 2>/dev/null

python "$(dirname "$0")/ozet.py" "$LOG" "$TAG" "$W" "$H" "$FPS" "$KBIT" "$ENC" "$KBPS" "$BPPF" "$BYTES" "$FRAMES"
