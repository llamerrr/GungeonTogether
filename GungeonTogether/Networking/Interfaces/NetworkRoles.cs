using GungeonTogether.Networking;

namespace GungeonTogether.Networking.Interfaces
{
    public interface INetworkRole
    {
        void Initialize();
        void Update();
        void Shutdown();
        void SendPacket(ulong targetId, INetworkPacket packet, bool reliable = true);
    }

    public interface IHost : INetworkRole
    {
        void StartSession();
        void HandleJoinRequest(ulong playerId);
        void Broadcast(INetworkPacket packet, ulong excludeId = 0, bool reliable = true);
    }

    public interface IClient : INetworkRole
    {
        void Connect(ulong hostId);
        void Disconnect();
        bool IsConnected { get; }
    }
}
