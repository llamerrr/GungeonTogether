# GungeonTogether Build and Deploy Script for BepInEx
param(
    [switch]$Release,
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Enter the Gungeon"
)

$Configuration = if ($Release) { "Release" } else { "Debug" }
$PluginsPath = "$GamePath\BepInEx\plugins"

# Check if we are using the gale mod manager
$HarmonyDLLPath = "$env:APPDATA\com.kesomannen.gale\enter-the-gungeon\profiles\Default\BepInEx\"
$HarmonyDLL = "core\0Harmony.dll"
if (Test-Path -Path (Join-Path $HarmonyDLLPath $HarmonyDLL)) {
    Write-Host "Gale mod manager found, using new mod path: $HarmonyDLLPath$HarmonyDLL" -ForegroundColor Green
    $PluginsPath = Join-Path $HarmonyDLLPath "plugins"
}

# check if we are using r2Modman (check appdata for r2Modman)
$r2ModmanPath = "$env:APPDATA\r2modmanPlus-local\ETG\profiles\a\BepInEx\plugins"
if (Test-Path -Path $r2ModmanPath) {
    Write-Host "r2Modman found, using new mod path: $r2ModmanPath" -ForegroundColor Green
    $PluginsPath = $r2ModmanPath
}

Write-Host "Building GungeonTogether ($Configuration)..." -ForegroundColor Green

# Build the project
dotnet build GungeonTogether.csproj --configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Deploy to BepInEx plugins folder
if (Test-Path $PluginsPath) {
    Write-Host "Deploying to: $PluginsPath" -ForegroundColor Yellow
    
    # Copy the DLL
    $SourceDLL = "bin\$Configuration\GungeonTogether.dll"
    if (Test-Path $SourceDLL) {
        Copy-Item $SourceDLL $PluginsPath -Force
        Write-Host "Deployed GungeonTogether.dll" -ForegroundColor Green
    } else {
        Write-Host "Source DLL not found: $SourceDLL" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "BepInEx plugins folder not found: $PluginsPath" -ForegroundColor Red
    Write-Host "Make sure Enter the Gungeon with BepInEx is installed at the specified path" -ForegroundColor Yellow
}

Write-Host "Build and deploy complete!" -ForegroundColor Green