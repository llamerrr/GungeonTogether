using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Networking.Packet;
using GungeonTogether.Networking.Packet.Data;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Manages client-side multiplayer functionality.
    /// </summary>
    public class ClientManager
    {        private SteamNetworkManager networkManager;
        private Dictionary<ushort, RemotePlayer> remotePlayers;
        private bool isActive = false;
        
        // Client identity
        public ushort ClientId { get; private set; } = 0;
        
        // Local player data tracking
        private Vector2 lastPosition;
        private string lastRoom;
        private string lastAnimation;
        private float updateTimer = 0f;
        private const float UPDATE_INTERVAL = 1f / 20f; // 20 updates per second
        
        public ClientManager(SteamNetworkManager networkManager)
        {
            this.networkManager = networkManager;
            this.remotePlayers = new Dictionary<ushort, RemotePlayer>();
        }
        
        public void StartAsHost()
        {
            Debug.Log("Starting ClientManager as host...");
            isActive = true;
            // Host doesn't need to do anything special for client startup
        }
        
        public void StartAsClient()
        {
            Debug.Log("Starting ClientManager as client...");
            isActive = true;
            // Client-specific initialization would go here
        }
        
        public void Stop()
        {
            Debug.Log("Stopping ClientManager...");
            isActive = false;
            
            // Clean up remote players
            foreach (var player in remotePlayers.Values)
            {
                player.Cleanup();
            }
            remotePlayers.Clear();
        }
        
        public void Update()
        {
            if (!isActive) return;
            
            updateTimer += Time.deltaTime;
            
            // Send position updates at regular intervals
            if (updateTimer >= UPDATE_INTERVAL)
            {
                SendPlayerUpdate();
                updateTimer = 0f;
            }
            
            // Update remote players
            foreach (var player in remotePlayers.Values)
            {
                player.Update();
            }
        }
        
        private void SendPlayerUpdate()
        {
            try
            {
                // Get local player data (this will need to be hooked into ETG's player system)
                var localPlayer = GetLocalPlayerData();
                if (localPlayer == null) return;
                
                // Only send if something changed
                if (HasPlayerDataChanged(localPlayer))
                {
                    var packet = new PlayerUpdatePacket
                    {
                        ClientId = networkManager.LocalClientId,
                        Position = localPlayer.Position,
                        Velocity = localPlayer.Velocity,
                        IsFacingRight = localPlayer.IsFacingRight,
                        IsGrounded = localPlayer.IsGrounded,
                        IsRolling = localPlayer.IsRolling,
                        IsShooting = localPlayer.IsShooting,
                        AimDirection = localPlayer.AimDirection,
                        CurrentAnimation = localPlayer.CurrentAnimation,
                        CurrentRoom = localPlayer.CurrentRoom
                    };
                    
                    if (networkManager.IsHost)
                    {
                        // Host sends to all clients
                        networkManager.SendPacketToAll(packet);
                    }
                    else
                    {
                        // Client sends to host
                        networkManager.SendPacketToHost(packet);
                    }
                    
                    // Update tracking data
                    lastPosition = localPlayer.Position;
                    lastRoom = localPlayer.CurrentRoom;
                    lastAnimation = localPlayer.CurrentAnimation;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending player update: {e.Message}");
            }
        }
        
        private LocalPlayerData GetLocalPlayerData()
        {
            // TODO: This needs to be implemented to hook into ETG's player system
            // For now, return dummy data
            return new LocalPlayerData
            {
                Position = Vector2.zero,
                Velocity = Vector2.zero,
                IsFacingRight = true,
                IsGrounded = true,
                IsRolling = false,
                IsShooting = false,
                AimDirection = 0f,
                CurrentAnimation = "idle",
                CurrentRoom = "test_room"
            };
        }
        
        private bool HasPlayerDataChanged(LocalPlayerData current)
        {
            const float POSITION_THRESHOLD = 0.1f;
            
            return Vector2.Distance(current.Position, lastPosition) > POSITION_THRESHOLD ||
                   current.CurrentRoom != lastRoom ||
                   current.CurrentAnimation != lastAnimation;
        }
        
        public void HandleServerPacket(IPacketData packet)
        {
            try
            {
                switch (packet)
                {
                    case PlayerUpdatePacket playerUpdate:
                        HandlePlayerUpdate(playerUpdate);
                        break;
                        
                    case PlayerEnterRoomPacket roomUpdate:
                        HandlePlayerEnterRoom(roomUpdate);
                        break;
                        
                    case PlayerWeaponSwitchPacket weaponSwitch:
                        HandlePlayerWeaponSwitch(weaponSwitch);
                        break;
                        
                    default:
                        Debug.LogWarning($"Unhandled server packet type: {packet.GetType()}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling server packet: {e.Message}");
            }
        }
        
        private void HandlePlayerUpdate(PlayerUpdatePacket packet)
        {
            if (packet.ClientId == networkManager.LocalClientId) return; // Don't update self
            
            if (!remotePlayers.ContainsKey(packet.ClientId))
            {
                // Create new remote player
                remotePlayers[packet.ClientId] = new RemotePlayer(packet.ClientId);
            }
            
            remotePlayers[packet.ClientId].UpdateFromPacket(packet);
        }
        
        private void HandlePlayerEnterRoom(PlayerEnterRoomPacket packet)
        {
            if (!remotePlayers.ContainsKey(packet.ClientId)) return;
            
            remotePlayers[packet.ClientId].ChangeRoom(packet.RoomName, packet.SpawnPosition);
        }
        
        private void HandlePlayerWeaponSwitch(PlayerWeaponSwitchPacket packet)
        {
            if (!remotePlayers.ContainsKey(packet.ClientId)) return;
            
            remotePlayers[packet.ClientId].SwitchWeapon(packet.WeaponId, packet.WeaponName);        }
        
        public void SendPacket(IPacketData packet)
        {
            if (networkManager == null) return;
            
            if (networkManager.IsHost)
            {
                // Host sends to all clients
                networkManager.SendPacketToAll(packet);
            }
            else
            {
                // Client sends to host
                networkManager.SendPacketToHost(packet);
            }
        }
        
        public void Dispose()
        {
            Stop();
        }
    }
    
    /// <summary>
    /// Represents local player data for networking.
    /// </summary>
    public class LocalPlayerData
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public bool IsFacingRight;
        public bool IsGrounded;
        public bool IsRolling;
        public bool IsShooting;
        public float AimDirection;
        public string CurrentAnimation;
        public string CurrentRoom;
    }
    
    /// <summary>
    /// Represents a remote player in the game.
    /// </summary>
    public class RemotePlayer
    {
        public ushort ClientId { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool IsFacingRight { get; private set; }
        public string CurrentRoom { get; private set; }        public string CurrentAnimation { get; private set; }
        
        public RemotePlayer(ushort clientId)
        {
            ClientId = clientId;
            // TODO: Create visual representation of remote player
        }
        
        public void UpdateFromPacket(PlayerUpdatePacket packet)
        {
            Position = packet.Position;
            Velocity = packet.Velocity;
            IsFacingRight = packet.IsFacingRight;
            CurrentAnimation = packet.CurrentAnimation;
            CurrentRoom = packet.CurrentRoom;
            
            // TODO: Update visual representation
        }
        
        public void ChangeRoom(string roomName, Vector2 spawnPosition)
        {
            CurrentRoom = roomName;
            Position = spawnPosition;
            
            // TODO: Handle room transition for remote player
        }
        
        public void SwitchWeapon(int weaponId, string weaponName)
        {
            // TODO: Update remote player's weapon visual
        }
        
        public void Update()
        {
            // TODO: Smooth movement interpolation, animation updates, etc.
        }
          public void Cleanup()
        {
            // TODO: Destroy visual representation when implemented
            // Currently no visual object to clean up
        }
    }
}
