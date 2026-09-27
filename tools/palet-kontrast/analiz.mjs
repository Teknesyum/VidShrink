import fs from 'node:fs';
import path from 'node:path';

const klasor = process.argv[2];
const asama = process.argv[3] || 'once';
const esik = t => (t === 'yazi' ? 7 : 3);

const lum = hex => {
  const h = hex.replace('#', '');
  const c = h.length === 8 ? h.slice(2) : h;
  const ch = i => {
    const v = parseInt(c.slice(i, i + 2), 16) / 255;
    return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * ch(0) + 0.7152 * ch(2) + 0.0722 * ch(4);
};

const ozet = {};
for (const dosya of fs.readdirSync(klasor).filter(f => f.startsWith('kontrast-' + asama + '-') && f.endsWith('.tsv'))) {
  const satirlar = fs.readFileSync(path.join(klasor, dosya), 'utf8').trim().split('\n').slice(1).map(s => s.split('\t'));
  for (const [palet, ekran, durum, tur, oran, zemin, on, anahtar, yol, metin] of satirlar) {
    if (durum === 'edilgen') continue;
    if (parseFloat(oran) >= esik(tur)) continue;
    const k = palet + ' ' + anahtar;
    ozet[k] ??= { palet, anahtar, en: 99, zeminler: new Map() };
    const o = ozet[k];
    o.en = Math.min(o.en, parseFloat(oran));
    const z = zemin + ' L' + lum(zemin).toFixed(3) + ' on ' + on;
    o.zeminler.set(z, (o.zeminler.get(z) || '') + ' ' + ekran + '/' + durum + '/' + tur + ':' + oran + ' ' + yol.split('/').slice(-2).join('/'));
  }
}
for (const o of Object.values(ozet).sort((a, b) => a.palet.localeCompare(b.palet))) {
  console.log(o.palet + '\t' + o.anahtar + '\t' + o.en.toFixed(2));
  for (const [z, n] of o.zeminler) console.log('    ' + z + ' |' + n.slice(0, 300));
}
console.log('toplam', Object.keys(ozet).length);
