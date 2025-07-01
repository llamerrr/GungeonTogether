using System;
using Steamworks;
using UnityEngine;

namespace GungeonTogether.Steam
{
    public class SteamCallbackManager : IDisposable
    {
        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<LobbyEnter_t> _lobbyEnter;
        private Callback<LobbyDataUpdate_t> _lobbyDataUpdate;
        private Callback<GameRichPresenceJoinRequested_t> _richPresenceJoinRequested;
        private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;

        public event Action<LobbyCreated_t> OnLobbyCreated;
        public event Action<LobbyEnter_t> OnLobbyEnter;
        public event Action<LobbyDataUpdate_t> OnLobbyDataUpdate;
        public event Action<GameRichPresenceJoinRequested_t> OnRichPresenceJoinRequested;
        public event Action<GameLobbyJoinRequested_t> OnGameLobbyJoinRequested;

        public static event Action<string> OnOverlayJoinRequested;
        public static event Action<bool> OnOverlayActivated;
        public static bool AreCallbacksRegistered { get; private set; }

        private static SteamCallbackManager _instance;

        public SteamCallbackManager()
        {
            Debug.Log("Initializing Steam Callback Manager");
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreatedInternal);
            _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnterInternal);
            _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateInternal);
            _richPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichPresenceJoinRequestedInternal);
            _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequestedInternal);

        }

        private void OnLobbyCreatedInternal(LobbyCreated_t param)
        {
            Debug.Log($"[steamcallbackmanager.lobbycreated] Lobby created with result: {param.m_eResult}");
            OnLobbyCreated?.Invoke(param);
        }

        private void OnLobbyEnterInternal(LobbyEnter_t param)
        {
            Debug.Log($"[steamcallbackmanager.lobbyenter] Lobby entered with ID: {param.m_ulSteamIDLobby}");
            // Print all current members
            int memberCount = SteamMatchmaking.GetNumLobbyMembers((CSteamID)param.m_ulSteamIDLobby);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)param.m_ulSteamIDLobby, i);
                string name = SteamFriends.GetFriendPersonaName(memberId);
                Debug.Log($"[steamcallbackmanager] player {name} has joined the session");
            }
            OnLobbyEnter?.Invoke(param);
        }

        private void OnLobbyDataUpdateInternal(LobbyDataUpdate_t param)
        {
            Debug.Log($"[steamcallbackmanager.lobbydataupdate] Lobby data updated for lobby ID: {param.m_ulSteamIDLobby}");
            // Detect leave events by comparing previous and current member lists if needed (not implemented here)
            OnLobbyDataUpdate?.Invoke(param);
        }

        private void OnRichPresenceJoinRequestedInternal(GameRichPresenceJoinRequested_t param)
        {
            Debug.Log("[steamcallbackmanager.richpresencejoinrequested] Rich presence join requested");
            OnRichPresenceJoinRequested?.Invoke(param);
        }

        private void OnGameLobbyJoinRequestedInternal(GameLobbyJoinRequested_t param)
        {
            Debug.Log("[steamcallbackmanager.gamelobbyjoinrequested] Game lobby join requested");
            OnGameLobbyJoinRequested?.Invoke(param);
        }

        public void HostLobby()
        {
            // Set the maximum number of players for the lobby
            int maxPlayers = 50; // Adjust as needed
            try
            {
                Debug.Log("[callbackmanager.hostlobby] requesting to host a lobby through steam");
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create lobby: {e.Message}");
            }
        }

        public void JoinLobby(CSteamID lobbyId)
        {
            Debug.Log($"Attempting to join lobby with ID: {lobbyId}");
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        public static void InitializeSteamCallbacks()
        {
            if (AreCallbacksRegistered)
            {
                return;
            }
            _instance = new SteamCallbackManager();
            AreCallbacksRegistered = true;
        }

        public static void TriggerOverlayJoinEvent(string hostSteamId)
        {
            OnOverlayJoinRequested?.Invoke(hostSteamId);
        }

        public static string GetCallbackStatus()
        {
            return AreCallbacksRegistered ? "Steam callbacks registered" : "Steam callbacks NOT registered";
        }

        public static void ProcessSteamCallbacks()
        {
            // SteamAPI.RunCallbacks() is required to process Steam events
            SteamAPI.RunCallbacks();
        }

        public void Dispose()
        {
            // Callbacks are automatically cleaned up by Steamworks.NET, but nullify for safety
            _lobbyCreated = null;
            _lobbyEnter = null;
            _lobbyDataUpdate = null;
            _richPresenceJoinRequested = null;
            _gameLobbyJoinRequested = null;
        }

        public static SteamCallbackManager Instance
        {
            get
            {
                if (ReferenceEquals(_instance, null))
                {
                    _instance = new SteamCallbackManager();
                }
                return _instance;
            }
        }
    }
}
