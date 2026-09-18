#!/bin/sh
cd /c/Users/Administrator/Desktop/Projeler/VidShrink/.calisma || exit 1
for k in p28-altyazi s20 t61 t57 t63 t194 tema serit-tik mini-olcu ayar-yolu a1/filtre a1 hb-1c-test kodek-etiketi; do
  if [ -d "$k" ]; then
    echo "--- klasor: .calisma/$k"
    find "$k" -mindepth 1 -maxdepth 2 | tr '/' '\\' | sort
  else
    echo "--- klasor: .calisma/$k -> YOK"
  fi
done
