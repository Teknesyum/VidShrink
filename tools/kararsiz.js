const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const kok = process.cwd();

const secenekler = { kosum: 10, etiket: 'tur', derleme: true, filtre: null, gozle: 0 };
for (let i = 2; i < process.argv.length; i++) {
  const arg = process.argv[i];
  if (arg === '--kosum') secenekler.kosum = Number(process.argv[++i]);
  else if (arg === '--etiket') secenekler.etiket = process.argv[++i];
  else if (arg === '--filtre') secenekler.filtre = process.argv[++i];
  else if (arg === '--derlemesiz') secenekler.derleme = false;
  else if (arg === '--gozle') secenekler.gozle = Number(process.argv[++i]);
  else {
    console.error('bilinmeyen secenek: ' + arg);
    process.exit(2);
  }
}

const gunluk = path.resolve(kok, '.calisma', 't62');
fs.mkdirSync(gunluk, { recursive: true });

if (secenekler.gozle > 0) {
  gozetle(secenekler.gozle);
  return;
}

if (secenekler.derleme) {
  console.log('derleme: dotnet build -c Release');
  const derle = spawnSync('dotnet', ['build', '-c', 'Release'], { cwd: kok, encoding: 'utf8' });
  fs.writeFileSync(path.join(gunluk, secenekler.etiket + '-derleme.log'), (derle.stdout || '') + (derle.stderr || ''));
  if (derle.status !== 0) {
    console.error('derleme basarisiz — ' + path.join(gunluk, secenekler.etiket + '-derleme.log'));
    process.exit(1);
  }
}

const sonuclar = [];

for (let n = 1; n <= secenekler.kosum; n++) {
  const ad = secenekler.etiket + '-' + String(n).padStart(2, '0');
  const trx = path.join(gunluk, ad + '.trx');
  if (fs.existsSync(trx)) fs.rmSync(trx);

  const args = ['test', '-c', 'Release', '--logger', 'trx;LogFileName=' + trx];
  if (!secenekler.derleme) args.push('--no-build');
  if (secenekler.filtre) args.push('--filter', secenekler.filtre);

  const basladi = Date.now();
  const kosum = spawnSync('dotnet', args, { cwd: kok, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const sure = ((Date.now() - basladi) / 1000).toFixed(1);
  fs.writeFileSync(path.join(gunluk, ad + '.log'), (kosum.stdout || '') + (kosum.stderr || ''));

  const rapor = trxOku(trx);
  sonuclar.push({ ad, sure, cikis: kosum.status, ...rapor });

  const satir = ad + '  ' + sure + ' sn  cikis ' + kosum.status
    + '  gecti ' + rapor.gecti + '  kaldi ' + rapor.kaldi.length + '  atlandi ' + rapor.atlandi;
  console.log(satir);
  for (const kirmizi of rapor.kaldi) console.log('    KIRMIZI ' + kirmizi.ad + '\n      ' + kirmizi.ileti.replace(/\s+/g, ' ').slice(0, 400));
}

const kirmiziKosum = sonuclar.filter((s) => s.kaldi.length > 0);
const ozet = [
  '',
  'kosum ' + sonuclar.length + ', kirmizi kosum ' + kirmiziKosum.length,
];
const sayac = new Map();
for (const s of kirmiziKosum) for (const k of s.kaldi) sayac.set(k.ad, (sayac.get(k.ad) || 0) + 1);
for (const [ad, adet] of sayac) ozet.push('  ' + ad + ' — ' + adet + ' kez');
console.log(ozet.join('\n'));

fs.writeFileSync(path.join(gunluk, secenekler.etiket + '-ozet.json'), JSON.stringify(sonuclar, null, 2));

process.exitCode = kirmiziKosum.length ? 1 : 0;

function gozetle(saniye) {
  const temp = process.env.TEMP || process.env.TMP || require('os').tmpdir();
  const cikti = path.join(gunluk, secenekler.etiket + '-temp.log');
  fs.writeFileSync(cikti, 'gozlenen dizin: ' + temp + '\n');
  let onceki = new Set();
  const bitis = Date.now() + saniye * 1000;

  const tik = setInterval(() => {
    let simdi;
    try { simdi = new Set(fs.readdirSync(temp).filter((ad) => ad.startsWith('vidshrink_'))); }
    catch { return; }

    const eklenen = [...simdi].filter((ad) => !onceki.has(ad));
    const silinen = [...onceki].filter((ad) => !simdi.has(ad));
    const damga = new Date().toISOString().slice(11, 23);
    for (const ad of eklenen) fs.appendFileSync(cikti, damga + '  + ' + ad + '\n');
    if (silinen.length) fs.appendFileSync(cikti, damga + '  - ' + silinen.length + ' dosya: ' + silinen.join(' ') + '\n');
    onceki = simdi;

    if (Date.now() > bitis) { clearInterval(tik); console.log('gozlem bitti: ' + cikti); }
  }, 50);
}

function trxOku(yol) {
  if (!fs.existsSync(yol)) return { gecti: 0, atlandi: 0, kaldi: [{ ad: '(trx yok)', ileti: yol }] };
  const metin = fs.readFileSync(yol, 'utf8');
  const sayilar = /<Counters[^>]*passed="(\d+)"[^>]*/.exec(metin);
  const atlanan = /notExecuted="(\d+)"/.exec(metin);
  const kaldi = [];
  const desen = /<UnitTestResult[^>]*testName="([^"]*)"[^>]*outcome="Failed"[\s\S]*?<\/UnitTestResult>/g;
  let esles;
  while ((esles = desen.exec(metin))) {
    const govde = esles[0];
    const ileti = /<Message>([\s\S]*?)<\/Message>/.exec(govde);
    const iz = /<StackTrace>([\s\S]*?)<\/StackTrace>/.exec(govde);
    kaldi.push({
      ad: coz(esles[1]),
      ileti: coz(ileti ? ileti[1] : '(ileti yok)'),
      iz: coz(iz ? iz[1] : '')
    });
  }
  return { gecti: sayilar ? Number(sayilar[1]) : 0, atlandi: atlanan ? Number(atlanan[1]) : 0, kaldi };
}

function coz(metin) {
  return metin
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&')
    .trim();
}
