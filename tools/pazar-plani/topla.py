import json
import os
import re
import sys

SUTUN = ['No', 'Eksik', 'Kimde Var', 'Bizde Durum', 'Fayda', 'İş', 'Öncelik', 'Karar', 'Kaynak']
SATIR = re.compile(r'^\|\s*([A-ZÇĞİÖŞÜ]{2,4})-(\d+)\s*\|')
PAKET = re.compile(r'^#{2,4}\s+`?([A-ZÇĞİÖŞÜ]{2,4})-P(\d+)`?\s*(?:—|–|-|:)\s*(.+?)\s*$')
ALAN = re.compile(r'^\s*[-*]\s+\**(Satırlar|İş|Karar|Dokunacağı yer|Plan)\**\s*:\**\s*(.*)$')
NOLAR = re.compile(r'([A-ZÇĞİÖŞÜ]{2,4})-(\d+)')
BASLIK = re.compile(r'^##\s+(.+?)\s*$')
KANIT = re.compile(r'([\w./\\-]+\.(?:cs|axaml|json|md|ps1|sh|yml|yaml|props|csproj|xaml|js|py|iss|txt)):(\d+)')
ALANLAR = ['Satırlar', 'İş', 'Karar', 'Dokunacağı yer', 'Plan']

KOL = [
    ('oynatici-cekirdek', 'OYA', 'Oynatıcı', 'Oynatma Çekirdeği'),
    ('oynatici-kitaplik', 'OYB', 'Oynatıcı', 'Liste, Kitaplık Ve Ağ'),
    ('duzenleyici-kesim', 'DZA', 'Düzenleyici', 'Kayıpsız Kesim Ve Birleştirme'),
    ('duzenleyici-kurgu', 'DZB', 'Düzenleyici', 'Kurgu Yönü'),
    ('kucult', 'SIK', 'Küçült', 'Küçültme, Önizleme Ve Kuyruk'),
    ('donustur', 'DON', 'Dönüştür', 'Dönüştürme Ve Gelişmiş'),
    ('kaydedici-yakalama', 'EKA', 'Kaydedici', 'Yakalama'),
    ('kaydedici-sunum', 'EKB', 'Kaydedici', 'Sunum, Kayıt Sonrası Ve Paylaşım'),
    ('ayarlar-kabuk', 'AYR', 'Ayarlar Ve Kabuk', 'Ayarlar, Hakkında Ve Pencere'),
    ('dagitim-otomasyon', 'DAG', 'Dağıtım Ve Otomasyon', 'Kurulum, Güncelleme Ve Komut Satırı'),
    ('eksik-araclar', 'ARC', 'Bizde Olmayan Araçlar', 'Yeni Araç Adayları'),
    ('platform-esitligi', 'PLT', 'Platform Eşitliği', 'Windows\'ta Olup macOS Ya Da Linux\'ta Eksik Kalanlar'),
]
DALGA_AD = {
    '1': 'Dalga 1 — Karar Gerekmez, Küçük İş, Öncelik 1',
    '2': 'Dalga 2 — Karar Gerekmez, Küçük İş Öncelik 2 Ya Da Orta İş Öncelik 1',
    '3': 'Dalga 3 — Karar Gerekmez, Orta İş Öncelik 2 Ya Da Büyük İş Öncelik 1',
    '4': 'Dalga 4 — Karar Gerekmez, Kalanlar',
    'K': 'Sahibin Kararını Bekleyenler',
}
DALGA_SIRA = ['1', '2', '3', '4', 'K']
IS_SIRA = {'küçük': 0, 'orta': 1, 'büyük': 2, '?': 3}


def norm(onek, sayi):
    return '%s-%02d' % (onek, int(sayi))


def hucreler(l):
    s = l.strip()
    if s.startswith('|'):
        s = s[1:]
    if s.endswith('|'):
        s = s[:-1]
    return [c.strip() for c in s.split('|')]


def is_sinif(metin):
    m = metin.strip().strip('*`').lower()
    for a in ('küçük', 'orta', 'büyük'):
        if m.startswith(a):
            return a
    return '?'


def karar_sinif(metin):
    m = metin.strip().strip('*`').lower()
    for a in ('gerekmez', 'sahip', 'kapalı'):
        if m.startswith(a):
            return a
    return '?'


def onc_sinif(metin):
    m = re.search(r'[123]', metin)
    return int(m.group(0)) if m else 0


def oku(yol):
    d = {'satir': {}, 'sira': [], 'bozuk': [], 'tekrar': [], 'bolum': {}, 'bolum_sira': [], 'paket': []}
    if not os.path.exists(yol):
        return None
    cur, p, son = None, None, None
    with open(yol, encoding='utf-8') as f:
        for i, ham in enumerate(f, 1):
            l = ham.rstrip('\r\n')
            m = SATIR.match(l)
            if m:
                h = hucreler(l)
                if len(h) != len(SUTUN):
                    d['bozuk'].append((i, len(h)))
                    continue
                no = norm(m.group(1), m.group(2))
                r = dict(zip(SUTUN, h))
                r['No'] = no
                if no in d['satir']:
                    d['tekrar'].append(no)
                else:
                    d['sira'].append(no)
                d['satir'][no] = r
                continue
            pm = PAKET.match(l)
            if pm:
                p = {'id': '%s-P%d' % (pm.group(1), int(pm.group(2))), 'onek': pm.group(1), 'n': int(pm.group(2)), 'ad': pm.group(3).strip(' *`')}
                d['paket'].append(p)
                son = None
                continue
            b = BASLIK.match(l)
            if b:
                cur = b.group(1).strip()
                d['bolum'].setdefault(cur, [])
                d['bolum_sira'].append(cur)
                p, son = None, None
                continue
            if p is not None:
                a = ALAN.match(l)
                if a:
                    p[a.group(1)] = a.group(2).strip()
                    son = a.group(1)
                    continue
                if son and l.strip() and not l.lstrip().startswith(('-', '*', '#', '|', '```')):
                    p[son] += ' ' + l.strip()
                    continue
                if l.strip():
                    son = None
            if cur is not None:
                d['bolum'][cur].append(l)
    return d


def bolum(d, ad):
    for k, v in d['bolum'].items():
        if k.lower().startswith(ad.lower()):
            return [x for x in v]
    return []


def yukle(klasor):
    kollar, uyari = [], []
    for ad, onek, sekme, alt in KOL:
        ham = oku(os.path.join(klasor, ad + '-eksikler.md'))
        dog = oku(os.path.join(klasor, ad + '-dogrulanmis.md'))
        k = {'ad': ad, 'onek': onek, 'sekme': sekme, 'alt': alt, 'ham': ham, 'dog': dog, 'paket': []}
        kollar.append(k)
        if ham is None:
            uyari.append('%s: ham tablo yok' % ad)
        if dog is None:
            uyari.append('%s: doğrulanmış dosya yok, ham tablo kullanılıyor' % ad)
        t = dog or ham
        k['t'] = t
        if t is None:
            k['t'] = {'satir': {}, 'sira': [], 'bozuk': [], 'tekrar': [], 'bolum': {}, 'bolum_sira': [], 'paket': []}
            continue
        if t['bozuk']:
            uyari.append('%s: bozuk satır %s' % (ad, t['bozuk']))
        if t['tekrar']:
            uyari.append('%s: yinelenen no %s' % (ad, ','.join(t['tekrar'])))
        for no, r in t['satir'].items():
            if not no.startswith(onek + '-'):
                uyari.append('%s: yabancı önek %s' % (ad, no))
            if is_sinif(r['İş']) == '?' or karar_sinif(r['Karar']) == '?' or onc_sinif(r['Öncelik']) == 0:
                uyari.append('%s: okunamayan hücre %s (iş=%r öncelik=%r karar=%r)' % (ad, no, r['İş'][:12], r['Öncelik'][:6], r['Karar'][:14]))
        if ham and dog:
            ek = [n for n in dog['sira'] if n not in ham['satir']]
            if ek:
                uyari.append('%s: ham tabloda olmayan satır eklendi %s' % (ad, ','.join(ek)))
        gorulen = {}
        for p in t['paket']:
            eksik = [a for a in ALANLAR if not p.get(a)]
            if eksik:
                uyari.append('%s: %s alanı eksik %s' % (ad, p['id'], ','.join(eksik)))
            nolar = []
            for o, s in NOLAR.findall(p.get('Satırlar', '')):
                n = norm(o, s)
                if n not in nolar:
                    nolar.append(n)
            var = [n for n in nolar if n in t['satir']]
            yok = [n for n in nolar if n not in t['satir']]
            if yok:
                uyari.append('%s: %s tabloda olmayan satırı sayıyor %s' % (ad, p['id'], ','.join(yok)))
            p['satirlar'] = var
            p['is'] = is_sinif(p.get('İş', ''))
            p['karar'] = karar_sinif(p.get('Karar', ''))
            if p['is'] == '?':
                uyari.append('%s: %s iş okunamadı %r' % (ad, p['id'], p.get('İş', '')[:20]))
            if p['karar'] not in ('gerekmez', 'sahip'):
                uyari.append('%s: %s karar okunamadı %r' % (ad, p['id'], p.get('Karar', '')[:20]))
            onc = [onc_sinif(t['satir'][n]['Öncelik']) for n in var]
            onc = [o for o in onc if o]
            p['onc'] = min(onc) if onc else 3
            kararlar = {karar_sinif(t['satir'][n]['Karar']) for n in var}
            if 'kapalı' in kararlar:
                uyari.append('%s: %s kapalı satır taşıyor' % (ad, p['id']))
            if p['karar'] == 'gerekmez' and 'sahip' in kararlar:
                uyari.append('%s: %s karar gerekmez diyor ama sahip satırı var' % (ad, p['id']))
            for n in var:
                if n in gorulen:
                    uyari.append('%s: %s hem %s hem %s içinde' % (ad, n, gorulen[n], p['id']))
                gorulen[n] = p['id']
            p['kol'] = ad
            p['sekme'] = sekme
            p['dalga'] = dalga(p)
            p['yer'] = [y.strip(' `.') for y in re.split(r'[,;]', p.get('Dokunacağı yer', '')) if y.strip(' `.')]
            k['paket'].append(p)
        k['pakette'] = gorulen
        acik = [n for n in t['sira'] if n not in gorulen and onc_sinif(t['satir'][n]['Öncelik']) in (1, 2) and karar_sinif(t['satir'][n]['Karar']) != 'kapalı']
        k['paketsiz'] = acik
        if dog and not t['paket']:
            uyari.append('%s: paket önerisi yok' % ad)
    tum = set(p['id'] for k in kollar for p in k['paket'])
    for k in kollar:
        komsu = {}
        for l in bolum(k['t'], 'Pakete Girmeyenler'):
            m = re.match(r'^\s*[-*]\s*([A-Z]{3})-(\d+)\s*:\s*([A-Z]{3}-P\d+) kapsıyor', l)
            if m:
                komsu[norm(m.group(1), m.group(2))] = m.group(3)
        yok = sorted(set(v for v in komsu.values() if v not in tum))
        if yok:
            uyari.append('%s: gösterilen komşu paket yok %s' % (k['ad'], ','.join(yok)))
        k['komsu'] = dict((n, v) for n, v in komsu.items() if v in tum and n in k['t']['satir'] and n not in k.get('pakette', {}))
        acik = [n for n in k['paketsiz'] if n not in k['komsu']]
        if acik and k['t']['paket']:
            uyari.append('%s: pakete girmemiş öncelik 1-2 satır %s' % (k['ad'], ','.join(acik)))
    return kollar, uyari


def dalga(p):
    if p['karar'] != 'gerekmez':
        return 'K'
    o, i = p['onc'], p['is']
    if i == 'küçük' and o == 1:
        return '1'
    if (i == 'küçük' and o == 2) or (i == 'orta' and o == 1):
        return '2'
    if (i == 'orta' and o == 2) or (i == 'büyük' and o == 1):
        return '3'
    return '4'


def kesisir(a, b):
    for x in a['yer']:
        for y in b['yer']:
            if x == y:
                return True
            if x.endswith('/') and y.startswith(x):
                return True
            if y.endswith('/') and x.startswith(y):
                return True
    return False


def say(t):
    s = {'toplam': 0, 1: 0, 2: 0, 3: 0, 'küçük': 0, 'orta': 0, 'büyük': 0, 'gerekmez': 0, 'sahip': 0, 'kapalı': 0}
    for r in t['satir'].values():
        s['toplam'] += 1
        o = onc_sinif(r['Öncelik'])
        if o:
            s[o] += 1
        i = is_sinif(r['İş'])
        if i != '?':
            s[i] += 1
        k = karar_sinif(r['Karar'])
        if k != '?':
            s[k] += 1
    return s


def sayim_tablosu(kollar):
    bas = ['Kol', 'Önek', 'Ham', 'Çıkarılan', 'Kalan', 'Ö1', 'Ö2', 'Ö3', 'Küçük', 'Orta', 'Büyük', 'Gerekmez', 'Sahip', 'Kapalı', 'Öncelik Değişen', 'Paket', 'Pakette Satır']
    out = ['| ' + ' | '.join(bas) + ' |', '|' + '---|' * len(bas)]
    top = [0] * (len(bas) - 2)
    veri = {}
    for k in kollar:
        ham = k['ham'] or {'satir': {}}
        t = k['t']
        s = say(t)
        cikan = len([n for n in ham['satir'] if n not in t['satir']]) if k['dog'] else 0
        degisen = len([n for n in t['satir'] if n in ham['satir'] and onc_sinif(ham['satir'][n]['Öncelik']) != onc_sinif(t['satir'][n]['Öncelik'])]) if k['dog'] else 0
        d = [len(ham['satir']), cikan, s['toplam'], s[1], s[2], s[3], s['küçük'], s['orta'], s['büyük'], s['gerekmez'], s['sahip'], s['kapalı'], degisen, len(k['paket']), len(k['pakette'])]
        veri[k['ad']] = dict(zip(bas[2:], d))
        top = [a + b for a, b in zip(top, d)]
        out.append('| ' + k['sekme'] + ' — ' + k['alt'] + ' | ' + k['onek'] + ' | ' + ' | '.join(str(x) for x in d) + ' |')
    out.append('| **Toplam** | | ' + ' | '.join('**%d**' % x for x in top) + ' |')
    veri['Toplam'] = dict(zip(bas[2:], top))
    return out, veri


def tum_paketler(kollar):
    kol_sira = {k['ad']: i for i, k in enumerate(kollar)}
    ps = [p for k in kollar for p in k['paket']]
    ps.sort(key=lambda p: (DALGA_SIRA.index(p['dalga']), p['onc'], IS_SIRA[p['is']], kol_sira[p['kol']], p['n']))
    for p in ps:
        p['cakisan'] = [q['id'] for q in ps if q is not p and q['dalga'] == p['dalga'] and kesisir(p, q)]
    return ps


def dalga_sayim(ps):
    bas = ['Dalga', 'Paket', 'Küçük', 'Orta', 'Büyük', 'Satır']
    out = ['| ' + ' | '.join(bas) + ' |', '|' + '---|' * len(bas)]
    top = [0] * 5
    for dl in DALGA_SIRA:
        g = [p for p in ps if p['dalga'] == dl]
        d = [len(g), len([p for p in g if p['is'] == 'küçük']), len([p for p in g if p['is'] == 'orta']), len([p for p in g if p['is'] == 'büyük']), sum(len(p['satirlar']) for p in g)]
        top = [a + b for a, b in zip(top, d)]
        out.append('| ' + DALGA_AD[dl] + ' | ' + ' | '.join(str(x) for x in d) + ' |')
    out.append('| **Toplam** | ' + ' | '.join('**%d**' % x for x in top) + ' |')
    return out


def paket_satiri(p, sekme=True):
    h = [p['id']]
    if sekme:
        h.append(p['sekme'])
    h += [p['ad'], ', '.join(p['satirlar']) or '—', p['is'], str(p['onc']), 'gerekmez' if p['karar'] == 'gerekmez' else p.get('Karar', '').replace('|', '/'), p['dalga']]
    return '| ' + ' | '.join(h) + ' |'


def kisa_yer(yer):
    ad = []
    for parca in re.split(r'[,;]', yer or ''):
        m = re.search(r'([\w.\-]+\.(?:cs|axaml|py|yml|md|json|ps1|sh|iss|csproj))', parca)
        if m and m.group(1) not in ad:
            ad.append(m.group(1))
    return ', '.join(ad[:10]) if ad else (yer or '—').strip()[:160]


def kisa_plan(plan):
    plan = (plan or '—').strip()
    return plan if len(plan) <= 340 else plan[:340].rsplit(' ', 1)[0] + ' …'


def girdi(kollar, uyari, cikti):
    tablo, veri = sayim_tablosu(kollar)
    ps = tum_paketler(kollar)
    o = ['# Sentez Girdisi', '', 'Bu dosyayı betik üretti. Sayıların tek kaynağı buradaki tablolardır.', '', '## Sayım', ''] + tablo + ['', '## Dalga Sayımı', ''] + dalga_sayim(ps) + ['']
    o += ['## Dalga Kuralı', '', 'Dalga, paketin kararına, işine ve içindeki en yüksek önceliğe göre betikle atanır:', '']
    o += ['- ' + DALGA_AD[d] for d in DALGA_SIRA] + ['']
    for k in kollar:
        t = k['t']
        o += ['## Kol: %s — %s (%s, dosya `%s`)' % (k['sekme'], k['alt'], k['onek'], k['ad']), '']
        v = veri[k['ad']]
        o += ['Ham %d, çıkarılan %d, kalan %d; öncelik 1/2/3: %d/%d/%d; paket %d.' % (v['Ham'], v['Çıkarılan'], v['Kalan'], v['Ö1'], v['Ö2'], v['Ö3'], v['Paket']), '']
        for b in ('Sekme Özeti', 'Yinelenenler', 'Doğrulanamayanlar', 'Ek Notlar', 'Pakete Girmeyenler'):
            m = [x for x in bolum(t, b) if x.strip()]
            if m:
                o += ['### %s' % b, ''] + m + ['']
        if k['paket']:
            o += ['### Paketler', '']
            for p in k['paket']:
                o += ['- **%s — %s** · iş %s · en yüksek öncelik %d · dalga %s · karar: %s' % (p['id'], p['ad'], p['is'], p['onc'], p['dalga'], p.get('Karar', '?')), '  - Satırlar: ' + ', '.join('%s (%s)' % (n, t['satir'][n]['Eksik'].split(':')[0][:60]) for n in p['satirlar']), '  - Yer: ' + kisa_yer(p.get('Dokunacağı yer', '')), '  - Plan: ' + kisa_plan(p.get('Plan', ''))]
            o += ['']
        kap = [r for r in t['satir'].values() if karar_sinif(r['Karar']) == 'kapalı']
        if kap:
            o += ['### Kapalı Karara Denk Gelen Satırlar', '']
            o += ['- %s · %s · %s · kimde: %s' % (r['No'], r['Eksik'], r['Karar'], r['Kimde Var']) for r in kap] + ['']
    os.makedirs(cikti, exist_ok=True)
    with open(os.path.join(cikti, 'sentez-girdisi.md'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(o) + '\n')
    with open(os.path.join(cikti, 'sayim.json'), 'w', encoding='utf-8') as f:
        json.dump(veri, f, ensure_ascii=False, indent=1)
    with open(os.path.join(cikti, 'uyari.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(uyari) + '\n')
    print('\n'.join(tablo))
    print()
    print('\n'.join(dalga_sayim(ps)))
    print()
    print('UYARI %d' % len(uyari))
    for u in uyari:
        print(' -', u)


def kanit(kollar, depo):
    for k in kollar:
        if not k['dog']:
            continue
        sat = [x for x in bolum(k['dog'], 'Çıkarılanlar') if x.strip().startswith('|') and '---' not in x]
        print('== %s: %d çıkarılan satır' % (k['ad'], max(0, len(sat) - 1)))
        for x in sat[1:]:
            h = hucreler(x)
            print('  ' + ' | '.join(c[:90] for c in h[:3]))
            bulundu = KANIT.findall(x)
            if not bulundu:
                print('     KANIT YOK')
            for yol, no in bulundu[:2]:
                tam = os.path.join(depo, yol.replace('\\', '/'))
                if not os.path.exists(tam):
                    print('     %s:%s DOSYA YOK' % (yol, no))
                    continue
                with open(tam, encoding='utf-8', errors='replace') as f:
                    ls = f.read().split('\n')
                n = int(no)
                for j in range(max(1, n - 1), min(len(ls), n + 1) + 1):
                    print('     %s:%d  %s' % (os.path.basename(yol), j, ls[j - 1].strip()[:150]))


TARIH = '2026-10-13'
TARIH_TR = '13 Ekim 2026'
SURUM = 'eb0a5db0 (v1.2.16)'
SOZ = 'şimdi tüm piyasayı araştır her bir sekmedeki eksiklerimiz hakkında kapsamlı plan yap saat 8 e kadar durma şu aşamada plan yap sadece işlem 2. aşama olacak bulduklarını later a eklersin'
TETIK = ['Api-Key', 'apiKey', 'api_key', 'secret', 'token', 'bearer', 'password', 'parola']
JWT = re.compile(r'eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.')
UZUN = re.compile(r'[A-Za-z0-9]{24,}')
PID = re.compile(r'([A-ZÇĞİÖŞÜ]{2,4})-P(\d+)')
UZANTI = re.compile(r'\.(cs|axaml|json|md|ps1|sh|yml|yaml|props|csproj|xaml|js|py|iss|txt|xml|targets|resx|toml|plist)$')
TUR_SINIRI = 6


def kokuyor(s):
    if JWT.search(s):
        return True
    a = s.lower()
    if not any(t.lower() in a for t in TETIK):
        return False
    return any(any(c.isdigit() for c in m) and any(c.isalpha() for c in m) for m in UZUN.findall(s))


def tara(dosyalar):
    vuran = []
    for yol in dosyalar:
        with open(yol, encoding='utf-8') as f:
            metin = f.read()
        for i, l in enumerate(metin.split('\n'), 1):
            if kokuyor(l):
                vuran.append((yol, i, l.strip()))
        bir = re.sub(r'"\s*\+\s*"', '', metin)
        if bir != metin:
            for i, l in enumerate(bir.split('\n'), 1):
                if kokuyor(l):
                    vuran.append((yol, -i, l.strip()))
    return vuran


def kirp(v):
    v = list(v)
    while v and not v[0].strip():
        v.pop(0)
    while v and not v[-1].strip():
        v.pop()
    return v


def sentez_oku(yol):
    d, cur = {}, None
    if not yol or not os.path.exists(yol):
        return d
    with open(yol, encoding='utf-8') as f:
        for ham in f:
            l = ham.rstrip('\r\n')
            b = BASLIK.match(l)
            if b:
                cur = b.group(1).strip()
                d[cur] = []
                continue
            if cur is not None:
                d[cur].append(l)
    return d


def sentez_bolum(s, ad):
    for k, v in s.items():
        if k.lower().startswith(ad.lower()):
            return kirp(v)
    return []


def esler(s, pm, uyari):
    es, neden = {}, {}
    for l in sentez_bolum(s, 'Aynı İş'):
        if not l.lstrip().startswith(('-', '*')):
            continue
        bas, _, son = l.partition(':')
        if '=' not in bas:
            continue
        ids = ['%s-P%d' % (o, int(n)) for o, n in PID.findall(bas)]
        bilinmeyen = [x for x in ids if x not in pm]
        if bilinmeyen:
            uyari.append('sentez: bilinmeyen paket %s' % ','.join(bilinmeyen))
        ids = [x for x in ids if x in pm]
        if len(ids) < 2:
            continue
        for x in ids[1:]:
            if x != ids[0] and x not in es and ids[0] not in es:
                es[x] = ids[0]
        neden[ids[0]] = son.strip()
    return es, neden


def yol_norm(p):
    out = []
    for y in p['yer']:
        m = re.match(r'[\w./\\*-]+', y.strip(' `'))
        if not m:
            continue
        s = m.group(0).replace('\\', '/')
        if '/' not in s and not UZANTI.search(s):
            continue
        if not s.endswith('/') and not UZANTI.search(s):
            s += '/'
        if 'Locales' in s or s.startswith(('tests/', 'docs/')):
            continue
        if s not in out:
            out.append(s)
    return out


def carpisir(a, b):
    for x in a['yol']:
        for y in b['yol']:
            if x == y or (x.endswith('/') and y.startswith(x)) or (y.endswith('/') and x.startswith(y)):
                return True
    return False


def turlar(g):
    tur = []
    for p in g:
        for t in tur:
            if len(t) < TUR_SINIRI and p['yol'] and not any(carpisir(p, q) for q in t):
                t.append(p)
                break
        else:
            tur.append([p])
    return tur


def hucre(s):
    return (s or '—').replace('|', '/').strip()


def karar_metni(p):
    k = p.get('Karar', '').strip()
    if p['karar'] == 'gerekmez':
        return 'gerekmez'
    return re.sub(r'^\**sahip\**\s*:?\s*', '', k, flags=re.I).strip() or 'sahip kararı'


def kopyala(src, hedef):
    if os.path.exists(src) and os.path.abspath(src) != os.path.abspath(hedef):
        with open(src, encoding='utf-8') as f:
            metin = f.read().replace('\r\n', '\n')
        os.makedirs(os.path.dirname(hedef), exist_ok=True)
        with open(hedef, 'w', encoding='utf-8', newline='\n') as f:
            f.write(metin.rstrip('\n') + '\n')
    return os.path.exists(hedef)


def yaz(yol, satirlar):
    os.makedirs(os.path.dirname(yol), exist_ok=True)
    with open(yol, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(satirlar).rstrip('\n') + '\n')


def kur(kollar, uyari, klasor, depo, cikti, sentez_yolu):
    tablo, veri = sayim_tablosu(kollar)
    ps = tum_paketler(kollar)
    pm = {p['id']: p for p in ps}
    kolm = {k['ad']: k for k in kollar}
    for p in ps:
        p['yol'] = yol_norm(p)
    if not os.path.exists(sentez_yolu):
        sentez_yolu = os.path.join(klasor, 'sentez.md')
    s = sentez_oku(sentez_yolu)
    if not s:
        uyari.append('sentez raporu yok: %s' % sentez_yolu)
    for ad in ('Genel Değerlendirme', 'İlk On Paket', 'Aynı İş, İki Kol', 'Sahibin Kararları', 'Kapalı Kararlar Ve Pazar', 'Sınırlar'):
        if s and len(' '.join(sentez_bolum(s, ad))) < 120:
            uyari.append('sentez: boş ya da eksik bölüm %s' % ad)
    es, es_neden = esler(s, pm, uyari)
    ek_satir = {}
    for ikinci, ilk in es.items():
        ek_satir.setdefault(ilk, []).append(ikinci)

    kanit_dir = os.path.join(depo, 'docs', 'piyasa', TARIH)
    os.makedirs(kanit_dir, exist_ok=True)
    yazilan = []
    for k in kollar:
        for ek in ('.md', '-eksikler.md', '-dogrulanmis.md'):
            hedef = os.path.join(kanit_dir, k['ad'] + ek)
            if kopyala(os.path.join(klasor, k['ad'] + ek), hedef):
                yazilan.append(hedef)
    ust = os.path.dirname(os.path.dirname(os.path.abspath(klasor)))
    kisa = os.path.join(cikti, 'kisa.md')
    if not os.path.exists(kisa):
        kisa = os.path.join(klasor, 'kisa-cevap.md')
    for src, hedef_ad in ((os.path.join(ust, 'ORTAK.md'), 'yontem-arastirma.md'), (os.path.join(ust, 'DOGRULAMA.md'), 'yontem-dogrulama.md'), (os.path.join(ust, 'SENTEZ.md'), 'yontem-sentez.md'),(sentez_yolu, 'sentez.md'), (kisa, 'kisa-cevap.md')):
        hedef = os.path.join(kanit_dir, hedef_ad)
        if kopyala(src, hedef):
            yazilan.append(hedef)

    for ad in ('t0-kod-okumasi.md', 'ek-kanit.md', 'karar-sorulari.md'):
        hedef = os.path.join(kanit_dir, ad)
        if kopyala(os.path.join(cikti, ad), hedef):
            yazilan.append(hedef)

    kb = 'piyasa/%s/' % TARIH
    top = veri['Toplam']
    o = ['# Pazar Eksikleri Planı', '', '%s · kod okuması `%s` · birinci aşama: yalnız araştırma ve plan' % (TARIH_TR, SURUM), '']
    o += ['Sahibin isteği, birebir:', '', '> ' + SOZ, '']
    o += ['Bu belge o isteğin birinci aşamasıdır. Kod yazılmadı, hiçbir şey derlenmedi, uygulama çalıştırılmadı.', 'İkinci aşamada yapılacak işler paket paket [rafta](later.md) duruyor; sıra oradaki sıradır.', '']
    o += ['## Kısa Cevap', '']
    raf_p = [p for p in ps if p['id'] not in es]
    dsay = dict((dl, len([p for p in raf_p if p['dalga'] == dl])) for dl in DALGA_SIRA)
    dusen = 'doğrulama turu %d tanesini bizde zaten var diye düşürdü, %d satır kaldı' % (top['Çıkarılan'], top['Kalan']) if top['Çıkarılan'] else 'doğrulama turunda bizde tam olarak var çıkan satır olmadı, %d satırın hepsi kaldı' % top['Kalan']
    o += ['%d kol %d eksik yazdı; %s. Kalanların %d tanesi öncelik 1, %d tanesi öncelik 2; %d satırın önceliği doğrulamada değişti.' % (len(kollar), top['Ham'], dusen, top['Ö1'], top['Ö2'], top['Öncelik Değişen'])]
    o += ['Satırlar %d iş paketine toplandı. %d paket bir karar beklemeden başlayabilir (dalga sırasıyla %s), %d paket senin kararını bekliyor.' % (len(raf_p), sum(n for dl, n in dsay.items() if dl != 'K'), ', '.join(str(dsay[dl]) for dl in DALGA_SIRA if dl != 'K'), dsay.get('K', 0))]
    o += ['%d satır daha önce kapattığın bir karara denk geliyor; onlar hiçbir pakete girmedi.' % top['Kapalı']]
    komsu_say = sum(len(k.get('komsu', {})) for k in kollar)
    if komsu_say:
        o += ['%d satır kendi kolunun paketine girmedi ama başka bir kolun paketi aynı işi kapsıyor; hangisi olduğu o kolun "Pakete girmeyenler" listesinde.' % komsu_say]
    o += ['']
    if os.path.exists(kisa):
        with open(kisa, encoding='utf-8') as f:
            o += kirp(f.read().replace('\r\n', '\n').split('\n')) + ['']
    else:
        uyari.append('kisa.md yok')
        o += ['(yazılmadı)', '']

    o += ['## Sayılar', '', 'Sayıların hepsi betikle tablolardan sayıldı; elle sayı yazılmadı.', '']
    o += tablo + ['']
    o += ['**Ham** araştırma kolunun yazdığı satır sayısıdır. **Çıkarılan**, doğrulamada bizde zaten var çıktığı için düşen satırdır.', '**Ö1, Ö2, Ö3** öncelik, **Küçük, Orta, Büyük** iş tahmini (bir günün altı, birkaç gün, bir hafta ve üstü).', '**Sahip** senin kararını bekleyen, **Kapalı** daha önce verdiğin bir karara denk gelen satırdır.', '']
    o += dalga_sayim([p for p in ps if p['id'] not in es]) + ['']
    if es:
        o += ['%d paket başka bir kolun paketiyle aynı iş çıktı; dalga tablosunda ve rafta bir kez sayıldı.' % len(es), '']

    for ad in ('Genel Değerlendirme', 'İlk On Paket'):
        o += ['## ' + ad, '']
        govde = sentez_bolum(s, ad)
        o += (govde or ['(sentez yazılmadı)']) + ['']
        if ad == 'İlk On Paket' and govde:
            sira = []
            for l in govde:
                for a, n in PID.findall(l):
                    i = '%s-P%d' % (a, int(n))
                    if i in pm and i not in sira:
                        sira.append(i)
            if sira:
                o += ['| Sıra | Paket | Sekme | Ad | İş | Öncelik | Karar | Dalga |', '|---|---|---|---|---|---|---|---|']
                for j, i in enumerate(sira[:10], 1):
                    p = pm[i]
                    o.append('| %d | %s | %s | %s | %s | %d | %s | %s |' % (j, i, p['sekme'], hucre(p['ad']), p['is'], p['onc'], hucre(karar_metni(p)), p['dalga']))
                o.append('')

    o += ['## Sekme Sekme', '', 'Her kolun altında doğrulama kolunun kendi özeti, paket tablosu ve paketlerin planı var.', 'Satır numaraları (örnek `OYA-07`) o kolun doğrulanmış tablosundaki satırı gösterir.', '']
    onceki = None
    for k in kollar:
        t, v = k['t'], veri[k['ad']]
        if k['sekme'] != onceki:
            o += ['### ' + k['sekme'], '']
            onceki = k['sekme']
        o += ['#### %s (%s)' % (k['alt'], k['onek']), '']
        o += ['%d eksik yazıldı, doğrulamada %d satır çıktı, %d satır kaldı. Öncelik 1, 2, 3: %d, %d, %d.' % (v['Ham'], v['Çıkarılan'], v['Kalan'], v['Ö1'], v['Ö2'], v['Ö3']), '%d satır sahibin kararını bekliyor, %d satır kapalı bir karara denk geliyor. %d paket önerildi.' % (v['Sahip'], v['Kapalı'], v['Paket']), '']
        if not k['dog']:
            o += ['**Bu kolun tablosu doğrulama turundan geçmedi; satırlar araştırma kolunun iddiasıdır.**', '']
        ozet = kirp(bolum(t, 'Sekme Özeti'))
        if ozet:
            o += ozet + ['']
        if k['paket']:
            o += ['| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |', '|---|---|---|---|---|---|---|']
            for p in k['paket']:
                o.append('| %s | %s | %s | %s | %d | %s | %s |' % (p['id'], hucre(p['ad']), ', '.join(p['satirlar']) or '—', p['is'], p['onc'], 'gerekmez' if p['karar'] == 'gerekmez' else 'sahip', p['dalga']))
            o.append('')
            for p in k['paket']:
                o.append('- **%s — %s.** %s' % (p['id'], p['ad'], (p.get('Plan', '') or 'Plan yazılmadı.').strip()))
                o.append('  - Dokunacağı yer: %s' % (p.get('Dokunacağı yer', '') or '—').strip())
                if p['karar'] != 'gerekmez':
                    o.append('  - Sahibin kararı: %s' % karar_metni(p))
                if p['id'] in es:
                    o.append('  - Aynı iş: %s. Rafta o başlığın altında duruyor.' % es[p['id']])
            o.append('')
        girmeyen = kirp(bolum(t, 'Pakete Girmeyenler'))
        if girmeyen:
            o += ['Pakete girmeyenler:', ''] + girmeyen + ['']
        bag = []
        if k['dog']:
            bag.append('[doğrulanmış tablo](%s%s-dogrulanmis.md)' % (kb, k['ad']))
        bag.append('[ham tablo](%s%s-eksikler.md)' % (kb, k['ad']))
        bag.append('[notlar ve kaynaklar](%s%s.md)' % (kb, k['ad']))
        o += ['Kanıt: ' + ' · '.join(bag), '']

    o += ['## İkinci Aşama Dalga Planı', '', 'Dalga, paketin kararına, işine ve içindeki en yüksek önceliğe göre betikle atandı; yargı değil kuraldır.', 'Karar beklemeyen iş önce gelir, çünkü senin cevabını beklemeden başlayabilir.', '']
    o += ['**Tur**, aynı anda ayrı işçilere verilebilecek paket kümesidir: dokunacakları yerler kesişmiyor.', 'Kesişme hesabında çeviri dosyaları ve testler sayılmadı; bir turda en çok %d paket var.' % TUR_SINIRI, '']
    for dl in DALGA_SIRA:
        g = [p for p in ps if p['dalga'] == dl and p['id'] not in es]
        o += ['### ' + DALGA_AD[dl], '']
        if not g:
            o += ['Bu dalgada paket yok.', '']
            continue
        if dl == 'K':
            o += ['| Paket | Sekme | Ad | İş | Öncelik | Sahibin Vereceği Karar |', '|---|---|---|---|---|---|']
            for p in g:
                o.append('| %s | %s | %s | %s | %d | %s |' % (p['id'], p['sekme'], hucre(p['ad']), p['is'], p['onc'], hucre(karar_metni(p))))
            o.append('')
            continue
        o += ['| Tur | Paket | Sekme | Ad | İş | Öncelik | Satır | Dokunacağı Yer |', '|---|---|---|---|---|---|---|---|']
        for i, tur in enumerate(turlar(g), 1):
            for p in tur:
                satir = len(p['satirlar']) + sum(len(pm[x]['satirlar']) for x in ek_satir.get(p['id'], []))
                o.append('| %s.%d | %s | %s | %s | %s | %d | %d | %s |' % (dl, i, p['id'], p['sekme'], hucre(p['ad']), p['is'], p['onc'], satir, hucre(p.get('Dokunacağı yer', ''))))
        o.append('')

    o += ['## Sahibin Kararları', '']
    o += (sentez_bolum(s, 'Sahibin Kararları') or ['(sentez yazılmadı)']) + ['']
    sorular = os.path.join(kanit_dir, 'karar-sorulari.md')
    if os.path.exists(sorular):
        with open(sorular, encoding='utf-8') as f:
            govde = f.read().strip('\n').split('\n')
        while govde and (govde[0].startswith('# ') or not govde[0].strip()):
            govde.pop(0)
        o += govde + ['']
    o += ['## Aynı İş, İki Kol', '']
    o += (sentez_bolum(s, 'Aynı İş') or ['İki kolun aynı işi önerdiği paket görülmedi.']) + ['']

    o += ['## Kapalı Kararlar Ve Pazar', '']
    o += (sentez_bolum(s, 'Kapalı Kararlar') or ['(sentez yazılmadı)']) + ['']
    kap = [(k, r) for k in kollar for r in k['t']['satir'].values() if karar_sinif(r['Karar']) == 'kapalı']
    if kap:
        o += ['Kapalı bir karara denk gelen %d satırın tamamı:' % len(kap), '', '| No | Eksik | Karar | Kimde Var |', '|---|---|---|---|']
        for k, r in kap:
            o.append('| %s | %s | %s | %s |' % (r['No'], hucre(r['Eksik']), hucre(r['Karar']), hucre(r['Kimde Var'])))
        o.append('')

    dogsuz = [k['sekme'] + ' — ' + k['alt'] for k in kollar if not k['dog']]
    o += ['## Yöntem Ve Sınırlar', '']
    o += ['- %d araştırma kolu aynı ortak kuralla çalıştı: önce koddan envanter (her "bizde var" satırı `dosya:satır` taşır), sonra rakip taraması, sonra eksik tablosu. Kurallar: [yontem-arastirma.md](%syontem-arastirma.md).' % (len(kollar), kb)]
    o += ['- Son kol (Platform Eşitliği) öteki kollardan farklı bir soruya baktı: pazarda olup bizde hiç olmayana değil, bizde Windows\'ta olup macOS ya da Linux\'ta eksik kalana. Öteki kolların aynı farkı yazdığı satırlar Aynı İş bölümünde eşlendi.']
    o += ['- Her tabloyu ayrı bir doğrulama kolu satır satır ölçtü: bizde gerçekten yok mu (kodda en az üç adla arama), önceki bir karara denk geliyor mu, öncelik 1 satırın kaynağı açılıyor mu. Kurallar: [yontem-dogrulama.md](%syontem-dogrulama.md).' % kb]
    if top['Çıkarılan']:
        o += ['- Doğrulamada %d satırdan %d tanesi bizde zaten var çıktığı için düştü, %d satırın önceliği değişti.' % (top['Ham'], top['Çıkarılan'], top['Öncelik Değişen'])]
    else:
        o += ['- Doğrulamada %d satırın hiçbiri bizde zaten var çıkmadı; %d satırın önceliği değişti.' % (top['Ham'], top['Öncelik Değişen'])]
    o += ['- Değerlendirme bölümlerini (Genel Değerlendirme, İlk On Paket, Sahibin Kararları, Aynı İş, Kapalı Kararlar) tek bir sentez kolu yazdı; sayılar, tablolar ve dalgalar betikten çıktı. Kurallar: [yontem-sentez.md](%syontem-sentez.md).' % kb]
    o += ['- İş büyüklüğü (küçük, orta, büyük) tahmindir, ölçüm değildir.']
    o += ['- Uygulama çalıştırılmadı: bizdeki davranış koddan okundu, ekranda denenmedi. Rakip özelliği de kurulup denenmedi; resmi sayfadan, belgeden ya da sürüm notundan okundu.']
    o += ['- Görülemeyen şeye "rakipte yok" denmedi, "doğrulanamadı" dendi. Kol kol liste doğrulanmış tabloların Doğrulanamayanlar bölümünde.']
    o += ['- Kod okuması 9de48d8b üzerinde yapıldı; v1.2.16 (eb0a5db0) ile arasındaki fark yalnız sürüm numarası ve değişiklik günlüğü.']
    t0 = os.path.join(kanit_dir, 't0-kod-okumasi.md')
    okunan = 0
    if os.path.exists(t0):
        with open(t0, encoding='utf-8') as f:
            t0l = f.read().split('\n')
        okunan = len([l for l in t0l if re.match(r'^\| \d+ \|', l)])
        orn = [l for l in t0l if re.match(r'^\| [A-Z]{3}-\d+ \|', l)]
        if orn:
            neden = 'yalnız %d satırı' % top['Çıkarılan'] if top['Çıkarılan'] else 'hiçbir satırı'
            ek = 'düşürdüğü' if top['Çıkarılan'] else 'düşürmediği'
            o += ['- Doğrulama turu %s "bizde zaten var" diye %s için ana oturum "yok" denen satırlardan rastgele %d tanesini kaynakta kendisi aradı; %d tanesi gerçekten yok çıktı. Bu bir yoklamadır, kanıt değil.' % (neden, ek, len(orn), len([l for l in orn if 'yok çıktı' in l]))]
        if any(l.startswith('## Ölçülen Tek Şey') for l in t0l):
            o += ['- Koşulan tek komut ffmpeg süzgeç listesidir; çıktısı kod okuması dosyasının sonunda.']
        tutan = len([l for l in t0l if re.match(r'^\| \d+ \|', l) and l.rstrip().endswith('| tuttu |')])
        sonuc = 'hepsi tuttu' if tutan == okunan else '%d tanesi tuttu' % tutan
        kimlik = set()
        for l in t0l:
            if re.match(r'^\| \d+ \|', l):
                kimlik.update(norm(a, b) for a, b in re.findall(r'([A-Z]{3})-(\d+)', l.split('|')[2]))
        bir = [no for k in kollar for no, r in k['t']['satir'].items() if onc_sinif(r['Öncelik']) == 1]
        bir_okunan = len([n for n in bir if n in kimlik])
        kapsam = 'hepsi' if bir_okunan == len(bir) else '%d tanesi' % bir_okunan
        ornek = set(norm(a, b) for l in orn for a, b in re.findall(r'^\| ([A-Z]{3})-(\d+) \|', l))
        iki = sorted(set(n for p in ps if p['dalga'] in ('1', '2') and p['id'] not in es for n in p['satirlar']))
        iki_okunan = len([n for n in iki if n in kimlik or n in ornek])
        iki_kapsam = 'hepsi' if iki_okunan == len(iki) else '%d tanesi' % iki_okunan
        uc = sorted(set(n for p in ps if p['dalga'] == '3' and p['id'] not in es for n in p['satirlar']))
        uc_okunan = len([n for n in uc if n in kimlik or n in ornek])
        uc_kapsam = 'hepsi' if uc_okunan == len(uc) else '%d tanesi' % uc_okunan
        dort = sorted(set(n for p in ps if p['dalga'] == '4' and p['id'] not in es for n in p['satirlar']))
        dort_okunan = len([n for n in dort if n in kimlik or n in ornek])
        dort_kapsam = 'hepsi' if dort_okunan == len(dort) else '%d tanesi' % dort_okunan
        tum = [no for k in kollar for no in k['t']['satir']]
        tum_okunan = len([n for n in tum if n in kimlik or n in ornek])
        o += ['- Ajan raporu iddiadır. Ana oturum %d iddiayı kendisi koddan açıp okudu, %s; öncelik 1 kalan %d satırın %s, ilk iki dalganın paketlerindeki %d satırın %s, üçüncü dalganın paketlerindeki %d satırın %s, dördüncü dalganın paketlerindeki %d satırın %s bu okumanın ya da yukarıdaki yoklamanın içinde. Bütün satırlarda sayı %d: %d satırı ana oturum da okudu, %d satır yalnız doğrulama kolunun okumasına dayanıyor. Liste: [t0-kod-okumasi.md](%st0-kod-okumasi.md).' % (okunan, sonuc, len(bir), kapsam, len(iki), iki_kapsam, len(uc), uc_kapsam, len(dort), dort_kapsam, len(tum), tum_okunan, len(tum) - tum_okunan, kb)]
    yz = os.path.join(kanit_dir, 'ajan-yazismalari.md')
    if os.path.exists(yz):
        with open(yz, encoding='utf-8') as f:
            yl = f.read().split('\n')
        alt = len([l for l in yl if l.startswith('### Alt Kol ')])
        donmeyen = len([l for l in yl if l.startswith('Rapor dönmedi: alt kol')])
        if alt:
            o += ['- Bazı araştırma kolları işi kendi içinde böldü: %d alt kol açıldı, %d tanesi rapor döndü. Alt kolların bulguları açan kolun notlarına girdi; görev metinleri ve raporları yazışma dosyasında.' % (alt, alt - donmeyen)]
    if dogsuz:
        o += ['- Doğrulama turundan geçmeyen kol: %s.' % ', '.join(dogsuz)]
    o += ['']
    sinir = sentez_bolum(s, 'Sınırlar')
    if sinir:
        o += sinir + ['']
    if uyari:
        o += ['Betiğin tablolarda gördüğü %d tutarsızlık:' % len(uyari), ''] + ['- ' + u for u in uyari] + ['']

    o += ['## Kanıt Dosyaları', '', '| Kol | Notlar Ve Kaynaklar | Ham Tablo | Doğrulanmış Tablo |', '|---|---|---|---|']
    for k in kollar:
        o.append('| %s — %s | [%s.md](%s%s.md) | [%s-eksikler.md](%s%s-eksikler.md) | %s |' % (k['sekme'], k['alt'], k['ad'], kb, k['ad'], k['ad'], kb, k['ad'], ('[%s-dogrulanmis.md](%s%s-dogrulanmis.md)' % (k['ad'], kb, k['ad'])) if k['dog'] else '—'))
    o += ['', 'Ajanlara giden görev metinleri ve dönen son raporlar: [ajan-yazismalari.md](%sajan-yazismalari.md).' % kb]
    if s:
        o += ['Sentez kolunun yazdığı metin, birebir: [sentez.md](%ssentez.md). Bu belgedeki değerlendirme bölümleri oradan alındı.' % kb]
    if okunan:
        o += ['Ana oturumun kendi okuduğu kod satırları: [t0-kod-okumasi.md](%st0-kod-okumasi.md).' % kb]
    ekk = os.path.join(kanit_dir, 'ek-kanit.md')
    if os.path.exists(ekk):
        with open(ekk, encoding='utf-8') as f:
            o += [l for l in f.read().split('\n') if l.strip() and not l.startswith('#')]
    o += ['Sayıları ve bu belgeyi üreten betik: `tools/pazar-plani/topla.py` (nasıl koşulur: `tools/pazar-plani/AGENTS.md`).', '']
    plan_yolu = os.path.join(depo, 'docs', 'plan-pazar-eksikleri.md')
    yaz(plan_yolu, o)
    yazilan.append(plan_yolu)

    r = ['# Raf', '', 'Sahibin sözü, %s:' % TARIH_TR, '', '> ' + SOZ, '']
    r += ['Aşağıdaki her başlık, ikinci aşamada tek seferde teslim edilebilecek bir iş paketidir; pazar araştırmasından çıktı.', 'Sıra yapım sırasıdır. Bir paketin işi başlayınca başlığı buradan silinir.', 'Gerekçe, sayılar ve kanıt: [plan-pazar-eksikleri.md](plan-pazar-eksikleri.md).', '']
    n = 0
    for dl in DALGA_SIRA:
        g = [p for p in ps if p['dalga'] == dl and p['id'] not in es]
        if not g:
            continue
        r += ['**%s**' % DALGA_AD[dl], '']
        for p in g:
            n += 1
            kimlik = [p['id']] + ek_satir.get(p['id'], [])
            r += ['## %d. %s' % (n, p['ad']), '']
            r += ['%s · "bulduklarını later a eklersin" · %s · %s · iş %s · öncelik %d · karar %s' % (TARIH_TR, ' = '.join(kimlik), p['sekme'], p['is'], p['onc'], 'gerekmez' if p['karar'] == 'gerekmez' else 'sahibin'), '']
            for i in kimlik:
                q = pm[i]
                t = kolm[q['kol']]['t']
                r.append('- Satırlar (%s): %s. [Tablo](%s%s-%s.md)' % (i, '; '.join('%s %s' % (no, t['satir'][no]['Eksik'].split(':')[0].strip()) for no in q['satirlar']) or '—', kb, q['kol'], 'dogrulanmis' if kolm[q['kol']]['dog'] else 'eksikler'))
            r.append('- Dokunacağı yer: %s' % (p.get('Dokunacağı yer', '') or '—').strip())
            if p['karar'] != 'gerekmez':
                r.append('- Sahibin kararı: %s' % karar_metni(p))
            r.append('- Plan: %s' % (p.get('Plan', '') or '—').strip())
            r.append('')
    raf_yolu = os.path.join(depo, 'docs', 'later.md')
    yaz(raf_yolu, r)
    yazilan.append(raf_yolu)

    yaz(os.path.join(cikti, 'uyari.txt'), uyari or ['yok'])
    print('\n'.join(tablo))
    print()
    print('\n'.join(dalga_sayim([p for p in ps if p['id'] not in es])))
    print()
    print('PAKET %d, RAF BAŞLIĞI %d, EŞ %d, YAZILAN DOSYA %d' % (len(ps), n, len(es), len(yazilan)))
    print('UYARI %d' % len(uyari))
    for u in uyari:
        print(' -', u)
    ek = [os.path.join(kanit_dir, x) for x in os.listdir(kanit_dir) if x.endswith('.md')]
    vuran = tara(sorted(set(yazilan + ek)))
    print('TARAMA %d' % len(vuran))
    for yol, i, l in vuran:
        print(' ! %s:%d  %s' % (os.path.relpath(yol, depo), i, l[:220]))


def main():
    kip, klasor = sys.argv[1], sys.argv[2]
    kollar, uyari = yukle(klasor)
    if kip == 'girdi':
        girdi(kollar, uyari, sys.argv[3])
    elif kip == 'kanit':
        kanit(kollar, sys.argv[3])
    elif kip == 'kur':
        depo, cikti = sys.argv[3], sys.argv[4]
        varsayilan = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(klasor))), 'reports', os.path.basename(os.path.abspath(klasor)) + '.md')
        kur(kollar, uyari, klasor, depo, cikti, sys.argv[5] if len(sys.argv) > 5 else varsayilan)


if __name__ == '__main__':
    main()
