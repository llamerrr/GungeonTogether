using System;
using System.Collections.Generic;
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
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;

        public event Action<LobbyCreated_t> OnLobbyCreated;
        public event Action<LobbyEnter_t> OnLobbyEnter;
        public event Action<LobbyDataUpdate_t> OnLobbyDataUpdate;
        public event Action<GameRichPresenceJoinRequested_t> OnRichPresenceJoinRequested;
        public event Action<GameLobbyJoinRequested_t> OnGameLobbyJoinRequested;

        public static event Action<string> OnOverlayJoinRequested;
        public static event Action<bool> OnOverlayActivated;
        public static bool AreCallbacksRegistered { get; private set; }

        private static SteamCallbackManager _instance;

        private HashSet<ulong> _previousLobbyMembers = new HashSet<ulong>();
        private ulong _currentLobbyId = 0;
        private SteamP2PHostManager _p2pHostManager;
        private SteamP2PClientManager _p2pClientManager;

        public SteamCallbackManager()
        {
            GungeonTogether.Logging.Debug.Log("Initializing Steam Callback Manager");
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreatedInternal);
            _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnterInternal);
            _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateInternal);
            _richPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichPresenceJoinRequestedInternal);
            _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequestedInternal);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateInternal);
        }

        private void OnLobbyCreatedInternal(LobbyCreated_t param)
        {
            GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager.lobbycreated] Lobby created with result: {param.m_eResult}");
            if (param.m_eResult.Equals((uint)1)) // 1 = k_EResultOK
            {
                ulong hostSteamId = SteamReflectionHelper.GetSteamID();
                _p2pHostManager = new SteamP2PHostManager(param.m_ulSteamIDLobby, hostSteamId);
            }
            OnLobbyCreated?.Invoke(param);
        }

        private void OnLobbyEnterInternal(LobbyEnter_t param)
        {
            GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager.lobbyenter] Lobby entered with ID: {param.m_ulSteamIDLobby}");
            _currentLobbyId = param.m_ulSteamIDLobby;
            _previousLobbyMembers.Clear();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers((CSteamID)param.m_ulSteamIDLobby);
            ulong localSteamId = SteamReflectionHelper.GetSteamID();
            ulong hostSteamId = 0UL;
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)param.m_ulSteamIDLobby, i);
                _previousLobbyMembers.Add(memberId.m_SteamID);
                string name = SteamFriends.GetFriendPersonaName(memberId);
                GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] player {name} has joined the session");
                if (i == 0)
                {
                    hostSteamId = memberId.m_SteamID;
                }
            }
            GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] OnLobbyEnterInternal complete for lobby: {param.m_ulSteamIDLobby}");
            // If not host, start P2P client manager
            if (!localSteamId.Equals(hostSteamId))
            {
                GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] Initializing SteamP2PClientManager: host={hostSteamId}, local={localSteamId}");
                _p2pClientManager = new SteamP2PClientManager(hostSteamId, localSteamId);
            }
            OnLobbyEnter?.Invoke(param);
        }

        private void OnLobbyDataUpdateInternal(LobbyDataUpdate_t param)
        {
            GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager.lobbydataupdate] Lobby data updated for lobby ID: {param.m_ulSteamIDLobby}");
            if (!param.m_ulSteamIDLobby.Equals(_currentLobbyId))
            {
                // Not our current lobby, ignore
                OnLobbyDataUpdate?.Invoke(param);
                return;
            }
            var currentMembers = new HashSet<ulong>();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers((CSteamID)param.m_ulSteamIDLobby);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)param.m_ulSteamIDLobby, i);
                currentMembers.Add(memberId.m_SteamID);
            }
            // Detect joins
            foreach (var member in currentMembers)
            {
                if (!_previousLobbyMembers.Contains(member))
                {
                    string name = SteamFriends.GetFriendPersonaName(new CSteamID(member));
                    GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] player {name} has joined the session");
                }
            }
            // Detect leaves
            foreach (var member in _previousLobbyMembers)
            {
                if (!currentMembers.Contains(member))
                {
                    string name = SteamFriends.GetFriendPersonaName(new CSteamID(member));
                    GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] player {name} has left the session");
                }
            }
            _previousLobbyMembers = currentMembers;
            if (!ReferenceEquals(_p2pHostManager, null))
            {
                _p2pHostManager.ConnectClientsInLobby();
                _p2pHostManager.SendTestMessageToAllClients("Test packet from host");
            }
            OnLobbyDataUpdate?.Invoke(param);
        }

        private void OnRichPresenceJoinRequestedInternal(GameRichPresenceJoinRequested_t param)
        {
            GungeonTogether.Logging.Debug.Log("[steamcallbackmanager.richpresencejoinrequested] Rich presence join requested");
            OnRichPresenceJoinRequested?.Invoke(param);
        }

        private void OnGameLobbyJoinRequestedInternal(GameLobbyJoinRequested_t param)
        {
            GungeonTogether.Logging.Debug.Log("[steamcallbackmanager.gamelobbyjoinrequested] Game lobby join requested");
            // Leave current lobby if already in one
            if (!_currentLobbyId.Equals(0UL))
            {
                GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] Leaving current lobby: {_currentLobbyId} before joining new lobby: {param.m_steamIDLobby}");
                SteamMatchmaking.LeaveLobby(new CSteamID(_currentLobbyId));
            }
            GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] Calling JoinLobby for lobby: {param.m_steamIDLobby}");
            SteamMatchmaking.JoinLobby(param.m_steamIDLobby);
            OnGameLobbyJoinRequested?.Invoke(param);
        }

        private void OnLobbyChatUpdateInternal(LobbyChatUpdate_t param)
        {
            GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager.lobbychatupdate] Lobby chat update for lobby ID: {param.m_ulSteamIDLobby} (member: {param.m_ulSteamIDUserChanged}, state: {param.m_rgfChatMemberStateChange})");
            // Refresh member list and log join/leave
            var currentMembers = new HashSet<ulong>();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers((CSteamID)param.m_ulSteamIDLobby);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)param.m_ulSteamIDLobby, i);
                currentMembers.Add(memberId.m_SteamID);
            }
            foreach (var member in currentMembers)
            {
                if (!_previousLobbyMembers.Contains(member))
                {
                    string name = SteamFriends.GetFriendPersonaName(new CSteamID(member));
                    GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] player {name} has joined the session (LobbyChatUpdate)");
                    
                    // CRITICAL: Notify NetworkManager about the player join
                    try
                    {
                        GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] Forwarding player join to NetworkManager: {member}");
                        NetworkManager.Instance.NotifyPlayerJoined(member);
                    }
                    catch (Exception e)
                    {
                        GungeonTogether.Logging.Debug.LogError($"[steamcallbackmanager] Error notifying NetworkManager of player join: {e.Message}");
                    }
                }
            }
            foreach (var member in _previousLobbyMembers)
            {
                if (!currentMembers.Contains(member))
                {
                    string name = SteamFriends.GetFriendPersonaName(new CSteamID(member));
                    GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] player {name} has left the session (LobbyChatUpdate)");
                    
                    // CRITICAL: Notify NetworkManager about the player leave
                    try
                    {
                        GungeonTogether.Logging.Debug.Log($"[steamcallbackmanager] Forwarding player leave to NetworkManager: {member}");
                        NetworkManager.Instance.NotifyPlayerLeft(member);
                    }
                    catch (Exception e)
                    {
                        GungeonTogether.Logging.Debug.LogError($"[steamcallbackmanager] Error notifying NetworkManager of player leave: {e.Message}");
                    }
                }
            }
            _previousLobbyMembers = currentMembers;
            // Optionally, trigger networking update here if needed
        }

        public void HostLobby()
        {
            // Set the maximum number of players for the lobby
            int maxPlayers = 50; // Adjust as needed
            try
            {
                GungeonTogether.Logging.Debug.Log("[callbackmanager.hostlobby] requesting to host a lobby through steam");
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"Failed to create lobby: {e.Message}");
            }
        }

        public void JoinLobby(CSteamID lobbyId)
        {
            GungeonTogether.Logging.Debug.Log($"Attempting to join lobby with ID: {lobbyId}");
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
            _lobbyChatUpdate = null;
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

        public ulong GetCurrentLobbyId()
        {
            return _currentLobbyId;
        }

        public ulong GetLobbyOwnerSteamId(ulong lobbyId)
        {
            try
            {
                // Try to get lobby owner using Steam API
                var lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(lobbyId));
                return lobbyOwner.m_SteamID;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamCallbackManager] Could not get lobby owner: {e.Message}");
                return 0UL;
            }
        }
    }
}
