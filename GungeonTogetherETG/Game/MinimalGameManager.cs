using System;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Minimal GameManager for testing TypeLoadException - zero external dependencies
    /// </summary>
    public class MinimalGameManager
    {
        // Basic state with no external dependencies
        public bool IsActive { get; private set; }
        public string Status { get; private set; }
        
        public MinimalGameManager()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            Status = "Initialized";
            IsActive = false;
        }
        
        public void StartSession()
        {
            IsActive = true;
            Status = "Hosting";
        }
        
        public void StopSession()
        {
            IsActive = false;
            Status = "Stopped";
        }
        
        public void Update()
        {
            // Minimal update logic
        }
    }
}
