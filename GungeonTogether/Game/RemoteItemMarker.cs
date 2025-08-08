using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Lightweight marker component to tag remote placeholder items on clients.
    /// </summary>
    public class RemoteItemMarker : MonoBehaviour
    {
        public int ItemId;
        public int Category;
        public int Quality;
    }
}
