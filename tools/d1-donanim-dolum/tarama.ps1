param(
    [Parameter(Mandatory)][string]$KesitDizini,
    [Parameter(Mandatory)][string]$Kollar,
    [string]$Calisma = '.calisma/t0-d1-oran',
    [string]$Cikti = 'tarama.tsv',
    [string]$Hucreler = 'A,B,C',
    [string]$IzgaraA = '600,620,640,650,656,657,660,670,685,700',
    [string]$IzgaraB = '1400,1430,1450,1465,1470,1475,1485,1500,1530',
    [string]$IzgaraC = '980,1010,1030,1040,1045,1050,1060,1080,1100'
)
$ErrorActionPreference = 'Stop'
$egri = Join-Path $PSScriptRoot 'egri.ps1'
$K = (Resolve-Path $KesitDizini).Path
$hucre = @{
    A = @{ Ad = 'parlak/hevc/1000'; Girdi = "$K\kesit-parlak.mkv"; Olcek = '1804:768'; Kodek = 'hevc_nvenc'; Onayar = 'p4'; Hedef = 1.2207; Izgara = $IzgaraA }
    B = @{ Ad = 'parlak/hevc/2000'; Girdi = "$K\kesit-parlak.mkv"; Olcek = '1882:802'; Kodek = 'hevc_nvenc'; Onayar = 'p4'; Hedef = 2.4414; Izgara = $IzgaraB }
    C = @{ Ad = 'karanlik/av1/1000'; Girdi = "$K\kesit-karanlik.mkv"; Olcek = '1882:802'; Kodek = 'av1_nvenc'; Onayar = 'p6'; Hedef = 1.2207; Izgara = $IzgaraC }
}
$yol = Join-Path $Calisma $Cikti
foreach ($kol in $Kollar.Split(';')) {
    $ad, $tepe, $tampon, $rc, $ek = $kol.Split('|')
    foreach ($h in $Hucreler.Split(',')) {
        $c = $hucre[$h]
        $satirlar = & pwsh -NoProfile -File $egri -Girdi $c.Girdi -Olcek $c.Olcek -Kodek $c.Kodek -Onayar $c.Onayar -Kbitler $c.Izgara -HedefMb $c.Hedef -Calisma $Calisma -Tepe $tepe -Tampon $tampon -Rc $rc @(if ($ek) { '-Ek'; $ek })
        foreach ($s in $satirlar) {
            $l = "$ad`t$($c.Ad)`t$s"
            Add-Content $yol $l
            Write-Output $l
        }
    }
}
