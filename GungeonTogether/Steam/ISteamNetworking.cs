using System;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Interface for Steam P2P networking to avoid direct type dependencies
    /// Only includes minimal lobby/session methods for modern Steam join flow
    /// </summary>
    public interface ISteamNetworking
    {
        bool IsAvailable();
        ulong GetSteamID();
        bool JoinLobby(ulong lobbyId);
        bool LeaveLobby();
        bool SetLobbyData(string key, string value);
        bool SetRichPresence(string key, string value);
        bool ClearRichPresence();
        void StartHostingSession();
        void StartJoiningSession(ulong hostSteamId);
        void StopSession();
        void Shutdown();
        void CheckForJoinRequests();

        /// <summary>
        /// Event triggered when a player joins the Steam lobby (host only)
        /// </summary>
        event Action<ulong, string> OnPlayerJoined;
    }
}
