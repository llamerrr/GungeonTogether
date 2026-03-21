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
                        Host.HandleJoinRequest(senderId);
                        Host.SendPacket(senderId, new ConnectionAcceptedPacket
                        {
                            HostId = _p2p.LocalSteamID,
                            ProtocolVersion = ProtocolVersion,
                        }, reliable: true);
                    }
                    break;

                case PacketType.ConnectionAccepted:
                    if (IsClient)
                    {
                        Client.HandleConnectionAccepted(senderId, (ConnectionAcceptedPacket)packet);
                    }
                    break;
                
                case PacketType.PlayerPosition:
                    if (IsHost)
                    {
                        Host.HandlePlayerPosition(senderId, (PlayerPositionPacket)packet);
                    }
                    break;
                    
                // ... other cases
            }
        }

        public void SendPacket(ulong targetId, INetworkPacket packet, bool reliable = true)
        {
            CurrentRole?.SendPacket(targetId, packet, reliable);
        }
    }
}
