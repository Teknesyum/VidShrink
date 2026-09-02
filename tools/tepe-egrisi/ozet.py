import csv, sys, collections

def oku(*yollar):
    son = {}
    for y in yollar:
        try:
            with open(y, newline='', encoding='utf-8') as f:
                for r in csv.DictReader(f, delimiter=';'):
                    son[(r['kaynak'], r['kodlayici'], r['yol'], r['oran'], r['tepe'])] = r
        except FileNotFoundError:
            pass
    return list(son.values())

def f(r, k):
    return float(r[k])

def tablo(rows, baslik):
    print(f"\n### {baslik}\n")
    print("| kaynak | kodlayici | oran | tepe | bitrate k | maxrate k | teslim MiB | video butcesi MiB | teslim/butce | mean | p10 |")
    print("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
    for r in rows:
        print("| {} | {} | {} | {} | {} | {} | {:.4f} | {:.4f} | {:.4f} | {:.3f} | {:.3f} |".format(
            r['kaynak'], r['kodlayici'], r['oran'], r['tepe'], r['bitrate_k'], r['maxrate_k'],
            f(r,'teslim_mib'), f(r,'hedef_mib'), f(r,'hedef_orani'), f(r,'vmaf_mean'), f(r,'vmaf_p10')))

def egri(rows):
    print("\n### Tepe egrisi: p10 ve boyut, oran basina\n")
    g = collections.defaultdict(dict)
    for r in rows:
        g[(r['kaynak'], r['kodlayici'], r['oran'])][r['tepe']] = r
    print("| kaynak | kodlayici | oran | p10 1.02 | p10 1.10 | p10 1.50 | p10 yayilim | yon | boyut tek yonlu mu | asan hucre |")
    print("|---|---|---:|---:|---:|---:|---:|---|---|---:|")
    for k in sorted(g, key=lambda x: (x[0], x[1], float(x[2]))):
        h = g[k]
        if not all(t in h for t in ('1.02','1.10','1.50')):
            continue
        p = [f(h[t],'vmaf_p10') for t in ('1.02','1.10','1.50')]
        b = [f(h[t],'teslim_mib') for t in ('1.02','1.10','1.50')]
        yon = 'tek yonlu artan' if p[0] < p[1] < p[2] else ('tek yonlu azalan' if p[0] > p[1] > p[2] else 'yon yok')
        mono = 'evet' if (b[0] <= b[1] <= b[2] or b[0] >= b[1] >= b[2]) else 'HAYIR'
        asan = sum(1 for t in ('1.02','1.10','1.50') if f(h[t],'hedef_orani') > 1.0)
        print("| {} | {} | {} | {:.3f} | {:.3f} | {:.3f} | {:.3f} | {} | {} | {}/3 |".format(
            k[0], k[1], k[2], p[0], p[1], p[2], max(p)-min(p), yon, mono, asan))

def asim(rows):
    asan = [r for r in rows if f(r,'hedef_orani') > 1.0]
    print(f"\n### Hedef asimi\n\nHucre: {len(rows)}, asan: {len(asan)}")
    if asan:
        en = max(asan, key=lambda r: f(r,'hedef_orani'))
        print("En buyuk asim: {} {} oran {} tepe {} -> {:.4f}".format(
            en['kaynak'], en['kodlayici'], en['oran'], en['tepe'], f(en,'hedef_orani')))
        print("\nAsan hucrelerin oranlari: " + ", ".join(sorted({r['oran'] for r in asan}, key=float)))

def kapi(rows):
    kotu = [r for r in rows if r['akis'] != '1' or r['pixfmt'] != 'yuv420p10le' or 'bt2020' not in r['renk']]
    print(f"\n### Boru hatti kapisi\n\nKapiyi gecmeyen satir: {len(kotu)}")

if __name__ == '__main__':
    rows = oku(*sys.argv[1:])
    rows.sort(key=lambda r: (r['kaynak'], r['kodlayici'], float(r['oran']), r['tepe']))
    tablo(rows, "Tum hucreler")
    egri(rows)
    asim(rows)
    kapi(rows)
