using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Diagnostic tool to explore ETG's loaded assemblies and Steam types
    /// </summary>
    public static class ETGSteamDiagnostics
    {
        /// <summary>
        /// Explore all loaded assemblies and types to find Steam-related classes
        /// </summary>
        public static void DiagnoseETGSteamTypes()
        {
            try
            {
                Debug.Log("[ETGDiagnostics] === Starting ETG Steam Diagnostics ===");
                
                // Get all loaded assemblies
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                Debug.Log($"[ETGDiagnostics] Found {assemblies.Length} loaded assemblies");
                
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly assembly = assemblies[i];
                    try
                    {
                        string assemblyName = assembly.GetName().Name;
                        Debug.Log($"[ETGDiagnostics] Checking assembly: {assemblyName}");
                        
                        // Look for Steam-related types using simple iteration
                        List<Type> steamTypes = new List<Type>();
                        Type[] allTypes = assembly.GetTypes();
                        
                        for (int j = 0; j < allTypes.Length; j++)
                        {
                            Type type = allTypes[j];
                            if (IsSteamRelatedType(type))
                            {
                                steamTypes.Add(type);
                            }
                        }
                        
                        if (steamTypes.Count > 0)
                        {
                            Debug.Log($"[ETGDiagnostics] Found {steamTypes.Count} Steam-related types in {assemblyName}:");
                            
                            int typesToShow = Math.Min(steamTypes.Count, 10);
                            for (int k = 0; k < typesToShow; k++)
                            {
                                Type type = steamTypes[k];
                                Debug.Log($"[ETGDiagnostics]   - {type.FullName}");
                                
                                // If this looks like a main Steam class, explore its methods
                                if (type.Name.Equals("SteamAPI") || type.Name.Equals("SteamUser") || type.Name.Equals("SteamNetworking"))
                                {
                                    ExploreSteamType(type);
                                }
                            }
                            
                            if (steamTypes.Count > 10)
                            {
                                Debug.Log($"[ETGDiagnostics]   ... and {steamTypes.Count - 10} more types");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ETGDiagnostics] Error checking assembly {assembly.GetName().Name}: {e.Message}");
                    }
                }
                
                // Try to find specific Steam classes that ETG might be using
                TryFindSpecificSteamClasses();
                
                Debug.Log("[ETGDiagnostics] === ETG Steam Diagnostics Complete ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGDiagnostics] Error during diagnostics: {e.Message}");
            }
        }
        
        private static bool IsSteamRelatedType(Type type)
        {
            try
            {
                return type.Name.Contains("Steam") || 
                       type.FullName.Contains("Steam") ||
                       type.Name.Contains("P2P") ||
                       type.FullName.Contains("Steamworks");
            }
            catch
            {
                return false;
            }
        }
        
        private static void ExploreSteamType(Type steamType)
        {
            try
            {
                Debug.Log($"[ETGDiagnostics] Exploring Steam type: {steamType.FullName}");
                
                // Check static methods
                var allMethods = steamType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var staticMethods = new List<MethodInfo>();
                
                for (int i = 0; i < allMethods.Length; i++)
                {
                    var method = allMethods[i];
                    if (!method.Name.StartsWith("get_") && !method.Name.StartsWith("set_"))
                    {
                        staticMethods.Add(method);
                    }
                }
                
                int methodsToShow = Math.Min(staticMethods.Count, 5);
                for (int i = 0; i < methodsToShow; i++)
                {
                    var method = staticMethods[i];
                    var parameters = method.GetParameters();
                    var paramNames = new string[parameters.Length];
                    
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        paramNames[j] = parameters[j].ParameterType.Name;
                    }
                    
                    var paramString = string.Join(", ", paramNames);
                    Debug.Log($"[ETGDiagnostics]   Static Method: {method.Name}({paramString})");
                }
                
                // Check static properties
                var allProps = steamType.GetProperties(BindingFlags.Public | BindingFlags.Static);
                int propsToShow = Math.Min(allProps.Length, 5);
                for (int i = 0; i < propsToShow; i++)
                {
                    var prop = allProps[i];
                    Debug.Log($"[ETGDiagnostics]   Static Property: {prop.PropertyType.Name} {prop.Name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGDiagnostics] Error exploring type {steamType.Name}: {e.Message}");
            }
        }
        
        private static void TryFindSpecificSteamClasses()
        {
            Debug.Log("[ETGDiagnostics] Searching for specific Steam classes...");
            
            string[] steamClassNames = {
                "SteamAPI",
                "SteamUser", 
                "SteamNetworking",
                "SteamFriends",
                "SteamMatchmaking",
                "SteamUtils",
                "Steamworks.SteamAPI",
                "Steamworks.SteamUser",
                "Steamworks.SteamNetworking"
            };
            
            for (int i = 0; i < steamClassNames.Length; i++)
            {
                var className = steamClassNames[i];
                try
                {
                    // Try to find the type by name across all assemblies
                    Type foundType = null;
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    
                    for (int j = 0; j < assemblies.Length; j++)
                    {
                        var assembly = assemblies[j];
                        try
                        {
                            foundType = assembly.GetType(className, false);
                            if (!object.ReferenceEquals(foundType, null))
                            {
                                Debug.Log($"[ETGDiagnostics] Found {className} in assembly: {assembly.GetName().Name}");
                                Debug.Log($"[ETGDiagnostics]   Full type name: {foundType.FullName}");
                                
                                // Try to check if Steam is initialized
                                if (className.Contains("SteamAPI"))
                                {
                                    CheckSteamAPIStatus(foundType);
                                }
                                break;
                            }
                        }
                        catch { /* Ignore errors when checking individual assemblies */ }
                    }
                    
                    if (object.ReferenceEquals(foundType, null))
                    {
                        Debug.Log($"[ETGDiagnostics] {className} not found");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ETGDiagnostics] Error searching for {className}: {e.Message}");
                }
            }
        }
        
        private static void CheckSteamAPIStatus(Type steamApiType)
        {
            try
            {
                Debug.Log($"[ETGDiagnostics] Checking SteamAPI status...");
                
                // Try to find IsInitialized or similar methods
                var initMethod = steamApiType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
                var isInitializedMethod = steamApiType.GetMethod("IsSteamRunning", BindingFlags.Public | BindingFlags.Static);
                if (object.ReferenceEquals(isInitializedMethod, null))
                {
                    isInitializedMethod = steamApiType.GetMethod("IsInitialized", BindingFlags.Public | BindingFlags.Static);
                }
                
                if (!object.ReferenceEquals(isInitializedMethod, null))
                {
                    try
                    {
                        var result = isInitializedMethod.Invoke(null, null);
                        Debug.Log($"[ETGDiagnostics] Steam Status: {result}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ETGDiagnostics] Error checking Steam status: {e.Message}");
                    }
                }
                else
                {
                    Debug.Log($"[ETGDiagnostics] No status checking method found on SteamAPI");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGDiagnostics] Error checking SteamAPI status: {e.Message}");
            }
        }
        
    }
}
