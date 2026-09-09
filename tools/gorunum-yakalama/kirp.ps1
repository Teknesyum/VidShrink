param([string]$Src, [int]$X, [int]$Y, [int]$W, [int]$H, [string]$Out, [double]$Olcek=2)
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Src))
$r = New-Object System.Drawing.Rectangle $X,$Y,$W,$H
$c = $b.Clone($r, $b.PixelFormat)
$n = New-Object System.Drawing.Bitmap ([int]($W*$Olcek)), ([int]($H*$Olcek))
$g = [System.Drawing.Graphics]::FromImage($n)
$g.InterpolationMode = "NearestNeighbor"
$g.DrawImage($c, 0, 0, [int]($W*$Olcek), [int]($H*$Olcek))
$n.Save((Join-Path (Get-Location) $Out))
$g.Dispose(); $n.Dispose(); $c.Dispose(); $b.Dispose()
$Out
