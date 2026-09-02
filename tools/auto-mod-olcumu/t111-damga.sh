#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
mkdir -p damga

dok () {
  ad="$1"; f="$2"
  [ -f "$f" ] || { echo "YOK $ad"; return; }
  [ -f "damga/${ad}.txt" ] && { echo "atlandi $ad"; return; }
  ffmpeg -hide_banner -nostdin -threads 4 -i "$f" -vf showinfo -an -f null - 2>&1 \
    | grep -o "pts_time:[0-9.]*" | cut -d: -f2 > "damga/${ad}.txt"
  echo "$ad $(wc -l < "damga/${ad}.txt") kare"
}

dok kaynak gui/parca-2.mkv
dok auto    gui/parca-2_shrunk.mp4
for n in uzman-hb uzman-hb2 uzman-biz3; do dok "$n" "ciktilar/${n}.mp4"; done

fark () {
  a="damga/kaynak.txt"; b="damga/${1}.txt"
  [ -f "$b" ] || return
  paste "$b" "$a" | awk -v ad="$1" '
    {d=($1-$2)*1000; n++; s+=d;
     if(n==1){mn=d;mx=d;f0=d}
     if(d<mn)mn=d; if(d>mx)mx=d;
     if(d<0)neg++;
     r=sprintf("%.2f",d); c[r]++}
    END{
      printf "%-12s kare %d | kare0 %+.2f ms | ort %+.2f ms | en dusuk %+.2f | en yuksek %+.2f | negatif %d\n", ad, n, f0, s/n, mn, mx, neg+0;
      best=""; bc=0; for(k in c){ if(c[k]>bc){bc=c[k];best=k} }
      printf "%-12s en sik deger %s ms, %d kare (%.1f%%)\n", ad, best, bc, 100.0*bc/n;
    }'
}

echo "=== kaynaga gore kare basina damga farki (cikti - kaynak)"
for n in auto uzman-hb uzman-hb2 uzman-biz3; do fark "$n"; done

echo "=== kaynagin kendi kare araliklari"
awk '{if(NR>1){d=($1-p)*1000; n++; s+=d; if(n==1){mn=d;mx=d} if(d<mn)mn=d; if(d>mx)mx=d} p=$1}
     END{printf "kaynak: ortalama %.4f ms, en kisa %.2f, en uzun %.2f, %d aralik\n", s/n, mn, mx, n}' damga/kaynak.txt
