using System;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Interface for Steam P2P networking to avoid direct type dependencies
    /// </summary>
    public interface ISteamNetworking
    {
        bool IsAvailable();
        ulong GetSteamID();
        bool SendP2PPacket(ulong targetSteamId, byte[] data);
        bool AcceptP2PSession(ulong steamId);
        bool CloseP2PSession(ulong steamId);
        void Update();
        void Shutdown();
        
        // Rich Presence and lobby methods for Steam overlay invites
        bool SetRichPresence(string key, string value);
        bool ClearRichPresence();
        bool CreateLobby(int maxPlayers = 4);
        bool JoinLobby(ulong lobbyId);
        bool LeaveLobby();
        bool SetLobbyData(string key, string value);
        void StartHostingSession();
        void StartJoiningSession(ulong hostSteamId);
        void StopSession();
        
        // Join request handling
        void CheckForJoinRequests();
        void HandleJoinRequest(ulong joinerSteamId);
        void SimulateJoinRequest(ulong hostSteamId);
        
        // Debug and diagnostics
        void DiscoverMethodSignatures();
        
        // Events using custom delegates
        event PlayerJoinedHandler OnPlayerJoined;
        event PlayerLeftHandler OnPlayerLeft;
        event DataReceivedHandler OnDataReceived;
        
        // Join request event
        event System.Action<ulong> OnJoinRequested;
        bool ReadP2PSessionRequest(out ulong requestingSteamId);
        bool ReadP2PPacket(out ulong senderSteamId, out byte[] data);
    }
}
