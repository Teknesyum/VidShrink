#!/usr/bin/env bash
# Iki yayimlanmis ikilinin hangi olcum grafigini tasidigini ikiliden okur.
# Kaynaga degil, KOSAN ikiliye bakar: "kilitsiz diye yayimladim" iddiasini
# dogrular. Kare kilidi iki ayri yerde: QualityMeter (urun olceri,
# VidShrink.Ffmpeg.dll) ve bench'in kendi grafigi (VidShrink.Bench.dll).
set -eu
cd "$(dirname "$0")/../../.." || exit 1
python - "$@" <<'PY'
import sys
pats=['settb=AVTB,setpts=N','[1:v]null[r]','flags=lanczos[t];']
for f in ['.calisma/T116/bench-kilitli/VidShrink.Bench.dll',
          '.calisma/T116/bench-kilitsiz/VidShrink.Bench.dll',
          '.calisma/T116/bench-kilitli/VidShrink.Ffmpeg.dll',
          '.calisma/T116/bench-kilitsiz/VidShrink.Ffmpeg.dll']:
    d=open(f,'rb').read()
    print(f, {p:d.count(p.encode('utf-16-le')) for p in pats})
PY
# Olculen sonuc:
#   bench-kilitli/Bench.dll    settb=2  null[r]=0  lanczos[t]=0  -> KILITLI
#   bench-kilitsiz/Bench.dll   settb=1  null[r]=1  lanczos[t]=1  -> KILITSIZ
#   bench-kilitli/Ffmpeg.dll   settb=2                            -> KILITLI
#   bench-kilitsiz/Ffmpeg.dll  settb=1                            -> KILITSIZ
# settb'nin kilitsizde 1 kalmasi const alanin metadata'da durmasindandir;
# kullanan yerlerde yok.
