import json, io, os, math, sys, itertools

WINDOW = 2.0

def load(path):
    return json.load(io.open(path, encoding="utf-8"))

def cache_for(clip_dir, clip, crf=23, preset="medium", threads=4):
    p = os.path.join(clip_dir, ".onbellek", f"{clip}-crf{crf}-{preset}-t{threads}.json")
    return json.load(io.open(p, encoding="utf-8"))

class Clip:
    def __init__(self, report, cache):
        self.name = report["Clip"]
        self.duration = report["DurationSeconds"]
        self.w = report["Width"]
        self.h = report["Height"]
        self.bits = report["SecondBits"]
        self.cuts = report.get("SceneCutTimes") or []
        self.cache = cache
        self.census = report["CensusBppf"]

    def bppf(self, start, length=WINDOW):
        key = f"{start:.3f}".rstrip("0").rstrip(".") + "/" + f"{length:.3f}".rstrip("0").rstrip(".")
        v = self.cache.get(key)
        if v is None:
            return None
        b, f = v
        return b * 8.0 / (self.w * self.h * f) if f > 0 else None

    def snap(self, start, length=WINDOW):
        return min(max(int(round(start)), 0), max(0, int(math.floor(self.duration - length))))

    def window_bits(self, start, length=WINDOW):
        first, last = int(math.floor(start)), int(math.ceil(start + length)) - 1
        vals = [self.bits[i] for i in range(first, last + 1) if 0 <= i < len(self.bits) and self.bits[i] > 0]
        return sum(vals) / len(vals) if vals else 0.0

    def file_mean_bits(self):
        vals = [b for b in self.bits if b > 0]
        return sum(vals) / len(vals) if vals else 0.0

def estimate(clip, windows, ratio=False):
    num = den = 0.0
    mean = clip.file_mean_bits()
    for start, weight, stratum_mean in windows:
        v = clip.bppf(clip.snap(start))
        if v is None:
            return None
        if ratio:
            wb = clip.window_bits(clip.snap(start))
            if wb > 0 and stratum_mean > 0:
                v *= stratum_mean / wb
        num += weight * v
        den += weight
    return num / den if den > 0 else None

def fixed_plan(clip):
    d = clip.duration
    if d <= WINDOW * 1.5:
        return [(0.0, 1.0, 0.0)]
    usable = max(0.0, d - WINDOW)
    n = 2 if d < WINDOW * 6 else 3
    return [(usable * (i + 0.5) / n, 1.0, 0.0) for i in range(n)]

def strata_indices(order, n):
    total = len(order)
    for s in range(n):
        yield order[total * s // n: total * (s + 1) // n]

def profile_plan(clip, n, target="mean", separate=False):
    usable = [i for i, b in enumerate(clip.bits) if b > 0]
    if len(usable) < 4:
        return None
    usable.sort(key=lambda i: (clip.bits[i], i))
    taken = set()
    out = []
    for group in strata_indices(usable, min(n, len(usable))):
        if not group:
            continue
        m = sum(clip.bits[i] for i in group) / len(group)
        if target == "mean":
            key = lambda i: (abs(clip.bits[i] - m), i)
        elif target == "median":
            med = sorted(clip.bits[i] for i in group)[len(group) // 2]
            key = lambda i: (abs(clip.bits[i] - med), i)
        else:
            key = lambda i: (abs(clip.window_bits(clip.snap(i)) - m), i)
        pick = None
        for i in sorted(group, key=key):
            if separate and any(abs(i - t) < WINDOW for t in taken):
                continue
            if i in taken:
                continue
            pick = i
            break
        if pick is None:
            continue
        taken.add(pick)
        out.append((float(pick), float(len(group)), m))
    return out or None

def scene_plan(clip, n, separate=False):
    cuts = [c for c in clip.cuts if 0 < c < clip.duration]
    bounds = [0.0]
    for c in sorted(cuts):
        if c - bounds[-1] >= WINDOW:
            bounds.append(c)
    bounds.append(clip.duration)
    if len(bounds) < 3:
        return profile_plan(clip, n)
    scenes = []
    for a, b in zip(bounds, bounds[1:]):
        if b - a < WINDOW:
            continue
        first, last = int(math.floor(a)), int(math.ceil(b)) - 1
        vals = [clip.bits[i] for i in range(first, last + 1) if 0 <= i < len(clip.bits) and clip.bits[i] > 0]
        if not vals:
            continue
        scenes.append((a, b - a, sum(vals) / len(vals)))
    if not scenes:
        return profile_plan(clip, n)
    scenes.sort(key=lambda s: (s[2], s[0]))
    total = sum(s[1] for s in scenes)
    strata = min(n, len(scenes))
    out, idx, carried = [], 0, 0.0
    for s in range(strata):
        edge = total * (s + 1) / strata
        best, best_len, weight, rate = -1, 0.0, 0.0, 0.0
        while idx < len(scenes) and (carried < edge - 1e-9 or best < 0):
            carried += scenes[idx][1]
            weight += scenes[idx][1]
            if scenes[idx][1] > best_len:
                best_len, best, rate = scenes[idx][1], idx, scenes[idx][2]
            idx += 1
        if best < 0:
            continue
        centre = scenes[best][0] + (scenes[best][1] - WINDOW) / 2.0
        out.append((centre, weight, rate))
    return out or None

def run(report_path, clip_dir):
    reports = load(report_path)
    clips = []
    for r in reports:
        try:
            clips.append(Clip(r, cache_for(clip_dir, r["Clip"])))
        except FileNotFoundError:
            print(f"onbellek yok: {r['Clip']}", file=sys.stderr)
    return clips

def deviation(clip, windows, ratio=False):
    e = estimate(clip, windows, ratio)
    return None if e is None or clip.census <= 0 else e / clip.census - 1.0

def cv(clip):
    vals = [b for b in clip.bits if b > 0]
    if not vals:
        return 0.0
    m = sum(vals) / len(vals)
    if m <= 0:
        return 0.0
    return (sum((v - m) ** 2 for v in vals) / len(vals)) ** 0.5 / m

def production_bias(report):
    scan, packet = report.get("ScanBias", 0.0), report.get("PacketBias", 0.0)
    if 0.5 <= scan <= 2.0:
        return scan, "scan"
    if 0.5 <= packet <= 2.0:
        return packet, "paket"
    return 1.0, "yok"

if __name__ == "__main__":
    reports = load(sys.argv[1])
    clip_dir = sys.argv[2]
    clips, bias = [], {}
    for r in reports:
        try:
            c = Clip(r, cache_for(clip_dir, r["Clip"]))
        except FileNotFoundError:
            print(f"onbellek yok: {r['Clip']}", file=sys.stderr)
            continue
        clips.append(c)
        bias[c.name] = production_bias(r)

    print("== K1: bugunku ornekleme, icerik basina ==")
    print(f"{'klip':16s} {'sure':>6s} {'cv':>6s} {'duzeltme':>9s} {'duzeltmesiz':>12s} {'bugun':>9s}")
    todays = []
    for c in clips:
        b, src = bias[c.name]
        raw = deviation(c, fixed_plan(c))
        corrected = (raw + 1.0) / b - 1.0
        todays.append(corrected)
        print(f"{c.name:16s} {c.duration:6.0f} {cv(c):6.2f} {src:>9s} {raw:+11.2%} {corrected:+8.2%}")
    if todays:
        print(f"yayilim: en dusuk {min(todays):+.2%}, en yuksek {max(todays):+.2%}, "
              f"aralik {max(todays)-min(todays):.2%}, ortalama mutlak {sum(abs(t) for t in todays)/len(todays):.2%}")

    variants = []
    for target in ("mean", "median", "window"):
        for ratio in (False, True):
            tag = f"profil-{target}{'-oran' if ratio else ''}"
            variants.append((tag, (lambda t: lambda c, n: profile_plan(c, n, t))(target), ratio))
    for sep in (False, True):
        for ratio in (False, True):
            variants.append((f"sahne{'-ayrik' if sep else ''}{'-oran' if ratio else ''}",
                             (lambda s: lambda c, n: scene_plan(c, n, s))(sep), ratio))

    ns = [2, 3, 4, 5, 6, 8, 10, 12, 16]
    print()
    print("== K2/K3: aday planlarin ortalama mutlak sapmasi (korpus ortalamasi) ==")
    print(f"{'plan':22s} " + " ".join(f"N={n:<6d}" for n in ns))
    table = {}
    for name, maker, ratio in variants:
        row = []
        for n in ns:
            devs = [abs(d) for c in clips
                    for w in [maker(c, n)] if w
                    for d in [deviation(c, w, ratio)] if d is not None]
            table[(name, n)] = sum(devs) / len(devs) if devs else None
            row.append(f"{table[(name,n)]:.3f}  " if devs else "  -    ")
        print(f"{name:22s} " + " ".join(row))

    best = min((v, k) for k, v in table.items() if v is not None)
    print()
    print(f"en dusuk ortalama mutlak sapma: {best[1][0]} N={best[1][1]} -> {best[0]:.3f}")

    print()
    print("== secilen adayin icerik basina sapmasi ==")
    name, n = best[1]
    maker, ratio = next((m, r) for t, m, r in variants if t == name)
    for c in clips:
        w = maker(c, n)
        d = deviation(c, w, ratio) if w else None
        b, src = bias[c.name]
        raw = deviation(c, fixed_plan(c))
        today = (raw + 1.0) / b - 1.0
        print(f"{c.name:16s} bugun {today:+8.2%}   {name}-N{n} {d:+8.2%}" if d is not None else f"{c.name:16s} -")
