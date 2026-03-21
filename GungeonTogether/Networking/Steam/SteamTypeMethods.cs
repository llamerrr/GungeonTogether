        using System;
using System.Reflection;

namespace GungeonTogether.Networking.Steam
{
    /// <summary>
    /// Partial class containing cached Steam types and methods for reflection-based access
    /// </summary>
    public static partial class SteamReflectionHelper
    {
        // Reflection types and methods for ETG's Steamworks
        private static Type steamUserType;
        private static Type steamFriendsType;
        private static Type steamNetworkingType;
        private static Type steamMatchmakingType;
        private static Type steamUtilsType;
        private static Type steamAppsType;
        private static Type gameJoinRequestedCallbackType;
        private static Type lobbyEnterCallbackType;
        private static Type lobbyCreatedCallbackType;
        private static Type lobbyDataUpdateCallbackType;

        // Reflected methods
        private static MethodInfo getSteamIdMethod;
        private static MethodInfo sendP2PPacketMethod;
        private static MethodInfo readP2PPacketMethod;
        private static MethodInfo readP2PSessionRequestMethod;
        private static MethodInfo isP2PPacketAvailableMethod;
        private static MethodInfo acceptP2PSessionMethod;
        private static MethodInfo closeP2PSessionMethod;

        // Rich Presence and lobby methods
        private static MethodInfo setRichPresenceMethod;
        private static MethodInfo clearRichPresenceMethod;
        private static MethodInfo createLobbyMethod;
        private static MethodInfo joinLobbyMethod;
        private static MethodInfo leaveLobbyMethod;
        private static MethodInfo setLobbyDataMethod;
        private static MethodInfo getLobbyDataMethod;
        private static MethodInfo setLobbyJoinableMethod;
        private static MethodInfo inviteUserToLobbyMethod;
        private static MethodInfo getLobbyOwnerMethod;

        // Steam Friends methods
        private static MethodInfo getFriendCountMethod;
        private static MethodInfo getFriendByIndexMethod;
        private static MethodInfo getFriendPersonaNameMethod;
        private static MethodInfo getFriendPersonaStateMethod;
        private static MethodInfo getFriendGamePlayedMethod;
        private static MethodInfo getFriendRichPresenceMethod;

        // Property accessors for the cached methods
        public static bool IsInitialised => initialised;
        public static MethodInfo SendP2PPacketMethod => sendP2PPacketMethod;
        public static MethodInfo ReadP2PPacketMethod => readP2PPacketMethod;
        public static MethodInfo ReadP2PSessionRequestMethod => readP2PSessionRequestMethod;
        public static MethodInfo IsP2PPacketAvailableMethod => isP2PPacketAvailableMethod;
        public static MethodInfo AcceptP2PSessionMethod => acceptP2PSessionMethod;
        public static MethodInfo CloseP2PSessionMethod => closeP2PSessionMethod;
        public static MethodInfo SetRichPresenceMethod => setRichPresenceMethod;
        public static MethodInfo ClearRichPresenceMethod => clearRichPresenceMethod;
        public static MethodInfo CreateLobbyMethod => createLobbyMethod;
        public static MethodInfo JoinLobbyMethod => joinLobbyMethod;
        public static MethodInfo LeaveLobbyMethod => leaveLobbyMethod;
        public static MethodInfo SetLobbyDataMethod => setLobbyDataMethod;
        public static MethodInfo GetLobbyDataMethod => getLobbyDataMethod;
        public static MethodInfo SetLobbyJoinableMethod => setLobbyJoinableMethod;
        public static MethodInfo InviteUserToLobbyMethod => inviteUserToLobbyMethod;
        public static MethodInfo GetLobbyOwnerMethod => getLobbyOwnerMethod;

        // Friends methods accessors
        public static MethodInfo GetFriendCountMethod => getFriendCountMethod;
        public static MethodInfo GetFriendByIndexMethod => getFriendByIndexMethod;
        public static MethodInfo GetFriendPersonaNameMethod => getFriendPersonaNameMethod;
        public static MethodInfo GetFriendPersonaStateMethod => getFriendPersonaStateMethod;
        public static MethodInfo GetFriendGamePlayedMethod => getFriendGamePlayedMethod;
        public static MethodInfo GetFriendRichPresenceMethod => getFriendRichPresenceMethod;

        // Additional public properties
        public static Type GameJoinRequestedCallbackType => gameJoinRequestedCallbackType;
        public static Type LobbyEnterCallbackType => lobbyEnterCallbackType;
        public static Type LobbyCreatedCallbackType => lobbyCreatedCallbackType;
        public static Type LobbyDataUpdateCallbackType => lobbyDataUpdateCallbackType;
    }
}