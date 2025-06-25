namespace GungeonTogether.Networking.Packet
{
    /// <summary>
    /// Packet IDs for client-to-server communication.
    /// </summary>
    public enum ClientPacketId : byte
    {
        LoginRequest = 0,
        PlayerUpdate = 1,
        PlayerEnterRoom = 2,
        PlayerWeaponSwitch = 3,
        PlayerItemUse = 4,
        ChatMessage = 5,
        Disconnect = 6
    }
    
    /// <summary>
    /// Packet IDs for server-to-client communication.
    /// </summary>
    public enum ServerPacketId : byte
    {
        LoginResponse = 0,
        PlayerConnect = 1,
        PlayerDisconnect = 2,
        PlayerUpdate = 3,
        PlayerEnterRoom = 4,
        PlayerWeaponSwitch = 5,
        PlayerItemUse = 6,
        RoomUpdate = 7,
        EnemyUpdate = 8,
        ItemPickup = 9,
        ChatMessage = 10,
        ServerSettings = 11
    }
}
