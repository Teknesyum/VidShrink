#!/usr/bin/env bash
# Duvar saati olcusu: tek basina kosar. Paralel kodlama varken cagirma.
set -u
ETI=${ETI:-bpp}
for s in s1-kesikli s2-durgun s3-hareketli s4-yuksek; do
  for sn in 2 5 10 15 20; do
    bash "$(dirname "$0")/atlama.sh" "$ETI-$s-g$sn"
  done
done
