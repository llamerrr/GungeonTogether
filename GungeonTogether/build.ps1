# GungeonTogether Build and Deploy Script for BepInEx
param(
    [switch]$Release,
    [string]$SteamAppsPath = $null
)

$AUTOLOAD = $true
# save the current directory to return later
$old_dir = Get-Location
$GamePathExe = $null

# if we are provided a SteamAppsPath, use it and don't bother checking libraryfolders.vdf
if ($SteamAppsPath -and (Test-Path $SteamAppsPath)) {
    Write-Host "Using provided SteamAppsPath: $SteamAppsPath" -ForegroundColor Green
    $GamePathExe = Join-Path $SteamAppsPath "common\Enter the Gungeon\EtG.exe"
}
else {
    if($SteamAppsPath) {
        Write-Host "Provided SteamAppsPath does not exist, defaulting back to using libraryfolders.vdf search" -ForegroundColor Red
    }

    # find Enter the Gungeon installation path via Steam's libraryfolders.vdf
    $steamAppsPath = "C:\Program Files (x86)\Steam\steamapps"
    $libraryFoldersPath = Join-Path $steamAppsPath "libraryfolders.vdf"
    
    if (-not (Test-Path $libraryFoldersPath)) {
        Write-Host "Steam libraryfolders.vdf not found at: $libraryFoldersPath" -ForegroundColor Red
        return $null
    }
    
    Write-Host "Searching for Enter the Gungeon (App ID: 311690) in Steam libraries..." -ForegroundColor Yellow
    
    # read the VDF file content
    $vdfContent = Get-Content $libraryFoldersPath -Raw
    
    # check if Enter the Gungeon (App ID 311690) is listed
    if ($vdfContent -notmatch '"311690"') {
        Write-Host "Enter the Gungeon (App ID: 311690) not found in Steam libraries" -ForegroundColor Red
        return $null
    }
    
    Write-Host "Found Enter the Gungeon in Steam library!" -ForegroundColor Green
    
    # extract all library paths
    $pathMatches = [regex]::Matches($vdfContent, '"path"\s*"([^"]+)"')
    
    foreach ($match in $pathMatches) {
        $libraryPath = $match.Groups[1].Value
        # convert double backslashes to single backslashes
        $libraryPath = $libraryPath -replace '\\\\', '\'
        
        $etgExePath = Join-Path $libraryPath "steamapps\common\Enter the Gungeon\EtG.exe"
        
        Write-Host "Checking path: $etgExePath" -ForegroundColor Cyan
        
        if (Test-Path $etgExePath) {
            Write-Host "Found Enter the Gungeon at: $etgExePath" -ForegroundColor Green
            $GamePathExe = $etgExePath
        }
    }
}

$GamePath = (Split-Path $GamePathExe)

$Configuration = if ($Release) { "Release" } else { "Debug" }
$ActivePath = "$GamePath\BepInEx\plugins"

# Kill ETG.exe if running
$etgProcess = Get-Process -Name "ETG" -ErrorAction SilentlyContinue
if ($etgProcess) {
    Write-Host "ETG.exe is running, attempting to close it..." -ForegroundColor Yellow
    Stop-Process -Id $etgProcess.Id -Force
    Start-Sleep -Seconds 2
}
else {
    Write-Host "ETG.exe is not running." -ForegroundColor Green
}

# check what profiles are available and use the last modified one
function getNewestProfile {
    param ( [string]$ProfilePath )
    # check what profiles are available and use the last modified one
    $profileDirs = Get-ChildItem -Path $ProfilePath -Directory | Sort-Object LastWriteTime -Descending
    if ($profileDirs.Count -gt 0) {
        $latestProfile = $profileDirs[0].FullName
        Write-Host "Using latest profile: $latestProfile" -ForegroundColor Green
        return Join-Path $latestProfile "BepInEx"
    }
    return $null
}

# check if we are using gale mod manager
$HarmonyDLLPath = "$env:APPDATA\com.kesomannen.gale\enter-the-gungeon\profiles"
if (Test-Path -Path $HarmonyDLLPath) {
    Write-Host "Gale mod manager found, using new mod path: $HarmonyDLLPath" -ForegroundColor Green
    $ActivePath = getNewestProfile $HarmonyDLLPath
}

# check if we are using r2Modman
$r2ModmanPath = "$env:APPDATA\r2modmanPlus-local\ETG\profiles"
if (Test-Path -Path $r2ModmanPath) {
    Write-Host "r2Modman found, using new mod path: $r2ModmanPath" -ForegroundColor Green
    $ActivePath = getNewestProfile $r2ModmanPath
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
if (Test-Path $ActivePath) {
    Write-Host "Deploying to: $ActivePath" -ForegroundColor Yellow
    
    # Copy the DLL
    $SourceDLL = "bin\$Configuration\GungeonTogether.dll"
    if (Test-Path $SourceDLL) {
        $deployPath = Join-Path $ActivePath "plugins"
        Copy-Item $SourceDLL $deployPath -Force
        Write-Host "Deployed GungeonTogether.dll" -ForegroundColor Green
    }
    else {
        Write-Host "Source DLL not found: $SourceDLL" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "BepInEx plugins folder not found: $ActivePath" -ForegroundColor Red
    Write-Host "Make sure Enter the Gungeon with BepInEx is installed at the specified path" -ForegroundColor Yellow
}

Write-Host "Build and deploy complete!" -ForegroundColor Green

if($AUTOLOAD) {
    if ($etgExePath) {
    Write-Host "Launching Enter the Gungeon with BepInEx..." -ForegroundColor Green
    try {
        # change directory to the exe path
        Set-Location (Split-Path $etgExePath)
        Start-Process $etgExePath -ArgumentList "--doorstop-enable true", "--doorstop-target", "`"$ActivePath\core\BepInEx.Preloader.dll`""
        Write-Host "Successfully launched Enter the Gungeon!" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to launch Enter the Gungeon: $($_.Exception.Message)" -ForegroundColor Red
    }
}
else {
    Write-Host "Could not find Enter the Gungeon installation. Please launch the game manually :(" -ForegroundColor Yellow
}
} else {
    Write-Host "Auto-launching is disabled. Please launch Enter the Gungeon manually." -ForegroundColor Yellow
}

cd $old_dir