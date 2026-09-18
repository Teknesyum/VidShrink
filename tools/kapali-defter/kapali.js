const fs = require('fs');
const path = require('path');

const GUN = 7;
const KOK = path.resolve(__dirname, '..', '..');
const BIN = path.join(KOK, 'trash');
const DEFTER = path.join(KOK, '.claude', 'kapali.md');

const BASLIK = '# Kapali Defter\n\n' +
  'Kapanan is satirlari. Kaynak: defter kancasinin `trash/acik-<damga>.md` dokumleri.\n' +
  'Yalniz son ' + GUN + ' gun tutulur; sekizinci gune gecen satir silinir.\n\n';

function gunBugun(ms) {
  const d = new Date(ms);
  const p = (n) => String(n).padStart(2, '0');
  return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate());
}

function damgadanGun(ad) {
  const m = /^acik-(\d{4})-(\d{2})-(\d{2})T/.exec(ad);
  return m ? m[1] + '-' + m[2] + '-' + m[3] : null;
}

function varOlan() {
  let metin = '';
  try { metin = fs.readFileSync(DEFTER, 'utf8'); } catch { return new Map(); }
  const bolumler = new Map();
  let gun = null;
  for (const satir of metin.split(/\r?\n/)) {
    const bas = /^## (\d{4}-\d{2}-\d{2})$/.exec(satir.trim());
    if (bas) { gun = bas[1]; if (!bolumler.has(gun)) bolumler.set(gun, []); continue; }
    if (gun && /^[-*]\s+\[x\]/i.test(satir.trim())) bolumler.get(gun).push(satir.trim());
  }
  return bolumler;
}

function topla(bolumler) {
  let adlar = [];
  try { adlar = fs.readdirSync(BIN).filter((n) => /^acik-.*\.md$/.test(n)); } catch { return []; }

  const yutulan = [];
  for (const ad of adlar) {
    const gun = damgadanGun(ad);
    if (!gun) continue;
    const dosya = path.join(BIN, ad);
    let metin = '';
    try { metin = fs.readFileSync(dosya, 'utf8'); } catch { continue; }

    if (!bolumler.has(gun)) bolumler.set(gun, []);
    const liste = bolumler.get(gun);
    for (const satir of metin.split(/\r?\n/)) {
      const s = satir.trim();
      if (!/^[-*]\s+\[x\]/i.test(s)) continue;
      if (!liste.includes(s)) liste.push(s);
    }
    yutulan.push(dosya);
  }
  return yutulan;
}

function yaz(bolumler) {
  const sinir = gunBugun(Date.now() - (GUN - 1) * 86400000);
  const gunler = [...bolumler.keys()].filter((g) => g >= sinir).sort().reverse();

  let govde = BASLIK;
  let sayi = 0;
  for (const gun of gunler) {
    const liste = bolumler.get(gun);
    if (!liste.length) continue;
    govde += '## ' + gun + '\n\n' + liste.join('\n') + '\n\n';
    sayi += liste.length;
  }

  fs.mkdirSync(path.dirname(DEFTER), { recursive: true });
  fs.writeFileSync(DEFTER, govde, 'utf8');
  return { sayi, gun: gunler.length, dusen: [...bolumler.keys()].filter((g) => g < sinir).length };
}

const bolumler = varOlan();
const yutulan = topla(bolumler);
const olcu = yaz(bolumler);
for (const dosya of yutulan) { try { fs.unlinkSync(dosya); } catch {} }

process.stdout.write(
  'kapali defter: ' + olcu.sayi + ' satir, ' + olcu.gun + ' gun; ' +
  yutulan.length + ' dokum yutuldu, ' + olcu.dusen + ' gun yasla dustu\n');
