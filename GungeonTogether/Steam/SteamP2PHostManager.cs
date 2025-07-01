using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GungeonTogether.Steam
{
    public class SteamP2PHostManager
    {
        private readonly Dictionary<ulong, object> _clientConnections = new Dictionary<ulong, object>();
        private object _listenSocket;
        private int _virtualPort = 1;
        private ulong _lobbyId;
        private ulong _hostSteamId;

        public SteamP2PHostManager(ulong lobbyId, ulong hostSteamId)
        {
            _lobbyId = lobbyId;
            _hostSteamId = hostSteamId;
            StartListening();
        }

        private void StartListening()
        {
            _listenSocket = SteamNetworkingSocketsHelper.CreateListenSocketP2P(_virtualPort);
            Debug.Log("[SteamP2PHostManager] Host is now listening for P2P connections");
        }

        public void ConnectClientsInLobby()
        {
            // Use reflection to get SteamMatchmaking and CSteamID
            var steamworksAssembly = typeof(SteamReflectionHelper).Assembly;
            var steamMatchmakingType = steamworksAssembly.GetType("Steamworks.SteamMatchmaking");
            var csteamIdType = steamworksAssembly.GetType("Steamworks.CSteamID");
            var getNumLobbyMembers = steamMatchmakingType.GetMethod("GetNumLobbyMembers");
            var getLobbyMemberByIndex = steamMatchmakingType.GetMethod("GetLobbyMemberByIndex");
            object csteamLobbyId = Activator.CreateInstance(csteamIdType, _lobbyId);
            int memberCount = (int)getNumLobbyMembers.Invoke(null, new object[] { csteamLobbyId });
            for (int i = 0; i < memberCount; i++)
            {
                object memberIdObj = getLobbyMemberByIndex.Invoke(null, new object[] { csteamLobbyId, i });
                ulong memberSteamId = (ulong)memberIdObj.GetType().GetField("m_SteamID").GetValue(memberIdObj);
                if (!memberSteamId.Equals(_hostSteamId) && !_clientConnections.ContainsKey(memberSteamId))
                {
                    object hConn = SteamNetworkingSocketsHelper.ConnectP2P(memberSteamId, _virtualPort);
                    _clientConnections[memberSteamId] = hConn;
                    Debug.Log($"[SteamP2PHostManager] Connected to client: {memberSteamId}");
                }
            }
        }

        public void SendTestMessageToAllClients(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            int flags = 0; // Reliable or Unreliable as needed
            foreach (var kvp in _clientConnections)
            {
                object hConn = kvp.Value;
                int result = SteamNetworkingSocketsHelper.SendMessageToConnection(hConn, data, flags);
                Debug.Log($"[SteamP2PHostManager] Sent test message to {kvp.Key}, result: {result}");
            }
        }
    }
}
