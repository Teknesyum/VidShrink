const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const D = require(cozumle('denetim-kaydi.js'));

function cozumle(ad) {
  const kokler = [
    path.join(process.env.USERPROFILE || process.env.HOME, '.claude', 'plugins', 'cache', 'teknesyum', 'teknesyum'),
    path.join(process.env.USERPROFILE || process.env.HOME, '.claude', 'plugins', 'marketplaces', 'teknesyum', 'teknesyum')
  ];
  for (const kok of kokler) {
    if (!fs.existsSync(kok)) continue;
    const dogrudan = path.join(kok, 'hooks', ad);
    if (fs.existsSync(dogrudan)) return dogrudan;
    const surumler = fs.readdirSync(kok).filter(s => /^\d+\.\d+\.\d+$/.test(s)).sort(karsilastir).reverse();
    for (const surum of surumler) {
      const aday = path.join(kok, surum, 'hooks', ad);
      if (fs.existsSync(aday)) return aday;
    }
  }
  throw new Error(ad + ' bulunamadi: teknesyum eklentisi kurulu mu?');
}

function karsilastir(a, b) {
  const x = a.split('.').map(Number), y = b.split('.').map(Number);
  for (let i = 0; i < 3; i++) if (x[i] !== y[i]) return x[i] - y[i];
  return 0;
}

const kok = process.cwd();
const relay = path.join(kok, '.claude', 'relay');
const headSha = execSync('git rev-parse HEAD').toString().trim();
const isler = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));

for (const is of isler) {
  const yol = path.join(relay, 'contracts', is.id + '.md');
  let metin = fs.readFileSync(yol, 'utf8');

  const blok = metin.match(/^owns:[ \t]*\r?\n((?:[ \t]+-[ \t]+.+\r?\n)+)/m);
  if (blok) {
    const ogeler = blok[1].split(/\r?\n/).map(s => s.replace(/^[ \t]*-[ \t]+/, '').trim()).filter(Boolean);
    metin = metin.replace(blok[0], 'owns: [' + ogeler.join(', ') + ']\n');
  }

  const owns = D.ownsListesi(metin);
  if (!owns.length) { console.log('ATLA ' + is.id + ' — owns bos'); continue; }
  const tur = (metin.match(/^round:[ \t]*(\d+)/m) || [null, '0'])[1];

  for (const [ad, deger] of [['audit', 'passed'], ['auditor_id', is.auditor], ['diff', is.diff], ['verification', is.verification]]) {
    if (new RegExp('^' + ad + ':', 'm').test(metin)) metin = metin.replace(new RegExp('^' + ad + ':.*$', 'm'), ad + ': ' + deger);
    else metin = metin.replace(/^owns:/m, ad + ': ' + deger + '\nowns:');
  }
  fs.writeFileSync(yol, metin);

  fs.writeFileSync(D.kayitYolu(relay, is.id, tur), JSON.stringify({
    contractId: is.id, auditorRunId: is.auditor, headSha,
    diffHash: D.dosyaOzeti(kok, owns), owns,
    verification: String(is.verification).split(' · '),
    result: 'GECTI', createdAt: new Date().toISOString()
  }, null, 2));
  console.log('HAZIR ' + is.id + ' tur ' + tur + ' owns=' + owns.length);
}
