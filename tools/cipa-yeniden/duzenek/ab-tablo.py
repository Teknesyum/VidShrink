import json, os, sys
W = '.calisma/T116/gunluk'
AD = {'sdr8': ('klip ikamesi', 8), 'sdr20': ('klip ikamesi', 20),
      'hdr1': ('hdr (parca-1)', 40), 'hdr2': ('hdr (parca-2)', 40)}

def oku(tag):
    p = os.path.join(W, tag + '.json')
    if not os.path.exists(p): return None
    return json.load(open(p, encoding='utf-8'))[0]

def n(v, f='%.2f'):
    return 'OLCULMEDI' if v is None else f % v

def satir(d):
    return '| %s@%d | %s %s | %.3f | %s | %s | %s | %.1f | %d |' % (
        d['Width'], d['Fps'], d['Mode'], d['CrfOrBitrate'], d['ActualMb'],
        n(d['VmafNegMean']), n(d['VmafNegHarmonic']), n(d['VmafNegP10']),
        d['EncodeSeconds'], d['Attempts'])

for meter in ['kilitli', 'kilitsiz']:
    print('### olcer: %s' % meter)
    print('| kaynak | hedef | kol | yerlesim | kip | teslim MB | mean | harm | p10 | kodlama sn | deneme |')
    print('| --- | ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |')
    for cfg, (ad, mb) in AD.items():
        for arm in ['eski', 'yeni']:
            d = oku('%s-%s-%s' % (cfg, arm, meter))
            if d is None:
                print('| %s | %d MB | %s | KOSULMADI |' % (ad, mb, arm)); continue
            print('| %s | %d MB | %s %s' % (ad, mb, arm, satir(d)))
    print()
    print('#### fark (yeni - eski)')
    print('| kaynak | hedef | dmean | dharm | dp10 | dteslim MB | dkodlama sn |')
    print('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
    for cfg, (ad, mb) in AD.items():
        a = oku('%s-eski-%s' % (cfg, meter)); b = oku('%s-yeni-%s' % (cfg, meter))
        if not a or not b:
            print('| %s | %d MB | KOSULMADI |' % (ad, mb)); continue
        def dl(k, f='%+.2f'):
            if a[k] is None or b[k] is None: return 'OLCULMEDI'
            return f % (b[k] - a[k])
        print('| %s | %d MB | %s | %s | %s | %s | %s |' % (
            ad, mb, dl('VmafNegMean'), dl('VmafNegHarmonic'), dl('VmafNegP10'),
            dl('ActualMb', '%+.3f'), dl('EncodeSeconds', '%+.1f')))
    print()
