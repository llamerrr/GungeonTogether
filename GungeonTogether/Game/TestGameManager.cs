using System;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Absolutely minimal test class to isolate TypeLoadException
    /// Contains ONLY basic C# features, no Unity dependencies
    /// </summary>
    public class TestGameManager
    {
        public bool IsWorking { get; private set; }
        
        public TestGameManager()
        {
            IsWorking = true;
        }
        
        public string GetStatus()
        {
            return "TestGameManager is working";
        }
    }
}
