# GungeonTogether

GungeonTogether is a multiplayer mod for Enter the Gungeon that allows online co-op play using Steam P2P networking.
WIP WIP WIP WIP WIP WIP pls be nice

The mod is free (because im not weird). however i am broke asf so feel free to donate for my INSANELY INTENSE AND HARD LABOUR (jk)
my paypal is llamerrr1@gmail.com if you actually want to (for some reason)

## What works so far
- pretty much nothing
- real connections and steam invites/lobby system
- singleplayer LOL!
  
## Features upon release 1.0
- Online co-op multiplayer for Enter the Gungeon
- Steam P2P networking (no dedicated servers required), no parsec or streaming required
- Real-time player synchronization
- Debug controls for testing
  
## Planned Features
- Unlimited players
- EVERYTHING synced
- Race mode (competing in identical dungeons, first player to finish wins)

## Installation
1. Install BepInEx for Enter the Gungeon
2. Copy `GungeonTogether.dll` to `[ETG Install]/BepInEx/plugins/`
3. Launch Enter the Gungeon

## Building
```bash
cd GungeonTogether
dotnet build --configuration Release
```
then hope for the best because honestly I have no idea if it will even build

## Technical Details
Built with BepInEx framework using Steam P2P networking for a seamless multiplayer experience.
Unfortunately if you play the game through epic or windows store this mod will not work, as it runs through steams p2p api only, which means you need a steamID for it to work. Sorry guys...

# 1.0 release coming one day!