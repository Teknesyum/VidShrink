import fs from 'node:fs';
import path from 'node:path';

const kok = path.resolve(import.meta.dirname, '../..');
const satirlar = fs.readFileSync(kok + '/src/VidShrink.App/Themes/Theme.axaml', 'utf8').split(/\r?\n/);

const hesap = {
  TrackingLg: 'hesapla türetildi: tr-h2 0 em',
  TrackingMd: 'hesapla türetildi: tr-h3 0,02 em × fs-2 16',
  TrackingLabel: 'hesapla türetildi: tr-label 0,08 em × fs-2 16',
  LineHeightBody: 'hesapla türetildi: lh-body 1,6 × fs-2 16',
};
const ozel = {
  ScrollBarThickness: 'bilerek farklı: projenin kaydırma çubuğu şablonu, UretilmisTemaTests.KaydirmaCubuguProjeninSablonuVeKalinligiylaKalir pinliyor',
  BorderThinScalar: 'standartta skaler kenar yok: BorderWidth Thickness, x:Double ondan kurulamaz',
  RadiusPanelScalar: 'standartta skaler köşe yok: WindowRadius CornerRadius, x:Double ondan kurulamaz',
  FocusRingInnerRadius: 'hesapla türetildi: Radius 4 + 1 (halkanın iç kenarı); CornerRadius skalerden kurulamaz',
  FocusRingOuterRadius: 'hesapla türetildi: Radius 4 + FocusOffset 2 + 1; CornerRadius skalerden kurulamaz',
  FocusRingOutset: 'hesapla türetildi: -(FocusWidth 2 + FocusOffset 2 + BorderWidth 1); AVLN3000',
  FocusRingInnerInset: 'hesapla türetildi: FocusWidth 2 + FocusOffset 2; AVLN3000',
  FocusRingOuterInset: 'hesapla türetildi: FocusOffset 2; AVLN3000',
  FocusRingThickness: 'FocusWidth 2 ile aynı değer; AVLN3000 (Thickness skalerden kurulamaz)',
  IconSizeLg: 'standartta 24 px simge adımı yok (IconSize3 22, IconSize4 56); bağlamak simgeyi büyütür ya da küçültür',
  DropIconSize: 'standartta 48 px simge adımı yok (IconSize4 56)',
  ShareQrModuleSize: 'QR modülü; Space1 ile aynı sayı ama aralık değil, bağlamak sahte bağ kurar',
  PauseGlyphHold: 'süre; standartta tutma süresi belirteci yok',
};
const tur = { Thickness: 'AVLN3000: standart bileşik kenar payı taşımıyor, Thickness skalerden kurulamaz',
  CornerRadius: 'AVLN3000: standartta yarım köşe yok, CornerRadius skalerden kurulamaz',
  GridLength: 'AVLN3000: GridLength skalerden kurulamaz' };

const cikti = [];
const baslangic = satirlar.findIndex(s => s.includes('x:Key="TrackingLg"'));
const bitis = satirlar.findIndex(s => s.includes('x:Key="LinkGitHub"'));
satirlar.forEach((s, i) => {
  const m = s.match(/<(x:Double|Thickness|CornerRadius|GridLength|sys:TimeSpan) x:Key="([^"]+)">([^<]+)</);
  if (!m) return;
  const [, t, ad, deger] = m;
  const opak = i < baslangic && ad.endsWith('Opacity');
  if (!opak && (i < baslangic || i > bitis)) return;
  let neden = hesap[ad] ?? ozel[ad] ?? tur[t];
  if (!neden && opak) neden = 'standartta saydamlık belirteci yok (renkler alfa ile tanımlı); proje yüzeyinin katman saydamlığı';
  if (!neden) neden = 'standartta belirteç yok: bileşene özgü boyut ya da yerleşim tavanı';
  cikti.push([ad, deger, 'Theme.axaml:' + (i + 1), neden].join('\t'));
});
const eki = ['MotionStaggerMs\t40\t(silindi)\tbağlandı: MainWindow.axaml.cs:442 standardın Stagger belirtecinden (0,04 sn) okuyor',
  'MotionStaggerCount\t-\t(silindi)\tölü belirteç, hiçbir yerde okunmuyordu'];
fs.writeFileSync(kok + '/docs/ui-denetim/2026-09-27-uc2/turetilemeyen.txt', ['ad\tdeğer\tyer\tdurum'].concat(cikti, eki).join('\r\n') + '\r\n');
const say = {};
for (const c of cikti) { const k = c.split('\t')[3].split(':')[0]; say[k] = (say[k] || 0) + 1; }
console.log(cikti.length, JSON.stringify(say));
