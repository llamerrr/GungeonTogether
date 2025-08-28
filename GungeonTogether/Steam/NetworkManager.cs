using GungeonTogether.Game;
using System;
using System.Collections.Generic;
using UnityEngine;
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
        private const float TIMEOUT_MULTIPLIER = 30f; // 30x heartbeat interval = 30 seconds timeout (increased for stability)

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
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] === INITIALIZING AS HOST ===");
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] Host lobby ID: {lobbyId}, Local Steam ID: {localSteamId}");

            if (isInitialized)
            {
                GungeonTogether.Logging.Debug.Log("[NetworkManager] Already initialized as host");
                return true;
            }

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
                GungeonTogether.Logging.Debug.Log("[NetworkManager] Successfully initialized as HOST");
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Connected players: {connectedPlayers.Count} (including host)");
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
        public bool InitializeAsClient(ulong hostSteamId, SteamP2PClientManager existingClientManager = null)
        {
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] === INITIALIZING AS CLIENT ===");
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] Host Steam ID: {hostSteamId}, Local Steam ID: {localSteamId}");
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] Using existing manager: {existingClientManager != null}");

            if (isInitialized)
            {
                GungeonTogether.Logging.Debug.Log("[NetworkManager] Already initialized as client");
                return true;
            }

            try
            {
                isHost = false;

                // Use existing manager if provided, otherwise create a new one
                clientManager = existingClientManager ?? new SteamP2PClientManager(hostSteamId, localSteamId);

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
                GungeonTogether.Logging.Debug.Log("[NetworkManager] Successfully initialized as CLIENT");
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Connected to host: {hostSteamId}");
                return true;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Failed to initialize as client: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize as client using an existing SteamP2PClientManager (backward compatibility wrapper)
        /// </summary>
        public bool InitializeAsClientWithExistingManager(SteamP2PClientManager existingClientManager, ulong hostSteamId)
        {
            return InitializeAsClient(hostSteamId, existingClientManager);
        }

        /// <summary>
        /// Update method to be called from main thread
        /// </summary>
        public void Update()
        {

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

        public void SendPlayerPositionUpdate(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling, string mapName, PlayerAnimationState animationState = PlayerAnimationState.Idle, Vector2 movementDirection = default, bool isRunning = false, bool isFalling = false, bool isTakingDamage = false, bool isDead = false, string currentAnimationName = "")
        {
            // Get current character info instead of using placeholders
            var characterInfo = PlayerSynchroniser.Instance.GetCurrentPlayerCharacter();

            var data = new PlayerPositionData
            {
                PlayerId = LocalSteamId,
                Position = position,
                Velocity = velocity,
                Rotation = rotation,
                IsGrounded = isGrounded,
                IsDodgeRolling = isDodgeRolling,
                MapName = mapName,
                CharacterId = characterInfo.CharacterId,
                CharacterName = characterInfo.CharacterName,

                // Animation state data
                AnimationState = animationState,
                MovementDirection = movementDirection,
                IsRunning = isRunning,
                IsFalling = isFalling,
                IsTakingDamage = isTakingDamage,
                IsDead = isDead,
                CurrentAnimationName = currentAnimationName
            };
            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.PlayerPosition, serializedData);
        }

        /// <summary>
        /// Send player position update (backward compatibility wrapper)
        /// </summary>
        public void SendPlayerPosition(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling)
        {
            SendPlayerPositionUpdate(position, velocity, rotation, isGrounded, isDodgeRolling, SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Send player position update (legacy method name - use SendPlayerPositionUpdate instead)
        /// </summary>
        public void OpeningPlayerPacket(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling, string mapName, int characterId = 0, string characterName = "PlayerRogue", PlayerAnimationState animationState = PlayerAnimationState.Idle, Vector2 movementDirection = default, bool isRunning = false, bool isFalling = false, bool isTakingDamage = false, bool isDead = false, string currentAnimationName = "")
        {
            SendPlayerPositionUpdate(position, velocity, rotation, isGrounded, isDodgeRolling, mapName, animationState, movementDirection, isRunning, isFalling, isTakingDamage, isDead, currentAnimationName);
        }

        /// <summary>
        /// Send player position update (legacy method name - use SendPlayerPositionUpdate instead)
        /// </summary>
        public void RegularPlayerPacket(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling, string mapName, PlayerAnimationState animationState = PlayerAnimationState.Idle, Vector2 movementDirection = default, bool isRunning = false, bool isFalling = false, bool isTakingDamage = false, bool isDead = false, string currentAnimationName = "")
        {
            SendPlayerPositionUpdate(position, velocity, rotation, isGrounded, isDodgeRolling, mapName, animationState, movementDirection, isRunning, isFalling, isTakingDamage, isDead, currentAnimationName);
        }

        // Send shoot request to server (Client -> Server)
        public void SendShootRequest(Vector2 position, Vector2 direction, int weaponId, bool isCharging = false, float chargeAmount = 0f)
        {
            var data = new PlayerShootRequestData
            {
                PlayerId = localSteamId,
                Position = position,
                Direction = direction,
                WeaponId = weaponId,
                IsCharging = isCharging,
                ChargeAmount = chargeAmount,
                RequestTimestamp = Time.time
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            if (isHost)
            {
                // If we're the host, process the shoot request immediately
                HandleShootRequest(data);
            }
            else
            {
                // Send to host for processing
                SendToAll(PacketType.PlayerShootRequest, serializedData);
            }
        }

        // Send enemy state update (host only)
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

        // Send enemy shooting event (host only)
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

        // Send lightweight enemy spawn (host only)
        public void SendEnemySpawn(int enemyId, int enemyType, Vector2 position, float rotation, float maxHealth)
        {
            if (!isHost) return;
            var data = new EnemySpawnData
            {
                EnemyId = enemyId,
                EnemyType = enemyType,
                Position = position,
                Rotation = rotation,
                MaxHealth = maxHealth
            };
            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.EnemySpawn, serializedData);
        }

        // Send projectile spawn even
        public void SendProjectileSpawn(int projectileId, Vector2 position, Vector2 velocity, float rotation, int ownerId, bool isPlayerProjectile, bool isServerAuthoritative = false)
        {
            var data = new ProjectileSpawnData
            {
                ProjectileId = projectileId,
                Position = position,
                Velocity = velocity,
                Rotation = rotation,
                OwnerId = (ulong)ownerId,
                IsPlayerProjectile = isPlayerProjectile,
                Damage = 0f,
                ProjectileType = 0,
                WeaponId = 0,
                IsServerAuthoritative = isServerAuthoritative || isHost // Server spawned or host spawned
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.ProjectileSpawn, serializedData);
        }

        /// <summary>
        /// Send room cleared event (host only)
        /// </summary>
        public void SendRoomCleared(Vector2 roomPosition)
        {
            if (!isHost) return;

            var data = new RoomClearedData
            {
                RoomPosition = roomPosition,
                ClearTime = Time.time
            };

            var serializedData = PacketSerializer.SerializeObject(data);
            SendToAll(PacketType.RoomCleared, serializedData);
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] Sent room cleared for {roomPosition}");
        }

        /// <summary>
        /// Send initial state sync to a new player (host only)
        /// </summary>
        public void SendInitialStateSync(ulong targetSteamId)
        {
            if (!isHost) return;

            try
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var hostPlayer = GameManager.Instance?.PrimaryPlayer;
                var hostPosition = hostPlayer != null ? (Vector2)hostPlayer.transform.position : Vector2.zero;

                // Collect all connected players' current positions
                var connectedPlayersList = new List<PlayerPositionData>();
                foreach (var kvp in connectedPlayers)
                {
                    if (kvp.Key != targetSteamId) // Don't include the new player
                    {
                        connectedPlayersList.Add(new PlayerPositionData
                        {
                            PlayerId = kvp.Key,
                            Position = kvp.Value.LastKnownPosition,
                            Velocity = Vector2.zero,
                            Rotation = 0f,
                            IsGrounded = true,
                            IsDodgeRolling = false,
                            MapName = currentScene
                        });
                    }
                }

                var initialState = new InitialStateSyncData
                {
                    MapName = currentScene,
                    HostPosition = hostPosition,
                    ConnectedPlayers = connectedPlayersList.ToArray(),
                    GameState = new GameStateSync
                    {
                        GameTime = Time.time,
                        CurrentFloor = GameManager.Instance?.CurrentFloor ?? 1,
                        CurrentRoomPosition = Vector2.zero,
                        IsPaused = false
                    }
                };

                var serializedData = PacketSerializer.SerializeObject(initialState);
                SendToPlayer(targetSteamId, PacketType.InitialStateSync, serializedData);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error sending initial state sync: {e.Message}");
            }
        }

        /// <summary>
        /// Send join confirmation to a new player (host only)
        /// </summary>
        public void SendPlayerJoinConfirmation(ulong targetSteamId)
        {
            if (!isHost) return;

            try
            {
                SendToPlayer(targetSteamId, PacketType.PlayerJoinConfirm, new byte[0]);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Sent join confirmation to {targetSteamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error sending join confirmation: {e.Message}");
            }
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

        // Item spawn (host authoritative for synced categories)
    public void SendItemSpawn(int itemId, Vector2 position, int itemType, int spriteId = 0, int quality = 0, int ammo = 0, int maxAmmo = 0, int charges = 0)
        {
            try
            {
                var data = new ItemData
                {
                    ItemId = itemId,
                    Position = position,
                    ItemType = itemType,
                    IsPickedUp = false,
            PickedUpBy = 0,
            SpriteId = spriteId,
                    Quality = quality,
                    Ammo = ammo,
                    MaxAmmo = maxAmmo,
                    Charges = charges
                };
                var bytes = PacketSerializer.SerializeObject(data);
                SendToAll(PacketType.ItemSpawn, bytes);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error sending item spawn: {e.Message}");
            }
        }

        // Item pickup broadcast so other clients remove it
    public void SendItemPickup(int itemId, int itemType, ulong pickerId, int spriteId = 0, int quality = 0, int ammo = 0, int maxAmmo = 0, int charges = 0)
        {
            try
            {
                var data = new ItemData
                {
                    ItemId = itemId,
                    Position = Vector2.zero,
                    ItemType = itemType,
                    IsPickedUp = true,
            PickedUpBy = pickerId,
            SpriteId = spriteId,
                    Quality = quality,
                    Ammo = ammo,
                    MaxAmmo = maxAmmo,
                    Charges = charges
                };
                var bytes = PacketSerializer.SerializeObject(data);
                SendToAll(PacketType.ItemPickup, bytes);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error sending item pickup: {e.Message}");
            }
        }

        #endregion

        #region Packet Processing

        private void ProcessIncomingPackets()
        {
            // Only log if there are many packets to process (potential performance issue)
            int packetCount = incomingPackets.Count;

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
                    case PacketType.EnemySpawn:
                        HandleEnemySpawn(packet);
                        break;
                    case PacketType.PlayerJoin:
                        HandlePlayerJoin(packet);
                        break;
                    case PacketType.PlayerLeave:
                        HandlePlayerLeave(packet);
                        break;
                    case PacketType.PlayerPosition:
                        HandlePlayerPosition(packet);
                        break;
                    case PacketType.PlayerShootRequest:
                        HandlePlayerShootRequest(packet);
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
                    case PacketType.RoomCleared:
                        HandleRoomCleared(packet);
                        break;
                    case PacketType.HeartBeat:
                        HandleHeartbeat(packet);
                        break;
                    case PacketType.InitialStateSync:
                        HandleInitialStateSync(packet);
                        break;
                    case PacketType.PlayerJoinConfirm:
                        HandlePlayerJoinConfirmation(packet);
                        break;
                    case PacketType.MapSync:
                        var mapName = System.Text.Encoding.UTF8.GetString(packet.Data);
                        PlayerSynchroniser.Instance.OnMapSyncReceived(mapName, packet.SenderId);
                        break;
                    case PacketType.ItemSpawn:
                        HandleItemSpawn(packet);
                        break;
                    case PacketType.ItemPickup:
                        HandleItemPickup(packet);
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
            GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] HandlePlayerJoin called - packet senderId: {packet.SenderId}, local SteamId: {localSteamId}, isHost: {isHost}");

            if (!connectedPlayers.ContainsKey(packet.SenderId))
            {
                // Parse the join data to get player info
                try
                {
                    var joinData = PacketSerializer.DeserializeObject<PlayerJoinData>(packet.Data);

                    connectedPlayers[packet.SenderId] = new PlayerInfo
                    {
                        SteamId = packet.SenderId,
                        Name = !string.IsNullOrEmpty(joinData.PlayerName) ? joinData.PlayerName : $"Player_{packet.SenderId}",
                        LastKnownPosition = joinData.Position,
                        LastUpdateTime = Time.time,
                        IsConnected = true
                    };

                    GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Added player {packet.SenderId} to connectedPlayers. Total players: {connectedPlayers.Count}");
                    OnPlayerJoined?.Invoke(packet.SenderId);
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player joined: {packet.SenderId} (Name: {joinData.PlayerName})");

                    // Host sends initial state sync to the new player
                    if (IsHost())
                    {
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager] Host sending initial state sync to new player {packet.SenderId}");
                        SendInitialStateSync(packet.SenderId);

                        // Also send join confirmation
                        SendPlayerJoinConfirmation(packet.SenderId);
                    }
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error parsing player join data: {e.Message}");

                    // Fallback to basic player info
                    connectedPlayers[packet.SenderId] = new PlayerInfo
                    {
                        SteamId = packet.SenderId,
                        Name = $"Player_{packet.SenderId}",
                        LastKnownPosition = Vector2.zero,
                        LastUpdateTime = Time.time,
                        IsConnected = true
                    };

                    OnPlayerJoined?.Invoke(packet.SenderId);

                    if (IsHost())
                    {
                        SendInitialStateSync(packet.SenderId);
                        SendPlayerJoinConfirmation(packet.SenderId);
                    }
                }
            }
            else
            {
                GungeonTogether.Logging.Debug.Log($"[NetworkManager][DEBUG] Player {packet.SenderId} already exists in connectedPlayers");
                // Update their last seen time
                var playerInfo = connectedPlayers[packet.SenderId];
                playerInfo.LastUpdateTime = Time.time;
                playerInfo.IsConnected = true;
                connectedPlayers[packet.SenderId] = playerInfo;
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
                }

                // Forward to PlayerSynchroniser for handling
                PlayerSynchroniser.Instance.OnPlayerPositionReceived(data);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling player position: {e.Message}");
            }
        }
        
        private void HandlePlayerShootRequest(NetworkPacket packet)
        {
            try
            {
                var data = PacketSerializer.DeserializeObject<PlayerShootRequestData>(packet.Data);

                // Only the host should process shoot requests
                if (isHost)
                {
                    HandleShootRequest(data);
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[NetworkManager] Non-host received shoot request from {data.PlayerId}");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling shoot request: {e.Message}");
            }
        }

        private void HandleShootRequest(PlayerShootRequestData data)
        {
            try
            {
                // Validate the shoot request (anti-cheat, rate limiting, etc.)
                if (!IsValidShootRequest(data))
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[NetworkManager] Invalid shoot request from {data.PlayerId}");
                    return;
                }

                // Server authoritative projectile spawning
                ProjectileSynchronizer.Instance.HandleServerShootRequest(data);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error processing shoot request: {e.Message}");
            }
        }

        private bool IsValidShootRequest(PlayerShootRequestData data)
        {
            // TODO: Implement proper validation
            // - Check if player exists and is alive
            // - Check weapon constraints (ammo, fire rate, etc.)
            // - Validate position is reasonable
            // - Check timing/rate limiting
            
            // For now, basic validation
            if (data.PlayerId == 0) return false;
            if (float.IsNaN(data.Position.x) || float.IsNaN(data.Position.y)) return false;
            if (float.IsNaN(data.Direction.x) || float.IsNaN(data.Direction.y)) return false;
            
            return true;
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

        private void HandleEnemySpawn(NetworkPacket packet)
        {
            if (!isHost) // Clients create remote enemy shell
            {
                try
                {
                    var data = PacketSerializer.DeserializeObject<EnemySpawnData>(packet.Data);
                    EnemySynchronizer.HandleEnemySpawn(data);
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling enemy spawn minimal: {e.Message}");
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

        private void HandleRoomCleared(NetworkPacket packet)
        {
            if (!isHost) // Only clients should handle room cleared from host
            {
                try
                {
                    var data = PacketSerializer.DeserializeObject<RoomClearedData>(packet.Data);
                    ClientRoomStateManager.Instance.OnHostRoomCleared(data.RoomPosition);
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Received room cleared for {data.RoomPosition}");
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling room cleared: {e.Message}");
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
                    lastHeartbeatLogTime = Time.time;
                }
            }
            else
            {
                GungeonTogether.Logging.Debug.LogWarning($"[NetworkManager] Received heartbeat from unknown player: {packet.SenderId}");
            }
        }

        private void HandleInitialStateSync(NetworkPacket packet)
        {
            try
            {
                var data = PacketSerializer.DeserializeObject<InitialStateSyncData>(packet.Data);
                // Process connected players info
                if (data.ConnectedPlayers != null)
                {
                    foreach (var playerData in data.ConnectedPlayers)
                    {
                        if (playerData.PlayerId != localSteamId) // Don't add ourselves
                        {
                            connectedPlayers[playerData.PlayerId] = new PlayerInfo
                            {
                                SteamId = playerData.PlayerId,
                                Name = $"Player_{playerData.PlayerId}",
                                LastKnownPosition = playerData.Position,
                                LastUpdateTime = Time.time,
                                IsConnected = true
                            };
                            GungeonTogether.Logging.Debug.Log($"[NetworkManager] Added player from initial state: {playerData.PlayerId} at {playerData.Position}");
                        }
                    }
                }

                // Notify PlayerSynchroniser to spawn remote players
                PlayerSynchroniser.Instance?.ProcessInitialStateSync(data);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling initial state sync: {e.Message}");
            }
        }

        private void HandlePlayerJoinConfirmation(NetworkPacket packet)
        {
            GungeonTogether.Logging.Debug.Log($"[NetworkManager] Received join confirmation from host. Starting position updates...");

            // Now that we're confirmed as joined, start sending our position updates
            if (!isHost)
            {
                PlayerSynchroniser.Instance?.StartSendingUpdates();
            }
        }

        private void HandleItemSpawn(NetworkPacket packet)
        {
            try
            {
                var data = PacketSerializer.DeserializeObject<ItemData>(packet.Data);
                ItemSynchronizer.Instance.OnItemSpawnReceived(data);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling item spawn: {e.Message}");
            }
        }

        private void HandleItemPickup(NetworkPacket packet)
        {
            try
            {
                var data = PacketSerializer.DeserializeObject<ItemData>(packet.Data);
                ItemSynchronizer.Instance.OnItemPickupReceived(data);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error handling item pickup: {e.Message}");
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


            // Send enemy updates (host only)
            if (isHost && currentTime - lastEnemyUpdateTime >= ENEMY_UPDATE_INTERVAL)
            {
                SendEnemyUpdates();
                lastEnemyUpdateTime = currentTime;
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
            var timeoutDuration = HEARTBEAT_INTERVAL * TIMEOUT_MULTIPLIER; // 30 seconds timeout

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
                    GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Player {kvp.Key} timed out: {timeSinceLastUpdate:F1}s since last update (threshold: {timeoutDuration:F1}s)");
                }
                else if (currentTime - lastNetworkLogTime < 1f) // Log during the throttle period
                {
                    GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player {kvp.Key}: {timeSinceLastUpdate:F1}s since last update (connected: {kvp.Value.IsConnected})");
                }
            }

            foreach (var playerId in playersToRemove)
            {
                var playerInfo = connectedPlayers[playerId];
                playerInfo.IsConnected = false;
                connectedPlayers[playerId] = playerInfo;

                // Remove from connected players completely when they timeout
                connectedPlayers.Remove(playerId);

                OnPlayerLeft?.Invoke(playerId);
                GungeonTogether.Logging.Debug.Log($"[NetworkManager] Player {playerId} timed out and removed from connected players");

                // If we're the host, close the P2P session with the timed out player
                if (isHost && hostManager != null)
                {
                    try
                    {
                        SteamNetworkingSocketsHelper.CloseP2PSession(playerId);
                        GungeonTogether.Logging.Debug.Log($"[NetworkManager] Closed P2P session with timed out player {playerId}");
                    }
                    catch (Exception e)
                    {
                        GungeonTogether.Logging.Debug.LogError($"[NetworkManager] Error closing P2P session with {playerId}: {e.Message}");
                    }
                }
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
        /// Cleanup and shutdown (enhanced with persistence)
        /// </summary>
        public void Shutdown()
        {
            // Shutdown persistence manager first
            if (PlayerPersistenceManager.Instance != null)
            {
                PlayerPersistenceManager.Instance.Shutdown();
            }

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
