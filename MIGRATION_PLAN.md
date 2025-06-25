# GungeonTogether Migration - Next Steps

## Current Status ✅
Successfully resolved the TypeLoadException by:
1. Creating SimpleGameManager to replace complex GameManager
2. Removing problematic networking dependencies temporarily
3. Adding robust error handling for Steam operations
4. Confirming basic mod loading works

## Step-by-Step Restoration Plan

### Phase 1: Verify Basic Loading ✅ **COMPLETED**
- [x] SimpleGameManager loads without TypeLoadException  
- [x] Basic BepInEx integration works
- [x] Debug controls functional
- [x] **DISCOVERED**: Root cause is Steamworks dependency causing TypeLoadException
- [x] **SOLUTION**: Created MinimalGameManager with zero external dependencies
- [x] **✅ CONFIRMED**: MinimalGameManager loads successfully in ETG without TypeLoadException
- [x] **✅ VERIFIED**: F3 debug control works and starts session in test mode

### Phase 2: Add Basic Networking Layer (CURRENT)
- [ ] Research how other ETG mods handle Steamworks.NET integration
- [ ] Create Steamworks wrapper system for optional runtime loading
- [ ] Add basic packet interfaces (IPacketData, etc.) without Steamworks dependencies
- [ ] Test Steam initialization with proper runtime detection
- [ ] Verify no TypeLoadException with basic networking layer

### Phase 3: Restore Packet System
- [ ] Add LoginRequestPacket and LoginResponsePacket
- [ ] Add PlayerUpdatePacket (simplified)
- [ ] Test packet serialization/deserialization
- [ ] Verify networking layer works

### Phase 4: Restore Full GameManager
- [ ] Gradually replace SimpleGameManager with full GameManager
- [ ] Add ClientManager and ServerManager
- [ ] Add PlayerSynchronizer
- [ ] Test complete multiplayer functionality

### Phase 5: Testing and Polish
- [ ] Test hosting sessions
- [ ] Test joining sessions
- [ ] Test player synchronization
- [ ] Performance optimization

## Key Lessons Learned

1. **TypeLoadException Root Cause**: **Steamworks.NET dependency** - ANY reference to Steamworks types in the assembly causes runtime loading failure
2. **Solution Strategy**: Remove ALL Steamworks references temporarily, then add back incrementally  
3. **Critical Dependencies**: Even unused `using Steamworks;` statements cause TypeLoadException
4. **Assembly Loading**: BepInEx/Enter the Gungeon environment has strict requirements for assembly dependencies

## Files to Monitor
- `GungeonTogetherMod.cs` - Main plugin entry point
- `Game/SimpleGameManager.cs` - Current simplified manager
- `Game/GameManager.cs` - Full manager to restore later
- `Networking/SteamNetworkManager.cs` - Core networking to restore
- `Networking/Packet/` - Packet system to restore

## Testing Checklist

### Basic Loading Test
1. Launch Enter the Gungeon
2. Check BepInEx console for mod loading messages
3. Verify no TypeLoadException
4. Test F3-F6 debug controls
5. Confirm SimpleGameManager initialization

### Networking Test (Future)
1. Verify Steam initialization
2. Test hosting session (F3)
3. Test showing status (F5)
4. Verify Steam P2P setup

### Full Multiplayer Test (Future)
1. Host session on one machine
2. Join from another machine
3. Verify player synchronization
4. Test real-time gameplay
