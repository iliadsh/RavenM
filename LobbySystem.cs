using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Steamworks;
using HarmonyLib;

namespace RavenM
{
    [HarmonyPatch(typeof(InstantActionMaps), nameof(InstantActionMaps.StartGame))]
    public class OnStartPatch
    {
        static bool Prefix()
        {
            if (LobbySystem.instance.InLobby && !LobbySystem.instance.IsLobbyOwner && !LobbySystem.instance.ReadyToPlay)
                return false;

            return true;
        }

        static void Postfix(InstantActionMaps __instance)
        {
            if (!LobbySystem.instance.LobbyDataReady)
                return;

            if (!LobbySystem.instance.IsLobbyOwner)
                return;
            
            // TODO: This is not a protocol. This is a single byte.
            SteamMatchmaking.SendLobbyChatMsg(LobbySystem.instance.ActualLobbyID, new byte[] { 1 }, 1);
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
            if (LobbySystem.instance.InLobby)
                return false;

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

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
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
            }
            else
            {
                OwnerID = new CSteamID(ulong.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "owner")));
                Plugin.logger.LogInfo($"Host ID: {OwnerID}");

                MainMenu.instance.OpenPageIndex(MainMenu.PAGE_INSTANT_ACTION);
                ReadyToPlay = false;
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

            if (OwnerID != user)
                return;

            if (len == 1)
            {
                ReadyToPlay = true;
                //No initial bots! Many errors otherwise!
                InstantActionMaps.instance.botNumberField.text = "0";
                InstantActionMaps.instance.StartGame();
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
            if (IsLobbyOwner)
            {
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "gameMode", InstantActionMaps.instance.gameModeDropdown.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "nightMode", InstantActionMaps.instance.nightToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "playerHasAllWeapons", InstantActionMaps.instance.playerHasAllWeaponsToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "reverseMode", InstantActionMaps.instance.reverseToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "configFlags", InstantActionMaps.instance.configFlagsToggle.isOn.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "botNumberField", InstantActionMaps.instance.botNumberField.text);
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "balance", InstantActionMaps.instance.balanceSlider.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "respawnTime", InstantActionMaps.instance.respawnTimeField.text);
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "gameLength", InstantActionMaps.instance.gameLengthDropdown.value.ToString());
                SteamMatchmaking.SetLobbyData(ActualLobbyID, "loadedLevelEntry", InstantActionMaps.instance.mapDropdown.value.ToString());
            }
            else
            {
                InstantActionMaps.instance.gameModeDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "gameMode"));
                InstantActionMaps.instance.nightToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "nightMode"));
                InstantActionMaps.instance.playerHasAllWeaponsToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "playerHasAllWeapons"));
                InstantActionMaps.instance.reverseToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "reverseMode"));
                InstantActionMaps.instance.configFlagsToggle.isOn = bool.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "configFlags"));
                InstantActionMaps.instance.botNumberField.text = SteamMatchmaking.GetLobbyData(ActualLobbyID, "botNumberField");
                InstantActionMaps.instance.balanceSlider.value = float.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "balance"));
                InstantActionMaps.instance.respawnTimeField.text = SteamMatchmaking.GetLobbyData(ActualLobbyID, "respawnTime");
                InstantActionMaps.instance.gameLengthDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "gameLength"));
                InstantActionMaps.instance.mapDropdown.value = int.Parse(SteamMatchmaking.GetLobbyData(ActualLobbyID, "loadedLevelEntry"));
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

                    GUI.Box(new Rect(25f, 90f + i * 30, 110f, 20f), name);
                }

                if (GUI.Button(new Rect(25f, 90f + len * 30, 110f, 20f), "Leave"))
                {
                    SteamMatchmaking.LeaveLobby(ActualLobbyID);
                    LobbyDataReady = false;
                    InLobby = false;
                }
            }
        }
    }
}
