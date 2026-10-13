import json
import os
import re
import sys

ANAHTAR = ('.calisma\\pazar', '.calisma/pazar', 'Sekme sekme pazar eksikleri', 'pazar-karar-sorulari')
BILDIRIM = re.compile(r'<task-notification>(.*?)</task-notification>', re.S)


def etiket(metin, ad):
    m = re.search(r'<%s>(.*?)</%s>' % (ad, ad), metin, re.S)
    return m.group(1).strip() if m else ''


def metinler(icerik):
    if isinstance(icerik, str):
        return [icerik]
    out = []
    for b in icerik or []:
        if isinstance(b, dict):
            if b.get('type') == 'text':
                out.append(b.get('text', ''))
            elif b.get('type') == 'tool_result':
                out += metinler(b.get('content'))
    return out


def cit(metin):
    return ['~~~~text'] + metin.replace('\r\n', '\n').replace('~~~~', '~~~').split('\n') + ['~~~~']


def saat(s):
    return s[:16].replace('T', ' ') + ' UTC' if s else '?'


def oku(yol, kabul):
    ajan, sira, kimlik, mesaj, rapor = {}, [], {}, [], {}
    with open(yol, encoding='utf-8') as f:
        for ham in f:
            try:
                e = json.loads(ham)
            except ValueError:
                continue
            m = e.get('message') or {}
            ic = m.get('content')
            if e.get('type') == 'assistant' and isinstance(ic, list):
                for b in ic:
                    if not isinstance(b, dict) or b.get('type') != 'tool_use':
                        continue
                    g = b.get('input') or {}
                    if b.get('name') in ('Agent', 'Task') and kabul(g.get('prompt', '')):
                        if b['id'] not in ajan:
                            ajan[b['id']] = {'ad': g.get('description', ''), 'tur': g.get('subagent_type', 'general-purpose'), 'model': g.get('model', ''), 'giden': g.get('prompt', ''), 'saat': e.get('timestamp', '')}
                            sira.append(b['id'])
                    elif b.get('name') == 'SendMessage':
                        mesaj.append((g.get('to', ''), g.get('message', '') or g.get('content', ''), e.get('timestamp', '')))
            if isinstance(ic, list):
                for b in ic:
                    if isinstance(b, dict) and b.get('type') == 'tool_result' and b.get('tool_use_id') in ajan:
                        t = '\n'.join(metinler(b.get('content')))
                        k = re.search(r'agentId:\s*(\w+)', t)
                        if k:
                            kimlik[k.group(1)] = b['tool_use_id']
            ek = e.get('attachment') or {}
            kaynaklar = metinler(ic) + [e.get('content') if isinstance(e.get('content'), str) else '', ek.get('prompt') if isinstance(ek.get('prompt'), str) else '']
            for t in kaynaklar:
                for blok in BILDIRIM.findall(t or ''):
                    tid, sonuc = etiket(blok, 'tool-use-id'), etiket(blok, 'result')
                    if tid in ajan and sonuc and len(sonuc) >= len(rapor.get(tid, '')):
                        rapor[tid] = sonuc
    return ajan, sira, kimlik, mesaj, rapor


def main():
    kaynak, hedef = sys.argv[1], sys.argv[2]
    altdir = sys.argv[3] if len(sys.argv) > 3 else os.path.join(os.path.splitext(kaynak)[0], 'subagents')
    ajan, sira, kimlik, mesaj, rapor = oku(kaynak, lambda p: any(a in p for a in ANAHTAR))
    o = ['# Ajan Yazışmaları', '', '13 Ekim 2026. Pazar araştırmasında her ajana giden görev metni ve ajanın döndüğü son rapor, birebir.', 'Dönen rapor bir iddiadır; doğrulama turunun neyi düzelttiği doğrulanmış tablolarda yazılı.', '', 'Bazı araştırma kolları işi kendi içinde böldü ve alt kol açtı. O alt kolların görev metni ve raporu, açan kolun altında "Alt Kol" başlığıyla durur; onları sahip ya da ana oturum değil, kolun kendisi yazdı.', '']
    ters = {v: k for k, v in kimlik.items()}
    alt_say = alt_rapor = 0
    for n, i in enumerate(sira, 1):
        a = ajan[i]
        aid = ters.get(i, '')
        o += ['## %d. %s' % (n, a['ad']), '', 'Ajan türü: %s%s · açılış: %s' % (a['tur'], ' · model: ' + a['model'] if a['model'] else '', saat(a['saat'])), '', '### Giden Görev', ''] + cit(a['giden']) + ['']
        for m, s in [(m, s) for to, m, s in mesaj if to == aid and m]:
            o += ['### Ara Mesaj (%s)' % saat(s), ''] + cit(m) + ['']
        o += ['### Dönen Rapor', '']
        o += cit(rapor[i]) if i in rapor else ['Rapor dönmedi ya da bu dosya yazılırken ajan hâlâ çalışıyordu.']
        o += ['']
        yol = os.path.join(altdir, 'agent-%s.jsonl' % aid)
        if not aid or not os.path.exists(yol):
            continue
        a2, s2, _, _, r2 = oku(yol, lambda p: True)
        for k, j in enumerate(s2, 1):
            b = a2[j]
            alt_say += 1
            o += ['### Alt Kol %d.%d: %s' % (n, k, b['ad']), '', 'Açan: yukarıdaki kol · ajan türü: %s%s · açılış: %s' % (b['tur'], ' · model: ' + b['model'] if b['model'] else '', saat(b['saat'])), '', '#### Giden Görev', ''] + cit(b['giden']) + ['', '#### Dönen Rapor', '']
            if j in r2:
                alt_rapor += 1
                o += cit(r2[j])
            else:
                o += ['Rapor dönmedi: alt kol açılamadı ya da kol onu beklemeden bitirdi.']
            o += ['']
    with open(hedef, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(o).rstrip('\n') + '\n')
    print('ajan %d, raporu dönen %d, ara mesaj %d, alt kol %d, alt kol raporu %d' % (len(sira), len([i for i in sira if i in rapor]), len([1 for to, m, s in mesaj if to in kimlik]), alt_say, alt_rapor))


main()
