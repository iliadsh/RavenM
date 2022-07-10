using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Steamworks;
using HarmonyLib;
using System;
using System.Text;

namespace RavenM
{
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartLevel))]
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
                LobbySystem.instance.SendLobbyChat(":start");

                // Reset the ready states for when everyone has actually loaded in.
                int len = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);

                for (int i = 0; i < len; i++)
                {
                    var memberId = SteamMatchmaking.GetLobbyMemberByIndex(LobbySystem.instance.ActualLobbyID, i);
                    SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "ready_" + memberId, "no");
                }

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
                // Wait for everyone to load in first.
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
                if (vehicle.TryGetComponent(out Vehicle _) && !vehicle.TryGetComponent(out PrefabTag _) && !Array.Exists(ignore, x => x == vehicle))
                {
                    Plugin.logger.LogInfo($"Detected map vehicle with name: {vehicle.name}, and from map: {map.name}.");

                    var tag = vehicle.gameObject.AddComponent<PrefabTag>();
                    tag.NameHash = vehicle.name.GetHashCode();
                    tag.Mod = (ulong)map.name.GetHashCode();
                    IngameNetManager.PrefabCache[new Tuple<int, ulong>(tag.NameHash, tag.Mod)] = vehicle.gameObject;
                }
            }
        }

        static void Postfix()
        {
            if (!LobbySystem.instance.LobbyDataReady)
                return;
            
            if (LobbySystem.instance.IsLobbyOwner)
            {
                IngameNetManager.instance.StartAsServer();
                SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "ready_" + SteamUser.GetSteamID(), "yes");
            }  
            else
            {
                IngameNetManager.instance.StartAsClient(LobbySystem.instance.OwnerID);
                LobbySystem.instance.SendLobbyChat(":ready");
            }
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
            
            LobbySystem.instance.SendLobbyChat(":ready");
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
        static void Postfix()
        {
            if (LobbySystem.instance.InLobby 
                && LobbySystem.instance.IsLobbyOwner 
                && !(bool)typeof(GameManager).GetField("restartArmed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GameManager.instance))
            {
                SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "freeze", "false");
                SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "started", "false");
            }
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

        public bool LoadedServerMods = false;

        public bool RequestModReload = false;

        public Texture2D LobbyBackground = new Texture2D(1, 1);

        public Texture2D ProgressTexture = new Texture2D(1, 1);

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
        }

        private void OnLobbyEnter(LobbyEnter_t pCallback)
        {
            Plugin.logger.LogInfo("Joined lobby!");
            RequestModReload = false;
            LoadedServerMods = false;

            if (pCallback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                InLobby = false;
                return;
            }

            LobbyDataReady = true;
            ActualLobbyID = new CSteamID(pCallback.m_ulSteamIDLobby);;

            if (IsLobbyOwner)
            {
                OwnerID = SteamUser.GetSteamID();
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "owner", OwnerID.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "build_id", Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString());

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
                
                if (SteamMatchmaking.GetLobbyData(ActualLobbyID, "started") == "yes")
                {
                    Plugin.logger.LogInfo("The game has already started :( Leaving lobby.");
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
                // ...means the owner left.
                if (OwnerID == new CSteamID(pCallback.m_ulSteamIDUserChanged))
                {
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
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

            if (SteamUser.GetSteamID() == user)
                return;

            if (OwnerID != user && !IsLobbyOwner)
                return;

            if (chat == ":start")
            {
                ReadyToPlay = true;
                //No initial bots! Many errors otherwise!
                InstantActionMaps.instance.botNumberField.text = "0";
                InstantActionMaps.instance.StartGame();
            }
            else if (chat == ":ready")
            {
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "ready_" + user, "yes");
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

                var enabledMutators = new List<bool>();
                ModManager.instance.loadedMutators.Sort((x, y) => x.name.CompareTo(y.name));
                foreach (var mutator in ModManager.instance.loadedMutators)
                {
                    enabledMutators.Add(mutator.isEnabled);
                }
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "mutators", string.Join(",", enabledMutators.ToArray()));
            }
            else if (SteamMatchmaking.GetLobbyData(ActualLobbyID, "freeze") != "true" && SteamMatchmaking.GetLobbyData(ActualLobbyID, "ready_" + SteamUser.GetSteamID()) == "yes")
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

                string[] enabledMutators = SteamMatchmaking.GetLobbyData(ActualLobbyID, "mutators").Split(',');
                ModManager.instance.loadedMutators.Sort((x, y) => x.name.CompareTo(y.name));
                for (int i = 0; i < ModManager.instance.loadedMutators.Count; i++)
                {
                    if (i > enabledMutators.Length)
                        break;

                    string enabled_str = enabledMutators[i];

                    if (enabled_str == string.Empty)
                        continue;

                    ModManager.instance.loadedMutators[i].isEnabled = bool.Parse(enabled_str);
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

                    PrivateLobby = GUILayout.Toggle(PrivateLobby, "FRIENDS ONLY");

                    GUILayout.Space(10f);

                    if (GUILayout.Button("START"))
                    {
                        SteamMatchmaking.CreateLobby(PrivateLobby ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePublic, 250);
                        InLobby = true;
                        IsLobbyOwner = true;
                        LobbyDataReady = false;
                    }
                }
                else if (GUIStack.Peek() == "Join")
                {
                    GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                            GUILayout.Label($"JOIN");
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
                }

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            if (InLobby && LobbyDataReady)
            {
                GUILayout.BeginArea(new Rect(10f, 10f, 150f, 10000f), string.Empty);
                GUILayout.BeginVertical(lobbyStyle);

                int len = SteamMatchmaking.GetNumLobbyMembers(ActualLobbyID);

                if (GameManager.IsInMainMenu() && GUILayout.Button("<color=red>LEAVE</color>"))
                {
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    LobbyDataReady = false;
                    InLobby = false;
                }

                GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                        GUILayout.Label($"LOBBY - {len}/250");
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
                    var memberId = SteamMatchmaking.GetLobbyMemberByIndex(ActualLobbyID, i);

                    string name = SteamFriends.GetFriendPersonaName(memberId);

                    var readyColor = SteamMatchmaking.GetLobbyData(ActualLobbyID, "ready_" + memberId) == "yes" ? "green" : "red";
                    GUILayout.Box($"<color={readyColor}>{name}</color>");
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
