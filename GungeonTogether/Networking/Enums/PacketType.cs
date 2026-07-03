namespace GungeonTogether.Networking.Enums
{
    public enum PacketType : byte
    {
        None = 0,
        PlayerPosition = 1,
        ConnectionRequest = 2,
        ConnectionAccepted = 3,
        ConnectionRejected = 4,
        Disconnect = 5,
        PlayerJoin = 6,
        PlayerLeave = 7,
        RoomChange = 8,
        EnemySpawn = 9,
        EnemyState = 10,
        EnemyDeath = 11,
        WorldState = 12,
        PlayerState = 13,
        LoadingState = 14,
    }
}
