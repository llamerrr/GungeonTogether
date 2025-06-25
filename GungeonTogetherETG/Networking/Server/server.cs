using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Networking.Packet;
using GungeonTogether.Networking.Packet.Data;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Manages server-side multiplayer functionality.
    /// </summary>
    public class ServerManager
    {
        private SteamNetworkManager networkManager;
        private Dictionary<ushort, ConnectedClient> connectedClients;
        private bool isRunning = false;
        
        public ServerManager(SteamNetworkManager networkManager)
        {
            this.networkManager = networkManager;
            this.connectedClients = new Dictionary<ushort, ConnectedClient>();
        }
        
        public void StartServer()
        {
            Debug.Log("Starting ServerManager...");
            isRunning = true;
            // Server initialization logic would go here
        }
        
        public void StopServer()
        {
            Debug.Log("Stopping ServerManager...");
            isRunning = false;
            
            // Disconnect all clients
            foreach (var client in connectedClients.Values)
            {
                networkManager.DisconnectClient(client.ClientId);
            }
            connectedClients.Clear();
        }
        
        public void Update()
        {
            if (!isRunning) return;
            
            // Server update logic (game state validation, etc.)
            UpdateGameState();
        }
        
        private void UpdateGameState()
        {
            // TODO: Implement server-side game state management
            // - Validate player positions
            // - Handle enemy AI (if server-authoritative)
            // - Manage room state
            // - Handle item pickups
        }
        
        public void OnClientConnected(ushort clientId, string playerName)
        {
            try
            {
                Debug.Log($"Client {clientId} ({playerName}) connected to server");
                
                var client = new ConnectedClient
                {
                    ClientId = clientId,
                    PlayerName = playerName,
                    ConnectedAt = DateTime.Now,
                    LastUpdate = DateTime.Now
                };
                
                connectedClients[clientId] = client;
                
                // Notify other clients about new player
                var connectPacket = new PlayerUpdatePacket
                {
                    ClientId = clientId,
                    // TODO: Set initial spawn data
                };
                
                // Send to all other clients
                foreach (var otherClientId in connectedClients.Keys)
                {
                    if (otherClientId != clientId)
                    {
                        networkManager.SendPacketToClient(otherClientId, connectPacket);
                    }
                }
                
                // Send existing players to new client
                foreach (var existingClient in connectedClients.Values)
                {
                    if (existingClient.ClientId != clientId)
                    {
                        var existingPlayerPacket = new PlayerUpdatePacket
                        {
                            ClientId = existingClient.ClientId,
                            Position = existingClient.LastKnownPosition,
                            CurrentRoom = existingClient.CurrentRoom
                        };
                        
                        networkManager.SendPacketToClient(clientId, existingPlayerPacket);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling client connection: {e.Message}");
            }
        }
        
        public void OnClientDisconnected(ushort clientId)
        {
            try
            {
                if (connectedClients.ContainsKey(clientId))
                {
                    var client = connectedClients[clientId];
                    Debug.Log($"Client {clientId} ({client.PlayerName}) disconnected from server");
                    
                    connectedClients.Remove(clientId);
                    
                    // Notify other clients about disconnection
                    // TODO: Send disconnect packet to remaining clients
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling client disconnection: {e.Message}");
            }
        }
        
        public void HandleClientPacket(ushort clientId, IPacketData packet)
        {
            try
            {
                if (!connectedClients.ContainsKey(clientId))
                {
                    Debug.LogWarning($"Received packet from unknown client: {clientId}");
                    return;
                }
                
                var client = connectedClients[clientId];
                client.LastUpdate = DateTime.Now;
                
                switch (packet)
                {
                    case PlayerUpdatePacket playerUpdate:
                        HandlePlayerUpdate(clientId, playerUpdate);
                        break;
                        
                    case PlayerEnterRoomPacket roomUpdate:
                        HandlePlayerEnterRoom(clientId, roomUpdate);
                        break;
                        
                    case PlayerWeaponSwitchPacket weaponSwitch:
                        HandlePlayerWeaponSwitch(clientId, weaponSwitch);
                        break;
                        
                    default:
                        Debug.LogWarning($"Unhandled client packet type: {packet.GetType()}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling client packet: {e.Message}");
            }
        }
        
        private void HandlePlayerUpdate(ushort clientId, PlayerUpdatePacket packet)
        {
            var client = connectedClients[clientId];
            
            // Update client's known position
            client.LastKnownPosition = packet.Position;
            client.CurrentRoom = packet.CurrentRoom;
            
            // Validate position (basic anti-cheat)
            if (!IsValidPosition(packet.Position))
            {
                Debug.LogWarning($"Invalid position from client {clientId}: {packet.Position}");
                return;
            }
            
            // Relay to other clients in the same room
            foreach (var otherClientId in connectedClients.Keys)
            {
                if (otherClientId != clientId && 
                    connectedClients[otherClientId].CurrentRoom == client.CurrentRoom)
                {
                    networkManager.SendPacketToClient(otherClientId, packet);
                }
            }
        }
        
        private void HandlePlayerEnterRoom(ushort clientId, PlayerEnterRoomPacket packet)
        {
            var client = connectedClients[clientId];
            var oldRoom = client.CurrentRoom;
            
            client.CurrentRoom = packet.RoomName;
            client.LastKnownPosition = packet.SpawnPosition;
            
            // Notify clients in both old and new rooms
            var roomsToNotify = new HashSet<string>();
            if (!string.IsNullOrEmpty(oldRoom)) roomsToNotify.Add(oldRoom);
            if (!string.IsNullOrEmpty(packet.RoomName)) roomsToNotify.Add(packet.RoomName);
            
            foreach (var room in roomsToNotify)
            {
                foreach (var otherClient in connectedClients.Values)
                {
                    if (otherClient.ClientId != clientId && otherClient.CurrentRoom == room)
                    {
                        networkManager.SendPacketToClient(otherClient.ClientId, packet);
                    }
                }
            }
        }
        
        private void HandlePlayerWeaponSwitch(ushort clientId, PlayerWeaponSwitchPacket packet)
        {
            var client = connectedClients[clientId];
            
            // Relay to other clients in the same room
            foreach (var otherClient in connectedClients.Values)
            {
                if (otherClient.ClientId != clientId && 
                    otherClient.CurrentRoom == client.CurrentRoom)
                {
                    networkManager.SendPacketToClient(otherClient.ClientId, packet);
                }
            }
        }
        
        private bool IsValidPosition(Vector2 position)
        {
            // TODO: Implement proper position validation based on room bounds
            // For now, just check for reasonable values
            return !float.IsNaN(position.x) && !float.IsNaN(position.y) &&
                   position.x > -1000 && position.x < 1000 &&
                   position.y > -1000 && position.y < 1000;
        }
        
        /// <summary>
        /// Broadcasts a packet to all connected clients.
        /// </summary>
        public void BroadcastPacket(IPacketData packet, ushort excludeClientId = 0)
        {
            try
            {
                foreach (var clientId in connectedClients.Keys)
                {
                    if (clientId != excludeClientId)
                    {
                        networkManager.SendPacketToClient(clientId, packet);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error broadcasting packet: {e.Message}");
            }
        }
        
        /// <summary>
        /// Broadcasts a packet to all connected clients except the sender.
        /// </summary>
        public void BroadcastPacketExceptSender(ushort senderClientId, IPacketData packet)
        {
            BroadcastPacket(packet, senderClientId);
        }
        
        public void Dispose()
        {
            StopServer();
        }
    }
    
    /// <summary>
    /// Represents a connected client on the server.
    /// </summary>
    public class ConnectedClient
    {
        public ushort ClientId;
        public string PlayerName;
        public DateTime ConnectedAt;
        public DateTime LastUpdate;
        public Vector2 LastKnownPosition;
        public string CurrentRoom;
    }
}