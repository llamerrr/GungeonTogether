# GungeonTogether

GungeonTogether is a multiplayer mod for Enter the Gungeon that allows online co-op play using Steam P2P networking.
WIP WIP WIP WIP WIP WIP pls be nice


## Features
- Online co-op multiplayer for Enter the Gungeon
- Steam P2P networking (no dedicated servers required), no parsec or streaming required
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
cd GungeonTogether
dotnet build --configuration Release
```
then hope for the best because honestly I have no idea if it will even build

## Technical Details
Built with BepInEx framework using Steam P2P networking for a seamless multiplayer experience.
