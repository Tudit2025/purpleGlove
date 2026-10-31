param([string]$ProjectRoot = (Resolve-Path "$PSScriptRoot/../..").Path)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$manifest = Get-Content -LiteralPath "$ProjectRoot/output/material-library-v1/generation-prompts.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$outputDirectory = Join-Path $ProjectRoot 'output/material-library-v1'
$textureDirectory = Join-Path $ProjectRoot 'Assets/Art/MaterialLibraryV1/Textures'
$titleFont = [System.Drawing.Font]::new('Microsoft YaHei', 26, [System.Drawing.FontStyle]::Bold)
$labelFont = [System.Drawing.Font]::new('Microsoft YaHei', 15, [System.Drawing.FontStyle]::Regular)
$smallFont = [System.Drawing.Font]::new('Consolas', 10, [System.Drawing.FontStyle]::Regular)
$bodyFont = [System.Drawing.Font]::new('Microsoft YaHei', 12, [System.Drawing.FontStyle]::Regular)
$ink = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(224, 215, 194))
$mutedInk = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(155, 155, 141))
$cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(38, 41, 41))
$rulePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(80, 77, 65), 1)
try {
    foreach ($repeat in @($false, $true)) {
        $canvas = [System.Drawing.Bitmap]::new(1712, 1516)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(23, 27, 28))
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $title = if ($repeat) { '紫手套 / 材质库 01 — 重复铺贴检查' } else { '紫手套 / 材质库 01 — 基础色总览' }
            $subtitle = if ($repeat) { '每格为 2 × 2 重复铺贴 · 128px 原尺寸 · 接缝位于格内中央' } else { '22 种表面 · 原生 128 × 128 · 每张最多 12 色 · 最近邻 2 倍放大' }
            $graphics.DrawString($title, $titleFont, $ink, 28, 20)
            $graphics.DrawString($subtitle, $bodyFont, $mutedInk, 30, 68)
            $graphics.DrawLine($rulePen, 28, 108, 1684, 108)
            for ($i = 0; $i -lt $manifest.assets.Count; $i++) {
                $entry = $manifest.assets[$i]
                $x = 28 + ($i % 6) * 280
                $y = 130 + [math]::Floor($i / 6) * 336
                $graphics.FillRectangle($cardBrush, [int]($x - 6), [int]($y - 6), 268, 324)
                $texture = [System.Drawing.Bitmap]::new((Join-Path $textureDirectory ('T_' + $entry.id + '.png')))
                try {
                    if ($repeat) {
                        for ($ry = 0; $ry -lt 2; $ry++) {
                            for ($rx = 0; $rx -lt 2; $rx++) {
                                $graphics.DrawImageUnscaled($texture, [int]($x + $rx * 128), [int]($y + $ry * 128))
                            }
                        }
                    } else {
                        $graphics.DrawImage($texture, [System.Drawing.Rectangle]::new([int]$x, [int]$y, 256, 256), 0, 0, 128, 128, [System.Drawing.GraphicsUnit]::Pixel)
                    }
                } finally { $texture.Dispose() }
                $graphics.DrawString(('{0:D2}  {1}' -f [int]$entry.index, $entry.label), $labelFont, $ink, [single]$x, [single]($y + 263))
                $graphics.DrawString($entry.id, $smallFont, $mutedInk, [single]$x, [single]($y + 293))
            }
            $graphics.DrawString('Built-in Standard  |  Point / Repeat / sRGB  |  128 texels per metre  |  Base color only: lighting is supplied by Unity', $smallFont, $mutedInk, 30, 1490)
            $filename = if ($repeat) { 'repeat-sheet.png' } else { 'contact-sheet.png' }
            $canvas.Save((Join-Path $outputDirectory $filename), [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $canvas.Dispose() }
    }
} finally {
    $titleFont.Dispose(); $labelFont.Dispose(); $smallFont.Dispose(); $bodyFont.Dispose()
    $ink.Dispose(); $mutedInk.Dispose(); $cardBrush.Dispose(); $rulePen.Dispose()
}
Write-Output 'Created contact-sheet.png and repeat-sheet.png from final imported textures.'
