# Testing Checklist - Phase 1 Complete

## 🚀 Success Criteria
- [x] Mod loads without TypeLoadException ✅
- [x] BepInEx recognizes the plugin ✅
- [x] MinimalGameManager initializes successfully ✅  
- [x] Debug controls respond (F3 tested and working) ✅
- [x] No critical errors in BepInEx console ✅

**🎉 COMPLETE SUCCESS - TypeLoadException fully resolved!**

## ✅ What We Fixed
- **Issue**: `TypeLoadException` when loading any class that references Steamworks types
- **Root Cause**: Steamworks.NET dependency not available in ETG+BepInEx runtime
- **Solution**: MinimalGameManager with zero external dependencies

## 🧪 Testing Steps

### 1. Verify Build and Deployment
```powershell
cd "GungeonTogetherETG"
.\build.ps1
```
**Expected**: Clean build and successful deployment to BepInEx plugins folder

### 2. Test Mod Loading
1. Launch Enter the Gungeon (Steam version with BepInEx installed)
2. Check BepInEx console output (press F12 or check logs)
3. Look for these log messages:
   ```
   [Info   :GungeonTogether] GungeonTogether mod loading...
   [Info   :GungeonTogether] Start() called, waiting for GameManager...
   [Info   :GungeonTogether] GameManager is alive! Initializing multiplayer systems...
   [Info   :GungeonTogether] Step 1: Skipping Steam checks for minimal test...
   [Info   :GungeonTogether] Step 2: Creating MinimalGameManager...
   [Info   :GungeonTogether] MinimalGameManager created successfully!
   ```

### 3. Test Debug Controls
In-game, press these keys:
- **F3**: Should log "F3: Starting host session..." and "Started hosting session in test mode!"
- **F4**: Should log "F4: Stopping multiplayer session..." and "Stopped session in test mode!"
- **F5**: Should show status with session state
- **F6**: Should log "Steam friend join functionality temporarily disabled for testing"

### 4. Verify No TypeLoadException
**❌ Before Fix**: Console showed `TypeLoadException: Could not load type 'GungeonTogether.Game.GameManager'`  
**❌ Previous Attempt**: `TypeLoadException: Could not load type 'GungeonTogether.Game.SimpleGameManager'`
**✅ After Fix**: No TypeLoadException, clean MinimalGameManager initialization

## 🚀 Success Criteria
- [ ] Mod loads without TypeLoadException
- [ ] BepInEx recognizes the plugin
- [ ] SimpleGameManager initializes successfully  
- [ ] Debug controls respond (F3-F6)
- [ ] No critical errors in BepInEx console

## 🔄 Next Steps After Successful Test
1. **Phase 2**: Gradually restore SteamNetworkManager
2. **Phase 3**: Add back packet system (simplified)
3. **Phase 4**: Restore full GameManager with networking
4. **Phase 5**: Complete multiplayer testing

## 📋 Common Issues & Solutions

### If mod still doesn't load:
- Verify BepInEx is properly installed
- Check that `GungeonTogether.dll` is in `BepInEx/plugins/`
- Ensure you're running the Steam version of ETG

### If Steam warnings appear:
- This is expected - Steam functionality is limited in SimpleGameManager
- Focus on verifying basic mod loading works first

### If debug controls don't work:
- Make sure you're in-game (not in menus)
- Check BepInEx console for any input handling errors

## 📊 Test Results Template
```
Date: ___________
ETG Version: ___________
BepInEx Version: ___________

✅/❌ Mod loads without TypeLoadException
✅/❌ SimpleGameManager initializes  
✅/❌ Debug controls functional
✅/❌ No critical errors

Notes: ___________________________
```
