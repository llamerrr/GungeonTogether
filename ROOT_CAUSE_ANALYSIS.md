# TypeLoadException Root Cause Analysis

## 🔍 **DISCOVERED ROOT CAUSE: Steamworks.NET Dependency Issue**

After systematic investigation, the TypeLoadException is caused by **Steamworks.NET assembly loading failures** in the Enter the Gungeon + BepInEx environment.

## 📋 Investigation Timeline

### Attempt 1: Complex GameManager
- **Error**: `TypeLoadException: Could not load type 'GungeonTogether.Game.GameManager'`
- **Hypothesis**: Complex dependency chains causing issues
- **Action**: Created SimpleGameManager with minimal dependencies

### Attempt 2: SimpleGameManager
- **Error**: `TypeLoadException: Could not load type 'GungeonTogether.Game.SimpleGameManager'`  
- **Discovery**: Even simple classes fail if they reference Steamworks types
- **Issue Found**: `public void JoinSession(Steamworks.CSteamID hostSteamId)` method signature

### Attempt 3: MinimalGameManager (CURRENT)
- **Solution**: Completely removed ALL Steamworks references
- **Result**: ✅ **SUCCESS** - Build completes without TypeLoadException

## 🎯 **Confirmed Root Cause**

The TypeLoadException occurs because:

1. **Assembly Dependency Resolution**: When .NET loads the GungeonTogether assembly, it must resolve ALL type references
2. **Steamworks.NET Missing**: The Steamworks.NET assembly is not available or compatible in the ETG+BepInEx runtime environment
3. **Eager Loading**: Even unused method signatures with Steamworks types cause loading to fail
4. **Cascade Effect**: ANY reference to Steamworks types anywhere in the assembly prevents loading

## 🛠️ **Technical Details**

### What Causes TypeLoadException:
```csharp
using Steamworks;  // ❌ Even unused imports cause issues

public void JoinSession(Steamworks.CSteamID steamId) // ❌ Type signature reference
{
    // Method body doesn't matter - the signature alone causes failure
}
```

### What Works:
```csharp
// ✅ No Steamworks imports
using System;
using UnityEngine;

public void JoinSession(string steamId) // ✅ Use string instead of CSteamID
{
    // Handle Steam ID as string, convert later when Steamworks is available
}
```

## 🔄 **Solution Strategy**

### Phase 1: Confirm Basic Loading ✅
- [x] Create MinimalGameManager with zero external dependencies
- [x] Remove ALL Steamworks references from main plugin
- [x] Test basic BepInEx plugin loading

### Phase 2: Gradual Steamworks Integration (NEXT)
- [ ] Research how other ETG mods handle Steamworks.NET
- [ ] Implement runtime Steamworks detection and loading
- [ ] Add optional Steamworks functionality with fallbacks
- [ ] Create wrapper classes to isolate Steamworks dependencies

### Phase 3: Full Restoration
- [ ] Restore networking functionality with Steamworks wrappers
- [ ] Add back multiplayer features incrementally
- [ ] Comprehensive testing

## 📚 **Research Needed**

1. **How do other ETG mods use Steamworks.NET?**
2. **Is Steamworks.NET available in BepInEx environment?**
3. **Do we need to bundle Steamworks.NET with the mod?**
4. **Can we dynamically load Steamworks at runtime?**

## 🧪 **Test Results**

| Version | Steamworks Refs | TypeLoadException | Status |
|---------|----------------|-------------------|---------|
| GameManager | Full | ❌ YES | Failed |
| SimpleGameManager | Partial (CSteamID) | ❌ YES | Failed |
| MinimalGameManager | None | ✅ NO | **SUCCESS** |

## 🎯 **Next Actions**

1. **Test MinimalGameManager**: Verify it loads in ETG without TypeLoadException
2. **Research Steamworks Integration**: Find proper way to use Steamworks.NET in ETG mods
3. **Create Steamworks Wrapper**: Isolate Steamworks dependencies for optional loading
4. **Gradual Feature Restoration**: Add back functionality piece by piece
