# TypeLoadException Troubleshooting Guide

## Issue Description
Receiving `System.TypeLoadException: Could not load type 'GungeonTogether.Game.MinimalGameManager'` when the mod attempts to initialize.

## Changes Made to Address the Issue

### 1. Simplified Class Structure ✅
- **Renamed**: `MinimalGameManager` → `BasicGameManager` to avoid potential naming conflicts
- **Simplified**: Removed complex dependencies and made the class ultra-minimal
- **Enhanced Error Handling**: Added extensive try-catch blocks and logging

### 2. Improved Error Handling ✅
- **Defensive Programming**: Added null checks throughout the initialization process
- **Detailed Logging**: Each step of initialization is now logged separately
- **Graceful Fallbacks**: Steam integration failures don't prevent basic mod loading

### 3. Cleaned Up Dependencies ✅
- **Removed**: Empty SafeGameManager.cs file that might cause conflicts
- **Removed**: Test mod files that reference old class names
- **Simplified**: Reduced external dependencies to minimum required

## Current Implementation Status

### Updated Class Structure
```csharp
// Old (causing TypeLoadException):
public class MinimalGameManager

// New (should resolve issue):
public class BasicGameManager
```

### Enhanced Initialization Process
```csharp
Logger.LogInfo("Step 1a: About to instantiate BasicGameManager...");
_gameManager = new Game.BasicGameManager();
Logger.LogInfo("Step 1b: BasicGameManager created successfully!");
```

### Improved Steam Integration
```csharp
try
{
    SteamSessionHelper.Initialize(_gameManager);
    Logger.LogInfo("Steam session helper initialized!");
}
catch (Exception steamEx)
{
    Logger.LogWarning($"Steam helper initialization failed: {steamEx.Message}");
    Logger.LogWarning("Continuing without Steam integration...");
}
```

## Testing Instructions

### 1. Test Basic Mod Loading
1. Launch Enter the Gungeon
2. Check BepInEx console for these log messages:
   ```
   [Info] TEST: GungeonTogether mod loading...
   [Info] TEST: Start() called, waiting for GameManager...
   [Info] TEST: GameManager is alive!
   [Info] Step 1a: About to instantiate BasicGameManager...
   [Info] Step 1b: BasicGameManager created successfully!
   ```

### 2. Expected Success Pattern
If successful, you should see:
```
[Info] Step 1: Creating ultra-minimal GameManager to isolate TypeLoadException...
[Info] Step 1a: About to instantiate BasicGameManager...
[Info] Step 1b: BasicGameManager created successfully!
[Info] Step 2: Checking manager state...
[Info] Step 2a: Manager Active: False
[Info] Step 2b: Manager Status: Initialized
[Info] Step 3: Attempting Steam session helper initialization...
[Info] Step 3a: Steam session helper initialized!
[Info] Step 4: Setting up event handlers...
[Info] Step 4a: Event handlers configured!
[Info] Step 5: Setting up debug controls...
[Info] GungeonTogether multiplayer systems initialized!
```

### 3. If TypeLoadException Still Occurs
Look for these error patterns in the console:
```
[Error] Failed to initialize GameManager: [Exception details]
[Error] Exception type: TypeLoadException
[Error] Stack trace: [Stack trace details]
```

## Potential Root Causes & Solutions

### 1. Assembly Loading Issues
**Problem**: BepInEx might not be loading dependencies correctly
**Solution**: 
- Ensure EtG.ModTheGungeonAPI v1.9.2 is properly installed
- Check that no other mods are conflicting with our assembly

### 2. Namespace Conflicts
**Problem**: Another mod or game assembly might have conflicting types
**Solution**: 
- Renamed class from MinimalGameManager to BasicGameManager
- Consider further renaming if issues persist

### 3. Framework Version Mismatch
**Problem**: .NET Framework version incompatibility
**Solution**:
- Verify project targets .NET Framework 4.7.2 (net472)
- Ensure BepInEx version compatibility

### 4. Missing Dependencies
**Problem**: Required dependencies not available at runtime
**Solution**:
- Simplified dependencies to absolute minimum
- Removed complex Steam integrations that might cause loading issues

## Fallback Implementation

If BasicGameManager still fails, we can implement an even simpler approach:

```csharp
// Ultra-minimal fallback
public class SimpleSessionManager
{
    public bool IsActive { get; set; }
    public string Status { get; set; } = "Ready";
    
    public void StartSession() 
    {
        IsActive = true;
        Debug.Log("Session started");
    }
    
    public void StopSession() 
    {
        IsActive = false;
        Debug.Log("Session stopped");
    }
}
```

## Debug Controls for Testing

Once the mod loads successfully:
- **F3** - Test basic session start
- **F4** - Test session stop  
- **F5** - Show current status
- **F6-F9** - Test Steam integration features

## Next Steps Based on Results

### If BasicGameManager Loads Successfully ✅
1. Gradually add back Steam integration features
2. Test each feature individually
3. Proceed with full multiplayer implementation

### If TypeLoadException Persists ❌
1. Implement SimpleSessionManager fallback
2. Investigate assembly loading in more detail
3. Consider alternative mod architecture

## File Status After Changes

### Modified Files ✅
- `GungeonTogetherMod.cs` - Enhanced error handling and logging
- `Game/MinimalGameManager.cs` - Renamed to BasicGameManager, simplified
- `Steam/SteamSessionHelper.cs` - Updated to use BasicGameManager

### Removed Files ✅
- `Game/SafeGameManager.cs` - Empty file removed
- `GungeonTogetherTestMod.cs` - Test file removed

### Clean Build Status ✅
- Build succeeds with only minor warnings
- No compilation errors
- DLL deployed to BepInEx plugins folder

**Ready for testing with improved error handling and simplified class structure.**
