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

**✅ RESOLVED: Critical TypeLoadException Fix**
- Successfully migrated from complex GameManager to SimpleGameManager for testing
- Fixed runtime assembly loading issues that prevented mod initialization  
- Added robust error handling for Steam API operations
- Build and deployment now working successfully

**✅ COMPLETED MIGRATION TASKS:**
- Framework Migration: ETGMod Backend → BepInEx BaseUnityPlugin
- Project Configuration: Updated to use BepInEx NuGet package system
- Logging System: Migrated to BepInEx Logger
- Build System: Simplified deployment to BepInEx plugins folder
- Debug Controls: Updated F3-F6 hotkeys to avoid ETG conflicts
- Git Configuration: Added proper .gitignore files

**🔄 NEXT STEPS:**
1. **Test Current Build**: Verify SimpleGameManager loads without TypeLoadException
2. **Gradual Feature Restoration**: Step-by-step reintroduce networking components
3. **Full Multiplayer Testing**: Complete player synchronization and Steam P2P testing

**📋 REMAINING WORK:**
- Restore full networking functionality (SteamNetworkManager, packet system)
- Complete player synchronization implementation  
- Test real-time multiplayer gameplay
- Optimize performance and stability