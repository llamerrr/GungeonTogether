# GungeonTogether

GungeonTogether is a multiplayer mod for Enter the Gungeon that allows online co-op play using Steam P2P networking.

> [!WARNING]
> _THIS MOD IS STILL VERY EARLY IN DEVELOPMENT - DO NOT EXPECT A FUNCTIONAL EXPERIENCE RIGHT NOW_

WIP WIP WIP WIP WIP WIP pls be nice 

<p align="center" href="https://github.com/llamerrr/GungeonTogether">
<img height="320" alt="image" src="https://github.com/user-attachments/assets/6c485240-5e3c-44ee-93bb-747d27a0731e"/>
</p>

The mod is free (because I'm not weird). however I'm broke asf so feel free to donate for my INSANELY INTENSE AND HARD LABOUR (jk)
My paypal is llamerrr1@gmail.com if you actually want to (for some reason)

## Contributers 
<p align="center" href="https://github.com/llamerrr/GungeonTogether">
<img height="100" alt="image" src="https://avatars.githubusercontent.com/u/47313866?v=4"/>
<img height="100" alt="image" src="https://avatars.githubusercontent.com/u/88169809?v=4"/>
</p>


> [!NOTE]
> As this mod runs through the steam network, it only works with the steam verison of the game as steamID is required

# Features
## What works so far
| System | Status | Notes |
|:---:|:---:|:---:|
| Steam invites/lobby system | 🟩 Done | Steam lobby creation and joining functional |
| Steam P2P networking | 🟨 Working | Real connections and Steam invites working |
| Basic UI | 🟨 Working | Modern multiplayer menu (Ctrl+P) available |
| Player Synchronization | 🟨 Working | Basic position and animation sync implemented, development ongoing |
| Enemy Synchronization | 🟥 Planned | Basic hooks into gameobjects |
| Dungeon Synchronization | 🟥 Planned | Save and load system working |
| Singleplayer | 🏁 Finished | Yippeeee!!!!! |

## Planed for 1.0 release
- Online co-op multiplayer for Enter the Gungeon
- Steam P2P networking (no dedicated servers required), no parsec or streaming required
- Real-time synchronisation of
  - Players
  - Enemies
  - Projectiles
  - Dungeon
- Debug controls for testing
- Teammate DBNO system + revive mechanic
- Spectate when dead
- Enemy scaling (set difficulty in game)
  
## Planned for the future
- EPIC GAMES SUPPORT
- Unlimited players
- EVERYTHING synced
- Race mode (competing in identical dungeons, first player to finish wins)
- Mirror mode (dupilcate dungeons, can't hurt one another but killing will cause an enemy to flip sides)
- Teammate revive items
- Team based buffs and items

## Installation
1. Install BepInEx for Enter the Gungeon
2. Copy `GungeonTogether.dll` to `[ETG Install]/BepInEx/plugins/`
3. Launch Enter the Gungeon

## Building
This only applies to people who want to build from source, or if we forget to post builds :)
1. You will need to setup BepInExPack_ETG (either with a mod manager like r2ModMan or manually)
2. Run the build script `build.ps1` in the GungeonTogether folder
3. Pray it works because honestly I have no idea if it does
4. Launch Enter the Gungeon

## Technical Details
Built with BepInEx framework using Steam P2P networking for a seamless multiplayer experience.
Unfortunately if you play the game through epic or windows store this mod will not work, as it runs through steams p2p api only, which means you need a steamID for it to work. Sorry guys...
(EPIC COMING SOON)

> [!NOTE]
> Wiki!!!
> https://deepwiki.com/llamerrr/GungeonTogether
> Thanks to https://github.com/TheExploration

# 1.0 release coming one day!!!!!!
