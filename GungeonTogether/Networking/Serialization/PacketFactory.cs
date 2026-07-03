using System;
using System.Collections.Generic;
using GungeonTogether.Networking.Packets;
using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Enums;

namespace GungeonTogether.Networking.Serialization
{
    public static class PacketFactory
    {
        private static Dictionary<PacketType, Type> _packetTypes = new Dictionary<PacketType, Type>
        {
            { PacketType.PlayerPosition, typeof(PlayerPositionPacket) },
            { PacketType.ConnectionRequest, typeof(ConnectionRequestPacket) },
            { PacketType.ConnectionAccepted, typeof(ConnectionAcceptedPacket) },
            { PacketType.Disconnect, typeof(DisconnectPacket) },
            { PacketType.ConnectionRejected, typeof(ConnectionRejectedPacket) },
            { PacketType.PlayerJoin, typeof(PlayerJoinPacket) },
            { PacketType.PlayerLeave, typeof(PlayerLeavePacket) },
            { PacketType.RoomChange, typeof(RoomChangePacket) },
            { PacketType.EnemySpawn, typeof(EnemySpawnPacket) },
            { PacketType.EnemyState, typeof(EnemyStatePacket) },
            { PacketType.EnemyDeath, typeof(EnemyDeathPacket) },
        };

        public static INetworkPacket Create(PacketType type)
        {
            if (_packetTypes.TryGetValue(type, out Type classType))
            {
                return (INetworkPacket)Activator.CreateInstance(classType);
            }
            return null;
        }
    }
}
