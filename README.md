# GungeonTogether

GungeonTogether is a multiplayer mod for Enter the Gungeon that allows online co-op play using Steam P2P networking.

## Features
- Online co-op multiplayer for Enter the Gungeon
- Steam P2P networking (no dedicated servers required)
- Real-time player synchronization
- Debug controls for testing

## Installation
1. Install BepInEx for Enter the Gungeon
2. Copy `GungeonTogether.dll` to `[ETG Install]/BepInEx/plugins/`
3. Launch Enter the Gungeon

## Debug Controls
- **F3** - Start hosting a multiplayer session
- **F4** - Stop multiplayer session
- **F5** - Show connection status
- **F6** - Join Steam friend (for testing)

## Building
```bash
cd GungeonTogetherETG
dotnet build --configuration Release
```

## Technical Details
Built with BepInEx framework using Steam P2P networking for seamless multiplayer experience.

## Current Status

**🎉 SUCCESS: Runtime Success Achieved!**
- **✅ CONFIRMED**: MinimalGameManager loads successfully in Enter the Gungeon without crashes! 🎉
- **✅ VERIFIED**: Debug controls work (F3 host command tested and functional)
- **✅ RUNTIME**: Mod appears in BepInEx logs with proper initialization
- **Root Cause**: Steamworks.NET dependency incompatibility with ETG+BepInEx environment  
- **Solution**: Zero-dependency approach with gradual feature restoration

**✅ COMPLETED MIGRATION TASKS:**
- Framework Migration: ETGMod Backend → BepInEx BaseUnityPlugin ✅
- Project Configuration: Updated to use BepInEx NuGet package system ✅
- Logging System: Migrated to BepInEx Logger ✅
- Build System: Simplified deployment to BepInEx plugins folder ✅
- Debug Controls: Updated F3-F6 hotkeys to avoid ETG conflicts ✅
- **TypeLoadException Fix**: Identified and completely resolved ✅
- **Runtime Testing**: Mod loads and functions in ETG ✅

**🔄 CURRENT PHASE: Steamworks Integration Research**
1. **Research ETG mod Steamworks patterns**: How do other mods handle Steam integration?
2. **Design wrapper system**: Create safe Steamworks loading with fallbacks
3. **Implement packet system**: Add networking without direct Steamworks dependencies
4. **Test incremental restoration**: Add features back one by one

**📋 REMAINING WORK:**
- Implement proper Steamworks.NET integration pattern for ETG mods
- Restore networking functionality with runtime Steamworks detection
- Complete player synchronization and multiplayer testing
- Performance optimization and stability improvements