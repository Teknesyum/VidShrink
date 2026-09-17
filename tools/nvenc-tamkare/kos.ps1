param([ValidateSet('Saglama', 'A', 'B')][string]$Adim, [switch]$TamKare, [string]$Calisma = '.calisma/tamkare', [string]$Json = 'tamkare.json')
$ErrorActionPreference = 'Stop'
$D = (Resolve-Path $Calisma).Path
$surePath = Join-Path $D 'kodlama-sn.txt'
if (-not (Test-Path $surePath)) { Set-Content $surePath '0' }
$temel = 'temel=-rc vbr -multipass fullres -g 120 PEAK=2 BUF=2'
$la20 = 'la20=-rc vbr -multipass fullres -g 120 PEAK=2 BUF=2 -rc-lookahead 20'
function Tara([string]$Hucre, [string]$Kol) {
    & (Join-Path $PSScriptRoot '..\nvenc-2\tara.ps1') -Calisma $Calisma -Json $Json -Hucreler $Hucre -KolTanim $Kol -EnCokDeneme 5
}
switch ($Adim) {
    'Saglama' { Tara 'hareketli:hevc_nvenc:1882:802:1916.3:p4' $temel }
    'A' {
        foreach ($h in 'hareketli:hevc_nvenc:1899.5:p4', 'parlak:hevc_nvenc:1980.4:p4', 'orta:hevc_nvenc:1905.3:p4',
            'karanlik:hevc_nvenc:1933.2:p4', 'hareketli:av1_nvenc:2020:p6', 'karanlik:av1_nvenc:1912.6:p6') {
            $k, $c, $b, $p = $h.Split(':')
            Tara "${k}:${c}:1920:818:${b}:$p" $temel
        }
    }
    'B' {
        $geo = if ($TamKare) { '1920:818' } else { '1882:802' }
        $hucreler = @("hareketli:hevc_nvenc:${geo}:1899.5:p4", "parlak:hevc_nvenc:${geo}:1980.4:p4", "orta:hevc_nvenc:${geo}:1905.3:p4",
            'karanlik:hevc_nvenc:1650:702:983.6:p4', 'karanlik:hevc_nvenc:1920:818:3458.2:p4', 'parlak:hevc_nvenc:1920:818:3498.3:p4',
            'hareketli:hevc_nvenc:1920:818:3447.5:p4')
        foreach ($h in $hucreler) { Tara $h "$temel;$la20" }
    }
}
