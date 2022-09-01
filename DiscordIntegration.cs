using System;
using System.Collections;
using RavenM.DiscordGameSDK;
using Steamworks;
using UnityEngine;
using System.Runtime.InteropServices;

namespace RavenM
{
    public class DiscordIntegration : MonoBehaviour
    {
        public static DiscordIntegration instance;
        
        public Discord Discord;

        public long discordClientID = 1007054793220571247;

        public long startSessionTime;

        private ActivityManager _activityManager;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpPathName);

        private void Start()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Environment.Is64BitProcess)
            {
                LoadLibrary("BepInEx/plugins/lib/x86/discord_game_sdk");
            }

            Discord = new Discord(discordClientID, (UInt64) CreateFlags.Default);
            Plugin.logger.LogWarning("Discord Instance created");
            startSessionTime = ((DateTimeOffset) DateTime.Now).ToUnixTimeSeconds();
            
            _activityManager = Discord.GetActivityManager();
            
            StartCoroutine(StartActivities());
            
            _activityManager.OnActivityJoin += secret =>
            {
                secret = secret.Replace("_join", "");
                
                Plugin.logger.LogInfo($"OnJoin {secret}");
                var LobbyID = new CSteamID(ulong.Parse(secret));

                if (_isInGame)
                {
                    GameManager.ReturnToMenu();
                }
                
                SteamMatchmaking.JoinLobby(LobbyID);
                LobbySystem.instance.InLobby = true;
                LobbySystem.instance.IsLobbyOwner = false;
                LobbySystem.instance.LobbyDataReady = false;
            };
            
            _activityManager.OnActivityJoinRequest += (ref User user) =>
            {
                // The Ask to join Button Doesnt even work rn (Discord's fault) try the right click Ask to join button instead
                Plugin.logger.LogInfo($"OnJoinRequest {user.Username} {user.Id}");
            };
        }
        
        IEnumerator StartActivities()
        {
            UpdateActivity(Discord, Activities.InitialActivity);
            yield return new WaitUntil(GameManager.IsInMainMenu);
            UpdateActivity(Discord, Activities.InMenu);
        }

        // Private Variables that makes me question my coding skills
        private TimedAction _timer = new TimedAction(5f);
        
        private string _gameMode = "Insert Game Mode";
        private void FixedUpdate()
        {
           Discord.RunCallbacks();
           
           if (_timer.TrueDone())
           {
               ChangeActivityDynamically();

               _timer.Start();
           }
        }

        private bool _isInGame;
        private bool _isInLobby;

        void ChangeActivityDynamically()
        {
            if (GameManager.instance == null) { return; }

            _isInGame = GameManager.instance.ingame;
            _isInLobby = LobbySystem.instance.InLobby;


            if (_isInGame && !_isInLobby)
            {
                var dropdown = InstantActionMaps.instance.gameModeDropdown;
                _gameMode = dropdown.options[dropdown.value].text;
                UpdateActivity(Discord, Activities.InSinglePlayerGame, true ,_gameMode);
            }
            else if (_isInLobby)
            {
                int currentLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                int currentLobbyMemberCap = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);

                if (!_isInGame) // Waiting in Lobby
                {
                    var dropdown = InstantActionMaps.instance.gameModeDropdown;
                    _gameMode = dropdown.options[dropdown.value].text;
                    UpdateActivity(Discord, Activities.InLobby, false ,_gameMode, currentLobbyMembers, currentLobbyMemberCap, LobbySystem.instance.ActualLobbyID.ToString());
                }
                else // Playing in a Lobby
                {
                    UpdateActivity(Discord, Activities.InLobby, true ,_gameMode, currentLobbyMembers, currentLobbyMemberCap, LobbySystem.instance.ActualLobbyID.ToString());
                }
            }
            else // Left the lobby
            {
                UpdateActivity(Discord, Activities.InMenu);
            }
        }
        
        public void UpdateActivity(Discord discord, Activities activity, bool inGame = false, string gameMode = "None", int currentPlayers = 1, int maxPlayers = 2, string lobbyID = "None")
        {
            var activityManager = discord.GetActivityManager();
            var activityPresence = new Activity();
            
            switch (activity)
            {
                case Activities.InitialActivity:
                    activityPresence = new Activity()
                    {
                        State = "Just Started Playing",
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InMenu:
                    activityPresence = new Activity()
                    {
                        State = "Waiting In Menu",
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InLobby:
                    var state = inGame ? "Plating Multiplayer" : "Waiting In Lobby";
                    activityPresence = new Activity()
                    {
                        State = state,
                        Details = $"Game Mode: {gameMode}",
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Party = {
                            Id = lobbyID,
                            Size = {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Secrets =
                        {
                            Join = lobbyID + "_join",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InSinglePlayerGame:
                    activityPresence = new Activity()
                    {
                        State = "Playing Singleplayer",
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                    
               
            }
            activityManager.UpdateActivity(activityPresence, result =>
            {
                Plugin.logger.LogInfo($"Update Discord Activity {result}");
            });
        }

        public enum Activities
        {
            InitialActivity,
            InMenu,
            InLobby,
            InSinglePlayerGame,
        }
    }
}