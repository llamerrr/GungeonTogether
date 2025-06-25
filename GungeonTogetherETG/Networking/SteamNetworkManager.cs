using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking.Packet;
using GungeonTogether.Networking.Packet.Data;

namespace GungeonTogether.Networking
{
    /// <summary>
    /// Handles Steam P2P networking for GungeonTogether.
    /// </summary>
    public class SteamNetworkManager
    {
        private Dictionary<CSteamID, ushort> connectedPlayers;
        private Dictionary<ushort, CSteamID> clientIdToSteamId;
        private bool isHost;
        private CSteamID hostId;
        private ushort nextClientId = 1;
        
        // Steam P2P callbacks
        private Callback<P2PSessionRequest_t> sessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> sessionFailCallback;
        
        // Events
        public event Action<ushort, LoginRequestPacket> OnClientLoginRequest;
        public event Action<LoginResponsePacket> OnLoginResponse;
        public event Action<ushort, IPacketData> OnPacketReceived;
        public event Action<ushort> OnClientDisconnected;
        
        public bool IsHost => isHost;
        public CSteamID HostId => hostId;
        public ushort LocalClientId { get; private set; }
        
        public SteamNetworkManager()
        {
            connectedPlayers = new Dictionary<CSteamID, ushort>();
            clientIdToSteamId = new Dictionary<ushort, CSteamID>();
            SetupCallbacks();
        }
        
        private void SetupCallbacks()
        {
            if (SteamManager.Initialized)
            {
                sessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
                sessionFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
            }
        }
        
        public void HostSession()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("Steam not initialized!");
                return;
            }
            
            isHost = true;
            hostId = SteamUser.GetSteamID();
            LocalClientId = 0; // Host is always client ID 0
            Debug.Log("Hosting session as " + hostId);
        }
        
        public void JoinSession(CSteamID hostSteamId)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("Steam not initialized!");
                return;
            }
            
            isHost = false;
            hostId = hostSteamId;
            
            // Send initial connection packet
            var packet = new LoginRequestPacket
            {
                PlayerName = SteamFriends.GetPersonaName(),
                ModVersion = "1.0.0"
            };
            SendPacketToHost(packet);
        }
        
        private void OnP2PSessionRequest(P2PSessionRequest_t request)
        {
            if (isHost)
            {
                // Accept connection requests when hosting
                SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
                Debug.Log("Accepted connection from " + request.m_steamIDRemote);
            }
        }
        
        private void OnP2PSessionConnectFail(P2PSessionConnectFail_t failure)
        {
            Debug.LogError($"P2P connection failed: {failure.m_eP2PSessionError}");
        }
        
        public void SendPacketToHost(IPacketData packet)
        {
            if (!isHost && hostId.IsValid())
            {
                SendPacket(hostId, packet);
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
            foreach (var steamId in connectedPlayers.Keys)
            {
                SendPacket(steamId, packet);
            }
        }
        
        private void SendPacket(CSteamID target, IPacketData packet)
        {
            try
            {
                byte[] data = packet.Serialize();
                EP2PSend sendType = packet.IsReliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliable;
                
                bool success = SteamNetworking.SendP2PPacket(target, data, (uint)data.Length, sendType, 0);
                if (!success)
                {
                    Debug.LogError($"Failed to send packet to {target}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending packet: {e.Message}");
            }
        }
        
        public void Update()
        {
            if (!SteamManager.Initialized) return;
            
            // Check for incoming P2P packets
            uint msgSize;
            while (SteamNetworking.IsP2PPacketAvailable(out msgSize, 0))
            {
                byte[] buffer = new byte[msgSize];
                CSteamID sender;
                
                if (SteamNetworking.ReadP2PPacket(buffer, msgSize, out msgSize, out sender, 0))
                {
                    ProcessIncomingPacket(sender, buffer);
                }
            }
        }
        
        private void ProcessIncomingPacket(CSteamID sender, byte[] data)
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
                    HandleHostPacket(sender, (ClientPacketId)packetType, packetData);
                }
                else
                {
                    HandleClientPacket(sender, (ServerPacketId)packetType, packetData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing packet from {sender}: {e.Message}");
            }
        }
        
        private void HandleHostPacket(CSteamID sender, ClientPacketId packetId, byte[] data)
        {
            switch (packetId)
            {
                case ClientPacketId.LoginRequest:
                    var loginRequest = new LoginRequestPacket();
                    loginRequest.Deserialize(data);
                    
                    // Assign client ID
                    ushort clientId = nextClientId++;
                    connectedPlayers[sender] = clientId;
                    clientIdToSteamId[clientId] = sender;
                    
                    OnClientLoginRequest?.Invoke(clientId, loginRequest);
                    break;
                    
                default:
                    // Handle other client packets
                    if (connectedPlayers.ContainsKey(sender))
                    {
                        IPacketData packet = CreatePacketFromId(packetId);
                        if (packet != null)
                        {
                            packet.Deserialize(data);
                            OnPacketReceived?.Invoke(connectedPlayers[sender], packet);
                        }
                    }
                    break;
            }
        }
        
        private void HandleClientPacket(CSteamID sender, ServerPacketId packetId, byte[] data)
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
        
        public void DisconnectClient(ushort clientId)
        {
            if (isHost && clientIdToSteamId.ContainsKey(clientId))
            {
                var steamId = clientIdToSteamId[clientId];
                SteamNetworking.CloseP2PSessionWithUser(steamId);
                
                connectedPlayers.Remove(steamId);
                clientIdToSteamId.Remove(clientId);
                
                OnClientDisconnected?.Invoke(clientId);
            }
        }
        
        public void Disconnect()
        {
            if (isHost)
            {
                // Close all client connections
                foreach (var steamId in connectedPlayers.Keys)
                {
                    SteamNetworking.CloseP2PSessionWithUser(steamId);
                }
            }
            else if (hostId.IsValid())
            {
                // Disconnect from host
                SteamNetworking.CloseP2PSessionWithUser(hostId);
            }
            
            connectedPlayers.Clear();
            clientIdToSteamId.Clear();
        }
    }
}
