using System;
using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Networking.Packet.Data;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Main game manager that coordinates all multiplayer systems for GungeonTogether.
    /// </summary>
    public class GameManager
    {
        private SteamNetworkManager networkManager;
        private ClientManager clientManager;
        private ServerManager serverManager;
        private PlayerSynchronizer playerSynchronizer;
        private bool isInitialized = false;
        
        // Game state
        public bool IsMultiplayerActive { get; private set; }
        public bool IsHost { get; private set; }
        
        // Public accessors for managers
        public ClientManager ClientManager => clientManager;
        public ServerManager ServerManager => serverManager;
        
        // Events
        public event Action OnMultiplayerStarted;
        public event Action OnMultiplayerStopped;
        public event Action<string> OnConnectionFailed;
        
        public GameManager()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            try
            {
                Debug.Log("Initializing GungeonTogether GameManager...");
                
                // Create network manager
                networkManager = new SteamNetworkManager();
                
                // Setup network event handlers
                networkManager.OnClientLoginRequest += OnClientLoginRequest;
                networkManager.OnLoginResponse += OnLoginResponse;
                networkManager.OnPacketReceived += OnPacketReceived;
                networkManager.OnClientDisconnected += OnClientDisconnected;
                  // Create client and server managers
                clientManager = new ClientManager(networkManager);
                serverManager = new ServerManager(networkManager);
                
                // Create player synchronizer
                playerSynchronizer = new PlayerSynchronizer(this);
                
                isInitialized = true;
                Debug.Log("GungeonTogether GameManager initialized successfully!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize GameManager: {e.Message}");
            }
        }
        
        public void StartHosting()
        {
            if (!isInitialized)
            {
                Debug.LogError("GameManager not initialized!");
                return;
            }
            
            try
            {
                Debug.Log("Starting multiplayer session as host...");
                
                networkManager.HostSession();
                serverManager.StartServer();
                clientManager.StartAsHost();
                
                IsHost = true;
                IsMultiplayerActive = true;
                
                OnMultiplayerStarted?.Invoke();
                Debug.Log("Multiplayer session started as host!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start hosting: {e.Message}");
                OnConnectionFailed?.Invoke($"Failed to start hosting: {e.Message}");
            }
        }
        
        public void JoinSession(Steamworks.CSteamID hostSteamId)
        {
            if (!isInitialized)
            {
                Debug.LogError("GameManager not initialized!");
                return;
            }
            
            try
            {
                Debug.Log($"Joining multiplayer session: {hostSteamId}");
                
                networkManager.JoinSession(hostSteamId);
                clientManager.StartAsClient();
                
                IsHost = false;
                IsMultiplayerActive = true;
                
                // OnMultiplayerStarted will be called when login response is received
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join session: {e.Message}");
                OnConnectionFailed?.Invoke($"Failed to join session: {e.Message}");
            }
        }
        
        public void StopMultiplayer()
        {
            if (!IsMultiplayerActive) return;
            
            try
            {
                Debug.Log("Stopping multiplayer session...");
                
                networkManager.Disconnect();
                
                if (IsHost)
                {
                    serverManager.StopServer();
                }
                
                clientManager.Stop();
                
                IsHost = false;
                IsMultiplayerActive = false;
                
                OnMultiplayerStopped?.Invoke();
                Debug.Log("Multiplayer session stopped!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error stopping multiplayer: {e.Message}");
            }
        }
          public void Update()
        {
            if (!isInitialized) return;
            
            try
            {
                // Update network manager
                networkManager?.Update();
                
                // Update managers
                if (IsMultiplayerActive)
                {
                    clientManager?.Update();
                    
                    if (IsHost)
                    {
                        serverManager?.Update();
                    }
                    
                    // Update player synchronization
                    playerSynchronizer?.Update();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in GameManager update: {e.Message}");
            }
        }
        
        private void OnClientLoginRequest(ushort clientId, LoginRequestPacket packet)
        {
            if (IsHost)
            {
                Debug.Log($"Client {clientId} ({packet.PlayerName}) requesting login...");
                
                // For now, accept all connections
                var response = new LoginResponsePacket
                {
                    Success = true,
                    Message = "Welcome to GungeonTogether!",
                    AssignedClientId = clientId
                };
                
                networkManager.SendPacketToClient(clientId, response);
                serverManager.OnClientConnected(clientId, packet.PlayerName);
            }
        }
        
        private void OnLoginResponse(LoginResponsePacket packet)
        {
            if (!IsHost)
            {
                if (packet.Success)
                {
                    Debug.Log($"Successfully connected to server! Client ID: {packet.AssignedClientId}");
                    OnMultiplayerStarted?.Invoke();
                }
                else
                {
                    Debug.LogError($"Failed to connect to server: {packet.Message}");
                    OnConnectionFailed?.Invoke(packet.Message);
                    StopMultiplayer();
                }
            }
        }
          private void OnPacketReceived(ushort clientId, Networking.Packet.IPacketData packet)
        {
            // Handle player update packets directly
            if (packet is PlayerUpdatePacket playerUpdate)
            {
                playerSynchronizer?.OnPlayerUpdateReceived(clientId, playerUpdate);
                return;
            }
            
            // Route other packets to appropriate managers
            if (IsHost)
            {
                serverManager.HandleClientPacket(clientId, packet);
            }
            else
            {
                clientManager.HandleServerPacket(packet);
            }
        }
          private void OnClientDisconnected(ushort clientId)
        {
            if (IsHost)
            {
                Debug.Log($"Client {clientId} disconnected");
                serverManager.OnClientDisconnected(clientId);
                playerSynchronizer?.OnClientDisconnected(clientId);
            }
        }
        
        public void Dispose()
        {
            StopMultiplayer();
            
            if (networkManager != null)
            {
                networkManager.OnClientLoginRequest -= OnClientLoginRequest;
                networkManager.OnLoginResponse -= OnLoginResponse;
                networkManager.OnPacketReceived -= OnPacketReceived;
                networkManager.OnClientDisconnected -= OnClientDisconnected;
            }
            
            clientManager?.Dispose();
            serverManager?.Dispose();
        }
    }
}
