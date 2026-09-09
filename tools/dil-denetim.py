import io, json, os, re, sys

root = "src/VidShrink.App/Locales"
domains = ["main", "performance", "playback", "settings"]
slot = re.compile(r"\{(\d+)[^}]*\}")

def load(lang):
    out = {}
    for d in domains:
        path = os.path.join(root, lang, d + ".json")
        if not os.path.exists(path):
            return None, f"eksik dosya: {d}.json"
        try:
            out[d] = json.load(io.open(path, encoding="utf-8"))
        except Exception as error:
            return None, f"bozuk JSON ({d}): {error}"
    return out, None

base, _ = load("en")
flat_base = {k: v for d in domains for k, v in base[d].items()}

for lang in sorted(os.listdir(root)):
    if lang == "en" or not os.path.isdir(os.path.join(root, lang)):
        continue
    data, error = load(lang)
    if error:
        print(f"{lang:10} KIRIK  {error}")
        continue

    flat = {k: v for d in domains for k, v in data[d].items()}
    missing = sorted(set(flat_base) - set(flat))
    extra = sorted(set(flat) - set(flat_base))
    slots = [k for k in flat_base if k in flat
             and sorted(slot.findall(flat_base[k])) != sorted(slot.findall(flat[k]))]
    same = [k for k in flat_base if k in flat and flat_base[k] == flat[k] and len(flat_base[k]) > 12]

    state = "TAMAM" if not (missing or extra or slots) else "KALDI"
    print(f"{lang:10} {state}  anahtar {len(flat)}/{len(flat_base)}  eksik {len(missing)}  fazla {len(extra)}  yertutucu {len(slots)}  ceviri-yok {len(same)}")
    for k in missing[:5]:
        print(f"           eksik: {k}")
    for k in slots[:5]:
        print(f"           yertutucu: {k}")
