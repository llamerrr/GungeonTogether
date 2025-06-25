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