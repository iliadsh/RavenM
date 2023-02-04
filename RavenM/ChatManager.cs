using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;
using Steamworks;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UnityEngine;
using Ravenfield.SpecOps;
using RavenM.Commands;

namespace RavenM
{
    /// <summary>
    /// Handles the display and backend for the Chat menu 
    /// </summary>
    public class ChatManager : MonoBehaviour
    {
        private string _currentChatMessage = string.Empty;
        public string CurrentChatMessage
        {
            get { return _currentChatMessage; }
            set { _currentChatMessage = value; }
        }
        private string _fullChatLink = string.Empty;
        public string FullChatLink
        {
            get { return _fullChatLink; }
            set { _fullChatLink = value; }
        }
        private Vector2 _chatScrollPosition = Vector2.zero;
        public Vector2 ChatScrollPosition
        {
            get { return _chatScrollPosition; }
            set { _chatScrollPosition = value; }
        }
        private Texture2D _greyBackground = new Texture2D(1, 1);
        public Texture2D GreyBackground
        {
            get { return _greyBackground; }
            set { _greyBackground = value; }
        }
        private bool _justFocused = false;
        public bool JustFocused
        {
            get { return _justFocused; }
            set { _justFocused = value; }
        }
        private bool _typeIntention = false;
        public bool TypeIntention
        {
            get { return _typeIntention; }
            set { _typeIntention = value; }
        }
        private bool _chatMode = false;
        public bool ChatMode
        {
            get { return _chatMode; }
            set { _chatMode = value; }
        }
        private CommandManager _commandManager;
        public CommandManager CommandManager
        {
            get { return _commandManager; }
            set { _commandManager = value; }
        }
        private KeyCode _globalChatKeybind = KeyCode.Y;
        public KeyCode GlobalChatKeybind
        {
            get { return _globalChatKeybind; }
            set { _globalChatKeybind = value; }
        }
        private KeyCode _teamChatKeybind = KeyCode.U;
        public KeyCode TeamChatKeybind
        {
            get { return _teamChatKeybind; }
            set { _teamChatKeybind = value; }
        }
        private CSteamID _steamId;
        public CSteamID SteamId
        {
            get { return _steamId; }
            set { _steamId = value; }
        }
        private string _steamUsername;
        public string SteamUsername
        {
            get { return _steamUsername; }
            private set { _steamUsername = value; }
        }

        public static ChatManager instance;

        private void Awake()
        {
            instance = this;

            GreyBackground.SetPixel(0, 0, Color.grey * 0.3f);
            GreyBackground.Apply();

            CommandManager = new CommandManager();

            SteamId = SteamUser.GetSteamID();
        }

        private void Start()
        {
            Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
            Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
        }

        public void PushChatMessage(Actor actor, string message, bool global, int team)
        {
            string name;
            if (actor != null)
                name = actor.name;
            else
                name = "";
            if (!global && GameManager.PlayerTeam() != team)
                return;

            if (team == -1)
                FullChatLink += $"<color=#eeeeee>{message}</color>\n";
            else
            {
                string color = !global ? "green" : (team == 0 ? "blue" : "red");
                FullChatLink += $"<color={color}><b><{name}></b></color> {message}\n";
                RSPatch.RavenscriptEventsManagerPatch.events.onReceiveChatMessage.Invoke(actor, message);
            }

            _chatScrollPosition.y = Mathf.Infinity;
        }

        private void OnPersonaStateChange(PersonaStateChange_t pCallback)
        {
            if (SteamId == (CSteamID)pCallback.m_ulSteamID)
            {
                SteamUsername = SteamFriends.GetFriendPersonaName(SteamId);
            }
        }

        public void PushLobbyChatMessage(string message)
        {
            string name;
            if (SteamUsername != null)
                name = SteamUsername;
            else
                name = "";

            // Players have no team in lobby so everyone is the same color
            string color = "white";
            FullChatLink += $"<color={color}><b><{name}></b></color> {message}\n";

            _chatScrollPosition.y = Mathf.Infinity;
        }

        public void PushLobbyCommandChatMessage(string message, Color color, bool teamOnly, bool sendToAll)
        {
            FullChatLink += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{message}</b></color>\n";
            _chatScrollPosition.y = Mathf.Infinity;
            if (!sendToAll)
                return;
            SendLobbyChat(message);
        }

        public void PushCommandChatMessage(string message, Color color, bool teamOnly, bool sendToAll)
        {
            FullChatLink += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{message}</b></color>\n";
            _chatScrollPosition.y = Mathf.Infinity;
            if (!sendToAll)
                return;
            using MemoryStream memoryStream = new MemoryStream();
            var chatPacket = new ChatPacket
            {
                Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                Message = message,
                TeamOnly = teamOnly,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(chatPacket);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
        }

        public void ProccessLobbyChatCommand(string message, ulong id, bool local)
        {
            string[] command = message.Trim().Substring(1, message.Length - 1).Split(' ');
            string initCommand = command[0];
            if (string.IsNullOrEmpty(initCommand))
            {
                return;
            }
            if (!CommandManager.ContainsCommand(initCommand))
            {
                PushLobbyCommandChatMessage($"No command with the name {initCommand} found.\nFor more information use Command /help", Color.red, false, false);
                return;
            }
            Command cmd = CommandManager.GetCommandFromName(initCommand);
            if (cmd == null)
            {
                Plugin.logger.LogError($"Command {initCommand} is not a registered Command!");
                return;
            }
            bool hasCommandPermission = CommandManager.HasPermission(cmd, id, local);
            // For Commands like /help that have reqArgs[0] = null we don't have to check for arguments
            bool hasRequiredArgs = true;
            if (cmd.reqArgs[0] != null)
                hasRequiredArgs = CommandManager.HasRequiredArgs(cmd, command);
            switch (cmd.CommandName)
            {
                case "nametags":

                    if (!local)
                    {
                        UI.GameUI.instance.ToggleNameTags();
                        PushLobbyCommandChatMessage("Set nametags to " + command[1], Color.green, false, false);
                        return;
                    }
                    if (!hasCommandPermission || !hasRequiredArgs)
                        return;
                    bool parsedArg = bool.TryParse(command[1], out bool result);
                    if (parsedArg)
                    {
                        SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "nameTags", result.ToString());
                        PushLobbyCommandChatMessage("Set nameTags to " + result.ToString(), Color.green, false, false);
                        UI.GameUI.instance.ToggleNameTags();
                    }

                    break;
                case "nametagsteamonly":

                    if (!local)
                    {
                        UI.GameUI.instance.ToggleNameTags();
                        PushLobbyCommandChatMessage("Set nameTags for Team only to " + command[1], Color.green, false, false);
                        return;
                    }
                    if (!hasCommandPermission || !hasRequiredArgs)
                        return;
                    bool parsedArg2 = bool.TryParse(command[1], out bool result2);
                    if (parsedArg2)
                    {
                        SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "nameTagsForTeamOnly", result2.ToString());
                        PushLobbyCommandChatMessage("Set nameTags for Team only to " + result2.ToString(), Color.green, false, false);
                        UI.GameUI.instance.ToggleNameTags();
                    }

                    break;
                case "help":
                    foreach (Command availableCommand in CommandManager.GetAllCommands())
                    {
                        PushLobbyCommandChatMessage($"{CommandManager.GetRequiredArgTypes(availableCommand)} Host Only: {availableCommand.HostOnly}", availableCommand.Scripted ? Color.green : Color.yellow, true, false);
                    }
                    break;
                default:
                    // TODO: Allow other mods to handle commands from the lobby
                    Plugin.logger.LogInfo("onReceiveCommand " + initCommand);
                    break;
            }

            if (!cmd.Global == local)
                return;

            SendLobbyChat(message);
        }

        public void ProcessChatCommand(string message, Actor actor, ulong id, bool local)
        {
            string[] command = message.Trim().Substring(1, message.Length - 1).Split(' ');
            string initCommand = command[0];
            if (string.IsNullOrEmpty(initCommand))
            {
                return;
            }
            if (!CommandManager.ContainsCommand(initCommand))
            {
                PushCommandChatMessage($"No Command with name {initCommand} found.\nFor more information use Command /help", Color.red, false, false);
                return;
            }
            Command cmd = CommandManager.GetCommandFromName(initCommand);
            if (cmd == null)
            {
                Plugin.logger.LogError($"Command {initCommand} is not a registered Command!");
                return;
            }
            bool hasCommandPermission = CommandManager.HasPermission(cmd, id, local);
            // For Commands like /help that have reqArgs[0] = null we don't have to check for arguments
            bool hasRequiredArgs = true;
            if (cmd.reqArgs[0] != null)
                hasRequiredArgs = CommandManager.HasRequiredArgs(cmd, command);
            switch (cmd.CommandName)
            {
                case "nametags":

                    if (!local)
                    {
                        UI.GameUI.instance.ToggleNameTags();
                        PushCommandChatMessage("Set nametags to " + command[1], Color.green, false, false);
                        return;
                    }
                    if (!hasCommandPermission || !hasRequiredArgs)
                        return;
                    bool parsedArg = bool.TryParse(command[1], out bool result);
                    if (parsedArg)
                    {
                        SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "nameTags", result.ToString());
                        PushCommandChatMessage("Set nameTags to " + result.ToString(), Color.green, false, false);
                        UI.GameUI.instance.ToggleNameTags();
                    }

                    break;
                case "nametagsteamonly":

                    if (!local)
                    {
                        UI.GameUI.instance.ToggleNameTags();
                        PushCommandChatMessage("Set nameTags for Team only to " + command[1], Color.green, false, false);
                        return;
                    }
                    if (!hasCommandPermission || !hasRequiredArgs)
                        return;
                    bool parsedArg2 = bool.TryParse(command[1], out bool result2);
                    if (parsedArg2)
                    {
                        SteamMatchmaking.SetLobbyData(LobbySystem.instance.ActualLobbyID, "nameTagsForTeamOnly", result2.ToString());
                        PushCommandChatMessage("Set nameTags for Team only to " + result2.ToString(), Color.green, false, false);
                        UI.GameUI.instance.ToggleNameTags();
                    }

                    break;
                case "help":
                    foreach (Command availableCommand in CommandManager.GetAllCommands())
                    {
                        PushCommandChatMessage($"{CommandManager.GetRequiredArgTypes(availableCommand)} Host Only: {availableCommand.HostOnly}", availableCommand.Scripted ? Color.green : Color.yellow, true, false);
                    }
                    break;
                case "kill":
                    if (!hasCommandPermission || !hasRequiredArgs)
                        return;
                    string target = command[1];
                    Actor targetActor = CommandManager.GetActorByName(target);
                    if (targetActor == null)
                    {
                        return;
                    }
                    targetActor.Kill(new DamageInfo(DamageInfo.DamageSourceType.FallDamage, actor, null));
                    PushCommandChatMessage($"Killed actor {targetActor.name}", Color.green, false, false);
                    break;
                default:
                    // If it's not build in command pass it to RS
                    Plugin.logger.LogInfo("onReceiveCommand " + initCommand);
                    RSPatch.RavenscriptEventsManagerPatch.events.onReceiveCommand.Invoke(actor, command, new bool[] { hasCommandPermission, hasRequiredArgs, local });
                    break;
            }

            if (!cmd.Global == local)
                return;

            using MemoryStream memoryStream = new MemoryStream();
            var chatCommandPacket = new ChatCommandPacket
            {
                Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                SteamID = SteamId.m_SteamID,
                Command = CurrentChatMessage,
                Scripted = cmd.Scripted,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(chatCommandPacket);
            }
            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.ChatCommand, Constants.k_nSteamNetworkingSend_Reliable);
        }

        private void OnLobbyChatMessage(LobbyChatMsg_t pCallback)
        {
            var buf = new byte[4096];
            int len = SteamMatchmaking.GetLobbyChatEntry(LobbySystem.instance.ActualLobbyID, (int)pCallback.m_iChatID, out CSteamID user, buf, buf.Length, out EChatEntryType chatType);
            string chat = DecodeLobbyChat(buf, len);

            if (chat.StartsWith("/") && user == LobbySystem.instance.OwnerID)
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
                        if (member == SteamId)
                        {
                            LobbySystem.instance.NotificationText = "You were kicked from the lobby! You can no longer join this lobby.";
                            SteamMatchmaking.LeaveLobby(LobbySystem.instance.ActualLobbyID);
                        }
                        else if (!LobbySystem.instance.CurrentKickedMembers.Contains(member))
                        {
                            LobbySystem.instance.CurrentKickedMembers.Add(member);
                        }
                    }
                }
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
        {
            // Anything other than a join...
            if ((pCallback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) == 0)
            {
                var id = new CSteamID(pCallback.m_ulSteamIDUserChanged);

                // ...means the owner left.
                if (LobbySystem.instance.OwnerID == id)
                {
                    LobbySystem.instance.NotificationText = "Lobby closed by host.";
                    SteamMatchmaking.LeaveLobby(LobbySystem.instance.ActualLobbyID);
                }
            }
            else
            {
                var id = new CSteamID(pCallback.m_ulSteamIDUserChanged);

                if (LobbySystem.instance.CurrentKickedMembers.Contains(id))
                {
                    SendLobbyChat($"/kick {id}");
                }
            }
        }

        public void SendLobbyChat(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            SteamMatchmaking.SendLobbyChatMsg(LobbySystem.instance.ActualLobbyID, bytes, bytes.Length);
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

        public void CreateChatArea(bool isLobbyChat = false)
        {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.None && JustFocused)
            {
                Event.current.Use();
                JustFocused = false;
                return;
            }

            if (Event.current.isKey && (Event.current.keyCode == KeyCode.Tab || Event.current.character == '\t'))
                Event.current.Use();

            if (TypeIntention)
            {
                GUI.SetNextControlName("chat");
                CurrentChatMessage = GUI.TextField(new Rect(10f, Screen.height - 160f, 500f, 25f), CurrentChatMessage);
                GUI.FocusControl("chat");

                string color = !ChatMode ? "green" : (GameManager.PlayerTeam() == 0 ? "blue" : "red");
                string text = ChatMode ? "GLOBAL" : "TEAM";
                GUI.Label(new Rect(510f, Screen.height - 160f, 70f, 25f), $"<color={color}><b>{text}</b></color>");

                if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape && TypeIntention)
                {
                    TypeIntention = false;
                }

                if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                {
                    if (!string.IsNullOrEmpty(CurrentChatMessage))
                    {
                        bool isCommand = CurrentChatMessage.Trim().StartsWith("/") ? true : false;
                        if (isCommand)
                        {
                            if (isLobbyChat)
                            {
                                ProccessLobbyChatCommand(CurrentChatMessage, SteamId.m_SteamID, true);
                            }
                            else
                            {
                                ProcessChatCommand(CurrentChatMessage, ActorManager.instance.player, SteamId.m_SteamID, true);
                            }

                            CurrentChatMessage = string.Empty;
                        }
                        else
                        {
                            if (isLobbyChat)
                            {
                                PushLobbyChatMessage(CurrentChatMessage);
                                SendLobbyChat(CurrentChatMessage);
                            }
                            else
                            {
                                PushChatMessage(ActorManager.instance.player, CurrentChatMessage, ChatMode, GameManager.PlayerTeam());

                                using MemoryStream memoryStream = new MemoryStream();
                                var chatPacket = new ChatPacket
                                {
                                    Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                                    Message = CurrentChatMessage,
                                    TeamOnly = !ChatMode,
                                };

                                using (var writer = new ProtocolWriter(memoryStream))
                                {
                                    writer.Write(chatPacket);
                                }
                                byte[] data = memoryStream.ToArray();

                                IngameNetManager.instance.SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
                            }

                            CurrentChatMessage = string.Empty;
                        }
                    }
                    TypeIntention = false;
                }
            }

            if (Event.current.isKey && Event.current.keyCode == GlobalChatKeybind && !TypeIntention)
            {
                TypeIntention = true;
                JustFocused = true;
                ChatMode = true;
            }

            if (Event.current.isKey && Event.current.keyCode == TeamChatKeybind && !TypeIntention)
            {
                TypeIntention = true;
                JustFocused = true;
                ChatMode = false;
            }

            var chatStyle = new GUIStyle();
            chatStyle.normal.background = GreyBackground;
            GUILayout.BeginArea(new Rect(10f, Screen.height - 370f, 500f, 200f), string.Empty, chatStyle);
            ChatScrollPosition = GUILayout.BeginScrollView(ChatScrollPosition, GUILayout.Width(500f), GUILayout.Height(200f));
            // Any player can break the formatting by using Rich Text e.g. <color=abcd> <b> - Chai
            GUILayout.Label(FullChatLink);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
