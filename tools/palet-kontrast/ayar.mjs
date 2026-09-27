import fs from 'node:fs';
import path from 'node:path';

const [klasor, asama, yazArg] = process.argv.slice(2);
const yaz = yazArg === '--yaz';
const kok = path.resolve(import.meta.dirname, '../..');
const tohumYolu = path.join(kok, 'src/VidShrink.App/Themes/Palette/seeds.json');
const PAY = 0.15;
const esik = t => (t === 'yazi' ? 7 : 3) + PAY;

const rgb = hex => { const h = hex.replace('#', ''); const c = h.length === 8 ? h.slice(2) : h; return [0, 2, 4].map(i => parseInt(c.slice(i, i + 2), 16)); };
const hexOf = ([r, g, b]) => '#' + [r, g, b].map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0').toUpperCase()).join('');
const lum = hex => { const k = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; const [r, g, b] = rgb(hex); return 0.2126 * k(r) + 0.7152 * k(g) + 0.0722 * k(b); };
const kars = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const hsl = hex => { let [r, g, b] = rgb(hex).map(v => v / 255); const mx = Math.max(r, g, b), mn = Math.min(r, g, b); let h = 0, s = 0; const l = (mx + mn) / 2; if (mx !== mn) { const d = mx - mn; s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn); h = mx === r ? (g - b) / d + (g < b ? 6 : 0) : mx === g ? (b - r) / d + 2 : (r - g) / d + 4; h /= 6; } return [h, s, l]; };
const fromHsl = (h, s, l) => { if (s === 0) return hexOf([l * 255, l * 255, l * 255]); const q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q; const f = t => { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1 / 6) return p + (q - p) * 6 * t; if (t < 1 / 2) return q; if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6; return p; }; return hexOf([f(h + 1 / 3) * 255, f(h) * 255, f(h - 1 / 3) * 255]); };
const kaydir = (hex, yon, kosul) => {
  const [h, s, l0] = hsl(hex);
  if (kosul(hex)) return { hex, tamam: true };
  let enIyi = hex;
  for (let i = 1; i <= 400; i++) {
    const l = Math.max(0, Math.min(1, l0 + yon * i * 0.0025));
    const aday = fromHsl(h, s, l);
    if (kosul(aday)) return { hex: aday, tamam: true };
    enIyi = aday;
    if (l === 0 || l === 1) break;
  }
  return { hex: enIyi, tamam: false };
};

const rol = { TextBody: 'text', NeonBlue: 'renk-1', PinkText: 'renk-2-text', NeonSuccess: 'success', EmberBlaze: 'warning' };
const dolgular = { NeonBlue: 'renk-1', NeonPink: 'renk-2', NeonPurple: 'renk-3' };

const metin = fs.readFileSync(tohumYolu, 'utf8');
const satirlar = metin.split('\r\n');
const tohumlar = JSON.parse(metin);

const rapor = [];
for (const dosya of fs.readdirSync(klasor).filter(f => f.startsWith('kontrast-' + asama + '-') && f.endsWith('.tsv'))) {
  const kayit = fs.readFileSync(path.join(klasor, dosya), 'utf8').trim().split('\n').slice(1).map(s => s.split('\t'))
    .map(([palet, ekran, durum, tur, oran, zemin, on, anahtar, yol]) => ({ palet, ekran, durum, tur, oran: +oran, zemin, on, anahtar, yol }))
    .filter(r => r.durum !== 'edilgen');
  if (!kayit.length) continue;
  const palet = kayit[0].palet;
  const acik = kayit.filter(r => r.oran < esik(r.tur) - PAY);
  if (!acik.length) continue;
  const tohum = tohumlar.find(t => t.name === palet);
  if (tohum.kaynak !== 'vidshrink') { rapor.push(palet + ' standart palet, atlandı'); continue; }
  const acikZemin = lum(tohum.black) > 0.5;
  const yeni = { ...tohum };
  const yapisal = [];

  for (const [anahtar, alan] of Object.entries(rol)) {
    if (!acik.some(r => r.anahtar === anahtar)) continue;
    const ilgili = kayit.filter(r => r.anahtar === anahtar);
    const cozulur = ilgili.filter(r => Math.max(kars('#000000', r.zemin), kars('#FFFFFF', r.zemin)) >= esik(r.tur));
    for (const r of ilgili.filter(r => !cozulur.includes(r))) yapisal.push(anahtar + ' ' + r.ekran + '/' + r.durum + ' zemin ' + r.zemin + ' ' + r.yol.split(' @')[0].split(' > ').slice(-2).join('>'));
    const zeminler = [...new Map(cozulur.map(r => [r.zemin + r.tur, r])).values()];
    const sonuc = kaydir(tohum[alan], acikZemin ? -1 : 1, h => zeminler.every(r => kars(h, r.zemin) >= esik(r.tur)));
    const enDusuk = Math.min(...zeminler.map(r => kars(sonuc.hex, r.zemin)));
    yeni[alan] = sonuc.hex;
    rapor.push(palet + ' ' + anahtar + ' (' + alan + ') ' + tohum[alan] + ' -> ' + sonuc.hex + ' en düşük ' + enDusuk.toFixed(2) + (sonuc.tamam ? '' : ' ULASILAMADI'));
  }

  const onSatir = kayit.filter(r => r.anahtar === 'OnNeon');
  const eskiDolgu = Object.fromEntries(Object.entries(dolgular).map(([k, a]) => [tohum[a].toUpperCase(), a]));
  const dolguSatir = onSatir.filter(r => eskiDolgu[r.zemin.slice(-6).replace(/^/, '#').toUpperCase()]);
  if (onSatir.length) {
    const enKotu = t => Math.min(...['renk-1', 'renk-2', 'renk-3'].map(a => kars(t, yeni[a])));
    const secilen = t => ['renk-1', 'renk-2', 'renk-3'].map(a => kaydir(yeni[a], t === '#000000' ? 1 : -1, h => kars(t, h) >= 7 + PAY));
    const ihtiyac = t => secilen(t).reduce((s, r, i) => s + Math.abs(hsl(r.hex)[2] - hsl(yeni[['renk-1', 'renk-2', 'renk-3'][i]])[2]), 0);
    const once = enKotu('#000000') >= enKotu('#FFFFFF') ? '#000000' : '#FFFFFF';
    if (enKotu(once) < 7 + PAY) {
      const hedef = acikZemin ? '#FFFFFF' : '#000000';
      const yeniDolgu = secilen(hedef);
      ['renk-1', 'renk-2', 'renk-3'].forEach((a, i) => {
        if (yeniDolgu[i].hex !== yeni[a]) rapor.push(palet + ' OnNeon ' + hedef + ' için ' + a + ' ' + yeni[a] + ' -> ' + yeniDolgu[i].hex + (yeniDolgu[i].tamam ? '' : ' ULASILAMADI'));
        yeni[a] = yeniDolgu[i].hex;
      });
      const son = enKotu('#000000') >= enKotu('#FFFFFF') ? '#000000' : '#FFFFFF';
      rapor.push(palet + ' OnNeon ' + son + ' en kötü dolgu ' + enKotu(son).toFixed(2) + ' (ölçülen dolgu satırı ' + dolguSatir.length + ')');
    }
  }
  if (yapisal.length) rapor.push(palet + ' yapısal (' + yapisal.length + '): ' + [...new Set(yapisal)].slice(0, 6).join(' | '));

  const i = satirlar.findIndex(s => s.includes('"name":"' + palet + '"'));
  for (const alan of [...Object.values(rol), 'renk-2', 'renk-3']) {
    if (yeni[alan] !== tohum[alan]) satirlar[i] = satirlar[i].replace('"' + alan + '":"' + tohum[alan] + '"', '"' + alan + '":"' + yeni[alan] + '"');
  }
}
console.log(rapor.join('\n'));
if (yaz) fs.writeFileSync(tohumYolu, satirlar.join('\r\n'));
