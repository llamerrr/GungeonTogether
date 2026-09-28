# GungeonTogether Build and Deploy Script for BepInEx
param(
    [switch]$Release,
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Enter the Gungeon"
)

$Configuration = if ($Release) { "Release" } else { "Debug" }
$PluginsPath = "$GamePath\BepInEx\plugins"
$BepInExCorePath = ""

# Check if we are using the gale mod manager
$HarmonyDLLPath = "$env:APPDATA\com.kesomannen.gale\enter-the-gungeon\profiles\Default\BepInEx\"
$HarmonyDLL = "core\0Harmony.dll"
if (Test-Path -Path (Join-Path $HarmonyDLLPath $HarmonyDLL)) {
    Write-Host "Gale mod manager found, using new mod path: $HarmonyDLLPath$HarmonyDLL" -ForegroundColor Green
    $PluginsPath = Join-Path $HarmonyDLLPath "plugins"
}

# Check if we are using r2Modman (check appdata for r2Modman)
$r2ModmanPath = "$env:APPDATA\r2modmanPlus-local\ETG\profiles\a\BepInEx\plugins"
if (Test-Path -Path $r2ModmanPath) {
    Write-Host "r2Modman found, using new mod path: $r2ModmanPath" -ForegroundColor Green
    $PluginsPath = $r2ModmanPath
}

# Check if we are using Thunderstore Mod Manager (prompt user to select profile)
$ThunderstoreDataPath = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\ETG\profiles"
if (Test-Path -Path $ThunderstoreDataPath) {
    $AvailableProfiles = Get-ChildItem -Path $ThunderstoreDataPath -Directory
    
    if ($AvailableProfiles.Count -gt 0) {
        $ActiveProfile = $null

        # One profile: auto select
        if ($AvailableProfiles.Count -eq 1) {
            $ActiveProfile = $AvailableProfiles[0].FullName
            Write-Host "Thunderstore Mod Manager profile detected: $($AvailableProfiles[0].Name)" -ForegroundColor Green
        }
        # Selection prompt
        else {
            Write-Host "`n--- Thunderstore Profiles Detected ---" -ForegroundColor Cyan
            for ($i = 0; $i -lt $AvailableProfiles.Count; $i++) {
                Write-Host "[$i] $($AvailableProfiles[$i].Name)" -ForegroundColor Yellow
            }
            
            $Selection = -1
            while ($Selection -lt 0 -or $Selection -ge $AvailableProfiles.Count) {
                $UserChoice = Read-Host "Select a profile index to deploy to (Default is 0)"
                if ([string]::IsNullOrWhiteSpace($UserChoice)) { 
                    $Selection = 0 
                } else {
                    [int]::TryParse($UserChoice, [ref]$Selection) | Out-Null
                }
            }
            $ActiveProfile = $AvailableProfiles[$Selection].FullName
            Write-Host "Selected profile: $($AvailableProfiles[$Selection].Name)" -ForegroundColor Green
        }
        
        $PluginsPath = Join-Path $ActiveProfile "BepInEx\plugins"
        $BepInExCorePath = Join-Path $ActiveProfile "BepInEx\core"
        
        # Create the plugins folder if not present
        if (-not (Test-Path -Path $PluginsPath)) {
            Write-Host "Plugins folder missing. Creating: $PluginsPath" -ForegroundColor Yellow
            New-Item -ItemType Directory -Path $PluginsPath -Force | Out-Null
        }
    }
}

Write-Host "Building GungeonTogether ($Configuration)..." -ForegroundColor Green

# Ensure Libs/ exists and contains required references
$SetupScript = Join-Path $PSScriptRoot "setup-libs.ps1"
if (Test-Path $SetupScript) {
    Write-Host "Preparing Libs/ from game install..." -ForegroundColor Yellow
    & $SetupScript -GamePath $GamePath -BepInExCorePath $BepInExCorePath
}

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
    $SourceDLL = "bin\$Configuration\net35\GungeonTogether.dll"
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