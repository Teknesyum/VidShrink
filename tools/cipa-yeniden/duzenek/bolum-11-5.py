import io, json, os, sys

W = sys.argv[1] if len(sys.argv) > 1 else '.calisma/T116/gunluk'
DOC = 'docs/olcumler/olculen-kaliteyle-plan.md'
AD = [('sdr8', 'klip ikamesi', 8), ('sdr20', 'klip ikamesi', 20),
      ('hdr1', 'hdr ikamesi (parca-1)', 40), ('hdr2', 'hdr ikamesi (parca-2)', 40)]
KOL = ['eski', 'yeni']
OLCER = ['kilitli', 'kilitsiz']


def oku(tag):
    p = os.path.join(W, tag + '.json')
    if not os.path.exists(p) or os.path.getsize(p) == 0:
        return None
    try:
        return json.load(io.open(p, encoding='utf-8'))[0]
    except Exception:
        return None


def n(v, f='%.2f'):
    return 'OLCULMEDI' if v is None else f % v


def kip(d):
    m, c = d['Mode'], d['CrfOrBitrate']
    return c if c.startswith(m) else '%s %s' % (m, c)


D = {}
eksik = []
for cfg, ad, mb in AD:
    for kol in KOL:
        for olcer in OLCER:
            tag = '%s-%s-%s' % (cfg, kol, olcer)
            d = oku(tag)
            D[tag] = d
            if d is None:
                eksik.append((tag, ad, mb, kol, olcer))

L = []
A = L.append
A(u'### 11.5 Kilitli ve kilitsiz ölçerle A/B (K2)')
A(u'')
A(u'Izgaradaki her kaynak **ikamedir** — §11.1. `oyun` satırı yok: o kaynak')
A(u'**ölçülmedi**, elde 48 fps av1 oyun kaydı bulunmadığı için.')
A(u'')
A(u'Aşağıdaki tablolar §1\'in tablosuyla **aynı kaynakları ölçmüyor; satırları')
A(u'karşılaştırılamaz.** §1\'in tablosu "kilitsiz (geçersiz), kaynak silinmiş,')
A(u'yeniden üretilemez" damgasıyla yerinde duruyor, silinmedi.')
A(u'')
A(u'Süre sayıları için **makine paylaşımlıydı** (dokuz ajan). Kalite ve boyut')
A(u'sayılarına bu damga basılmadı.')
A(u'')

for olcer in OLCER:
    A(u'#### Satırlar — ölçer **%s**, kaynaklar ikame' % olcer)
    A(u'')
    A(u'| kaynak (ikame) | hedef | kol | yerleşim | kip | teslim MB | mean | harm | p10 | kodlama sn | deneme |')
    A(u'| --- | ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |')
    for cfg, ad, mb in AD:
        for kol in KOL:
            d = D['%s-%s-%s' % (cfg, kol, olcer)]
            if d is None:
                continue
            A(u'| %s | %d MB | %s | %s@%d | %s | %.3f | %s | %s | %s | %.1f | %d |' % (
                ad, mb, kol, d['Width'], d['Fps'], kip(d), d['ActualMb'],
                n(d['VmafNegMean']), n(d['VmafNegHarmonic']), n(d['VmafNegP10']),
                d['EncodeSeconds'], d['Attempts']))
    A(u'')

A(u'#### Kol farkı (yeni − eski), aynı ölçer, kaynaklar ikame')
A(u'')
A(u'| kaynak (ikame) | hedef | ölçer | Δmean | Δharm | Δp10 | Δteslim MB | Δkodlama sn |')
A(u'| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |')
for cfg, ad, mb in AD:
    for olcer in OLCER:
        a = D['%s-eski-%s' % (cfg, olcer)]
        b = D['%s-yeni-%s' % (cfg, olcer)]
        if a is None or b is None:
            continue
        def f(k):
            if a[k] is None or b[k] is None:
                return 'OLCULMEDI'
            return '%+.2f' % (b[k] - a[k])
        A(u'| %s | %d MB | %s | %s | %s | %s | %+.3f | %+.1f |' % (
            ad, mb, olcer, f('VmafNegMean'), f('VmafNegHarmonic'), f('VmafNegP10'),
            b['ActualMb'] - a['ActualMb'], b['EncodeSeconds'] - a['EncodeSeconds']))
A(u'')

A(u'#### Kilidin bedeli (aynı kol, kilitli − kilitsiz ölçer)')
A(u'')
A(u'| kaynak (ikame) | hedef | kol | Δmean | Δharm | Δp10 | plan aynı mı |')
A(u'| --- | ---: | --- | ---: | ---: | ---: | --- |')
for cfg, ad, mb in AD:
    for kol in KOL:
        a = D['%s-%s-kilitsiz' % (cfg, kol)]
        b = D['%s-%s-kilitli' % (cfg, kol)]
        if a is None or b is None:
            continue
        def f(k):
            if a[k] is None or b[k] is None:
                return 'OLCULMEDI'
            return '%+.2f' % (b[k] - a[k])
        plan = all(a[k] == b[k] for k in
                   ['Width', 'Height', 'Fps', 'Codec', 'Mode', 'CrfOrBitrate'])
        A(u'| %s | %d MB | %s | %s | %s | %s | %s |' % (
            ad, mb, kol, f('VmafNegMean'), f('VmafNegHarmonic'), f('VmafNegP10'),
            u'evet' if plan else u'**hayır**'))
A(u'')

A(u'#### Izgarada eksik kalan hücreler')
A(u'')
if eksik:
    A(u'Onaltı hücrenin **%d\'i ölçülmedi**:' % len(eksik))
    A(u'')
    for tag, ad, mb, kol, olcer in eksik:
        A(u'- `%s` — %s, %d MB, %s kolu, %s ölçer: **ölçülmedi**' % (tag, ad, mb, kol, olcer))
else:
    A(u'Yok. Onaltı hücrenin onaltısı da koşuldu.')
A(u'')

s = io.open(DOC, encoding='utf-8').read()
i = s.find(u'### 11.5')
j = s.find(u'### 11.6')
assert i > 0 and j > i
io.open(DOC, 'w', encoding='utf-8').write(s[:i] + u'\n'.join(L) + u'\n' + s[j:])
print('11.5 yazildi; eksik hucre: %d' % len(eksik))
