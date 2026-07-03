namespace GungeonTogether.Networking.Enums
{
    public enum PacketType : byte
    {
        None = 0,
        PlayerPosition = 1,
        ConnectionRequest = 2,
        ConnectionAccepted = 3,
        ConnectionRejected = 4,
        Disconnect = 5
    }
}
