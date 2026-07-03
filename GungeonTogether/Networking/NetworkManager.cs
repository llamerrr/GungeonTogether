using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Core;
using GungeonTogether.Systems.Logging;
using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Serialization;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Networking.Packets;
using Debug = GungeonTogether.Systems.Logging.Debug;
using static ETGMod;
using GungeonTogether.Networking.Sync;

namespace GungeonTogether.Networking
{
    public class NetworkManager
    {
        private static NetworkManager _instance;
        public static NetworkManager Instance => _instance ?? (_instance = new NetworkManager());

        public bool IsHost { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsConnected => (CurrentRole != null);

        public INetworkRole CurrentRole { get; private set; }
        public HostController Host { get; private set; }
        public ClientController Client { get; private set; }

        private SteamP2PManager _p2p;
        private SteamLobbyManager _lobby;

        public const int ProtocolVersion = 1;

        public void Initialise()
        {
            try
            {
                Debug.Log("NetworkManager: Initializing P2P Manager...");
                _p2p = SteamP2PManager.Instance;
                _p2p.Initialise();
                _p2p.OnPacketReceived += HandlePacket;
                Debug.Log("NetworkManager: P2P Manager initialized.");

                Debug.Log("NetworkManager: Initializing Lobby Manager...");
                _lobby = SteamLobbyManager.Instance;
                _lobby.Initialise();
                Debug.Log("NetworkManager: Lobby Manager initialized.");
                
                Debug.Log("NetworkManager Initialised.");
                PlayerManager.Instance.gameObject.SetActive(true);
                ETGReflectionHelper.Initialise();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"NetworkManager: Exception during initialization: {ex.GetType().Name}: {ex.Message}");
                Debug.LogError($"NetworkManager: Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public void Update()
        {
            _lobby?.Update();
            _p2p?.Update();
            CurrentRole?.Update();
        }

        public void StartHosting()
        {
            if (CurrentRole != null) Shutdown();

            IsHost = true;
            IsClient = false;
            
            Host = new HostController();
            Host.Initialise();
            Host.StartSession();
            
            CurrentRole = Host;
            Debug.Log("Started Hosting.");
        }

        public void ConnectTo(ulong hostId)
        {
            if (CurrentRole != null) Shutdown();

            IsHost = false;
            IsClient = true;

            Client = new ClientController();
            Client.Initialise();
            Client.Connect(hostId);

            CurrentRole = Client;
            Debug.Log($"Connecting to host {hostId}...");
        }

        public void Shutdown()
        {
            CurrentRole?.Shutdown();
            CurrentRole = null;
            Host = null;
            Client = null;
            IsHost = false;
            IsClient = false;
        }

        private void HandlePacket(ulong senderId, byte[] data)
        {
            try
            {
                INetworkPacket packet = PacketSerializer.Deserialize(data);
                if (packet == null) return;

                ProcessPacket(senderId, packet);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling packet from {senderId}: {e.Message}");
            }
        }

        private void ProcessPacket(ulong senderId, INetworkPacket packet)
        {
            // Route packet to appropriate controller or handle globally
            switch (packet.Type)
            {
                case PacketType.ConnectionRequest:
                    if (IsHost)
                    {
                        var req = (ConnectionRequestPacket)packet;
                        Host.HandleJoinRequest(senderId, req.ProtocolVersion);
                    }
                    break;

                case PacketType.ConnectionAccepted:
                    if (IsClient)
                    {
                        Client.HandleConnectionAccepted(senderId, (ConnectionAcceptedPacket)packet);
                    }
                    break;
                
                case PacketType.PlayerPosition:
                    var posPacket = (PlayerPositionPacket)packet;
                    if (IsClient && posPacket.PlayerId != SteamReflectionHelper.GetLocalSteamId())
                    {
                        PlayerManager.Instance.UpdateRemotePlayer(posPacket.PlayerId, posPacket.Position, posPacket.Rotation);
                    }
                    else if (IsHost)
                    {
                        Host.HandlePlayerPosition(senderId, posPacket);
                    }
                    break;
                case PacketType.Disconnect:
                    if (IsHost)
                        Host.HandleClientDisconnect(senderId);
                    break;
                case PacketType.PlayerJoin:
                    var joinPacket = (PlayerJoinPacket)packet;
                    if (IsClient && joinPacket.PlayerId != SteamReflectionHelper.GetLocalSteamId())
                    {
                        PlayerManager.Instance.SpawnRemotePlayer(joinPacket.PlayerId, joinPacket.Position, joinPacket.Rotation);
                    }
                    break;

                case PacketType.PlayerLeave:
                    var leavePacket = (PlayerLeavePacket)packet;
                    if (IsClient)
                    {
                        PlayerManager.Instance.RemoveRemotePlayer(leavePacket.PlayerId);
                    }
                    else if (IsHost)
                    {
                        // Host should also remove from its list of connected clients
                        Host.HandleClientDisconnect(leavePacket.PlayerId);
                    }
                    break;
                case PacketType.RoomChange:
                    var roomChange = (RoomChangePacket)packet;
                    // Client: clear old remote entities and prepare for new room
                    NetworkEntityManager.Instance.Clear();
                    // Possibly also call LoadRoom on client (but we'll rely on enemy spawns)
                    break;

                case PacketType.EnemySpawn:
                    var spawn = (EnemySpawnPacket)packet;
                    // Client: spawn enemy prefab
                    GameObject enemyPrefab = Resources.Load<GameObject>(spawn.PrefabName); // or use a prefab registry
                    if (enemyPrefab != null)
                    {
                        GameObject enemyGO = UnityEngine.Object.Instantiate(enemyPrefab, spawn.Position, Quaternion.Euler(0, 0, spawn.Rotation));
                        // Set health via reflection or component
                        // Store in NetworkEntityManager for future updates
                        NetworkEntityManager.Instance.AddRemote(spawn.EnemyId, enemyGO);
                    }
                    break;

                case PacketType.EnemyState:
                    var state = (EnemyStatePacket)packet;
                    var remoteGO = NetworkEntityManager.Instance.GetRemote(state.EnemyId);
                    if (remoteGO != null)
                    {
                        remoteGO.transform.position = state.Position;
                        remoteGO.transform.rotation = Quaternion.Euler(0, 0, state.Rotation);
                        // Update health and AI state via reflection on the remote enemy component
                        var enemyComp = remoteGO.GetComponent<AIActor>();
                        if (enemyComp != null)
                        {
                            // Set health property
                        }
                    }
                    break;

                case PacketType.EnemyDeath:
                    var death = (EnemyDeathPacket)packet;
                    NetworkEntityManager.Instance.RemoveRemote(death.EnemyId);
                    break;

                case PacketType.WorldState:
                    var world = (WorldStatePacket)packet;
                    if (IsClient)
                    {
                        WorldSyncManager.Instance.ApplyWorldState(world);
                    }
                    break;
                case PacketType.PlayerState:
                    var playerState = (PlayerStatePacket)packet;
                    if (IsClient) PlayerSyncManager.Instance.ApplyPlayerState(playerState);
                    break;
                case PacketType.LoadingState:
                    var load = (LoadingStatePacket)packet;
                    if (IsClient) LoadingSyncManager.Instance.ApplyLoadingState(load.IsLoading);
                    break;
            }
        }

        public void SendPacket(ulong targetId, INetworkPacket packet, bool reliable = true)
        {
            CurrentRole?.SendPacket(targetId, packet, reliable);
        }
    }
}
