import subprocess, sys, re, statistics, json

FLOOR, CEIL, OFFSET, SLOPE, NB, PCT = 0.05, 0.15, 0.08, 2.09, 40.0, 0.90
MIN_SCENE = 1.0
BASE = 0.012
MERGE = 28.0 / 28.0
CLAMP_MIN, CLAMP_MAX, DEFAULT = 5.0, 10.0, 10.0

def scan(path):
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "info", "-nostats", "-i", path,
           "-filter_complex", f"[0:v]select='gte(scene,{BASE})',metadata=print",
           "-an", "-f", "null", "-"]
    log = subprocess.run(cmd, capture_output=True, text=True, errors="replace").stderr
    out, t = [], None
    for line in log.splitlines():
        m = re.search(r"pts_time:([0-9.]+)", line)
        if m:
            t = float(m.group(1)); continue
        m = re.search(r"lavfi\.scene_score=([0-9.eE+-]+)", line)
        if m and t is not None:
            out.append((t, float(m.group(1)))); t = None
    return sorted(out)

def agitation(ordered, time, duration, fps):
    lo = sum(1 for x in ordered if x[0] <= time - NB)
    hi = sum(1 for x in ordered if x[0] <= time + NB)
    span = min(time + NB, duration) - max(time - NB, 0.0)
    if span <= 0: return 0.0
    count = max(1, round(span * fps))
    index = int(PCT * count)
    present = hi - lo
    zeros = max(0, count - present)
    if index < zeros or present == 0: return 0.0
    vals = sorted(x[1] for x in ordered[lo:hi])
    return vals[min(index - zeros, present - 1)]

def cuts(ordered, duration, fps):
    res, last = [], 0.0
    for time, score in ordered:
        thr = min(max(OFFSET + SLOPE * agitation(ordered, time, duration, fps), FLOOR), CEIL)
        if score < thr: continue
        if time - last < MIN_SCENE: continue
        if duration - time < MIN_SCENE: continue
        res.append(time); last = time
    return res

def main(path, duration, fps):
    ordered = scan(path)
    cs = cuts(ordered, duration, fps)
    bounds = [0.0] + cs + [duration]
    lengths = [b - a for a, b in zip(bounds, bounds[1:]) if b - a > 0]
    if not lengths:
        return dict(aday=len(ordered), kesim=0, sahne=0, medyan=None, tavan=DEFAULT, kaynaktan="varsayilan")
    med = statistics.median(sorted(lengths))
    ceiling = min(max(med / MERGE, CLAMP_MIN), CLAMP_MAX)
    return dict(aday=len(ordered), kesim=len(cs), sahne=len(lengths), medyan=round(med, 3),
                tavan=round(ceiling, 3), kaynaktan="harita",
                kesim_zamanlari=[round(c, 3) for c in cs],
                sahne_sureleri=[round(x, 3) for x in lengths])

if __name__ == "__main__":
    print(json.dumps(main(sys.argv[1], float(sys.argv[2]), float(sys.argv[3])), ensure_ascii=False))
