using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Networking.Packets;
using GungeonTogether.Systems.Logging;
using UnityEngine;
using Debug = GungeonTogether.Systems.Logging.Debug;

namespace GungeonTogether.Networking
{
    public class ClientController : IClient
    {
        private ulong _hostId;
        private SteamP2PManager _p2p;
        public bool IsConnected { get; private set; }

        private float _nextPositionSendTime;
        private const float PositionSendInterval = 0.25f;

        public void Initialise()
        {
            _p2p = SteamP2PManager.Instance;
            Debug.Log("ClientController Initialised.");
        }

        private bool _connectPending = false;
        private float _connectRetryTimer = 0f;
        private const float ConnectRetryInterval = 1f;

        public void Connect(ulong hostId)
        {
            _hostId = hostId;
            IsConnected = false;
            _connectPending = true;
            _connectRetryTimer = 0f;
            Debug.Log($"[Client] Connect called to {hostId}, sending initial connection request...");
        }

        public void Update()
        {
            if (_connectPending && Time.realtimeSinceStartup >= _connectRetryTimer)
            {
                _connectRetryTimer = Time.realtimeSinceStartup + ConnectRetryInterval;
                Debug.Log($"Sending connection request to {_hostId}...");
                SendPacket(_hostId, new ConnectionRequestPacket
                {
                    ClientId = _p2p.LocalSteamID,
                    ProtocolVersion = NetworkManager.ProtocolVersion
                }, reliable: true);
            }
            if (!IsConnected) return;

            if (Time.realtimeSinceStartup < _nextPositionSendTime) return;
            _nextPositionSendTime = Time.realtimeSinceStartup + PositionSendInterval;

            var packet = PlayerManager.Instance.CreateLocalPositionPacket(_p2p.LocalSteamID);
            if (packet == null) return;

            SendPacket(_hostId, packet, reliable: false);
            Debug.Log($"[Client] Sent position packet: ({packet.Position.x:0.00}, {packet.Position.y:0.00})");
        }

        public void HandleConnectionAccepted(ulong senderId, ConnectionAcceptedPacket packet)
        {
            if (senderId != _hostId) return;

            if (packet.ProtocolVersion != NetworkManager.ProtocolVersion)
            {
                Debug.LogWarning($"[Client] Protocol mismatch. Host={packet.ProtocolVersion} Local={NetworkManager.ProtocolVersion}");
                Disconnect();
                return;
            }

            IsConnected = true;
            _connectPending = false;
            _nextPositionSendTime = 0;
            Debug.Log($"[Client] Connection accepted by host {senderId}.");
        }

        public void Shutdown()
        {
            Disconnect();
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                SendPacket(_hostId, new DisconnectPacket(), reliable: true);
                IsConnected = false;
                Debug.Log("Disconnected from host.");
            }
        }

        public void SendPacket(ulong targetId, INetworkPacket packet, bool reliable = true)
        {
            // Clients usually only send to host
            if (targetId == 0) targetId = _hostId;
            
            byte[] data = Serialization.PacketSerializer.Serialize(packet);
            _p2p.SendPacket(targetId, data, reliable);
        }
    }
}
