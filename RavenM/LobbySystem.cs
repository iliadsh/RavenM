using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Steamworks;
using HarmonyLib;
using System;
using System.Text;
using System.Diagnostics;
using System.Linq;
using RavenM.RSPatch.Wrapper;

namespace RavenM
{
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.OnGameManagerStart))]
    public class CleanupListPatch
    {
        static void Prefix(ModManager __instance)
        {
            if (__instance.noContentMods)
                __instance.noWorkshopMods = true;
        }
    }
    
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartLevel))]
    public class OnStartPatch
    {
        static bool Prefix()
        {
            if (LobbySystem.instance.InLobby && !LobbySystem.instance.IsLobbyOwner && !LobbySystem.instance.ReadyToPlay)
                return false;
            OptionsPatch.SetConfigValues(false);
            // Only start if all members are ready.
            if (LobbySystem.instance.LobbyDataReady && LobbySystem.instance.IsLobbyOwner)
            {
                foreach (var memberId in LobbySystem.instance.GetLobbyMembers())
                {
                    if (SteamMatchmaking.GetLobbyMemberData(LobbySystem.instance.ActualLobbyID, memberId, "loaded") != "yes")
                    {
                        return false;
                    }
                }
            }

            if (LobbySystem.instance.IsLobbyOwner)
            {
                IngameNetManager.instance.OpenRelay();

                SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "started", "yes");
            }
            LobbySystem.instance.ReadyToPlay = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(LoadoutUi), nameof(LoadoutUi.OnDeployClick))]
    public class FirstDeployPatch
    {
        static bool Prefix()
        {
            if (LobbySystem.instance.InLobby)
            {
                // Ignore players who joined mid-game.
                if ((bool)typeof(LoadoutUi).GetField("hasAcceptedLoadoutOnce", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(LoadoutUi.instance))
                    return true;

                // Wait for everyone to load in first.
                foreach (var memberId in LobbySystem.instance.GetLobbyMembers())
                {
                    if (SteamMatchmaking.GetLobbyMemberData(LobbySystem.instance.ActualLobbyID, memberId, "ready") != "yes")
                    {
                        // Ignore players that just joined and are loading mods.
                        if (SteamMatchmaking.GetLobbyMemberData(LobbySystem.instance.ActualLobbyID, memberId, "loaded") != "yes")
                            continue;

                        return false;
                    }
                }
            }
            if (IngameNetManager.instance.IsHost || LobbySystem.instance.IsLobbyOwner)
            {
                Plugin.logger.LogInfo("SendNetworkGameObjectsHashesPacket()");
                WLobby.SendNetworkGameObjectsHashesPacket();
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameManager), "StartGame")]
    public class FinalizeStartPatch
    {
        // Maps sometimes have their own vehicles. We need to tag them.
        static void Prefix()
        {
            if (!LobbySystem.instance.InLobby)
                return;

            // The game will destroy any vehicles that have already spawned. Ignore them.
            var ignore = UnityEngine.Object.FindObjectsOfType<Vehicle>();
            Plugin.logger.LogInfo($"Ignore list: {ignore.Length}");

            var map = GameManager.instance.lastMapEntry;

            foreach (var vehicle in Resources.FindObjectsOfTypeAll<Vehicle>())
            {
                if (!vehicle.TryGetComponent(out PrefabTag _) && !Array.Exists(ignore, x => x == vehicle))
                {
                    Plugin.logger.LogInfo($"Detected map vehicle with name: {vehicle.name}, and from map: {map.name}.");

                    var tag = vehicle.gameObject.AddComponent<PrefabTag>();
                    tag.NameHash = vehicle.name.GetHashCode();
                    tag.Mod = (ulong)map.name.GetHashCode();
                    IngameNetManager.PrefabCache[new Tuple<int, ulong>(tag.NameHash, tag.Mod)] = vehicle.gameObject;
                }
            }

            foreach (var projectile in Resources.FindObjectsOfTypeAll<Projectile>())
            {
                if (!projectile.TryGetComponent(out PrefabTag _))
                {
                    Plugin.logger.LogInfo($"Detected map projectile with name: {projectile.name}, and from map: {map.name}.");

                    var tag = projectile.gameObject.AddComponent<PrefabTag>();
                    tag.NameHash = projectile.name.GetHashCode();
                    tag.Mod = (ulong)map.name.GetHashCode();
                    IngameNetManager.PrefabCache[new Tuple<int, ulong>(tag.NameHash, tag.Mod)] = projectile.gameObject;
                }
            }

            foreach (var destructible in Resources.FindObjectsOfTypeAll<Destructible>())
            {
                var prefab = DestructiblePacket.Root(destructible);

                if (!prefab.TryGetComponent(out PrefabTag _))
                {
                    Plugin.logger.LogInfo($"Detected map destructible with name: {prefab.name}, and from map: {map.name}.");

                    IngameNetManager.TagPrefab(prefab, (ulong)map.name.GetHashCode());
                }
            }

            foreach (var destructible in UnityEngine.Object.FindObjectsOfType<Destructible>())
            {
                // One shot created destructibles -- not cool!
                var root = DestructiblePacket.Root(destructible);

                if (IngameNetManager.instance.ClientDestructibles.ContainsValue(root))
                    continue;

                // FIXME: Shitty hack. The assumption is map destructibles are consistent
                // and thus will always spawn in the same positions regardless of the
                // client run. I have no idea how correct this assumption actually is.
                int id = root.transform.position.GetHashCode();

                if (root.TryGetComponent(out GuidComponent guid))
                    id = guid.guid;
                else
                    root.AddComponent<GuidComponent>().guid = id;

                IngameNetManager.instance.ClientDestructibles.Add(id, root);

                Plugin.logger.LogInfo($"Registered new destructible root with name: {root.name} and id: {id}");
            }
        }

        static void Postfix()
        {
            if (!LobbySystem.instance.LobbyDataReady)
                return;

            if (LobbySystem.instance.IsLobbyOwner)
                IngameNetManager.instance.StartAsServer();
            else
                IngameNetManager.instance.StartAsClient(LobbySystem.instance.OwnerID); 

            SteamMatchmaking.SetLobbyMemberData(LobbySystem.instance.ActualLobbyID, "ready", "yes");
        }
    }

    [HarmonyPatch(typeof(MainMenu), nameof(MainMenu.GoBack))]
    public class GoBackPatch
    {
        static bool Prefix()
        {
            if (LobbySystem.instance.InLobby && (int)typeof(MainMenu).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MainMenu.instance) == MainMenu.PAGE_INSTANT_ACTION)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(InstantActionMaps), "SetupSkinList")]
    public class SkinListPatch
    {
        static void Prefix() => ModManager.instance.actorSkins.Sort((x, y) => x.name.CompareTo(y.name));
    }

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.FinalizeLoadedModContent))]
    public class AfterModsLoadedPatch
    {
        static void Postfix()
        {
            if (InstantActionMaps.instance != null)
            {
                // We need to update the skin dropdown with the new mods.
                typeof(InstantActionMaps).GetMethod("SetupSkinList", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(InstantActionMaps.instance, null);
            }

            ModManager.instance.ContentChanged();

            if (!LobbySystem.instance.InLobby || !LobbySystem.instance.LobbyDataReady || LobbySystem.instance.IsLobbyOwner || LobbySystem.instance.ModsToDownload.Count > 0)
                return;

            SteamMatchmaking.SetLobbyMemberData(LobbySystem.instance.ActualLobbyID, "loaded", "yes");
        }
    }

    [HarmonyPatch(typeof(SteamMatchmaking), nameof(SteamMatchmaking.LeaveLobby))]
    public class OnLobbyLeavePatch
    {
        static void Postfix()
        {
            LobbySystem.instance.InLobby = false;
            LobbySystem.instance.ReadyToPlay = true;
            LobbySystem.instance.LobbyDataReady = false;
            if (LobbySystem.instance.LoadedServerMods)
                LobbySystem.instance.RequestModReload = true;
            LobbySystem.instance.IsLobbyOwner = false;
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.ReturnToMenu))]
    public class LeaveOnEndGame
    {
        static void Prefix()
        {
            if (!LobbySystem.instance.InLobby || LobbySystem.instance.IsLobbyOwner)
                return;

            // Exit the lobby if we actually want to leave.
            if (new StackFrame(2).GetMethod().Name == "Menu")
                SteamMatchmaking.LeaveLobby(LobbySystem.instance.ActualLobbyID);
        }

        static void Postfix()
        {
            if (!LobbySystem.instance.InLobby)
                return;

            if (LobbySystem.instance.IsLobbyOwner)
                SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "started", "false");

            SteamMatchmaking.SetLobbyMemberData(LobbySystem.instance.ActualLobbyID, "ready", "no");
        }
    }

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.GetActiveMods))]
    public class ActiveModsPatch
    {
        static bool Prefix(ModManager __instance, ref List<ModInformation> __result)
        {
            if (LobbySystem.instance.LoadedServerMods && LobbySystem.instance.ServerMods.Count > 0)
            {
                __result = new List<ModInformation>();
                foreach (var mod in __instance.mods)
                {
                    if (LobbySystem.instance.ServerMods.Contains(mod.workshopItemId))
                    {
                        __result.Add(mod);
                    }
                }
                return false;
            }

            return true;
        }
    }

    public class LobbySystem : MonoBehaviour
    {
        public static LobbySystem instance;

        public bool PrivateLobby = false;

        public bool ShowOnList = true;

        public bool MidgameJoin = false;

        public string JoinLobbyID = string.Empty;

        public bool InLobby = false;

        public bool LobbyDataReady = false;

        public string LobbyMemberCap = "250";

        public CSteamID ActualLobbyID = CSteamID.Nil;

        public bool IsLobbyOwner = false;

        // FIXME: A stack is maybe overkill. There are only 3 menu states.
        public Stack<string> GUIStack = new Stack<string>();

        public CSteamID OwnerID = CSteamID.Nil;

        public bool ReadyToPlay = false;

        public List<PublishedFileId_t> ServerMods = new List<PublishedFileId_t>();

        public List<PublishedFileId_t> ModsToDownload = new List<PublishedFileId_t>();

        public bool LoadedServerMods = false;

        public bool RequestModReload = false;

        public Texture2D LobbyBackground = new Texture2D(1, 1);

        public Texture2D ProgressTexture = new Texture2D(1, 1);

        public List<CSteamID> OpenLobbies = new List<CSteamID>();

        public CSteamID LobbyView = CSteamID.Nil;

        public List<CSteamID> CurrentKickedMembers = new List<CSteamID>();

        public CSteamID KickPrompt = CSteamID.Nil;

        public string NotificationText = string.Empty;

        public bool nameTagsEnabled = true;

        public bool nameTagsForTeamOnly = false;
        private void Awake()
        {
            instance = this;

            LobbyBackground.SetPixel(0, 0, Color.black);
            LobbyBackground.Apply();

            ProgressTexture.SetPixel(0, 0, Color.green);
            ProgressTexture.Apply();
        }

        private void Start()
        {
            Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
            Callback<DownloadItemResult_t>.Create(OnItemDownload);
            Callback<LobbyMatchList_t>.Create(OnLobbyList);
            Callback<LobbyDataUpdate_t>.Create(OnLobbyData);
        }

        public List<CSteamID> GetLobbyMembers()
        {
            int len = SteamMatchmaking.GetNumLobbyMembers(ActualLobbyID);
            var ret = new List<CSteamID>(len);

            for (int i = 0; i < len; i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(ActualLobbyID, i);
                if (!CurrentKickedMembers.Contains(member))
                    ret.Add(member);
            }

            return ret;
        }

        private void OnLobbyData(LobbyDataUpdate_t pCallback)
        {
            var lobby = new CSteamID(pCallback.m_ulSteamIDLobby);

            if (pCallback.m_bSuccess == 0 || SteamMatchmaking.GetLobbyDataCount(lobby) == 0 || SteamMatchmaking.GetLobbyData(lobby, "hidden") == "true")
                OpenLobbies.Remove(lobby);
        }

        private void OnLobbyEnter(LobbyEnter_t pCallback)
        {
            Plugin.logger.LogInfo("Joined lobby!");
            CurrentKickedMembers.Clear();
            RequestModReload = false;
            LoadedServerMods = false;

            if (pCallback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                NotificationText = "Unknown error joining lobby. (Does it still exist?)";
                InLobby = false;
                return;
            }

            LobbyDataReady = true;
            ActualLobbyID = new CSteamID(pCallback.m_ulSteamIDLobby);

            // Sort vehicles
            var moddedVehicles = ModManager.AllVehiclePrefabs().ToList();
            moddedVehicles.Sort((x, y) => x.name.CompareTo(y.name));

            if (IsLobbyOwner)
            {
                OwnerID = SteamUser.GetSteamID();
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "owner", OwnerID.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "build_id", Plugin.BuildGUID);
                if (!ShowOnList)
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, "hidden", "true");
                if (MidgameJoin)
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, "hotjoin", "true");
                if (nameTagsEnabled)
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, "nameTags","true");
                if (nameTagsForTeamOnly)
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, "nameTagsForTeamOnly", "true");

                bool needsToReload = false;
                List<PublishedFileId_t> mods = new List<PublishedFileId_t>();

                foreach (var mod in ModManager.instance.GetActiveMods())
                {
                    if (mod.workshopItemId.ToString() == "0")
                    {
                        mod.enabled = false;
                        needsToReload = true;
                    }
                    else
                        mods.Add(mod.workshopItemId);
                }

                if (needsToReload)
                    ModManager.instance.ReloadModContent();
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "owner", OwnerID.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "mods", string.Join(",", mods.ToArray()));
                SteamMatchmaking.SetLobbyMemberData(ActualLobbyID, "loaded", "yes");
            }
            else
            {
                OwnerID = new CSteamID(ulong.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "owner")));
                Plugin.logger.LogInfo($"Host ID: {OwnerID}");

                MainMenu.instance.OpenPageIndex(MainMenu.PAGE_INSTANT_ACTION);
                ReadyToPlay = false;

                if (Plugin.BuildGUID != SteamMatchmaking.GetLobbyData(ActualLobbyID, "build_id"))
                {
                    Plugin.logger.LogInfo("Build ID mismatch! Leaving lobby.");
                    NotificationText = "You cannot join this lobby because you and the host are using different versions of RavenM.";
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    return;
                }

                ServerMods.Clear();
                ModsToDownload.Clear();
                string[] mods = SteamMatchmaking.GetLobbyData(ActualLobbyID, "mods").Split(',');
                foreach (string mod_str in mods)
                {
                    if (mod_str == string.Empty)
                        continue;
                    PublishedFileId_t mod_id = new PublishedFileId_t(ulong.Parse(mod_str));
                    if (mod_id.ToString() == "0")
                        continue;

                    ServerMods.Add(mod_id);

                    bool alreadyHasMod = false;
                    foreach (var mod in ModManager.instance.mods)
                    {
                        if (mod.workshopItemId == mod_id)
                        {
                            alreadyHasMod = true;
                            break;
                        }
                    }

                    if (!alreadyHasMod)
                    {
                        ModsToDownload.Add(mod_id);
                    }
                }
                TriggerModRefresh();
                bool nameTagsConverted = bool.TryParse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "nameTags"),out bool nameTagsOn);
                if (nameTagsConverted)
                {
                    nameTagsEnabled = nameTagsOn;
                }
                else
                {
                    nameTagsEnabled = false;
                }
                bool nameTagsTeamOnlyConverted = bool.TryParse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "nameTagsForTeamOnly"), out bool nameTagsForTeamOnlyOn);
                if (nameTagsTeamOnlyConverted)
                {
                    nameTagsForTeamOnly = nameTagsForTeamOnlyOn;
                }
                else
                {
                    nameTagsForTeamOnly = false;
                }
                if (SteamMatchmaking.GetLobbyData(ActualLobbyID, "started") == "yes" && SteamMatchmaking.GetLobbyData(ActualLobbyID, "hotjoin") != "true")
                {
                    Plugin.logger.LogInfo("The game has already started :( Leaving lobby.");
                    NotificationText = "This lobby has already started a match and has disabled mid-game joining or is playing a gamemode that does not support it.";
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    return;
                }
            }
        }

        private void OnItemDownload(DownloadItemResult_t pCallback)
        {
            Plugin.logger.LogInfo($"Downloaded mod! {pCallback.m_nPublishedFileId}");
            if (ModsToDownload.Contains(pCallback.m_nPublishedFileId))
            {
                var mod = (ModInformation)typeof(ModManager).GetMethod("AddWorkshopItemAsMod", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ModManager.instance, new object[] { pCallback.m_nPublishedFileId });
                mod.hideInModList = true;
                mod.enabled = false;

                ModsToDownload.Remove(pCallback.m_nPublishedFileId);

                TriggerModRefresh();
            }
        }

        public void TriggerModRefresh()
        {
            if (ModsToDownload.Count == 0)
            {
                Plugin.logger.LogInfo($"All server mods downloaded.");

                if (InLobby && LobbyDataReady && !IsLobbyOwner)
                {
                    List<bool> oldState = new List<bool>();

                    foreach (var mod in ModManager.instance.mods)
                    {
                        oldState.Add(mod.enabled);

                        mod.enabled = ServerMods.Contains(mod.workshopItemId);
                    }

                    // Clones the list of enabled mods.
                    ModManager.instance.ReloadModContent();
                    LoadedServerMods = true;

                    for (int i = 0; i < ModManager.instance.mods.Count; i++)
                        ModManager.instance.mods[i].enabled = oldState[i];
                }
            }
            else
            {
                var mod_id = ModsToDownload[0];
                bool isDownloading = SteamUGC.DownloadItem(mod_id, true);
                Plugin.logger.LogInfo($"Downloading mod with id: {mod_id} -- {isDownloading}");
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
        {
            // Anything other than a join...
            if ((pCallback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) == 0)
            {
                var id = new CSteamID(pCallback.m_ulSteamIDUserChanged);

                // ...means the owner left.
                if (OwnerID == id)
                {
                    NotificationText = "Lobby closed by host.";
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                }
            }
            else
            {
                var id = new CSteamID(pCallback.m_ulSteamIDUserChanged);

                if (CurrentKickedMembers.Contains(id))
                {
                    SendLobbyChat($"/kick {id}");
                }
            }
        }

        public void SendLobbyChat(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            SteamMatchmaking.SendLobbyChatMsg(ActualLobbyID, bytes, bytes.Length);
        }

        public string DecodeLobbyChat(byte[] bytes, int len)
        {
            // Don't want some a-hole crashing the lobby.
            try
            {
                return Encoding.UTF8.GetString(bytes, 0, len);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void OnLobbyChatMessage(LobbyChatMsg_t pCallback)
        {
            var buf = new byte[4096];
            int len = SteamMatchmaking.GetLobbyChatEntry(ActualLobbyID, (int)pCallback.m_iChatID, out CSteamID user, buf, buf.Length, out EChatEntryType chatType);
            string chat = DecodeLobbyChat(buf, len);

            if (chat.StartsWith("/") && user == OwnerID)
            {
                var parts = chat.Split(' ');
                if (parts.Length < 1)
                    return;
                var command = parts[0];
                if (command == "/kick")
                {
                    if (parts.Length != 2)
                        return;
                    var userIdS = parts[1];
                    if (ulong.TryParse(userIdS, out ulong memberIdI))
                    {
                        var member = new CSteamID(memberIdI);
                        if (member == SteamUser.GetSteamID())
                        {
                            NotificationText = "You were kicked from the lobby! You can no longer join this lobby.";
                            SteamMatchmaking.LeaveLobby(ActualLobbyID);
                        }
                        else if (!CurrentKickedMembers.Contains(member))
                        {
                            CurrentKickedMembers.Add(member);
                        }
                    }
                }
            }
        }

        private void StartAsClient()
        {
            ReadyToPlay = true;
            //No initial bots! Many errors otherwise!
            InstantActionMaps.instance.botNumberField.text = "0";
            InstantActionMaps.instance.StartGame();
        }

        private void OnLobbyList(LobbyMatchList_t pCallback)
        {
            Plugin.logger.LogInfo("Got lobby list.");

            OpenLobbies.Clear();
            for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
            {
                var lobby = SteamMatchmaking.GetLobbyByIndex(i);
                Plugin.logger.LogInfo($"Requesting lobby data for {lobby} -- {SteamMatchmaking.RequestLobbyData(lobby)}");
                OpenLobbies.Add(lobby);
            }
        }

        private void Update()
        {
            if (GameManager.instance == null || GameManager.IsIngame() || GameManager.IsInLoadingScreen())
                return;

            if (Input.GetKeyDown(KeyCode.M) && !InLobby)
            {
                if (GUIStack.Count == 0)
                    GUIStack.Push("Main");
                else
                    GUIStack.Clear();
            }

            if (MainMenu.instance != null
                && (int)typeof(MainMenu).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MainMenu.instance) < MainMenu.PAGE_INSTANT_ACTION
                && InLobby)
                MainMenu.instance.OpenPageIndex(MainMenu.PAGE_INSTANT_ACTION);

            if (LoadedServerMods && RequestModReload)
            {
                LoadedServerMods = false;
                RequestModReload = false;
                ModManager.instance.ReloadModContent();
            }

            if (!LobbyDataReady)
                return;

            // TODO: Ok. This is really bad. We should either:
            // A) Update the menu items only when they are changed, or,
            // B) Sidestep the menu entirely, and send the game information
            //     when the host starts.
            // The latter option is the cleanest and most efficient way, but
            // the former at least has visual input for the non-host clients,
            // which is also important.
            // InstantActionMaps.instance.gameModeDropdown.value = 0;
            int customMapOptionIndex = (int)typeof(InstantActionMaps).GetField("customMapOptionIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(InstantActionMaps.instance);
            var entries = (List<InstantActionMaps.MapEntry>)typeof(InstantActionMaps).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(InstantActionMaps.instance);
            // Don't allow spectator.
            if (InstantActionMaps.instance.teamDropdown.value == 2)
            {
                InstantActionMaps.instance.teamDropdown.value = 0;
            }
            SteamMatchmaking.SetLobbyMemberData(ActualLobbyID, "team", InstantActionMaps.instance.teamDropdown.value == 0 ? "<color=blue>E</color>" : "<color=red>R</color>");
            if (IsLobbyOwner)
            {
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "gameMode", InstantActionMaps.instance.gameModeDropdown.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "nightMode", InstantActionMaps.instance.nightToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "playerHasAllWeapons", InstantActionMaps.instance.playerHasAllWeaponsToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "reverseMode", InstantActionMaps.instance.reverseToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "botNumberField", InstantActionMaps.instance.botNumberField.text);
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "balance", InstantActionMaps.instance.balanceSlider.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "respawnTime", InstantActionMaps.instance.respawnTimeField.text);
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "gameLength", InstantActionMaps.instance.gameLengthDropdown.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "loadedLevelEntry", InstantActionMaps.instance.mapDropdown.value.ToString());
                // For SpecOps.
                if (InstantActionMaps.instance.gameModeDropdown.value == 1)
                {
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, "team", InstantActionMaps.instance.teamDropdown.value.ToString());
                }

                if (InstantActionMaps.instance.mapDropdown.value == customMapOptionIndex)
                {
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, "customMap", entries[customMapOptionIndex].name);
                }

                for (int i = 0; i < 2; i++)
                {
                    var teamInfo = GameManager.instance.gameInfo.team[i];

                    var weapons = new List<int>();
                    foreach (var weapon in teamInfo.availableWeapons)
                    {
                        weapons.Add(weapon.nameHash);
                    }
                    string weaponString = string.Join(",", weapons.ToArray());
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, i + "weapons", weaponString);

                    foreach (var vehiclePrefab in teamInfo.vehiclePrefab)
                    {
                        var type = vehiclePrefab.Key;
                        var prefab = vehiclePrefab.Value;

                        bool isDefault = true; // Default vehicle.
                        int idx = Array.IndexOf(ActorManager.instance.defaultVehiclePrefabs, prefab);

                        if (idx == -1)
                        {
                            isDefault = false;
                            idx = ModManager.AllVehiclePrefabs().ToList().IndexOf(prefab);
                        }

                        SteamMatchmaking.SetLobbyData(ActualLobbyID, i + "vehicle_" + type, prefab == null ? "NULL" : isDefault + "," + idx);
                    }

                    foreach (var turretPrefab in teamInfo.turretPrefab)
                    {
                        var type = turretPrefab.Key;
                        var prefab = turretPrefab.Value;

                        bool isDefault = true; // Default turret.
                        int idx = Array.IndexOf(ActorManager.instance.defaultTurretPrefabs, prefab);

                        if (idx == -1)
                        {
                            isDefault = false;
                            var moddedTurrets = ModManager.AllTurretPrefabs().ToList();
                            moddedTurrets.Sort((x, y) => x.name.CompareTo(y.name));
                            idx = moddedTurrets.IndexOf(prefab);
                        }

                        SteamMatchmaking.SetLobbyData(ActualLobbyID, i + "turret_" + type, prefab == null ? "NULL" : isDefault + "," + idx);
                    }

                    SteamMatchmaking.SetLobbyData(ActualLobbyID, i + "skin", InstantActionMaps.instance.skinDropdowns[i].value.ToString());
                }

                var enabledMutators = new List<int>();
                ModManager.instance.loadedMutators.Sort((x, y) => x.name.CompareTo(y.name));
                foreach (var mutator in ModManager.instance.loadedMutators)
                {
                    if (!mutator.isEnabled)
                        continue;

                    int id = mutator.name.GetHashCode();
                    enabledMutators.Add(id);

                    var config = new List<string>();
                    foreach (var item in mutator.configuration.GetAllFields())
                    {
                        config.Add(item.SerializeValue());
                    }
                    SteamMatchmaking.SetLobbyData(ActualLobbyID, id + "config", string.Join(",", config.ToArray()));
                }
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "mutators", string.Join(",", enabledMutators.ToArray()));
            }
            else if (SteamMatchmaking.GetLobbyMemberData(ActualLobbyID, SteamUser.GetSteamID(), "loaded") == "yes")
            {
                InstantActionMaps.instance.gameModeDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "gameMode"));
                InstantActionMaps.instance.nightToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "nightMode"));
                InstantActionMaps.instance.playerHasAllWeaponsToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "playerHasAllWeapons"));
                InstantActionMaps.instance.reverseToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "reverseMode"));
                InstantActionMaps.instance.configFlagsToggle.isOn = false;
                InstantActionMaps.instance.botNumberField.text = SteamMatchmaking.GetLobbyData(ActualLobbyID, "botNumberField");
                InstantActionMaps.instance.balanceSlider.value = float.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "balance"));
                InstantActionMaps.instance.respawnTimeField.text = SteamMatchmaking.GetLobbyData(ActualLobbyID, "respawnTime");
                InstantActionMaps.instance.gameLengthDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "gameLength"));
                // For SpecOps.
                if (InstantActionMaps.instance.gameModeDropdown.value == 1)
                {
                    InstantActionMaps.instance.teamDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "team"));
                }
                int givenEntry = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "loadedLevelEntry"));

                if (givenEntry == customMapOptionIndex)
                {
                    string mapName = SteamMatchmaking.GetLobbyData(ActualLobbyID, "customMap");

                    if (InstantActionMaps.instance.mapDropdown.value != customMapOptionIndex || entries[customMapOptionIndex].name != mapName)
                    {
                        foreach (var mod in ModManager.instance.GetActiveMods())
                        {
                            foreach (var map in mod.content.GetMaps())
                            {
                                string currentName = string.Empty;
                                string[] parts = map.Name.Split('.');
                                for (int i = 0; i < parts.Length - 1; i++)
                                {
                                    currentName += parts[i];
                                }
                                if (currentName == mapName)
                                {
                                    InstantActionMaps.MapEntry entry = new InstantActionMaps.MapEntry
                                    {
                                        name = currentName,
                                        sceneName = map.FullName,
                                        isCustomMap = true,
                                        hasLoadedMetaData = true,
                                        image = mod.content.HasIconImage()
                                                ? Sprite.Create(mod.iconTexture, new Rect(0f, 0f, mod.iconTexture.width, mod.iconTexture.height), Vector2.zero, 100f)
                                                : null,
                                        suggestedBots = 0,
                                    };
                                    InstantActionMaps.SelectedCustomMapEntry(entry);
                                }
                            }
                        }
                    }
                }
                else
                {
                    InstantActionMaps.instance.mapDropdown.value = givenEntry;
                }

                for (int i = 0; i < 2; i++)
                {
                    var teamInfo = GameManager.instance.gameInfo.team[i];

                    teamInfo.availableWeapons.Clear();
                    string[] weapons = SteamMatchmaking.GetLobbyData(ActualLobbyID, i + "weapons").Split(',');
                    foreach (string weapon_str in weapons)
                    {
                        if (weapon_str == string.Empty)
                            continue;
                        int hash = int.Parse(weapon_str);
                        var weapon = NetActorController.GetWeaponEntryByHash(hash);
                        teamInfo.availableWeapons.Add(weapon);
                    }

                    bool changedVehicles = false;
                    foreach (var vehicleType in (VehicleSpawner.VehicleSpawnType[])Enum.GetValues(typeof(VehicleSpawner.VehicleSpawnType)))
                    {
                        var type = vehicleType;
                        var prefab = teamInfo.vehiclePrefab[type];

                        var targetPrefab = SteamMatchmaking.GetLobbyData(ActualLobbyID, i + "vehicle_" + type);

                        GameObject newPrefab = null;
                        if (targetPrefab != "NULL")
                        {
                            string[] args = targetPrefab.Split(',');
                            bool isDefault = bool.Parse(args[0]);
                            int idx = int.Parse(args[1]);

                            if (isDefault)
                            {
                                newPrefab = ActorManager.instance.defaultVehiclePrefabs[idx];
                            }
                            else
                            {
                                var moddedVehicles = ModManager.AllVehiclePrefabs().ToList();
                                newPrefab = moddedVehicles[idx];
                            }
                        }

                        if (prefab != newPrefab)
                            changedVehicles = true;

                        teamInfo.vehiclePrefab[type] = newPrefab;
                    }

                    bool changedTurrets = false;
                    foreach (var turretType in (TurretSpawner.TurretSpawnType[])Enum.GetValues(typeof(TurretSpawner.TurretSpawnType)))
                    {
                        var type = turretType;
                        var prefab = teamInfo.turretPrefab[type];

                        var targetPrefab = SteamMatchmaking.GetLobbyData(ActualLobbyID, i + "turret_" + type);

                        GameObject newPrefab = null;
                        if (targetPrefab != "NULL")
                        {
                            string[] args = targetPrefab.Split(',');
                            bool isDefault = bool.Parse(args[0]);
                            int idx = int.Parse(args[1]);

                            if (isDefault)
                            {
                                newPrefab = ActorManager.instance.defaultTurretPrefabs[idx];
                            }
                            else
                            {
                                var moddedTurrets = ModManager.AllTurretPrefabs().ToList();
                                newPrefab = moddedTurrets[idx];
                            }
                        }

                        if (prefab != newPrefab)
                            changedTurrets = true;

                        teamInfo.turretPrefab[type] = newPrefab;
                    }

                    if (changedVehicles || changedTurrets)
                        GamePreview.UpdatePreview();

                    InstantActionMaps.instance.skinDropdowns[i].value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, i + "skin"));
                }

                string[] enabledMutators = SteamMatchmaking.GetLobbyData(ActualLobbyID, "mutators").Split(',');
                ModManager.instance.loadedMutators.Sort((x, y) => x.name.CompareTo(y.name));
                foreach (var mutator in ModManager.instance.loadedMutators)
                    mutator.isEnabled = false;
                foreach (var mutatorStr in enabledMutators)
                {
                    if (mutatorStr == string.Empty)
                        continue;

                    int id = int.Parse(mutatorStr);

                    foreach (var mutator in ModManager.instance.loadedMutators)
                    {
                        int candidate = mutator.name.GetHashCode();
                        if (id == candidate)
                        {
                            mutator.isEnabled = true;

                            string[] config = SteamMatchmaking.GetLobbyData(ActualLobbyID, candidate + "config").Split(',');
                            int i = 0;
                            foreach (var item in mutator.configuration.GetAllFields())
                            {
                                item.DeserializeValue(config[i]);
                                i++;
                            }
                        }
                    }
                }

                if (SteamMatchmaking.GetLobbyData(ActualLobbyID, "started") == "yes")
                {
                    StartAsClient();
                }
            }
        }

        private void OnGUI()
        {
            if (GameManager.instance == null || (GameManager.IsIngame() && LoadoutUi.instance != null && LoadoutUi.HasBeenClosed()))
                return;

            var menu_page = (int)typeof(MainMenu).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MainMenu.instance);

            if (menu_page != MainMenu.PAGE_INSTANT_ACTION)
                return;

            var lobbyStyle = new GUIStyle(GUI.skin.box);
            lobbyStyle.normal.background = LobbyBackground;

            if (GameManager.IsInMainMenu() && NotificationText != string.Empty)
            {
                GUILayout.BeginArea(new Rect((Screen.width - 250f) / 2f, (Screen.height - 200f) / 2f, 250f, 200f), string.Empty);
                GUILayout.BeginVertical(lobbyStyle);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("<color=red>RavenM Message:</color>");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(7f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(NotificationText);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(15f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("OK"))
                    NotificationText = string.Empty;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            if (!InLobby && GUIStack.Count != 0 && GameManager.IsInMainMenu())
            {
                GUILayout.BeginArea(new Rect(10f, 10f, 150f, 10000f), string.Empty);
                GUILayout.BeginVertical(lobbyStyle);

                if (GUIStack.Peek() == "Main")
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"RavenM");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(15f);

                    if (GUILayout.Button("HOST"))
                        GUIStack.Push("Host");

                    GUILayout.Space(5f);

                    if (GUILayout.Button("JOIN"))
                        GUIStack.Push("Join");
                }
                else if (GUIStack.Peek() == "Host")
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"HOST");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(5f);

                    GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                            GUILayout.Label($"MEMBER LIMIT: ");
                            LobbyMemberCap = GUILayout.TextField(LobbyMemberCap);
                        GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    // Ensure we are working with a valid (positive) integer.
                    for (int i = LobbyMemberCap.Length - 1; i >= 0; i--)
                        if (LobbyMemberCap[i] < '0' || LobbyMemberCap[i] > '9')
                            LobbyMemberCap = LobbyMemberCap.Remove(i, 1);

                    // Trim to max 3 characters.
                    if (LobbyMemberCap.Length > 3)
                        LobbyMemberCap = LobbyMemberCap.Remove(3);

                    PrivateLobby = GUILayout.Toggle(PrivateLobby, "FRIENDS ONLY");

                    ShowOnList = GUILayout.Toggle(ShowOnList, "SHOW ON LOBBY\nLIST");

                    MidgameJoin = GUILayout.Toggle(MidgameJoin, "JOINABLE\nMIDGAME");

                    nameTagsEnabled = GUILayout.Toggle(nameTagsEnabled, "NAMETAGS");

                    nameTagsForTeamOnly = GUILayout.Toggle(nameTagsForTeamOnly, "FOR TEAM ONLY");


                    GUILayout.Space(10f);

                    if (GUILayout.Button("START"))
                    {
                        // No friends?
                        if (LobbyMemberCap.Length == 0 || int.Parse(LobbyMemberCap) < 2)
                            LobbyMemberCap = "2";

                        // Maximum possible allowed by steam.
                        if (int.Parse(LobbyMemberCap) > 250)
                            LobbyMemberCap = "250";

                        SteamMatchmaking.CreateLobby(PrivateLobby ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePublic, int.Parse(LobbyMemberCap));
                        InLobby = true;
                        IsLobbyOwner = true;
                        LobbyDataReady = false;
                    }

                    GUILayout.Space(3f);

                    if (GUILayout.Button("<color=#888888>BACK</color>"))
                        GUIStack.Pop();
                }
                else if (GUIStack.Peek() == "Join")
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"JOIN");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10f);

                    if (GUILayout.Button("BROWSE"))
                    {
                        OpenLobbies.Clear();
                        SteamMatchmaking.RequestLobbyList();
                        GUIStack.Push("Browse");
                    }

                    GUILayout.Space(5f);

                    if (GUILayout.Button("DIRECT CONNECT"))
                        GUIStack.Push("Direct");

                    GUILayout.Space(3f);

                    if (GUILayout.Button("<color=#888888>BACK</color>"))
                        GUIStack.Pop();
                }
                else if (GUIStack.Peek() == "Direct")
                {
                    GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                            GUILayout.Label($"DIRECT CONNECT");
                        GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10f);

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"LOBBY ID");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    JoinLobbyID = GUILayout.TextField(JoinLobbyID);

                    GUILayout.Space(15f);

                    if (GUILayout.Button("START"))
                    {
                        if (uint.TryParse(JoinLobbyID, out uint idLong))
                        {
                            CSteamID lobbyId = new CSteamID(new AccountID_t(idLong), (uint)EChatSteamIDInstanceFlags.k_EChatInstanceFlagLobby | (uint)EChatSteamIDInstanceFlags.k_EChatInstanceFlagMMSLobby, EUniverse.k_EUniversePublic, EAccountType.k_EAccountTypeChat);
                            SteamMatchmaking.JoinLobby(lobbyId);
                            InLobby = true;
                            IsLobbyOwner = false;
                            LobbyDataReady = false;
                        }
                    }

                    GUILayout.Space(3f);

                    if (GUILayout.Button("<color=#888888>BACK</color>"))
                        GUIStack.Pop();
                }
                else if (GUIStack.Peek() == "Browse")
                {
                    GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                            GUILayout.Label($"BROWSE");
                        GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10f);

                    if (GUILayout.Button("REFRESH"))
                    {
                        OpenLobbies.Clear();
                        SteamMatchmaking.RequestLobbyList();
                    }

                    GUILayout.Space(10f);

                    GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                            GUILayout.Label($"LOBBIES - ({OpenLobbies.Count})");
                        GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    foreach (var lobby in OpenLobbies)
                    {
                        var owner = SteamMatchmaking.GetLobbyData(lobby, "owner");

                        bool hasData = false;
                        string name = "<color=#777777>Loading...</color>";
                        if (owner != string.Empty)
                        {
                            var ownerId = new CSteamID(ulong.Parse(owner));
                            hasData = !SteamFriends.RequestUserInformation(ownerId, true);
                            if (hasData)
                            {
                                name = SteamFriends.GetFriendPersonaName(ownerId);
                                if (name.Length > 10)
                                {
                                    name = name.Substring(0, 10) + "...";
                                }
                                name += $" - ({SteamMatchmaking.GetNumLobbyMembers(lobby)}/{SteamMatchmaking.GetLobbyMemberLimit(lobby)})";
                            }  
                        }

                        if (GUILayout.Button($"{name}") && hasData)
                        {
                            LobbyView = lobby;
                            GUIStack.Push("Lobby View");
                        }
                    }

                    GUILayout.Space(3f);

                    if (GUILayout.Button("<color=#888888>BACK</color>"))
                        GUIStack.Pop();
                }
                else if (GUIStack.Peek() == "Lobby View")
                {
                    var owner = new CSteamID(ulong.Parse(SteamMatchmaking.GetLobbyData(LobbyView, "owner")));
                    var name = SteamFriends.GetFriendPersonaName(owner);

                    GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                            GUILayout.Label($"{name}'s");
                        GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                            GUILayout.Label($"LOBBY");
                        GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10f);

                    if (GUILayout.Button("REFRESH"))
                    {
                        SteamMatchmaking.RequestLobbyData(LobbyView);
                    }

                    GUILayout.Space(10f);

                    GUILayout.Label($"MEMBERS: {SteamMatchmaking.GetNumLobbyMembers(LobbyView)}/{SteamMatchmaking.GetLobbyMemberLimit(LobbyView)}");

                    var modList = SteamMatchmaking.GetLobbyData(LobbyView, "mods");
                    var modCount = modList != string.Empty ? modList.Split(',').Length : 0;
                    GUILayout.Label($"MODS: {modCount}");

                    GUILayout.Label($"BOTS: {SteamMatchmaking.GetLobbyData(LobbyView, "botNumberField")}");

                    var map = SteamMatchmaking.GetLobbyData(LobbyView, "customMap");
                    map = map != string.Empty ? map : "Default";
                    GUILayout.Label($"MAP: {map}");

                    var status = SteamMatchmaking.GetLobbyData(LobbyView, "started") == "yes" ? "<color=green>In-game</color>" : "Configuring";
                    GUILayout.Label($"STATUS: {status}");

                    GUILayout.Space(10f);

                    if (Plugin.BuildGUID != SteamMatchmaking.GetLobbyData(LobbyView, "build_id"))
                    {
                        GUILayout.Label("<color=red>This lobby is running on a different version of RavenM!</color>");
                    }
                    else if (ModManager.instance.noContentMods && modCount > 0)
                    {
                        GUILayout.Label("<color=red>Lobby is using mods! Please run Ravenfield with mods enabled to join.</color>");
                    }
                    else if (GUILayout.Button("JOIN"))
                    {
                        SteamMatchmaking.JoinLobby(LobbyView);
                        InLobby = true;
                        IsLobbyOwner = false;
                        LobbyDataReady = false;
                    }

                    GUILayout.Space(3f);

                    if (GUILayout.Button("<color=#888888>BACK</color>"))
                        GUIStack.Pop();
                }

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            if (InLobby && LobbyDataReady)
            {
                GUILayout.BeginArea(new Rect(10f, 10f, 150f, 10000f), string.Empty);
                GUILayout.BeginVertical(lobbyStyle);

                var members = GetLobbyMembers();
                int len = members.Count;

                if (GameManager.IsInMainMenu() && GUILayout.Button("<color=red>LEAVE</color>"))
                {
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    LobbyDataReady = false;
                    InLobby = false;
                }

                GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                        GUILayout.Label($"LOBBY - {len}/{SteamMatchmaking.GetLobbyMemberLimit(ActualLobbyID)}");
                    GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(5f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(ActualLobbyID.GetAccountID().ToString());
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (GameManager.IsInMainMenu() && GUILayout.Button("COPY ID"))
                {
                    GUIUtility.systemCopyBuffer = ActualLobbyID.GetAccountID().ToString();
                }

                GUILayout.Space(15f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("MEMBERS:");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                for (int i = 0; i < len; i++)
                {
                    var memberId = members[i];

                    string team = SteamMatchmaking.GetLobbyMemberData(ActualLobbyID, memberId, "team");
                    string name = SteamFriends.GetFriendPersonaName(memberId);

                    var readyColor = (GameManager.IsInMainMenu() ? SteamMatchmaking.GetLobbyMemberData(ActualLobbyID, memberId, "loaded") == "yes" 
                                                                    : SteamMatchmaking.GetLobbyMemberData(ActualLobbyID, memberId, "ready") == "yes") 
                                                                    ? "green" : "red";

                    if (memberId != KickPrompt)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Box(team);
                        GUILayout.FlexibleSpace();
                        GUILayout.Box($"<color={readyColor}>{name}</color>");
                        GUILayout.FlexibleSpace();
                        GUILayout.Box(team);
                        GUILayout.EndHorizontal();

                        if (Event.current.type == EventType.Repaint
                            && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)
                            && Input.GetMouseButtonUp(1)
                            && IsLobbyOwner
                            && memberId != SteamUser.GetSteamID())
                        {
                            KickPrompt = memberId;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button($"<color=red>KICK {name}</color>"))
                        {
                            SendLobbyChat($"/kick {memberId}");
                            CurrentKickedMembers.Add(memberId);
                            foreach (var connection in IngameNetManager.instance.ServerConnections)
                            {
                                if (SteamNetworkingSockets.GetConnectionInfo(connection, out SteamNetConnectionInfo_t pInfo) && pInfo.m_identityRemote.GetSteamID() == memberId)
                                {
                                    SteamNetworkingSockets.CloseConnection(connection, 0, null, false);
                                }
                            }
                        }

                        if (Event.current.type == EventType.Repaint
                            && !GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)
                            && Input.GetMouseButtonDown(0) || Input.GetMouseButton(1))
                            KickPrompt = CSteamID.Nil;
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            if (ModsToDownload.Count > 0)
            {
                GUILayout.BeginArea(new Rect(160f, 10f, 150f, 10000f), string.Empty);
                GUILayout.BeginVertical(lobbyStyle);

                int hasDownloaded = ServerMods.Count - ModsToDownload.Count;

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("DOWNLOADING");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("MODS:");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(5f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{hasDownloaded}/{ServerMods.Count}");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (SteamUGC.GetItemDownloadInfo(new PublishedFileId_t(ModsToDownload[0].m_PublishedFileId), out ulong punBytesDownloaded, out ulong punBytesTotal))
                {
                    GUILayout.Space(5f);

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{punBytesDownloaded / 1024}KB/{punBytesTotal / 1024}KB");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(5f);

                    GUIStyle progressStyle = new GUIStyle();
                    progressStyle.normal.background = ProgressTexture;

                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(progressStyle);
                    GUILayout.Box(ProgressTexture);
                    GUILayout.EndVertical();
                    GUILayout.Space((float)(punBytesTotal - punBytesDownloaded) / punBytesTotal * 150f);
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(15f);

                if (GUILayout.Button("<color=red>CANCEL</color>"))
                {
                    if (InLobby)
                    {
                        SteamMatchmaking.LeaveLobby(ActualLobbyID);
                        LobbyDataReady = false;
                        InLobby = false;
                    }
                    ModsToDownload.Clear();
                }

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }
    }
}