$ErrorActionPreference = 'Stop'

# 改成你的游戏根目录
$gameDir = "D:\Rebirth Pub"
# WebView2 SDK 解压目录（NuGet 包 Microsoft.Web.WebView2）
$webView2Dir = "D:\Rebirth Pub\RebirthPubTrainer\webview2"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$out = Join-Path $gameDir "RebirthPubTrainer.exe"
$src = Join-Path $PSScriptRoot "..\src\Trainer.cs"
$html = Join-Path $PSScriptRoot "..\src\index.html"
$icon = Join-Path $PSScriptRoot "..\assets\app.ico"

$manifest = Join-Path $PSScriptRoot "app.manifest"

$args = @("/nologo", "/target:winexe", "/langversion:5", "/optimize+", "/out:$out")
$args += "/win32icon:$icon"
$args += "/win32manifest:$manifest"
$args += "/r:$fw\System.Windows.Forms.dll"
$args += "/r:$fw\System.Drawing.dll"
$args += "/r:$fw\System.Web.Extensions.dll"
$args += "/r:$webView2Dir\lib\net462\Microsoft.Web.WebView2.WinForms.dll"
$args += "/r:$webView2Dir\lib\net462\Microsoft.Web.WebView2.Core.dll"

$res = @()
$res += ,@((Join-Path $gameDir "winhttp.dll"), "pkg__winhttp.dll")
$res += ,@((Join-Path $gameDir "doorstop_config.ini"), "pkg__doorstop_config.ini")
Get-ChildItem (Join-Path $gameDir "BepInEx\core\*.dll") | ForEach-Object {
    $res += ,@($_.FullName, ("pkg__BepInEx__core__" + $_.Name))
}
$res += ,@((Join-Path $gameDir "BepInEx\plugins\RebirthPubTrainer.dll"), "pkg__BepInEx__plugins__RebirthPubTrainer.dll")
$res += ,@("$webView2Dir\lib\net462\Microsoft.Web.WebView2.WinForms.dll", "pkg__Microsoft.Web.WebView2.WinForms.dll")
$res += ,@("$webView2Dir\lib\net462\Microsoft.Web.WebView2.Core.dll", "pkg__Microsoft.Web.WebView2.Core.dll")
$res += ,@("$webView2Dir\runtimes\win-x64\native\WebView2Loader.dll", "pkg__WebView2Loader.dll")
$res += ,@($html, "ui__index.html")
$res += ,@($icon, "ico__app.ico")

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
