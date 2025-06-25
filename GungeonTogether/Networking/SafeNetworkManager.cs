using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking.Packet;
using GungeonTogether.Networking.Packet.Data;

namespace GungeonTogether.Networking
{
    /// <summary>
    /// Network manager that uses Steam abstraction layer for safe multiplayer networking
    /// </summary>
    public class SafeNetworkManager
    {
        private ISteamService steamService;
        private Dictionary<string, ushort> steamIdToClientId;
        private Dictionary<ushort, string> clientIdToSteamId;
        private bool isHost;
        private string hostSteamId;
        private ushort nextClientId = 1;
        
        // Events
        public event Action<ushort, LoginRequestPacket> OnClientLoginRequest;
        public event Action<LoginResponsePacket> OnLoginResponse;
        public event Action<ushort, IPacketData> OnPacketReceived;
        public event Action<ushort> OnClientDisconnected;
        
        public bool IsHost => isHost;
        public string HostSteamId => hostSteamId;
        public ushort LocalClientId { get; private set; }
        public bool IsConnected => steamService?.IsAvailable == true;
        
        public SafeNetworkManager()
        {
            steamIdToClientId = new Dictionary<string, ushort>();
            clientIdToSteamId = new Dictionary<ushort, string>();
            
            try
            {
                steamService = SteamServiceFactory.GetSteamService();
                SetupCallbacks();
                Debug.Log($"[SafeNetworkManager] Created with service: {steamService.GetType().Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SafeNetworkManager] Failed to initialize: {e.Message}");
                steamService = new MockSteamService();
                SetupCallbacks();
            }
        }
        
        private void SetupCallbacks()
        {
            if (steamService != null)
            {
                steamService.OnSessionJoinRequest += OnSessionJoinRequest;
                steamService.OnPacketReceived += OnPacketReceived_Internal;
                steamService.OnPlayerDisconnected += OnPlayerDisconnected;
                steamService.OnConnectionFailed += OnConnectionFailed_Internal;
            }
        }
        
        public void HostSession()
        {
            if (steamService?.IsAvailable != true)
            {
                Debug.LogError("[SafeNetworkManager] Steam service not available for hosting!");
                return;
            }
            
            try
            {
                if (steamService.StartHosting())
                {
                    isHost = true;
                    hostSteamId = steamService.LocalSteamId;
                    LocalClientId = 0; // Host is always client ID 0
                    Debug.Log($"[SafeNetworkManager] Hosting session as {hostSteamId}");
                }
                else
                {
                    Debug.LogError("[SafeNetworkManager] Failed to start hosting session");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SafeNetworkManager] Error starting host session: {e.Message}");
            }
        }
        
        public void JoinSession(string hostSteamId)
        {
            if (steamService?.IsAvailable != true)
            {
                Debug.LogError("[SafeNetworkManager] Steam service not available for joining!");
                return;
            }
            
            try
            {
                if (steamService.JoinSession(hostSteamId))
                {
                    isHost = false;
                    this.hostSteamId = hostSteamId;
                    
                    // Send initial connection packet
                    var packet = new LoginRequestPacket
                    {
                        PlayerName = steamService.LocalPlayerName,
                        ModVersion = "1.0.0"
                    };
                    SendPacketToHost(packet);
                    Debug.Log($"[SafeNetworkManager] Joined session: {hostSteamId}");
                }
                else
                {
                    Debug.LogError("[SafeNetworkManager] Failed to join session");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SafeNetworkManager] Error joining session: {e.Message}");
            }
        }
        
        public void SendPacketToHost(IPacketData packet)
        {
            if (!isHost && !string.IsNullOrEmpty(hostSteamId))
            {
                SendPacket(hostSteamId, packet);
            }
        }
        
        public void SendPacketToClient(ushort clientId, IPacketData packet)
        {
            if (isHost && clientIdToSteamId.ContainsKey(clientId))
            {
                SendPacket(clientIdToSteamId[clientId], packet);
            }
        }
        
        public void SendPacketToAll(IPacketData packet)
        {
            foreach (var steamId in steamIdToClientId.Keys)
            {
                SendPacket(steamId, packet);
            }
        }
        
        private void SendPacket(string targetSteamId, IPacketData packet)
        {
            try
            {
                byte[] data = packet.Serialize();
                bool success = steamService.SendPacket(targetSteamId, data, packet.IsReliable);
                if (!success)
                {
                    Debug.LogError($"[SafeNetworkManager] Failed to send packet to {targetSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SafeNetworkManager] Error sending packet: {e.Message}");
            }
        }
        
        private void OnSessionJoinRequest(string steamId)
        {
            if (isHost)
            {
                Debug.Log($"[SafeNetworkManager] Join request from: {steamId}");
                // Auto-accept for now (could add approval logic later)
                ushort clientId = nextClientId++;
                steamIdToClientId[steamId] = clientId;
                clientIdToSteamId[clientId] = steamId;
            }
        }
        
        private void OnPacketReceived_Internal(string senderSteamId, byte[] data)
        {
            try
            {
                // First byte is packet type
                if (data.Length < 1) return;
                
                byte packetType = data[0];
                byte[] packetData = new byte[data.Length - 1];
                Array.Copy(data, 1, packetData, 0, packetData.Length);
                
                // Handle different packet types
                if (isHost)
                {
                    HandleHostPacket(senderSteamId, (ClientPacketId)packetType, packetData);
                }
                else
                {
                    HandleClientPacket(senderSteamId, (ServerPacketId)packetType, packetData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SafeNetworkManager] Error processing packet from {senderSteamId}: {e.Message}");
            }
        }
        
        private void HandleHostPacket(string senderSteamId, ClientPacketId packetId, byte[] data)
        {
            switch (packetId)
            {
                case ClientPacketId.LoginRequest:
                    var loginRequest = new LoginRequestPacket();
                    loginRequest.Deserialize(data);
                    
                    // Assign client ID if not already assigned
                    if (!steamIdToClientId.ContainsKey(senderSteamId))
                    {
                        ushort clientId = nextClientId++;
                        steamIdToClientId[senderSteamId] = clientId;
                        clientIdToSteamId[clientId] = senderSteamId;
                    }
                    
                    OnClientLoginRequest?.Invoke(steamIdToClientId[senderSteamId], loginRequest);
                    break;
                    
                default:
                    // Handle other client packets
                    if (steamIdToClientId.ContainsKey(senderSteamId))
                    {
                        IPacketData packet = CreatePacketFromId(packetId);
                        if (packet != null)
                        {
                            packet.Deserialize(data);
                            OnPacketReceived?.Invoke(steamIdToClientId[senderSteamId], packet);
                        }
                    }
                    break;
            }
        }
        
        private void HandleClientPacket(string senderSteamId, ServerPacketId packetId, byte[] data)
        {
            switch (packetId)
            {
                case ServerPacketId.LoginResponse:
                    var loginResponse = new LoginResponsePacket();
                    loginResponse.Deserialize(data);
                    LocalClientId = loginResponse.AssignedClientId;
                    OnLoginResponse?.Invoke(loginResponse);
                    break;
                    
                default:
                    IPacketData packet = CreatePacketFromId(packetId);
                    if (packet != null)
                    {
                        packet.Deserialize(data);
                        OnPacketReceived?.Invoke(0, packet); // Server packets don't have client ID
                    }
                    break;
            }
        }
        
        private IPacketData CreatePacketFromId(ClientPacketId packetId)
        {
            switch (packetId)
            {
                case ClientPacketId.PlayerUpdate: return new PlayerUpdatePacket();
                case ClientPacketId.PlayerEnterRoom: return new PlayerEnterRoomPacket();
                case ClientPacketId.PlayerWeaponSwitch: return new PlayerWeaponSwitchPacket();
                default: return null;
            }
        }
        
        private IPacketData CreatePacketFromId(ServerPacketId packetId)
        {
            switch (packetId)
            {
                case ServerPacketId.PlayerUpdate: return new PlayerUpdatePacket();
                case ServerPacketId.PlayerEnterRoom: return new PlayerEnterRoomPacket();
                case ServerPacketId.PlayerWeaponSwitch: return new PlayerWeaponSwitchPacket();
                default: return null;
            }
        }
        
        private void OnPlayerDisconnected(string steamId)
        {
            if (steamIdToClientId.ContainsKey(steamId))
            {
                ushort clientId = steamIdToClientId[steamId];
                steamIdToClientId.Remove(steamId);
                clientIdToSteamId.Remove(clientId);
                OnClientDisconnected?.Invoke(clientId);
            }
        }
        
        private void OnConnectionFailed_Internal(string reason)
        {
            Debug.LogError($"[SafeNetworkManager] Connection failed: {reason}");
        }
        
        public void DisconnectClient(ushort clientId)
        {
            if (isHost && clientIdToSteamId.ContainsKey(clientId))
            {
                var steamId = clientIdToSteamId[clientId];
                // Note: We don't have a direct disconnect method in ISteamService yet
                // This could trigger OnPlayerDisconnected callback
                OnPlayerDisconnected(steamId);
            }
        }
        
        public void Disconnect()
        {
            try
            {
                steamService?.LeaveSession();
                
                steamIdToClientId.Clear();
                clientIdToSteamId.Clear();
                isHost = false;
                hostSteamId = null;
                
                Debug.Log("[SafeNetworkManager] Disconnected from session");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SafeNetworkManager] Error during disconnect: {e.Message}");
            }
        }
    }
}
