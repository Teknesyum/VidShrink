param([string]$Calisma = '.calisma/gop10', [string]$Json = 'gop.json')
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$surePath = Join-Path $D 'kodlama-sn.txt'
if (-not (Test-Path $surePath)) { Set-Content $surePath '0' }
$urun = @(
    'hareketli:av1_nvenc:1000:1882x802:-11.07', 'hareketli:av1_nvenc:2000:1882x802:-1.37',
    'hareketli:h264_nvenc:1000:1382x588:-4.06', 'hareketli:h264_nvenc:2000:1382x588:-5.67',
    'hareketli:hevc_nvenc:1000:1650x702:-8.68', 'hareketli:hevc_nvenc:2000:1882x802:-6.43',
    'karanlik:av1_nvenc:1000:1882x802:-3.07', 'karanlik:av1_nvenc:2000:1882x802:-6.05',
    'karanlik:h264_nvenc:1000:1382x588:-4.15', 'karanlik:h264_nvenc:2000:1382x588:-0.54',
    'karanlik:hevc_nvenc:1000:1650x702:-2.61', 'karanlik:hevc_nvenc:2000:1882x802:-5.31',
    'parlak:av1_nvenc:1000:1882x802:-14.81', 'parlak:av1_nvenc:2000:1882x802:-1.91',
    'parlak:h264_nvenc:1000:1344x572:-6.29', 'parlak:h264_nvenc:2000:1344x572:-1.29',
    'parlak:hevc_nvenc:1000:1804x768:-14.59', 'parlak:hevc_nvenc:2000:1882x802:-3.80',
    'orta:av1_nvenc:2000:1920x818:-1.68', 'orta:h264_nvenc:2000:1536x654:-4.12', 'orta:hevc_nvenc:2000:1882x802:-6.14'
)
$kol = 'g120=-rc vbr -multipass fullres -g 120 PEAK=2 BUF=2;g240=-rc vbr -multipass fullres -g 240 PEAK=2 BUF=2'
foreach ($u in $urun) {
    $k, $c, $kbit, $geo, $sap = $u.Split(':')
    $w, $h = $geo.Split('x')
    $hedef = [math]::Round([double]$kbit * 1.024 * (1 + [double]::Parse($sap, $Inv) / 100), 1)
    $pr = if ($c -eq 'av1_nvenc') { 'p6' } else { 'p4' }
    & (Join-Path $PSScriptRoot '..\nvenc-2\tara.ps1') -Calisma $Calisma -Json $Json -Hucreler "${k}:${c}:${w}:${h}:$($hedef.ToString($Inv)):$pr" -KolTanim $kol
}
