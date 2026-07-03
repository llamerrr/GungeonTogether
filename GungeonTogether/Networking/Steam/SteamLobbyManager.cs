using System;
using System.Reflection;
using GungeonTogether.Systems.Logging;
using Debug = GungeonTogether.Systems.Logging.Debug;

namespace GungeonTogether.Networking.Steam
{
    public class SteamLobbyManager
    {
        private static SteamLobbyManager _instance;
        
        public static SteamLobbyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    try
                    {
                        _instance = new SteamLobbyManager();
                    }
                    catch (System.TypeLoadException tle)
                    {
                        Debug.LogError($"SteamLobbyManager: TypeLoadException: {tle.Message}");
                        throw;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"SteamLobbyManager: {ex.GetType().Name}: {ex.Message}");
                        throw;
                    }
                }
                return _instance;
            }
        }

        public bool IsInitialised { get; private set; }
        public bool IsInLobby { get; private set; }
        public ulong CurrentLobbyId { get; private set; }

        private Assembly _steamworksAssembly;
        private Type _steamApiType;
        private MethodInfo _runCallbacksMethod;

        private object _lobbyCreatedCb;
        private object _lobbyEnterCb;
        private object _richPresenceJoinRequestedCb;

        public SteamLobbyManager()
        {
            IsInitialised = false;
            IsInLobby = false;
            CurrentLobbyId = 0;
        }
        public void Initialise()
        {
            if (IsInitialised) return;

            SteamReflectionHelper.InitialiseSteamTypes();
            if (!SteamReflectionHelper.IsInitialised)
            {
                Debug.LogWarning("[SteamLobby] Steamworks not available (not running Steam build???) [launch though steam]");
                return;
            }

            _steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
            if (_steamworksAssembly == null)
            {
                Debug.LogWarning("[SteamLobby] Steamworks assembly not found...");
                return;
            }

            _steamApiType = _steamworksAssembly.GetType("Steamworks.SteamAPI", false);
            _runCallbacksMethod = _steamApiType != null
                ? _steamApiType.GetMethod("RunCallbacks", BindingFlags.Public | BindingFlags.Static)
                : null;

            HookCallbacks();   // <-- hook immediately, as before

            IsInitialised = true;
            Debug.Log("[SteamLobby] Initialised");
        }

        public void Update()
        {
            if (!IsInitialised) return;
            try
            {
                if (_runCallbacksMethod == null)
                {
                    Debug.LogWarning("[SteamLobby] RunCallbacks method is NULL");
                    return;
                }
                
                _runCallbacksMethod.Invoke(null, null);
            }
            catch (Exception e)
            {
                Debug.LogError("[SteamLobby] Error in Update/RunCallbacks: " + e.Message);
            }
        }

        public void CreateLobby(int maxMembers = 4)
        {
            if (!IsInitialised) return;
            if (SteamReflectionHelper.CreateLobbyMethod == null)
            {
                Debug.LogWarning("[SteamLobby] CreateLobby not available");
                return;
            }

            try
            {
                // CreateLobby(ELobbyType, int)
                object lobbyType = GetLobbyTypeEnum("k_ELobbyTypeFriendsOnly") ?? GetLobbyTypeEnum("k_ELobbyTypePublic");
                SteamReflectionHelper.CreateLobbyMethod.Invoke(null, new object[] { lobbyType, maxMembers });
                Debug.Log("[SteamLobby] Creating lobby...");
            }
            catch (Exception e)
            {
                Debug.LogError("[SteamLobby] Failed to create lobby: " + e.Message);
            }
        }

        public void JoinLobby(ulong lobbyId)
        {
            if (!IsInitialised) return;
            if (SteamReflectionHelper.JoinLobbyMethod == null)
            {
                Debug.LogWarning("[SteamLobby] JoinLobby not available");
                return;
            }

            try
            {
                object lobbySteamId = SteamReflectionHelper.CreateCSteamID(lobbyId);
                SteamReflectionHelper.JoinLobbyMethod.Invoke(null, new object[] { lobbySteamId });
                Debug.Log("[SteamLobby] Joining lobby " + lobbyId + "...");
            }
            catch (Exception e)
            {
                Debug.LogError("[SteamLobby] Failed to join lobby: " + e.Message);
            }
        }

        public void LeaveLobby()
        {
            if (!IsInitialised) return;
            if (!IsInLobby || CurrentLobbyId == 0) return;

            try
            {
                if (SteamReflectionHelper.LeaveLobbyMethod != null)
                {
                    object lobbySteamId = SteamReflectionHelper.CreateCSteamID(CurrentLobbyId);
                    SteamReflectionHelper.LeaveLobbyMethod.Invoke(null, new object[] { lobbySteamId });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SteamLobby] Failed to leave lobby cleanly: " + e.Message);
            }

            IsInLobby = false;
            CurrentLobbyId = 0;
            Debug.Log("[SteamLobby] Left lobby.");
        }

        public void OpenInviteDialog()
        {
            if (!IsInitialised) return;
            if (!IsInLobby || CurrentLobbyId == 0)
            {
                Debug.LogWarning("[SteamLobby] Not in a lobby to invite from");
                return;
            }

            try
            {
                if (_steamworksAssembly == null) return;
                Type steamFriendsType = _steamworksAssembly.GetType("Steamworks.SteamFriends", false);
                if (steamFriendsType == null) return;
                MethodInfo method = steamFriendsType.GetMethod("ActivateGameOverlayInviteDialog", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Debug.LogWarning("[SteamLobby] Steam overlay invite dialog method not found");
                    return;
                }

                object lobbySteamId = SteamReflectionHelper.CreateCSteamID(CurrentLobbyId);
                method.Invoke(null, new object[] { lobbySteamId });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SteamLobby] Failed to open invite dialog: " + e.Message);
            }
        }

        private void HookCallbacks()
        {
            try
            {
                // Guard against leaking handlers if this is ever called more than once
                // (e.g. a future reconnect/reset flow) - CreateCallback appends to a
                // static list that otherwise never shrinks.
                SteamCallbackRouter.Clear();

                Type lobbyCreatedType = SteamReflectionHelper.LobbyCreatedCallbackType;
                Type lobbyEnterType = SteamReflectionHelper.LobbyEnterCallbackType;
                Type richPresenceJoinRequestedType = SteamReflectionHelper.GameJoinRequestedCallbackType;

                if (_steamworksAssembly == null)
                {
                    Debug.LogWarning("[SteamLobby] Steamworks assembly is null, cannot hook callbacks");
                    return;
                }

                Debug.Log("[SteamLobby] Attempting to hook callbacks...");

                if (lobbyCreatedType != null)
                {
                    Debug.Log("[SteamLobby] Creating LobbyCreated callback...");
                    _lobbyCreatedCb = SteamCallbackRouter.CreateCallback(_steamworksAssembly, lobbyCreatedType, OnLobbyCreated);
                    Debug.Log("[SteamLobby] LobbyCreated callback created: " + (_lobbyCreatedCb != null ? "SUCCESS" : "FAILED"));
                }
                else
                {
                    Debug.LogWarning("[SteamLobby] LobbyCreatedCallbackType is null");
                }

                if (lobbyEnterType != null)
                {
                    Debug.Log("[SteamLobby] Creating LobbyEnter callback...");
                    _lobbyEnterCb = SteamCallbackRouter.CreateCallback(_steamworksAssembly, lobbyEnterType, OnLobbyEnter);
                    Debug.Log("[SteamLobby] LobbyEnter callback created: " + (_lobbyEnterCb != null ? "SUCCESS" : "FAILED"));
                }
                else
                {
                    Debug.LogWarning("[SteamLobby] LobbyEnterCallbackType is null");
                }

                if (richPresenceJoinRequestedType != null)
                {
                    Debug.Log("[SteamLobby] Creating RichPresenceJoinRequested callback...");
                    _richPresenceJoinRequestedCb = SteamCallbackRouter.CreateCallback(_steamworksAssembly, richPresenceJoinRequestedType, OnRichPresenceJoinRequested);
                    Debug.Log("[SteamLobby] RichPresenceJoinRequested callback created: " + (_richPresenceJoinRequestedCb != null ? "SUCCESS" : "FAILED"));
                }
                else
                {
                    Debug.LogWarning("[SteamLobby] GameJoinRequestedCallbackType is null");
                }

                if (_lobbyCreatedCb == null && _lobbyEnterCb == null && _richPresenceJoinRequestedCb == null)
                {
                    Debug.LogError("[SteamLobby] Could not hook ANY callbacks (Steamworks callback API mismatch?)");
                }
                else
                {
                    Debug.Log("[SteamLobby] Successfully hooked callbacks");
                }
                Type lobbyJoinRequestedType = SteamReflectionHelper.GameLobbyJoinRequestedCallbackType;
                if (lobbyJoinRequestedType != null)
                {
                    Debug.Log("[SteamLobby] Creating GameLobbyJoinRequested callback...");
                    _lobbyJoinRequestedCb = SteamCallbackRouter.CreateCallback(_steamworksAssembly, lobbyJoinRequestedType, OnGameLobbyJoinRequested);
                    Debug.Log("[SteamLobby] GameLobbyJoinRequested callback created: " + (_lobbyJoinRequestedCb != null ? "SUCCESS" : "FAILED"));
                }
                else
                {
                    Debug.LogWarning("[SteamLobby] GameLobbyJoinRequestedCallbackType is null");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SteamLobby] Failed to hook callbacks: " + e.Message);
                Debug.LogError("[SteamLobby] Stack trace: " + e.StackTrace);
            }
        }

        private void OnLobbyCreated(object callbackData)
        {
            try
            {
                ulong lobbyId = ReadUlongField(callbackData, "m_ulSteamIDLobby", "m_SteamIDLobby", "m_ulSteamIDLobbyID");
                uint result = ReadUIntField(callbackData, "m_eResult", "m_EResult");

                if (lobbyId == 0)
                {
                    Debug.LogWarning("[SteamLobby] LobbyCreated callback but lobby id was 0");
                    return;
                }

                Debug.Log("[SteamLobby] Lobby created: " + lobbyId + " result=" + result);

                CurrentLobbyId = lobbyId;
                IsInLobby = true;

                // Set some lobby data so clients can find the host
                try
                {
                    if (SteamReflectionHelper.SetLobbyDataMethod != null)
                    {
                        object lobbySteamId = SteamReflectionHelper.CreateCSteamID(lobbyId);
                        SteamReflectionHelper.SetLobbyDataMethod.Invoke(null, new object[] { lobbySteamId, "gt_host", SteamReflectionHelper.GetLocalSteamId().ToString() });
                        SteamReflectionHelper.SetLobbyDataMethod.Invoke(null, new object[] { lobbySteamId, "gt_proto", Networking.NetworkManager.ProtocolVersion.ToString() });
                        Debug.Log("[SteamLobby] Lobby data set");
                    }

                    if (SteamReflectionHelper.SetLobbyJoinableMethod != null)
                    {
                        object lobbySteamId = SteamReflectionHelper.CreateCSteamID(lobbyId);
                        SteamReflectionHelper.SetLobbyJoinableMethod.Invoke(null, new object[] { lobbySteamId, true });
                        Debug.Log("[SteamLobby] Lobby joinable set");
                    }

                    if (SteamReflectionHelper.SetRichPresenceMethod != null)
                    {
                        // Common pattern: Rich presence key "connect" set to lobby id
                        SteamReflectionHelper.SetRichPresenceMethod.Invoke(null, new object[] { "connect", lobbyId.ToString() });
                        Debug.Log("[SteamLobby] Rich presence set to connect=" + lobbyId.ToString());
                    }
                    else
                    {
                        Debug.LogWarning("[SteamLobby] SetRichPresenceMethod is NULL!");
                    }
                }
                catch (Exception richPresenceEx)
                {
                    Debug.LogError("[SteamLobby] Error setting rich presence: " + richPresenceEx.Message);
                }

                // Start hosting session at networking layer
                Networking.NetworkManager.Instance.StartHosting();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SteamLobby] OnLobbyCreated error: " + e.Message);
            }
        }

        private void OnGameLobbyJoinRequested(object callbackData)
        {
            try
            {
                var type = callbackData.GetType();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[SteamLobby] ========== GAME LOBBY JOIN REQUESTED ==========");
                sb.AppendLine($"Type: {type.FullName}");
                sb.AppendLine("Fields:");
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var val = f.GetValue(callbackData);
                    sb.AppendLine($"  {f.Name} ({f.FieldType.Name}) = {val}");
                }
                Debug.Log(sb.ToString());

                // After logging, we can attempt to extract using the actual field names.
                // We'll use the field names found in the log to adjust the extraction.
                // For now, we'll try to read them generically.
                ulong lobbyId = ReadUlongField(callbackData, "m_steamIDLobby", "m_SteamIDLobby", "m_ulSteamIDLobby");
                ulong friendId = ReadUlongField(callbackData, "m_steamIDFriend", "m_SteamIDFriend", "m_ulSteamIDFriend");

                Debug.Log($"[SteamLobby] Extracted lobby: {lobbyId}, friend: {friendId}");

                if (lobbyId != 0)
                    JoinLobby(lobbyId);
                else
                    Debug.LogWarning("[SteamLobby] GameLobbyJoinRequested had null lobby ID");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamLobby] OnGameLobbyJoinRequested error: {e.Message}");
                Debug.LogError($"[SteamLobby] Stack trace: {e.StackTrace}");
            }
        }

        private void OnLobbyEnter(object callbackData)
        {
            try
            {
                ulong lobbyId = ReadUlongField(callbackData, "m_ulSteamIDLobby", "m_SteamIDLobby", "m_ulSteamIDLobbyID");
                if (lobbyId == 0) return;

                CurrentLobbyId = lobbyId;
                IsInLobby = true;

                ulong ownerId = 0;
                try
                {
                    if (SteamReflectionHelper.GetLobbyOwnerMethod != null)
                    {
                        object lobbySteamId = SteamReflectionHelper.CreateCSteamID(lobbyId);
                        object ownerCSteamId = SteamReflectionHelper.GetLobbyOwnerMethod.Invoke(null, new object[] { lobbySteamId });
                        ownerId = ExtractSteamId(ownerCSteamId);
                    }
                }
                catch { }

                ulong local = SteamReflectionHelper.GetLocalSteamId();
                Debug.Log("[SteamLobby] Entered lobby: " + lobbyId + " owner=" + ownerId + " local=" + local);

                if (ownerId != 0 && ownerId != local)
                {
                    Networking.NetworkManager.Instance.ConnectTo(ownerId);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SteamLobby] OnLobbyEnter error: " + e.Message);
            }
        }

        private void OnRichPresenceJoinRequested(object callbackData)
        {
            Debug.Log("[SteamLobby] ========== OnRichPresenceJoinRequested CALLBACK FIRED ==========");
            try
            {
                Debug.Log("[SteamLobby] Callback data type: " + (callbackData != null ? callbackData.GetType().Name : "NULL"));
                
                // GameRichPresenceJoinRequested_t typically has a string connect field named m_rgchConnect
                string connect = ReadStringField(callbackData, "m_rgchConnect", "m_connect", "m_rgchConnectString");

                Debug.Log("[SteamLobby] Connect string: " + (string.IsNullOrEmpty(connect) ? "(empty or null)" : connect));

                if (string.IsNullOrEmpty(connect))
                {
                    Debug.Log("[SteamLobby] Join requested (empty connect string). Accepting invite might not include lobby id");
                    return;
                }

                ulong lobbyId;
                if (TryExtractFirstUlong(connect, out lobbyId) && lobbyId != 0)
                {
                    Debug.Log("[SteamLobby] Join requested via rich presence. Extracted lobby id: " + lobbyId);
                    JoinLobby(lobbyId);
                }
                else
                {
                    Debug.Log("[SteamLobby] Join requested but couldn't parse lobby id from connect string: " + connect);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SteamLobby] OnRichPresenceJoinRequested error: " + e.Message);
                Debug.LogError("[SteamLobby] Stack trace: " + e.StackTrace);
            }
            Debug.Log("[SteamLobby] ========== OnRichPresenceJoinRequested END ==========");
        }

        private object GetLobbyTypeEnum(string name)
        {
            try
            {
                if (_steamworksAssembly == null) return null;
                Type t = _steamworksAssembly.GetType("Steamworks.ELobbyType", false);
                if (t == null) return null;
                return Enum.Parse(t, name);
            }
            catch
            {
                return null;
            }
        }

        private static ulong ExtractSteamId(object cSteamId)
        {
            if (cSteamId == null) return 0;
            try
            {
                FieldInfo f = cSteamId.GetType().GetField("m_SteamID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) return 0;
                object val = f.GetValue(cSteamId);
                return val != null ? Convert.ToUInt64(val) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static ulong ReadUlongField(object obj, params string[] fieldNames)
        {
            if (obj == null) return 0;
            Type t = obj.GetType();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                FieldInfo f = t.GetField(fieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) continue;
                object v = f.GetValue(obj);
                if (v == null) continue;
                try { return Convert.ToUInt64(v); } catch { }
            }
            return 0;
        }

        private static uint ReadUIntField(object obj, params string[] fieldNames)
        {
            if (obj == null) return 0;
            Type t = obj.GetType();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                FieldInfo f = t.GetField(fieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) continue;
                object v = f.GetValue(obj);
                if (v == null) continue;
                try { return Convert.ToUInt32(v); } catch { }
            }
            return 0;
        }

        private static string ReadStringField(object obj, params string[] fieldNames)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                FieldInfo f = t.GetField(fieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) continue;
                object v = f.GetValue(obj);
                if (v == null) continue;
                return v.ToString();
            }
            return null;
        }

        private static bool TryExtractFirstUlong(string s, out ulong value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;

            int start = -1;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i])) { start = i; break; }
            }
            if (start < 0) return false;

            int end = start;
            while (end < s.Length && char.IsDigit(s[end])) end++;

            string digits = s.Substring(start, end - start);
            return ulong.TryParse(digits, out value);
        }
        private object _lobbyJoinRequestedCb;
    }

}
