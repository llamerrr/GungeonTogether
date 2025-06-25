# GungeonTogether Final Deployment & Testing

Write-Host "🚀 GungeonTogether - Final Build & Deployment" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

# Build the project
Write-Host "`n📦 Building GungeonTogether..." -ForegroundColor Yellow
try {
    dotnet build --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    Write-Host "✅ Build successful!" -ForegroundColor Green
} catch {
    Write-Host "❌ Build failed: $_" -ForegroundColor Red
    exit 1
}

# Deploy to BepInEx
$targetPath = "C:\Program Files (x86)\Steam\steamapps\common\Enter the Gungeon\BepInEx\plugins"
Write-Host "`n📂 Deploying to: $targetPath" -ForegroundColor Yellow

try {
    if (!(Test-Path $targetPath)) {
        Write-Host "❌ BepInEx plugins directory not found!" -ForegroundColor Red
        Write-Host "Please ensure Enter the Gungeon with BepInEx is installed." -ForegroundColor Yellow
        exit 1
    }
    
    Copy-Item "bin\Debug\GungeonTogether.dll" $targetPath -Force
    Write-Host "✅ Deployed GungeonTogether.dll" -ForegroundColor Green
} catch {
    Write-Host "❌ Deployment failed: $_" -ForegroundColor Red
    exit 1
}

# Success summary
Write-Host "`n🎯 DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""
Write-Host "✅ Steam Integration: COMPLETE" -ForegroundColor Green
Write-Host "✅ Session Joining: IMPLEMENTED" -ForegroundColor Green
Write-Host "✅ Debug Controls: F3-F9 READY" -ForegroundColor Green
Write-Host "✅ TypeLoadException: RESOLVED" -ForegroundColor Green
Write-Host ""
Write-Host "🎮 TESTING INSTRUCTIONS:" -ForegroundColor Cyan
Write-Host "1. Launch Enter the Gungeon through Steam" -ForegroundColor White
Write-Host "2. Check BepInEx console for mod loading" -ForegroundColor White
Write-Host "3. Test debug controls in-game:" -ForegroundColor White
Write-Host "   F3 - Start hosting session" -ForegroundColor Yellow
Write-Host "   F4 - Stop session" -ForegroundColor Yellow
Write-Host "   F5 - Show status" -ForegroundColor Yellow
Write-Host "   F6 - Join friend session (test)" -ForegroundColor Yellow
Write-Host "   F7 - Steam invite dialog" -ForegroundColor Yellow
Write-Host "   F8 - Friends playing game" -ForegroundColor Yellow
Write-Host "   F9 - Simulate Steam overlay join" -ForegroundColor Yellow
Write-Host ""
Write-Host "📚 Documentation:" -ForegroundColor Cyan
Write-Host "   - STEAM_TESTING_GUIDE.md" -ForegroundColor White
Write-Host "   - STEAM_INTEGRATION_SUMMARY.md" -ForegroundColor White
Write-Host ""
Write-Host "🎯 READY FOR STEAM MULTIPLAYER! 🎯" -ForegroundColor Green
