$ErrorActionPreference = 'Stop'

# 改成你的游戏根目录
$gameDir = "D:\Rebirth Pub"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$managed = Join-Path $gameDir "Rebirth Pub_Data\Managed"
$bepInEx = Join-Path $gameDir "BepInEx\core\BepInEx.dll"
$out = Join-Path $gameDir "BepInEx\plugins\RebirthPubTrainer.dll"
$src = Join-Path $PSScriptRoot "..\src\Plugin.cs"

$refs = @(
    $bepInEx,
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "netstandard.dll"),
    (Join-Path $managed "System.Runtime.dll"),
    (Join-Path $managed "Assembly-CSharp.dll")
)

$args = @("/nologo", "/target:library", "/langversion:5", "/out:$out")
foreach ($r in $refs) { $args += "/r:$r" }
$args += $src

& $csc @args
Write-Output "csc exit: $LASTEXITCODE"
