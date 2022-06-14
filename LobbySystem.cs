using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Steamworks;
using HarmonyLib;
using System;

namespace RavenM
{
    [HarmonyPatch(typeof(InstantActionMaps), nameof(InstantActionMaps.StartGame))]
    public class OnStartPatch
    {
        static bool Prefix()
        {
            if (LobbySystem.instance.InLobby && !LobbySystem.instance.IsLobbyOwner && !LobbySystem.instance.ReadyToPlay)
                return false;

            // Only start if all members are ready.
            if (LobbySystem.instance.LobbyDataReady && LobbySystem.instance.IsLobbyOwner)
            {
                int len = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);

                for (int i = 0; i < len; i++)
                {
                    var memberId = SteamMatchmaking.GetLobbyMemberByIndex(LobbySystem.instance.ActualLobbyID, i);
                    if (SteamMatchmaking.GetLobbyData(LobbySystem.instance.ActualLobbyID, "ready_" + memberId) != "yes")
                    {
                        return false;
                    }
                }
            }

            if (LobbySystem.instance.IsLobbyOwner)
            {
                SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "freeze", "true");

                IngameNetManager.instance.OpenRelay();
                // TODO: This is not a protocol. This is a single byte.
                SteamMatchmaking.SendLobbyChatMsg(LobbySystem.instance.ActualLobbyID, new byte[] { 1 }, 1);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GameManager), "StartGame")]
    public class FinalizeStartPatch
    {
        static void Postfix()
        {
            if (!LobbySystem.instance.LobbyDataReady)
                return;

            if (LobbySystem.instance.IsLobbyOwner)
                IngameNetManager.instance.StartAsServer();
            else
            {
                IngameNetManager.instance.StartAsClient(LobbySystem.instance.OwnerID);
            }

            SteamMatchmaking.LeaveLobby(LobbySystem.instance.ActualLobbyID);
            LobbySystem.instance.InLobby = false;
            LobbySystem.instance.LobbyDataReady = false;
            LobbySystem.instance.ReadyToPlay = true;
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

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.FinalizeLoadedModContent))]
    public class AfterModsLoadedPatch
    {
        static void Postfix()
        {
            if (!LobbySystem.instance.InLobby || !LobbySystem.instance.LobbyDataReady || LobbySystem.instance.IsLobbyOwner || LobbySystem.instance.ModsToDownload.Count > 0)
                return;

            ModManager.instance.ContentChanged();
            // TODO
            SteamMatchmaking.SendLobbyChatMsg(LobbySystem.instance.ActualLobbyID, new byte[] { 1, 2 }, 2);

            // We need to update the skin dropdown with the new mods.
            typeof(InstantActionMaps).GetMethod("SetupSkinList", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(InstantActionMaps.instance, null);
        }
    }

    [HarmonyPatch(typeof(SteamMatchmaking), nameof(SteamMatchmaking.LeaveLobby))]
    public class OnLobbyLeavePatch
    {
        static void Postfix()
        {
            if (LobbySystem.instance.IsLobbyOwner)
                return;

            // Reconstruct mods
            foreach (var mod in ModManager.instance.mods)
            {
                mod.enabled = LobbySystem.instance.ModsWeOwn.Contains(mod.workshopItemId);
            }
            LobbySystem.instance.ModsWeOwn.Clear();
        }
    }

    public class LobbySystem : MonoBehaviour
    {
        public static LobbySystem instance;

        public bool PrivateLobby = false;

        public string JoinLobbyID = string.Empty;

        public bool InLobby = false;

        public bool LobbyDataReady = false;

        public CSteamID ActualLobbyID = CSteamID.Nil;

        public bool IsLobbyOwner = false;

        // FIXME: A stack is maybe overkill. There are only 3 menu states.
        public Stack<string> GUIStack = new Stack<string>();

        public CSteamID OwnerID = CSteamID.Nil;

        public bool ReadyToPlay = false;

        public List<PublishedFileId_t> ServerMods = new List<PublishedFileId_t>();

        public List<PublishedFileId_t> ModsToDownload = new List<PublishedFileId_t>();

        public List<PublishedFileId_t> ModsWeOwn = new List<PublishedFileId_t>();

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
            Callback<DownloadItemResult_t>.Create(OnItemDownload);
        }

        private void OnLobbyEnter(LobbyEnter_t pCallback)
        {
            Plugin.logger.LogInfo("Joined lobby!");

            if (pCallback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                InLobby = false;
                return;
            }

            LobbyDataReady = true;
            ActualLobbyID = new CSteamID(pCallback.m_ulSteamIDLobby);;

            if (IsLobbyOwner)
            {
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "owner", SteamUser.GetSteamID().ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "build_id", Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString());

                List<PublishedFileId_t> mods = new List<PublishedFileId_t>();
                foreach (var mod in ModManager.instance.GetActiveMods())
                {
                    mods.Add(mod.workshopItemId);
                }
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "mods", string.Join(",", mods.ToArray()));
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "ready_" + SteamUser.GetSteamID(), "yes");
            }
            else
            {
                OwnerID = new CSteamID(ulong.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "owner")));
                Plugin.logger.LogInfo($"Host ID: {OwnerID}");

                MainMenu.instance.OpenPageIndex(MainMenu.PAGE_INSTANT_ACTION);
                ReadyToPlay = false;

                if (Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString() != SteamMatchmaking.GetLobbyData(ActualLobbyID, "build_id"))
                {
                    Plugin.logger.LogInfo("Build ID mismatch! Leaving lobby.");
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    InLobby = false;
                    ReadyToPlay = true;
                    LobbyDataReady = false;
                    return;
                }

                ModsWeOwn.Clear();
                foreach (var mod in ModManager.instance.mods)
                {
                    if (mod.enabled)
                    {
                        mod.enabled = false;
                        ModsWeOwn.Add(mod.workshopItemId);
                    }
                }

                ServerMods.Clear();
                ModsToDownload.Clear();
                string[] mods = SteamMatchmaking.GetLobbyData(ActualLobbyID, "mods").Split(',');
                foreach (string mod_str in mods)
                {
                    if (mod_str == string.Empty)
                        continue;
                    PublishedFileId_t mod_id = new PublishedFileId_t(ulong.Parse(mod_str));

                    ServerMods.Add(mod_id);

                    bool alreadyHasMod = false;
                    foreach (var mod in ModManager.instance.mods)
                    {
                        if (mod.workshopItemId == mod_id)
                        {
                            alreadyHasMod = true;
                            mod.enabled = true;
                        }
                    }

                    if (!alreadyHasMod)
                    {
                        ModsToDownload.Add(mod_id);
                        bool isDownloading = SteamUGC.DownloadItem(mod_id, true);
                        Plugin.logger.LogInfo($"Downloading mod with id: {mod_id} -- {isDownloading}");
                    }
                }
                TriggerModRefresh();
            }
        }

        private void OnItemDownload(DownloadItemResult_t pCallback)
        {
            Plugin.logger.LogInfo($"Downloaded mod! {pCallback.m_nPublishedFileId}");
            var mod = (ModInformation)typeof(ModManager).GetMethod("AddWorkshopItemAsMod", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ModManager.instance, new object[] { pCallback.m_nPublishedFileId });
            mod.enabled = true;
            ModsToDownload.Remove(pCallback.m_nPublishedFileId);

            TriggerModRefresh();
        }

        public void TriggerModRefresh()
        {
            if (ModsToDownload.Count == 0)
            {
                Plugin.logger.LogInfo($"All server mods downloaded.");
                ModManager.instance.ReloadModContent();
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
        {
            // Anything other than a join...
            if ((pCallback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) == 0)
            {
                // ...means the owner left.
                if (OwnerID == new CSteamID(pCallback.m_ulSteamIDUserChanged))
                {
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    InLobby = false;
                    ReadyToPlay = true;
                    // Keep the lobby data as available, so that we can still join the match
                    // connection if needed.
                }
            }
        }

        private void OnLobbyChatMessage(LobbyChatMsg_t pCallback)
        {
            var buf = new byte[4096];
            int len = SteamMatchmaking.GetLobbyChatEntry(ActualLobbyID, (int)pCallback.m_iChatID, out CSteamID user, buf, buf.Length, out EChatEntryType chatType);

            if (SteamUser.GetSteamID() == user)
                return;

            if (OwnerID != user && !IsLobbyOwner)
                return;

            if (len == 1)
            {
                ReadyToPlay = true;
                //No initial bots! Many errors otherwise!
                InstantActionMaps.instance.botNumberField.text = "0";
                InstantActionMaps.instance.StartGame();
            }
            // WTF am I doing. MAKE THIS A REAL PROTOCOL!
            else if (len == 2)
            {
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "ready_" + user, "yes");
            }
        }

        private void Update()
        {
            if (GameManager.instance == null || GameManager.IsIngame())
                return;

            if (Input.GetKeyDown(KeyCode.M) && !InLobby)
            {
                if (GUIStack.Count == 0)
                    GUIStack.Push("Main");
                else
                    GUIStack.Clear();
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
            InstantActionMaps.instance.gameModeDropdown.value = 0;
            int customMapOptionIndex = (int)typeof(InstantActionMaps).GetField("customMapOptionIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(InstantActionMaps.instance);
            var entries = (List<InstantActionMaps.MapEntry>)typeof(InstantActionMaps).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(InstantActionMaps.instance);
            if (IsLobbyOwner)
            {
                // SteamMatchmaking.SetLobbyData(ActualLobbyID, "gameMode", InstantActionMaps.instance.gameModeDropdown.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "nightMode", InstantActionMaps.instance.nightToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "playerHasAllWeapons", InstantActionMaps.instance.playerHasAllWeaponsToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "reverseMode", InstantActionMaps.instance.reverseToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "botNumberField", InstantActionMaps.instance.botNumberField.text);
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "balance", InstantActionMaps.instance.balanceSlider.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "respawnTime", InstantActionMaps.instance.respawnTimeField.text);
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "gameLength", InstantActionMaps.instance.gameLengthDropdown.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "loadedLevelEntry", InstantActionMaps.instance.mapDropdown.value.ToString());

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
                            idx = ModManager.instance.vehiclePrefabs[type].IndexOf(prefab);
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
                            idx = ModManager.instance.turretPrefabs[type].IndexOf(prefab);
                        }

                        SteamMatchmaking.SetLobbyData(ActualLobbyID, i + "turret_" + type, prefab == null ? "NULL" : isDefault + "," + idx);
                    }

                    SteamMatchmaking.SetLobbyData(ActualLobbyID, i + "skin", InstantActionMaps.instance.skinDropdowns[i].value.ToString());
                }
            }
            else if (SteamMatchmaking.GetLobbyData(ActualLobbyID, "freeze") != "true")
            {
                // InstantActionMaps.instance.gameModeDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "gameMode"));
                InstantActionMaps.instance.nightToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "nightMode"));
                InstantActionMaps.instance.playerHasAllWeaponsToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "playerHasAllWeapons"));
                InstantActionMaps.instance.reverseToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "reverseMode"));
                InstantActionMaps.instance.configFlagsToggle.isOn = false;
                InstantActionMaps.instance.botNumberField.text = SteamMatchmaking.GetLobbyData(ActualLobbyID, "botNumberField");
                InstantActionMaps.instance.balanceSlider.value = float.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "balance"));
                InstantActionMaps.instance.respawnTimeField.text = SteamMatchmaking.GetLobbyData(ActualLobbyID, "respawnTime");
                InstantActionMaps.instance.gameLengthDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "gameLength"));
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
                                newPrefab = ModManager.instance.vehiclePrefabs[type][idx];
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
                                newPrefab = ModManager.instance.turretPrefabs[type][idx];
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
            }
        }

        private void OnGUI()
        {
            if (GameManager.instance == null || GameManager.IsIngame())
                return;

            var menu_page = (int)typeof(MainMenu).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MainMenu.instance);

            if (menu_page != MainMenu.PAGE_INSTANT_ACTION)
                return;

            if (!InLobby && GUIStack.Count != 0)
            {
                if (GUIStack.Peek() == "Main")
                {
                    GUI.Box(new Rect(10f, 10f, 100f, 90f), "RavenM");
                    if (GUI.Button(new Rect(20f, 40f, 80f, 20f), "Host"))
                    {
                        GUIStack.Push("Host");
                    }
                    if (GUI.Button(new Rect(20f, 70f, 80f, 20f), "Join"))
                    {
                        GUIStack.Push("Join");
                    }
                }
                else if (GUIStack.Peek() == "Host")
                {
                    GUI.Box(new Rect(10f, 10f, 130f, 90f), "Host");
                    PrivateLobby = GUI.Toggle(new Rect(20f, 40f, 110f, 20f), PrivateLobby, "Friends Only");
                    if (GUI.Button(new Rect(30f, 70f, 80f, 20f), "Start"))
                    {
                        SteamMatchmaking.CreateLobby(PrivateLobby ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePublic, 20);
                        InLobby = true;
                        IsLobbyOwner = true;
                        LobbyDataReady = false;
                    }
                }
                else if (GUIStack.Peek() == "Join")
                {
                    GUI.Box(new Rect(10f, 10f, 130f, 120f), "Join");
                    GUI.Label(new Rect(20f, 40f, 110f, 20f), "Lobby ID");
                    JoinLobbyID = GUI.TextField(new Rect(20f, 65f, 110f, 20f), JoinLobbyID);
                    if (GUI.Button(new Rect(30f, 100f, 80f, 20f), "Start"))
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
                }
            }

            if (InLobby && LobbyDataReady)
            {
                int len = SteamMatchmaking.GetNumLobbyMembers(ActualLobbyID);

                GUI.Box(new Rect(10f, 10f, 150f, 120f + len * 30), $"Lobby - {len}/20");
                GUI.Label(new Rect(35f, 40f, 130f, 20f), ActualLobbyID.GetAccountID().ToString());

                if (GUI.Button(new Rect(25f, 60f, 110f, 20f), "Copy ID"))
                {
                    GUIUtility.systemCopyBuffer = ActualLobbyID.GetAccountID().ToString();
                }

                for (int i = 0; i < len; i++)
                {
                    var memberId = SteamMatchmaking.GetLobbyMemberByIndex(ActualLobbyID, i);

                    string name = SteamFriends.GetFriendPersonaName(memberId);

                    var old_color = GUI.contentColor;
                    GUI.contentColor = SteamMatchmaking.GetLobbyData(ActualLobbyID, "ready_" + memberId) == "yes" ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
                    GUI.Box(new Rect(25f, 90f + i * 30, 110f, 20f), name);
                    GUI.contentColor = old_color;
                }

                if (GUI.Button(new Rect(25f, 90f + len * 30, 110f, 20f), "Leave"))
                {
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    LobbyDataReady = false;
                    InLobby = false;
                }

                if (ModsToDownload.Count > 0)
                {
                    int hasDownloaded = ServerMods.Count - ModsToDownload.Count;

                    // Really needs work.
                    GUI.Box(new Rect(10, 130f + len * 30, 150f, 60f), $"Downloading Mods\n\n   {hasDownloaded}/{ServerMods.Count}");
                }
            }
        }
    }
}
