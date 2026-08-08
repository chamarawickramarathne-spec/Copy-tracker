Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$logo = Join-Path $root "media\logo.png"
$icoOut = Join-Path $root "media\smartcopy.ico"
$appAsset = Join-Path $root "src\SmartCopy.App\Assets\smartcopy.ico"

if (-not (Test-Path $logo)) { Write-Error "logo not found: $logo"; exit 1 }
New-Item -ItemType Directory -Force -Path (Join-Path $root "src\SmartCopy.App\Assets") | Out-Null

$src = [System.Drawing.Image]::FromFile((Resolve-Path $logo))
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @{}

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $s, $s)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs[$s] = $ms.ToArray()
    $bmp.Dispose()
}
$src.Dispose()

function WriteUInt16([byte[]]$buf, [int]$offset, [int]$value) {
    $buf[$offset] = [byte]($value -band 0xFF)
    $buf[$offset + 1] = [byte](($value -shr 8) -band 0xFF)
}
function WriteUInt32([byte[]]$buf, [int]$offset, [int]$value) {
    $buf[$offset] = [byte]($value -band 0xFF)
    $buf[$offset + 1] = [byte](($value -shr 8) -band 0xFF)
    $buf[$offset + 2] = [byte](($value -shr 16) -band 0xFF)
    $buf[$offset + 3] = [byte](($value -shr 24) -band 0xFF)
}

$num = $sizes.Count
$dataTotal = ($pngs.Values | Measure-Object -Property Length -Sum).Sum
$total = 6 + (16 * $num) + $dataTotal
$ico = New-Object byte[] $total

WriteUInt16 $ico 0 0
WriteUInt16 $ico 2 1
WriteUInt16 $ico 4 $num

$offset = 6 + (16 * $num)
for ($i = 0; $i -lt $num; $i++) {
    $s = $sizes[$i]
    $entry = 6 + (16 * $i)
    $dim = if ($s -ge 256) { 0 } else { $s }
    $ico[$entry] = [byte]$dim
    $ico[$entry + 1] = [byte]$dim
    $ico[$entry + 2] = 0
    $ico[$entry + 3] = 0
    WriteUInt16 $ico ($entry + 4) 1
    WriteUInt16 $ico ($entry + 6) 32
    $data = $pngs[$s]
    WriteUInt32 $ico ($entry + 8) $data.Length
    WriteUInt32 $ico ($entry + 12) $offset
    [Array]::Copy($data, 0, $ico, $offset, $data.Length)
    $offset += $data.Length
}

[System.IO.File]::WriteAllBytes($icoOut, $ico)
[System.IO.File]::WriteAllBytes($appAsset, $ico)
Write-Output "Wrote $icoOut ($total bytes)"
Write-Output "Wrote $appAsset ($total bytes)"
