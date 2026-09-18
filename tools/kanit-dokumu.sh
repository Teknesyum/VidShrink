#!/bin/sh
cd /c/Users/Administrator/Desktop/Projeler/VidShrink/.calisma || exit 1
for k in oynatici-motor dalga2 dalga3 dalga7b dalga4a dalga4b dalga5 T176 dalga1 oynatici-yol-haritasi oynatici-kisayol girdi-dalga3 girdi; do
  if [ -d "$k" ]; then
    echo "--- klasor: .calisma/$k"
    find "$k" -mindepth 1 -maxdepth 2 | tr '/' '\\' | sort
  else
    echo "--- klasor: .calisma/$k -> YOK"
  fi
done
