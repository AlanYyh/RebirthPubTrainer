$ErrorActionPreference = 'Stop'

# 改成你的游戏根目录
$gameDir = "D:\Rebirth Pub"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$out = Join-Path $gameDir "RebirthPubTrainer.exe"
$src = Join-Path $PSScriptRoot "..\src\Trainer.cs"

$args = @("/nologo", "/target:winexe", "/langversion:5", "/optimize+", "/out:$out")
$args += "/r:$fw\System.Windows.Forms.dll"
$args += "/r:$fw\System.Drawing.dll"

$res = @()
$res += ,@((Join-Path $gameDir "winhttp.dll"), "pkg__winhttp.dll")
$res += ,@((Join-Path $gameDir "doorstop_config.ini"), "pkg__doorstop_config.ini")
Get-ChildItem (Join-Path $gameDir "BepInEx\core\*.dll") | ForEach-Object {
    $res += ,@($_.FullName, ("pkg__BepInEx__core__" + $_.Name))
}
$res += ,@((Join-Path $gameDir "BepInEx\plugins\RebirthPubTrainer.dll"), "pkg__BepInEx__plugins__RebirthPubTrainer.dll")

$qrFiles = @(Get-ChildItem (Join-Path $gameDir "*.jpg") | Sort-Object Name)
if ($qrFiles.Count -ge 2) {
    Copy-Item $qrFiles[0].FullName "$env:TEMP\qr1.jpg" -Force
    Copy-Item $qrFiles[1].FullName "$env:TEMP\qr2.jpg" -Force
    $res += ,@("$env:TEMP\qr1.jpg", "qr__1.jpg")
    $res += ,@("$env:TEMP\qr2.jpg", "qr__2.jpg")
}

foreach ($r in $res) {
    $args += "/res:$($r[0]),$($r[1])"
}

$args += $src
& $csc @args
Write-Output "csc exit: $LASTEXITCODE"
