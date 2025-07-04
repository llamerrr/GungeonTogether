using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Game;
using UnityEngine.SceneManagement;

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
        private const float TIMEOUT_MULTIPLIER = 12f; // 12x heartbeat interval = 12 seconds timeout (increased for debugging)
        
        private float lastPlayerUpdateTime;
        private float lastEnemyUpdateTime;
        
        // Logging spam reduction  
        private float lastNetworkLogTime = 0f;
        private float lastHeartbeatLogTime = 0f;
        private const float NETWORK_LOG_THROTTLE = 10f; // Only log routine network activity every 10 seconds

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
            // Debug heartbeat every 5 seconds to confirm Update is being called
            if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
            {
                GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Update heartbeat - IsHost: {isHost}, IncomingPackets: {incomingPackets.Count}, OutgoingPackets: {outgoingPackets.Count}");
            }
            
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
            packet.TargetSteamId = targetSteamId; // Set target for directed packets
            outgoingPackets.Enqueue(packet);
        }

        /// <summary>
        /// Send player position update
        /// </summary>
        public void SendPlayerPositionWithMap(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling, string mapName)
        {
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] SendPlayerPositionWithMap: pos={position} rot={rotation} map={mapName}");
            // TEMP: Per-frame debug log for testing packet send frequency
            //GungeonTogether.Logging.Debug.Log($"[NetworkManager][PER-FRAME] Sending player position: pos={position} rot={rotation} map={mapName}");
            // Only log player position sends every 10 seconds to reduce spam
            if (Time.time - lastNetworkLogTime > NETWORK_LOG_THROTTLE)
            {
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Sending player position: pos={position} rot={rotation} map={mapName}");
                lastNetworkLogTime = Time.time;
            }
            
            var data = new PlayerPositionData
            {
                PlayerId = localSteamId,
                Position = position,
                Velocity = velocity,
                Rotation = rotation,
                IsGrounded = isGrounded,
                IsDodgeRolling = isDodgeRolling,
                MapName = mapName
            };
            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.PlayerPosition, serializedData);
        }

        /// <summary>
        /// Send player position update (backward compatibility)
        /// </summary>
        public void SendPlayerPosition(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling)
        {
            SendPlayerPositionWithMap(position, velocity, rotation, isGrounded, isDodgeRolling, SceneManager.GetActiveScene().name);
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

        /// <summary>
        /// Send map synchronization packet
        /// </summary>
        public void SendMapSync(ulong targetSteamId, string mapName)
        {
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] Sending map sync to {targetSteamId} with map '{mapName}'");
            var data = System.Text.Encoding.UTF8.GetBytes(mapName);
            SendToPlayer(targetSteamId, PacketType.MapSync, data);
        }

        #endregion

        #region Packet Processing

        private void ProcessIncomingPackets()
        {
            int packetCount = incomingPackets.Count;
            if (packetCount > 0)
            {
                GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Processing {packetCount} incoming packets");
            }
            while (incomingPackets.Count > 0)
            {
                var packet = incomingPackets.Dequeue();
                GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Processing packet type {packet.Type} from {packet.SenderId}");
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
                    // Check if this is a targeted packet
                    if (packet.TargetSteamId != 0UL)
                    {
                        // Send to specific client
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager] Sending targeted packet {packet.Type} to {packet.TargetSteamId}");
                        hostManager.SendToClient(packet.TargetSteamId, serializedPacket);
                    }
                    else
                    {
                        // Send to all clients
                        hostManager.SendToAllClients(serializedPacket);
                    }
                }
                else if (!isHost && clientManager != null)
                {
                    // Clients can only send to host
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
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Received PlayerPosition packet from {packet.SenderId}");
                        HandlePlayerPosition(packet);
                        break;
                    case PacketType.PlayerShooting:
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Received PlayerShooting packet from {packet.SenderId}");
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
                    case PacketType.MapSync:
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Received MapSync packet from {packet.SenderId}");
                        var mapName = System.Text.Encoding.UTF8.GetString(packet.Data);
                        PlayerSynchroniser.Instance.OnMapSyncReceived(mapName, packet.SenderId);
                        break;
                    default:
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager] Unhandled packet type: {packet.Type}");
                        break;
                }

                OnPacketReceived?.Invoke(packet);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling packet: {e.Message}");
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
                // Send map sync to the new player
                if (IsHost())
                {
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Host sending map sync to new player {packet.SenderId}");
                    SendMapSync(packet.SenderId, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
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
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Received player position from {data.PlayerId}: {data.Position} (map={data.MapName})");
                
                if (connectedPlayers.ContainsKey(data.PlayerId))
                {
                    var playerInfo = connectedPlayers[data.PlayerId];
                    playerInfo.LastKnownPosition = data.Position;
                    playerInfo.LastUpdateTime = Time.time;
                    connectedPlayers[data.PlayerId] = playerInfo;
                }

                // Forward to PlayerSynchroniser for handling
                PlayerSynchroniser.Instance.OnPlayerPositionReceived(data);
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
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Received player shooting from {data.PlayerId}");
                PlayerSynchroniser.Instance.OnPlayerShootingReceived(data);
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
                var oldUpdateTime = playerInfo.LastUpdateTime;
                playerInfo.LastUpdateTime = Time.time;
                connectedPlayers[packet.SenderId] = playerInfo;
                
                // More frequent logging for debugging
                if (Time.time - lastHeartbeatLogTime > 3.0f) // Log every 3 seconds instead of 10
                {
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Heartbeat received from {packet.SenderId}, last update: {playerInfo.LastUpdateTime:F1}s (was {oldUpdateTime:F1}s)");
                    lastHeartbeatLogTime = Time.time;
                }
            }
            else
            {
                GungeonTogether.Logging.Debug.LogWarning($"[NetworkManager] Received heartbeat from unknown player: {packet.SenderId}");
            }
        }

        #endregion

        #region Public Handlers for P2P Managers

        /// <summary>
        /// Handle a player joining directly from P2P connection (called by P2P managers)
        /// </summary>
        public void HandlePlayerJoin(ulong steamId)
        {
            if (!connectedPlayers.ContainsKey(steamId))
            {
                connectedPlayers[steamId] = new PlayerInfo
                {
                    SteamId = steamId,
                    Name = $"Player_{steamId}",
                    LastKnownPosition = Vector2.zero,
                    LastUpdateTime = Time.time,
                    IsConnected = true
                };

                OnPlayerJoined?.Invoke(steamId);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player joined directly: {steamId}");
                
                // Send map sync to the new player
                if (IsHost())
                {
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Host sending map sync to new player {steamId}");
                    SendMapSync(steamId, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
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
            var timeoutDuration = HEARTBEAT_INTERVAL * TIMEOUT_MULTIPLIER; // 12 seconds timeout

            var playersToRemove = new List<ulong>();
            ulong localSteamId = LocalSteamId;
            foreach (var kvp in connectedPlayers)
            {
                // Never timeout the local/host player
                if (kvp.Key.Equals(localSteamId))
                    continue;
                    
                var timeSinceLastUpdate = currentTime - kvp.Value.LastUpdateTime;
                
                if (kvp.Value.IsConnected && timeSinceLastUpdate > timeoutDuration)
                {
                    playersToRemove.Add(kvp.Key);
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Player {kvp.Key} timed out: {timeSinceLastUpdate:F1}s since last update");
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
            GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] QueueIncomingPacket: type={packet.Type}, sender={packet.SenderId}, dataSize={packet.Data?.Length ?? 0}");
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

        public bool IsHost()
        {
            return isHost;
        }

        /// <summary>
        /// Notify NetworkManager that a player joined (called from Steam callbacks)
        /// </summary>
        public void NotifyPlayerJoined(ulong steamId)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player join notification: {steamId}");
                
                // Don't notify about ourselves
                if (steamId.Equals(localSteamId))
                {
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Ignoring self join notification: {steamId}");
                    return;
                }
                
                // Add to connected players if not already present
                if (!connectedPlayers.ContainsKey(steamId))
                {
                    connectedPlayers[steamId] = new PlayerInfo
                    {
                        SteamId = steamId,
                        Name = $"Player_{steamId}",
                        LastKnownPosition = Vector2.zero,
                        LastUpdateTime = Time.time,
                        IsConnected = true
                    };
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Added player to connected list: {steamId}");
                }
                
                // Fire the event for PlayerSynchroniser
                OnPlayerJoined?.Invoke(steamId);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Fired OnPlayerJoined event for: {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error in NotifyPlayerJoined: {e.Message}");
            }
        }

        /// <summary>
        /// Notify NetworkManager that a player left (called from Steam callbacks)
        /// </summary>
        public void NotifyPlayerLeft(ulong steamId)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player leave notification: {steamId}");
                
                // Don't notify about ourselves
                if (steamId.Equals(localSteamId))
                {
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Ignoring self leave notification: {steamId}");
                    return;
                }
                
                // Remove from connected players
                if (connectedPlayers.ContainsKey(steamId))
                {
                    connectedPlayers.Remove(steamId);
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Removed player from connected list: {steamId}");
                }
                
                // Fire the event for PlayerSynchroniser
                OnPlayerLeft?.Invoke(steamId);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Fired OnPlayerLeft event for: {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error in NotifyPlayerLeft: {e.Message}");
            }
        }
    }
}
