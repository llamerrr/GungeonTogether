param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Enter the Gungeon"
)

$ErrorActionPreference = "Stop"

function Resolve-ManagedPath([string]$root) {
    $candidates = @(
        (Join-Path $root "EtG_Data\Managed"),
        (Join-Path $root "Enter the Gungeon_Data\Managed")
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return $p }
    }
    throw "Could not find Managed folder under '$root' (tried EtG_Data and Enter the Gungeon_Data)"
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$libs = Join-Path $projectRoot "Libs"
New-Item -ItemType Directory -Force -Path $libs | Out-Null

$managed = Resolve-ManagedPath $GamePath
$bepCore = Join-Path $GamePath "BepInEx\core"

Write-Host "GamePath:   $GamePath" -ForegroundColor Cyan
Write-Host "Managed:    $managed" -ForegroundColor Cyan
Write-Host "BepInExCore:$bepCore" -ForegroundColor Cyan

function Copy-IfExists([string]$from, [string]$toName) {
    if (Test-Path $from) {
        Copy-Item $from (Join-Path $libs $toName) -Force
        Write-Host "Copied $toName" -ForegroundColor Green
    } else {
        Write-Host "Missing $from" -ForegroundColor Yellow
    }
}

# BepInEx
Copy-IfExists (Join-Path $bepCore "BepInEx.dll") "BepInEx.dll"
Copy-IfExists (Join-Path $bepCore "0Harmony.dll") "0Harmony.dll"
Copy-IfExists (Join-Path $bepCore "MonoMod.Utils.dll") "MonoMod.Utils.dll"

# Game
Copy-IfExists (Join-Path $managed "Assembly-CSharp.dll") "Assembly-CSharp.dll"
Copy-IfExists (Join-Path $managed "Assembly-CSharp-firstpass.dll") "Assembly-CSharp-firstpass.dll"
Copy-IfExists (Join-Path $managed "PlayMaker.dll") "PlayMaker.dll"

# Unity 
Copy-IfExists (Join-Path $managed "UnityEngine.dll") "UnityEngine.dll"
Copy-IfExists (Join-Path $managed "UnityEngine.CoreModule.dll") "UnityEngine.CoreModule.dll"
Copy-IfExists (Join-Path $managed "UnityEngine.IMGUIModule.dll") "UnityEngine.IMGUIModule.dll"
Copy-IfExists (Join-Path $managed "UnityEngine.UIModule.dll") "UnityEngine.UIModule.dll"
Copy-IfExists (Join-Path $managed "UnityEngine.UI.dll") "UnityEngine.UI.dll"

# Optional: ModTheGungeonAPI + Newtonsoft.Json (best-effort search)
$mtg = Get-ChildItem -Path (Join-Path $GamePath "BepInEx") -Filter "ModTheGungeonAPI.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($mtg) { Copy-Item $mtg.FullName (Join-Path $libs "ModTheGungeonAPI.dll") -Force; Write-Host "Copied ModTheGungeonAPI.dll" -ForegroundColor Green }

$json = Get-ChildItem -Path (Join-Path $GamePath "BepInEx") -Filter "Newtonsoft.Json.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($json) { Copy-Item $json.FullName (Join-Path $libs "Newtonsoft.Json.dll") -Force; Write-Host "Copied Newtonsoft.Json.dll" -ForegroundColor Green }