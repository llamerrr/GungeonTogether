namespace GungeonTogether.Steam
{
    // Custom delegates to avoid Action<T> generic issues in .NET Framework 4.7.2
    public delegate void PlayerJoinedHandler(ulong steamId);
    public delegate void PlayerLeftHandler(ulong steamId);
    public delegate void DataReceivedHandler(ulong steamId, byte[] data);
}
