using System;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Absolute minimal session manager as fallback for TypeLoadException issues
    /// </summary>
    public class SimpleSessionManager
    {
        public bool IsActive { get; private set; }
        public string Status { get; private set; }
        
        public SimpleSessionManager()
        {
            IsActive = false;
            Status = "Ready";
            Debug.Log("[SimpleSessionManager] Fallback session manager initialized");
        }
        
        public void StartSession()
        {
            IsActive = true;
            Status = "Active";
            Debug.Log("[SimpleSessionManager] Session started");
        }
        
        public void StopSession()
        {
            IsActive = false;
            Status = "Stopped";
            Debug.Log("[SimpleSessionManager] Session stopped");
        }
        
        public void Update()
        {
            // Minimal update
        }
    }
}
