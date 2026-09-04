#!/usr/bin/env bash
set -u
. "$(dirname "$0")/ortak.sh"
out="$ROOT/.calisma/t133/harita.json"
{
  echo "{"
  ilk=1
  for s in s1-kesikli s2-durgun s3-hareketli s4-yuksek; do
    f="$KAY/$s.mkv"; fps=$(fps_of "$f")
    [ $ilk -eq 1 ] || echo ","
    ilk=0
    printf '"%s": %s' "$s" "$(python "$(dirname "$0")/harita.py" "$f" 20 "$fps")"
  done
  echo
  echo "}"
} > "$out"
python -c "import json;print(json.dumps(json.load(open(r'$out')),ensure_ascii=False)[:400])"
