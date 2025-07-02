using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Game;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Main network manager for GungeonTogether multiplayer
    /// Handles packet transmission, reception, and processing
    /// </summary>
    public class NetworkManager
    {
        private static NetworkManager instance;
        public static NetworkManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new NetworkManager();
                }
                return instance;
            }
        }

        private SteamP2PHostManager hostManager;
        private SteamP2PClientManager clientManager;
        private bool isHost;
        private bool isInitialized;
        private ulong localSteamId;
        
        // Packet queues for processing
        private readonly Queue<NetworkPacket> incomingPackets = new Queue<NetworkPacket>();
        private readonly Queue<NetworkPacket> outgoingPackets = new Queue<NetworkPacket>();
        
        // Connected players
        private readonly Dictionary<ulong, PlayerInfo> connectedPlayers = new Dictionary<ulong, PlayerInfo>();
        
        // Network statistics
        private float lastHeartbeatTime;
        private const float HEARTBEAT_INTERVAL = 1.0f;
        private const float PLAYER_UPDATE_INTERVAL = 0.05f; // 20 FPS for player updates
        private const float ENEMY_UPDATE_INTERVAL = 0.1f;   // 10 FPS for enemy updates
        
        private float lastPlayerUpdateTime;
        private float lastEnemyUpdateTime;

        // Events
        public event Action<ulong> OnPlayerJoined;
        public event Action<ulong> OnPlayerLeft;
        public event Action<NetworkPacket> OnPacketReceived;

        public struct PlayerInfo
        {
            public ulong SteamId;
            public string Name;
            public Vector2 LastKnownPosition;
            public float LastUpdateTime;
            public bool IsConnected;
        }

        private NetworkManager()
        {
            localSteamId = SteamReflectionHelper.GetLocalSteamId();
        }

        /// <summary>
        /// Initialize as host
        /// </summary>
        public bool InitializeAsHost(ulong lobbyId)
        {
            if (isInitialized) return true;
            
            try
            {
                isHost = true;
                hostManager = new SteamP2PHostManager(lobbyId, localSteamId);
                
                // Add ourselves as a player
                connectedPlayers[localSteamId] = new PlayerInfo
                {
                    SteamId = localSteamId,
                    Name = "Host",
                    LastKnownPosition = Vector2.zero,
                    LastUpdateTime = Time.time,
                    IsConnected = true
                };
                
                isInitialized = true;
                GungeonTogether.Logging.Debug.Log("[NetworkManager] Initialized as HOST");
                return true;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Failed to initialize as host: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize as client
        /// </summary>
        public bool InitializeAsClient(ulong hostSteamId)
        {
            if (isInitialized) return true;
            
            try
            {
                isHost = false;
                clientManager = new SteamP2PClientManager(hostSteamId, localSteamId);
                
                // Add ourselves as a player
                connectedPlayers[localSteamId] = new PlayerInfo
                {
                    SteamId = localSteamId,
                    Name = "Client",
                    LastKnownPosition = Vector2.zero,
                    LastUpdateTime = Time.time,
                    IsConnected = true
                };
                
                isInitialized = true;
                GungeonTogether.Logging.Debug.Log("[NetworkManager] Initialized as CLIENT");
                return true;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Failed to initialize as client: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update method to be called from main thread
        /// </summary>
        public void Update()
        {
            if (!isInitialized) return;

            // Update host/client managers
            if (isHost && !ReferenceEquals(hostManager, null))
            {
                hostManager.Update();
            }
            else if (!isHost && !ReferenceEquals(clientManager, null))
            {
                clientManager.Update();
            }

            ProcessIncomingPackets();
            ProcessOutgoingPackets();
            SendPeriodicUpdates();
            CheckPlayerConnections();
        }

        #region Packet Sending

        /// <summary>
        /// Send packet to all connected players
        /// </summary>
        public void SendToAll(PacketType type, byte[] data)
        {
            if (!isInitialized) return;

            var packet = new NetworkPacket(type, localSteamId, data);
            outgoingPackets.Enqueue(packet);
        }

        /// <summary>
        /// Send packet to specific player
        /// </summary>
        public void SendToPlayer(ulong targetSteamId, PacketType type, byte[] data)
        {
            if (!isInitialized) return;

            var packet = new NetworkPacket(type, localSteamId, data);
            // For now, we'll use the same queue and filter in ProcessOutgoingPackets
            outgoingPackets.Enqueue(packet);
        }

        /// <summary>
        /// Send player position update
        /// </summary>
        public void SendPlayerPosition(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling)
        {
            var data = new PlayerPositionData
            {
                PlayerId = localSteamId,
                Position = position,
                Velocity = velocity,
                Rotation = rotation,
                IsGrounded = isGrounded,
                IsDodgeRolling = isDodgeRolling
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.PlayerPosition, serializedData);
        }

        /// <summary>
        /// Send player shooting event
        /// </summary>
        public void SendPlayerShooting(Vector2 position, Vector2 direction, int weaponId, bool isCharging = false, float chargeAmount = 0f)
        {
            var data = new PlayerShootingData
            {
                PlayerId = localSteamId,
                Position = position,
                Direction = direction,
                WeaponId = weaponId,
                IsCharging = isCharging,
                ChargeAmount = chargeAmount
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.PlayerShooting, serializedData);
        }

        /// <summary>
        /// Send enemy state update (host only)
        /// </summary>
        public void SendEnemyState(int enemyId, Vector2 position, Vector2 velocity, float rotation, float health, int animationState, bool isActive)
        {
            if (!isHost) return; // Only host sends enemy updates
            
            var data = new EnemyPositionData
            {
                EnemyId = enemyId,
                Position = position,
                Velocity = velocity,
                Rotation = rotation,
                Health = health,
                AnimationState = animationState,
                IsActive = isActive
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.EnemyPosition, serializedData);
        }

        /// <summary>
        /// Send enemy shooting event (host only)
        /// </summary>
        public void SendEnemyShooting(int enemyId, Vector2 position, Vector2 direction, int projectileId)
        {
            if (!isHost) return; // Only host sends enemy updates
            
            var data = new EnemyShootingData
            {
                EnemyId = enemyId,
                Position = position,
                Direction = direction,
                ProjectileId = projectileId
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.EnemyShooting, serializedData);
        }

        /// <summary>
        /// Send projectile spawn event
        /// </summary>
        public void SendProjectileSpawn(int projectileId, Vector2 position, Vector2 velocity, float rotation, int ownerId, bool isPlayerProjectile)
        {
            var data = new ProjectileSpawnData
            {
                ProjectileId = projectileId,
                Position = position,
                Velocity = velocity,
                Rotation = rotation,
                OwnerId = ownerId,
                IsPlayerProjectile = isPlayerProjectile
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.ProjectileSpawn, serializedData);
        }

        /// <summary>
        /// Send heartbeat to maintain connection
        /// </summary>
        public void SendHeartbeat()
        {
            SendToAll(PacketType.HeartBeat, new byte[0]);
        }

        #endregion

        #region Packet Processing

        private void ProcessIncomingPackets()
        {
            while (incomingPackets.Count > 0)
            {
                var packet = incomingPackets.Dequeue();
                HandlePacket(packet);
            }
        }

        private void ProcessOutgoingPackets()
        {
            while (outgoingPackets.Count > 0)
            {
                var packet = outgoingPackets.Dequeue();
                SendPacketToNetwork(packet);
            }
        }

        private void SendPacketToNetwork(NetworkPacket packet)
        {
            try
            {
                var serializedPacket = PacketSerializer.SerializePacket(packet);
                if (serializedPacket == null) return;

                if (isHost && hostManager != null)
                {
                    hostManager.SendToAllClients(serializedPacket);
                }
                else if (!isHost && clientManager != null)
                {
                    clientManager.SendToHost(serializedPacket);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Failed to send packet: {e.Message}");
            }
        }

        private void HandlePacket(NetworkPacket packet)
        {
            try
            {
                switch (packet.Type)
                {
                    case PacketType.PlayerJoin:
                        HandlePlayerJoin(packet);
                        break;
                    case PacketType.PlayerLeave:
                        HandlePlayerLeave(packet);
                        break;
                    case PacketType.PlayerPosition:
                        HandlePlayerPosition(packet);
                        break;
                    case PacketType.PlayerShooting:
                        HandlePlayerShooting(packet);
                        break;
                    case PacketType.EnemyPosition:
                        HandleEnemyPosition(packet);
                        break;
                    case PacketType.EnemyPathUpdate:
                        HandleEnemyPath(packet);
                        break;
                    case PacketType.EnemyShooting:
                        HandleEnemyShooting(packet);
                        break;
                    case PacketType.ProjectileSpawn:
                        HandleProjectileSpawn(packet);
                        break;
                    case PacketType.HeartBeat:
                        HandleHeartbeat(packet);
                        break;
                    default:
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager] Unhandled packet type: {packet.Type}");
                        break;
                }

                OnPacketReceived?.Invoke(packet);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling packet {packet.Type}: {e.Message}");
            }
        }

        #endregion

        #region Packet Handlers

        private void HandlePlayerJoin(NetworkPacket packet)
        {
            if (!connectedPlayers.ContainsKey(packet.SenderId))
            {
                connectedPlayers[packet.SenderId] = new PlayerInfo
                {
                    SteamId = packet.SenderId,
                    Name = $"Player_{packet.SenderId}",
                    LastKnownPosition = Vector2.zero,
                    LastUpdateTime = Time.time,
                    IsConnected = true
                };

                OnPlayerJoined?.Invoke(packet.SenderId);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player joined: {packet.SenderId}");
            }
        }

        private void HandlePlayerLeave(NetworkPacket packet)
        {
            if (connectedPlayers.ContainsKey(packet.SenderId))
            {
                var playerInfo = connectedPlayers[packet.SenderId];
                playerInfo.IsConnected = false;
                connectedPlayers[packet.SenderId] = playerInfo;

                OnPlayerLeft?.Invoke(packet.SenderId);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player left: {packet.SenderId}");
            }
        }

        private void HandlePlayerPosition(NetworkPacket packet)
        {
            try
            {
                var data = PacketSerializer.DeserializeObject<PlayerPositionData>(packet.Data);
                if (connectedPlayers.ContainsKey(data.PlayerId))
                {
                    var playerInfo = connectedPlayers[data.PlayerId];
                    playerInfo.LastKnownPosition = data.Position;
                    playerInfo.LastUpdateTime = Time.time;
                    connectedPlayers[data.PlayerId] = playerInfo;

                    // Update visual representation of the player
                    PlayerSynchronizer.UpdateRemotePlayer(data);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling player position: {e.Message}");
            }
        }

        private void HandlePlayerShooting(NetworkPacket packet)
        {
            try
            {
                var data = PacketSerializer.DeserializeObject<PlayerShootingData>(packet.Data);
                PlayerSynchronizer.HandleRemotePlayerShooting(data);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling player shooting: {e.Message}");
            }
        }

        private void HandleEnemyPosition(NetworkPacket packet)
        {
            if (!isHost) // Only clients should handle enemy position updates from host
            {
                try
                {
                    var data = PacketSerializer.DeserializeObject<EnemyPositionData>(packet.Data);
                    EnemySynchronizer.UpdateEnemyPosition(data);
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling enemy position: {e.Message}");
                }
            }
        }

        private void HandleEnemyPath(NetworkPacket packet)
        {
            if (!isHost) // Only clients should handle enemy path updates from host
            {
                try
                {
                    var data = PacketSerializer.DeserializeObject<EnemyPathData>(packet.Data);
                    EnemySynchronizer.HandleEnemyPath(data);
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling enemy path: {e.Message}");
                }
            }
        }

        private void HandleEnemyShooting(NetworkPacket packet)
        {
            if (!isHost) // Only clients should handle enemy shooting updates from host
            {
                try
                {
                    var data = PacketSerializer.DeserializeObject<EnemyShootingData>(packet.Data);
                    EnemySynchronizer.HandleEnemyShooting(data);
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling enemy shooting: {e.Message}");
                }
            }
        }

        private void HandleProjectileSpawn(NetworkPacket packet)
        {
            try
            {
                var data = PacketSerializer.DeserializeObject<ProjectileSpawnData>(packet.Data);
                ProjectileSynchronizer.SpawnRemoteProjectile(data);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling projectile spawn: {e.Message}");
            }
        }

        private void HandleHeartbeat(NetworkPacket packet)
        {
            if (connectedPlayers.ContainsKey(packet.SenderId))
            {
                var playerInfo = connectedPlayers[packet.SenderId];
                playerInfo.LastUpdateTime = Time.time;
                connectedPlayers[packet.SenderId] = playerInfo;
            }
        }

        #endregion

        #region Periodic Updates

        private void SendPeriodicUpdates()
        {
            var currentTime = Time.time;

            // Send heartbeat
            if (currentTime - lastHeartbeatTime >= HEARTBEAT_INTERVAL)
            {
                SendToAll(PacketType.HeartBeat, new byte[0]);
                lastHeartbeatTime = currentTime;
            }

            // Send player updates
            if (currentTime - lastPlayerUpdateTime >= PLAYER_UPDATE_INTERVAL)
            {
                SendPlayerUpdates();
                lastPlayerUpdateTime = currentTime;
            }

            // Send enemy updates (host only)
            if (isHost && currentTime - lastEnemyUpdateTime >= ENEMY_UPDATE_INTERVAL)
            {
                SendEnemyUpdates();
                lastEnemyUpdateTime = currentTime;
            }
        }

        private void SendPlayerUpdates()
        {
            // Get player position from game
            var player = GameManager.Instance?.PrimaryPlayer;
            if (player != null)
            {
                SendPlayerPosition(
                    player.transform.position,
                    player.specRigidbody?.Velocity ?? Vector2.zero,
                    player.transform.eulerAngles.z,
                    player.IsGrounded,
                    player.IsDodgeRolling
                );
            }
        }

        private void SendEnemyUpdates()
        {
            try
            {
                // Get active enemies from the StaticReferenceManager
                var allEnemies = StaticReferenceManager.AllEnemies;
                if (allEnemies != null)
                {
                    foreach (var enemy in allEnemies)
                    {
                        if (enemy != null && enemy.healthHaver != null && !enemy.healthHaver.IsDead)
                        {
                            SendEnemyState(
                                enemy.GetInstanceID(),
                                enemy.transform.position,
                                enemy.specRigidbody?.Velocity ?? Vector2.zero,
                                enemy.transform.eulerAngles.z,
                                enemy.healthHaver.GetCurrentHealth(),
                                0, // TODO: Get correct animation state
                                enemy.isActiveAndEnabled
                            );
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error sending enemy updates: {e.Message}");
            }
        }

        private void CheckPlayerConnections()
        {
            var currentTime = Time.time;
            var timeoutDuration = HEARTBEAT_INTERVAL * 3; // 3x heartbeat interval

            var playersToRemove = new List<ulong>();
            ulong localSteamId = LocalSteamId;
            foreach (var kvp in connectedPlayers)
            {
                // Never timeout the local/host player
                if (kvp.Key.Equals(localSteamId))
                    continue;
                if (kvp.Value.IsConnected && currentTime - kvp.Value.LastUpdateTime > timeoutDuration)
                {
                    playersToRemove.Add(kvp.Key);
                }
            }

            foreach (var playerId in playersToRemove)
            {
                var playerInfo = connectedPlayers[playerId];
                playerInfo.IsConnected = false;
                connectedPlayers[playerId] = playerInfo;
                
                OnPlayerLeft?.Invoke(playerId);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player {playerId} timed out");
            }
        }

        #endregion

        /// <summary>
        /// Add incoming packet to processing queue (called by P2P managers)
        /// </summary>
        public void QueueIncomingPacket(NetworkPacket packet)
        {
            incomingPackets.Enqueue(packet);
        }

        /// <summary>
        /// Get list of connected players
        /// </summary>
        public Dictionary<ulong, PlayerInfo> GetConnectedPlayers()
        {
            return new Dictionary<ulong, PlayerInfo>(connectedPlayers);
        }

        /// <summary>
        /// Cleanup and shutdown
        /// </summary>
        public void Shutdown()
        {
            isInitialized = false;
            hostManager = null;
            clientManager = null;
            connectedPlayers.Clear();
            
            while (incomingPackets.Count > 0) { incomingPackets.Dequeue(); }
            while (outgoingPackets.Count > 0) { outgoingPackets.Dequeue(); }
            
            GungeonTogether.Logging.Debug.Log("[NetworkManager] Shutdown complete");
        }

        public ulong LocalSteamId => localSteamId;
    }
}
