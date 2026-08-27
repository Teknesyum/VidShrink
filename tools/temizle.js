const fs = require('fs');
const path = require('path');

const kok = process.cwd();
const calisma = path.resolve(kok, '.calisma');

function icerideMi(hedef) {
  const bagil = path.relative(calisma, hedef);
  return bagil !== '' && !bagil.startsWith('..') && !path.isAbsolute(bagil);
}

const istekler = process.argv.slice(2);

if (!istekler.length) {
  if (!fs.existsSync(calisma)) {
    console.log('.calisma yok');
    process.exit(0);
  }
  const girdiler = fs.readdirSync(calisma);
  if (!girdiler.length) {
    console.log('.calisma bos');
    process.exit(0);
  }
  let toplam = 0;
  for (const ad of girdiler) {
    const boy = boyut(path.join(calisma, ad));
    toplam += boy;
    console.log(mb(boy).padStart(9) + '  ' + ad);
  }
  console.log('-'.repeat(24));
  console.log(mb(toplam).padStart(9) + '  toplam');
  console.log('\nSilmek icin: node tools/temizle.js <ad> [<ad> ...]   ya da   node tools/temizle.js --hepsi');
  process.exit(0);
}

const hedefler =
  istekler[0] === '--hepsi'
    ? fs.readdirSync(calisma).map((ad) => path.join(calisma, ad))
    : istekler.map((ad) => path.resolve(calisma, ad));

let silinen = 0;
for (const hedef of hedefler) {
  if (!icerideMi(hedef)) {
    console.error('REDDEDILDI ' + hedef + ' — .calisma disinda');
    process.exitCode = 1;
    continue;
  }
  if (!fs.existsSync(hedef)) {
    console.log('yok       ' + path.relative(calisma, hedef));
    continue;
  }
  const boy = boyut(hedef);
  fs.rmSync(hedef, { recursive: true, force: true });
  silinen += boy;
  console.log('silindi   ' + path.relative(calisma, hedef) + '  (' + mb(boy) + ')');
}

if (silinen) console.log('\nkazanilan: ' + mb(silinen));

function boyut(hedef) {
  let bilgi;
  try {
    bilgi = fs.statSync(hedef);
  } catch {
    return 0;
  }
  if (!bilgi.isDirectory()) return bilgi.size;
  let toplam = 0;
  for (const ad of fs.readdirSync(hedef)) toplam += boyut(path.join(hedef, ad));
  return toplam;
}

function mb(bayt) {
  if (bayt >= 1024 * 1024 * 1024) return (bayt / 1024 / 1024 / 1024).toFixed(1) + ' GB';
  if (bayt >= 1024 * 1024) return (bayt / 1024 / 1024).toFixed(1) + ' MB';
  if (bayt >= 1024) return (bayt / 1024).toFixed(1) + ' KB';
  return bayt + ' B';
}
