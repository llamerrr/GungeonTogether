using System;
using UnityEngine;
using GungeonTogether.Networking.Packet.Data;

namespace GungeonTogether.Networking
{
    /// <summary>
    /// Interface for Steam networking services with fallback support
    /// </summary>
    public interface ISteamService
    {
        bool IsAvailable { get; }
        bool IsInitialized { get; }
        string LocalSteamId { get; }
        string LocalPlayerName { get; }
        
        // Session management
        bool StartHosting();
        bool JoinSession(string hostSteamId);
        void LeaveSession();
        
        // Networking
        bool SendPacket(string targetSteamId, byte[] data, bool reliable = true);
        bool SendPacketToAll(byte[] data, bool reliable = true);
        
        // Callbacks
        event Action<string> OnSessionJoinRequest;
        event Action<string, byte[]> OnPacketReceived;
        event Action<string> OnPlayerDisconnected;
        event Action<string> OnConnectionFailed;
    }
    
    /// <summary>
    /// Mock Steam service for testing without Steam
    /// </summary>
    public class MockSteamService : ISteamService
    {
        public bool IsAvailable => true;
        public bool IsInitialized => true;
        public string LocalSteamId => "76561198000000000"; // Mock Steam ID
        public string LocalPlayerName => "TestPlayer";
        
        private bool isHosting = false;
        private string currentHost = null;
        
        public event Action<string> OnSessionJoinRequest;
        public event Action<string, byte[]> OnPacketReceived;
        public event Action<string> OnPlayerDisconnected;
        public event Action<string> OnConnectionFailed;
        
        public bool StartHosting()
        {
            Debug.Log("[MockSteam] Starting host session");
            isHosting = true;
            return true;
        }
        
        public bool JoinSession(string hostSteamId)
        {
            Debug.Log($"[MockSteam] Joining session: {hostSteamId}");
            currentHost = hostSteamId;
            return true;
        }
        
        public void LeaveSession()
        {
            Debug.Log("[MockSteam] Leaving session");
            isHosting = false;
            currentHost = null;
        }
        
        public bool SendPacket(string targetSteamId, byte[] data, bool reliable = true)
        {
            Debug.Log($"[MockSteam] Sending packet to {targetSteamId}, size: {data.Length}, reliable: {reliable}");
            return true;
        }
        
        public bool SendPacketToAll(byte[] data, bool reliable = true)
        {
            Debug.Log($"[MockSteam] Broadcasting packet, size: {data.Length}, reliable: {reliable}");
            return true;
        }
    }
}
