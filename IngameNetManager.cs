using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RavenM
{
    /// <summary>
    /// Shut down the connection if we leave the match.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.ReturnToMenu))]
    public class OnExitGamePatch
    {
        static void Postfix()
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            SteamNetworkingSockets.CloseConnection(IngameNetManager.instance.C2SConnection, 0, string.Empty, false);

            if (IngameNetManager.instance.IsHost)
                SteamNetworkingSockets.CloseListenSocket(IngameNetManager.instance.ServerSocket);

            IngameNetManager.instance.ResetState();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RestartLevel))]
    public class RestartPatch
    {
        static bool Prefix()
        {
            if (IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Don't spawn a foreign actor before they wish
    /// to spawn.
    /// </summary>
    [HarmonyPatch(typeof(Actor), nameof(Actor.IsReadyToSpawn))]
    public class SpawnReadyPatch
    {
        static bool Prefix(Actor __instance, ref bool __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            // How???
            if (__instance == null)
            {
                __result = false;
                return false;
            }

            var guid = __instance.GetComponent<GuidComponent>();

            if (guid != null && !IngameNetManager.instance.OwnedActors.Contains(guid.guid))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Mortar), "GetTargetPosition")]
    public class MortarTargetPatch
    {
        // Patch the first conditional jump to an unconditional one. This will skip the
        // block which assumes the Actor is a bot and has an Actor target.
        // FIXME: This means the bots will have garbage aim with the mortar.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool first = true;

            foreach (var instruction in instructions)
            {
                if (first && instruction.opcode == OpCodes.Brtrue)
                {
                    instruction.opcode = OpCodes.Brfalse;
                    first = false;
                }

                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(ActorManager), nameof(ActorManager.CreateAIActor))]
    public class CreateBotPatch
    {
        static bool Prefix(ref Actor __result)
        {
            if (!IngameNetManager.instance.IsClient || IngameNetManager.instance.IsHost)
                return true;

            if (IngameNetManager.instance.ClientCanSpawnBot)
                return true;

            __result = null;
            return false;
        }

        static void Postfix(Actor __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            if (__result == null)
                return;

            if (IngameNetManager.instance.ClientCanSpawnBot)
                return;

            // Smaller integers do better with ProtoBuf's integer compression.
            int id = IngameNetManager.instance.BotIdGen++;

            __result.gameObject.AddComponent<GuidComponent>().guid = id;

            IngameNetManager.instance.ClientActors.Add(id, __result);
            IngameNetManager.instance.OwnedActors.Add(id);
        }
    }

    [HarmonyPatch(typeof(ModManager), "LoadModContentFromObject")]
    public class ModdedPrefabTagPatch
    {
        static void Postfix(ModContentInformation contentInfo)
        {
            // Pretty expensive function, but only when used per-frame.
            // It shouldn't affect mod load performance.
            foreach (var vehicle in Resources.FindObjectsOfTypeAll<Vehicle>())
            {
                var prefab = vehicle.gameObject;

                if (!prefab.TryGetComponent(out PrefabTag _))
                {
                    Plugin.logger.LogInfo($"Detected vehicle prefab with name: {prefab.name}, and from mod: {contentInfo.sourceMod.workshopItemId}");

                    IngameNetManager.TagPrefab(prefab, (ulong)contentInfo.sourceMod.workshopItemId);
                }
            }

            // We also do the projectiles.
            foreach (var vehicle in Resources.FindObjectsOfTypeAll<Projectile>())
            {
                var prefab = vehicle.gameObject;

                if (!prefab.TryGetComponent(out PrefabTag _))
                {
                    Plugin.logger.LogInfo($"Detected projectile prefab with name: {prefab.name}, and from mod: {contentInfo.sourceMod.workshopItemId}");

                    IngameNetManager.TagPrefab(prefab, (ulong)contentInfo.sourceMod.workshopItemId);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ActorManager), "Awake")]
    public class DefaultPrefabsPatch
    {
        static void Postfix(ActorManager __instance)
        {
            foreach (var vehicle in __instance.defaultVehiclePrefabs)
            {
                Plugin.logger.LogInfo($"Tagging default vehicle: {vehicle.name}");

                IngameNetManager.TagPrefab(vehicle);
            }

            foreach (var turret in __instance.defaultTurretPrefabs)
            {
                Plugin.logger.LogInfo($"Tagging default turret: {turret.name}");

                IngameNetManager.TagPrefab(turret);
            }

            foreach (var projectile in Resources.FindObjectsOfTypeAll<Projectile>())
            {
                var prefab = projectile.gameObject;
                Plugin.logger.LogInfo($"Tagging default projectile: {prefab.name}");

                IngameNetManager.TagPrefab(prefab);
            }
        }
    }

    [HarmonyPatch(typeof(Vehicle), "Start")]
    public class VehicleCreatedPatch
    {
        static bool Prefix(Vehicle __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            if (IngameNetManager.instance.IsHost)
            {
                int id = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);

                // The "vehicle" might already be ID'd by also being for ex. a Projectile.
                if (__instance.TryGetComponent(out GuidComponent guid))
                    id = guid.guid;
                else
                    __instance.gameObject.AddComponent<GuidComponent>().guid = id;

                IngameNetManager.instance.ClientVehicles.Add(id, __instance);
                IngameNetManager.instance.OwnedVehicles.Add(id);

                Plugin.logger.LogInfo($"Registered new spawned vehicle with name: {__instance.name} and id: {id}");
            }
            // Again with the Projectile ID BS.
            else if (!__instance.TryGetComponent(out GuidComponent guid) || !IngameNetManager.instance.ClientVehicles.ContainsKey(guid.guid))
            {
                Plugin.logger.LogInfo($"Cleaning up unwanted vehicle with name: {__instance.name}");
                typeof(Vehicle).GetMethod("Cleanup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { });
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Projectile), "StartTravelling")]
    public class ProjectileCreatedPatch
    {
        static bool Prefix(Projectile __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            var sourceId = -1;
            if (__instance.source != null && __instance.source.TryGetComponent(out GuidComponent aguid))
                sourceId = aguid.guid;

            if (IngameNetManager.instance.OwnedActors.Contains(sourceId) || (sourceId == -1 && IngameNetManager.instance.IsHost))
            {
                int id = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);

                if (__instance.TryGetComponent(out GuidComponent guid))
                    id = guid.guid;
                else
                    __instance.gameObject.AddComponent<GuidComponent>().guid = id;

                IngameNetManager.instance.ClientProjectiles.Add(id, __instance);
                IngameNetManager.instance.OwnedProjectiles.Add(id);

                var tag = __instance.gameObject.GetComponent<PrefabTag>();

                if (tag == null)
                {
                    Plugin.logger.LogError($"Projectile {__instance.name} is somehow untagged!");
                    return true;
                }

                using MemoryStream memoryStream = new MemoryStream();
                var spawnPacket = new SpawnProjectilePacket
                {
                    SourceId = sourceId,
                    NameHash = tag.NameHash,
                    Mod = tag.Mod,
                    Position = __instance.transform.position,
                    Rotation = __instance.transform.rotation,
                    performInfantryInitialMuzzleTravel = __instance.performInfantryInitialMuzzleTravel,
                    initialMuzzleTravelDistance  = __instance.initialMuzzleTravelDistance,
                    ProjectileId = id,
                };

                using (var writer = new ProtocolWriter(memoryStream))
                {
                    writer.Write(spawnPacket);
                }
                byte[] data = memoryStream.ToArray();

                IngameNetManager.instance.SendPacketToServer(data, PacketType.SpawnProjectile, Constants.k_nSteamNetworkingSend_Reliable);

                Plugin.logger.LogInfo($"Registered new spawned projectile with name: {__instance.name} and id: {id}");
            }
            else if (!__instance.TryGetComponent(out GuidComponent guid) || !IngameNetManager.instance.ClientProjectiles.ContainsKey(guid.guid))
            {
                Plugin.logger.LogInfo($"Cleaning up unwanted projectile with name: {__instance.name}");
                typeof(Projectile).GetMethod("Cleanup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { false });
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(FpsActorController), "EnablePhotoMode")]
    public class EnablePhotoModePatch
    {
        static void Postfix(FpsActorController __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            __instance.DisableMovement();
        }
    }

    [HarmonyPatch(typeof(FpsActorController), "DisablePhotoMode")]
    public class DisablePhotoModePatch
    {
        static void Postfix(FpsActorController __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            __instance.EnableMovement();
        }
    }

    public class PrefabTag : MonoBehaviour
    {
        public int NameHash;

        public ulong Mod;
    }

    public class IngameNetManager : MonoBehaviour
    {
        public class AudioContainer
        {
            public List<float[]> VoiceQueue = new List<float[]>();

            public bool Buffering = true;

            public float[] CurrentData = null;

            public int SampleIndex = 0;
        }

        public static IngameNetManager instance;

        public float _ticker2 = 0f;
        public int _pps = 0;
        public int _total = 0;
        public int _ppsOut = 0;
        public int _totalOut = 0;
        public int _bytesOut = 0;
        public int _totalBytesOut = 0;

        public bool _showSpecificOutbound = false;
        public Dictionary<PacketType, int> _savedBytesOuts = new Dictionary<PacketType, int>();
        public Dictionary<PacketType, int> _specificBytesOut = new Dictionary<PacketType, int>();

        public int BotIdGen = 0;

        public Guid OwnGUID = Guid.NewGuid();

        public TimedAction MainSendTick = new TimedAction(1.0f / 10);

        public Dictionary<int, int> ActorStateCache = new Dictionary<int, int>();

        public HashSet<int> OwnedActors = new HashSet<int>();

        public Dictionary<int, Actor> ClientActors = new Dictionary<int, Actor>();

        public HashSet<int> OwnedVehicles = new HashSet<int>();

        public Dictionary<int, Vehicle> ClientVehicles = new Dictionary<int, Vehicle>();

        public HashSet<int> RemoteDeadVehicles = new HashSet<int>();

        public Dictionary<int, VehiclePacket> TargetVehicleStates = new Dictionary<int, VehiclePacket>();

        public HashSet<int> OwnedProjectiles = new HashSet<int>();

        public Dictionary<int, Projectile> ClientProjectiles = new Dictionary<int, Projectile>();

        public bool ClientCanSpawnBot = false;

        public HSteamNetConnection C2SConnection;

        /// Server owned
        public HSteamListenSocket ServerSocket;

        public HSteamNetPollGroup PollGroup;

        public List<HSteamNetConnection> ServerConnections = new List<HSteamNetConnection>();

        public Dictionary<HSteamNetConnection, Guid> ConnectionGuidMap = new Dictionary<HSteamNetConnection, Guid>();

        public Dictionary<Guid, List<int>> GuidActorOwnership = new Dictionary<Guid, List<int>>();
        /// Server owned

        public readonly System.Random RandomGen = new System.Random();

        private static readonly int PACKET_SLACK = 256;

        public bool IsHost = false;

        public bool IsClient = false;

        public Texture2D MarkerTexture = new Texture2D(2, 2);

        public Texture2D RightMarker = new Texture2D(2, 2);

        public Texture2D LeftMarker = new Texture2D(2, 2);

        public Vector3 MarkerPosition = Vector3.zero;

        public string CurrentChatMessage = string.Empty;

        public string FullChatLink = string.Empty;

        public Vector2 ChatScrollPosition = Vector2.zero;

        public Texture2D GreyBackground = new Texture2D(1, 1);

        public bool JustFocused = false;

        public bool TypeIntention = false;

        public bool ChatMode = false;

        public bool UsingMicrophone = false;

        public Texture2D MicTexture = new Texture2D(2, 2);

        public Dictionary<int, AudioContainer> PlayVoiceQueue = new Dictionary<int, AudioContainer>();

        public static readonly Dictionary<Tuple<int, ulong>, GameObject> PrefabCache = new Dictionary<Tuple<int, ulong>, GameObject>();


        public Type Steamworks_NativeMethods;

        public MethodInfo SteamAPI_SteamNetworkingMessage_t_Release;

        private void Awake()
        {
            instance = this;

            using var markerResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.marker.png");
            using var resourceMemory = new MemoryStream();
            markerResource.CopyTo(resourceMemory);
            var imageBytes = resourceMemory.ToArray();

            MarkerTexture.LoadImage(imageBytes);

            GreyBackground.SetPixel(0, 0, Color.grey * 0.3f);
            GreyBackground.Apply();

            using var micResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.mic.png");
            resourceMemory.SetLength(0);
            micResource.CopyTo(resourceMemory);
            imageBytes = resourceMemory.ToArray();

            MicTexture.LoadImage(imageBytes);

            using var leftMarkerResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.marker_left.png");
            resourceMemory.SetLength(0);
            leftMarkerResource.CopyTo(resourceMemory);
            imageBytes = resourceMemory.ToArray();

            LeftMarker.LoadImage(imageBytes);

            using var rightMarkerResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.marker_right.png");
            resourceMemory.SetLength(0);
            rightMarkerResource.CopyTo(resourceMemory);
            imageBytes = resourceMemory.ToArray();

            RightMarker.LoadImage(imageBytes);

            Steamworks_NativeMethods = Type.GetType("Steamworks.NativeMethods, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            SteamAPI_SteamNetworkingMessage_t_Release = Steamworks_NativeMethods.GetMethod("SteamAPI_SteamNetworkingMessage_t_Release", BindingFlags.Static | BindingFlags.Public);
        }

        private void Start()
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();

            Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatus);
        }

        private void LateUpdate()
        {
            if (!IsClient)
                return;

            Time.timeScale = 1f;
            Time.fixedDeltaTime = Time.timeScale / 60f;
            GameManager.instance?.sfxMixer?.SetFloat("pitch", Time.timeScale);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7))
                _showSpecificOutbound = !_showSpecificOutbound;

            // AKA Tilde Key.
            if (Input.GetKeyDown(KeyCode.BackQuote)
                && GameManager.instance != null && GameManager.IsIngame()
                && !ActorManager.instance.player.dead
                && ActorManager.instance.player.activeWeapon != null)
            {
                Physics.Raycast(ActorManager.instance.player.activeWeapon.transform.position, ActorManager.instance.player.activeWeapon.transform.forward, out RaycastHit hit, Mathf.Infinity, Physics.AllLayers);

                if (hit.point != null)
                {
                    Plugin.logger.LogInfo($"Placing marker at: {hit.point}");

                    if ((hit.point - MarkerPosition).magnitude > 10)
                        MarkerPosition = hit.point;
                    else
                        MarkerPosition = Vector3.zero;
                }
            }

            SendActorFlags();

            foreach (var kv in TargetVehicleStates)
            {
                int id = kv.Key;
                var vehiclePacket = kv.Value;

                if (!ClientVehicles.ContainsKey(id))
                    continue;

                if (OwnedVehicles.Contains(id))
                    continue;

                var vehicle = ClientVehicles[id];

                if (vehicle == null)
                    continue;

                vehicle.transform.position = Vector3.Lerp(vehicle.transform.position, vehiclePacket.Position, 5f * Time.deltaTime);

                vehicle.transform.rotation = Quaternion.Slerp(vehicle.transform.rotation, vehiclePacket.Rotation, 5f * Time.deltaTime);
            }

            _ticker2 += Time.deltaTime;

            if (_ticker2 > 1)
            {
                _ticker2 = 0;

                _pps = _total;
                _total = 0;

                _ppsOut = _totalOut;
                _totalOut = 0;

                _bytesOut = _totalBytesOut;
                _totalBytesOut = 0;

                _savedBytesOuts = _specificBytesOut.ToDictionary(entry => entry.Key, entry => entry.Value);
                _specificBytesOut.Clear();
            }

            if (Input.GetKeyDown(KeyCode.CapsLock))
            {
                SteamUser.StartVoiceRecording();
                UsingMicrophone = true;
            }

            if (Input.GetKeyUp(KeyCode.CapsLock))
            {
                SteamUser.StopVoiceRecording();
                UsingMicrophone = false;
            }

            SendVoiceData();
        }

        public static void TagPrefab(GameObject prefab, ulong mod = 0)
        {
            var tag = prefab.gameObject.AddComponent<PrefabTag>();
            tag.NameHash = prefab.name.GetHashCode();
            tag.Mod = mod;
            PrefabCache[new Tuple<int, ulong>(tag.NameHash, tag.Mod)] = prefab;
        }

        private void DrawMarker(Vector3 worldPos)
        {
            if (worldPos != Vector3.zero)
            {
                var camera = FpsActorController.instance.inPhotoMode ? SpectatorCamera.instance.camera : FpsActorController.instance.GetActiveCamera();
                Vector3 vector = camera.WorldToScreenPoint(worldPos);

                if (vector.z > 0.5f)
                    if (vector.x >= 0 && vector.x < Screen.width)
                        GUI.DrawTexture(new Rect(vector.x - 15f, Screen.height - vector.y, 30f, 30f), MarkerTexture);
                    else if (vector.x > Screen.width / 2)
                        GUI.DrawTexture(new Rect(Screen.width - 60f, Mathf.Clamp(Screen.height - vector.y, 0, Screen.height - 50f), 50f, 50f), RightMarker);
                    else
                        GUI.DrawTexture(new Rect(10f, Mathf.Clamp(Screen.height - vector.y, 0, Screen.height - 50f), 50f, 50f), LeftMarker);
                else
                    if (Vector3.Dot(camera.transform.right, worldPos - camera.transform.position) < 0)
                    GUI.DrawTexture(new Rect(10f, 0f, 50f, 50f), LeftMarker);
                else
                    GUI.DrawTexture(new Rect(Screen.width - 60f, 0f, 50f, 50f), RightMarker);
            }
        }

        private void OnGUI()
        {
            if (!IsClient)
                return;

            GUI.Label(new Rect(10, 30, 200, 40), $"Inbound: {_pps} PPS");
            GUI.Label(new Rect(10, 50, 200, 40), $"Outbound: {_ppsOut} PPS -- {_bytesOut} Bytes");

            SteamNetworkingSockets.GetQuickConnectionStatus(C2SConnection, out SteamNetworkingQuickConnectionStatus pStats);
            GUI.Label(new Rect(10, 80, 200, 40), $"Ping: {pStats.m_nPing} ms");

            if (_showSpecificOutbound)
            {
                var ordered = _savedBytesOuts.OrderBy(x => -x.Value).ToDictionary(x => x.Key, x => x.Value);
                int i = 0;
                foreach (var kv in ordered)
                {
                    GUI.Label(new Rect(10, 110 + i * 30, 200, 40), $"{kv.Key} - {kv.Value}B");
                    i++;
                }
            }

            DrawMarker(MarkerPosition);

            foreach (var kv in ClientActors)
            {
                var id = kv.Key;
                var actor = kv.Value;

                if (OwnedActors.Contains(id))
                    continue;

                var controller = actor.controller as NetActorController;

                if ((controller.Flags & (int)ActorStateFlags.AiControlled) != 0)
                    continue;

                if (FpsActorController.instance == null)
                    continue;

                if (actor.team != GameManager.PlayerTeam())
                    continue;

                DrawMarker(controller.Targets.MarkerPosition ?? Vector3.zero);

                var camera = FpsActorController.instance.inPhotoMode ? SpectatorCamera.instance.camera : FpsActorController.instance.GetActiveCamera();
                Vector3 vector = camera.WorldToScreenPoint(actor.CenterPosition() + new Vector3(0, 1f, 0));

                if (vector.z < 0f)
                    continue;

                var nameStyle = new GUIStyle();
                nameStyle.normal.background = GreyBackground;
                GUILayout.BeginArea(new Rect(vector.x - 50f, Screen.height - vector.y, 110f, 20f), string.Empty);
                GUILayout.BeginHorizontal(nameStyle);
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                GUILayout.Label(actor.name);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

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
                        PushChatMessage(ActorManager.instance.player.name, CurrentChatMessage, ChatMode, GameManager.PlayerTeam());

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

                        SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);

                        CurrentChatMessage = string.Empty;
                    }
                    TypeIntention = false;
                }
            }

            if (Event.current.isKey && Event.current.keyCode == KeyCode.Y && !TypeIntention)
            {
                TypeIntention = true;
                JustFocused = true;
                ChatMode = true;
            }

            if (Event.current.isKey && Event.current.keyCode == KeyCode.U && !TypeIntention)
            {
                TypeIntention = true;
                JustFocused = true;
                ChatMode = false;
            }

            var chatStyle = new GUIStyle();
            chatStyle.normal.background = GreyBackground;
            GUILayout.BeginArea(new Rect(10f, Screen.height - 370f, 500f, 200f), string.Empty, chatStyle);
            ChatScrollPosition = GUILayout.BeginScrollView(ChatScrollPosition, GUILayout.Width(500f), GUILayout.Height(200f));
            GUILayout.Label(FullChatLink);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (UsingMicrophone)
                GUI.DrawTexture(new Rect(315f, Screen.height - 60f, 50f, 50f), MicTexture);
        }

        public void PushChatMessage(string name, string message, bool global, int team)
        {
            if (!global && GameManager.PlayerTeam() != team)
                return;

            if (team == -1)
                FullChatLink += $"<color=#eeeeee>{message}</color>";
            else
            {
                string color = !global ? "green" : (team == 0 ? "blue" : "red");

                FullChatLink += $"<color={color}><b><{name}></b></color> {message}\n";
            }
            
            ChatScrollPosition.y = Mathf.Infinity;
        }

        public void ResetState()
        {
            _ticker2 = 0f;
            _pps = 0;
            _total = 0;
            _ppsOut = 0;
            _totalOut = 0;
            _bytesOut = 0;
            _totalBytesOut = 0;

            BotIdGen = 0;

            MainSendTick.Start();

            ActorStateCache.Clear();
            OwnedActors.Clear();
            ClientActors.Clear();
            OwnedVehicles.Clear();
            ClientVehicles.Clear();
            RemoteDeadVehicles.Clear();
            TargetVehicleStates.Clear();
            OwnedProjectiles.Clear();
            ClientProjectiles.Clear();

            ClientCanSpawnBot = false;

            IsHost = false;

            IsClient = false;

            MarkerPosition = Vector3.zero;

            CurrentChatMessage = string.Empty;
            FullChatLink = string.Empty;
            ChatScrollPosition = Vector2.zero;
            JustFocused = false;
            TypeIntention = false;
            ChatMode = false;

            UsingMicrophone = false;
            PlayVoiceQueue.Clear();

            ReleaseProjectilePatch.ConfigCache.Clear();
        }

        public void OpenRelay()
        {
            Plugin.logger.LogInfo("Starting server socket for connections.");

            ServerConnections.Clear();
            ConnectionGuidMap.Clear();
            GuidActorOwnership.Clear();
            ServerSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);

            PollGroup = SteamNetworkingSockets.CreatePollGroup();

            IsHost = true;
        }

        public void StartAsServer()
        {
            Plugin.logger.LogInfo("Starting server and client.");

            IsHost = true;

            IsClient = true;

            foreach (var actor in FindObjectsOfType<Actor>())
            {
                int id = actor.aiControlled ? BotIdGen++ : RandomGen.Next(13337, int.MaxValue);

                actor.gameObject.AddComponent<GuidComponent>().guid = id;

                ClientActors.Add(id, actor);
                OwnedActors.Add(id);
            }

            var iden = new SteamNetworkingIdentity
            {
                m_eType = ESteamNetworkingIdentityType.k_ESteamNetworkingIdentityType_SteamID,
            };

            iden.SetSteamID(SteamUser.GetSteamID());

            C2SConnection = SteamNetworkingSockets.ConnectP2P(ref iden, 0, 0, null);
        }

        public void StartAsClient(CSteamID host)
        {
            Plugin.logger.LogInfo("Starting client.");

            IsClient = true;

            var player = ActorManager.instance.player;
            {
                int id = RandomGen.Next(13337, int.MaxValue);

                player.gameObject.AddComponent<GuidComponent>().guid = id;

                ClientActors.Add(id, player);
                OwnedActors.Add(id);
            }

            var iden = new SteamNetworkingIdentity
            {
                m_eType = ESteamNetworkingIdentityType.k_ESteamNetworkingIdentityType_SteamID,
            };

            iden.SetSteamID(host);

            StartCoroutine(RepeatTryConnect(iden));
        }

        IEnumerator RepeatTryConnect(SteamNetworkingIdentity iden)
        {
            for (int i = 0; i < 30; i++)
            {
                Plugin.logger.LogInfo($"Attempting connection... {i + 1}/30");

                // Set the initial connection timeout to 2 minutes, for slow hosts.
                SteamNetworkingConfigValue_t timeout = new SteamNetworkingConfigValue_t
                {
                    m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                    m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                    m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 2 * 60 * 1000 },
                };

                C2SConnection = SteamNetworkingSockets.ConnectP2P(ref iden, 0, 1, new SteamNetworkingConfigValue_t[] { timeout });

                if (C2SConnection != HSteamNetConnection.Invalid)
                    yield break;

                yield return new WaitForSeconds(0.5f);
            }

            GameManager.ReturnToMenu();
        }

        public void SendPacketToServer(byte[] data, PacketType type, int send_flags)
        {
            _totalOut++;

            using MemoryStream compressOut = new MemoryStream();
            using (DeflateStream deflateStream = new DeflateStream(compressOut, CompressionLevel.Optimal))
            {
                deflateStream.Write(data, 0, data.Length);
            }
            byte[] compressed = compressOut.ToArray();

            using MemoryStream packetStream = new MemoryStream();
            Packet packet = new Packet
            {
                Id = type,
                sender = OwnGUID,
                data = compressed
            };

            using (var writer = new ProtocolWriter(packetStream))
            {
                writer.Write(packet);
            }
            byte[] packet_data = packetStream.ToArray();

            _totalBytesOut += packet_data.Length;

            if (_specificBytesOut.ContainsKey(type))
                _specificBytesOut[type] += packet_data.Length;
            else
                _specificBytesOut[type] = packet_data.Length;

            // This is safe. We are only pinning the array.
            unsafe
            {
                fixed (byte* p_msg = packet_data)
                {
                    var res = SteamNetworkingSockets.SendMessageToConnection(C2SConnection, (IntPtr)p_msg, (uint)packet_data.Length, send_flags, out long num);
                    if (res != EResult.k_EResultOK)
                        Plugin.logger.LogError($"Packet failed to send: {res}");
                }
            }
        }

        private void OnConnectionStatus(SteamNetConnectionStatusChangedCallback_t pCallback)
        {
            var info = pCallback.m_info;

            // Callback while server
            if (info.m_hListenSocket != HSteamListenSocket.Invalid)
            {
                switch (info.m_eState)
                {
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                        Plugin.logger.LogInfo($"Connection request from: {info.m_identityRemote.GetSteamID()}");

                        bool inLobby = false;
                        int len = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                        for (int i = 0; i < len; i++)
                        {
                            if (info.m_identityRemote.GetSteamID() == SteamMatchmaking.GetLobbyMemberByIndex(LobbySystem.instance.ActualLobbyID, i))
                            {
                                inLobby = true;
                                break;
                            }
                        }

                        if (!inLobby)
                        {
                            SteamNetworkingSockets.CloseConnection(pCallback.m_hConn, 0, null, false);
                            Plugin.logger.LogError("This user is not part of the lobby! Rejecting the connection.");
                            break;
                        }

                        if (SteamNetworkingSockets.AcceptConnection(pCallback.m_hConn) != EResult.k_EResultOK)
                        {
                            SteamNetworkingSockets.CloseConnection(pCallback.m_hConn, 0, null, false);
                            Plugin.logger.LogError("Failed to accept connection");
                            break;
                        }

                        ServerConnections.Add(pCallback.m_hConn);

                        SteamNetworkingSockets.SetConnectionPollGroup(pCallback.m_hConn, PollGroup);

                        // Unsafe just for the ptr to int.
                        // We are increasing the send buffer size for each connection.
                        unsafe
                        {
                            int _2mb = 2097152;

                            SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize,
                                ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection,
                                (IntPtr)pCallback.m_hConn.m_HSteamNetConnection, ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                                (IntPtr)(&_2mb));
                        }
                        Plugin.logger.LogInfo("Accepted the connection");
                        break;

                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                        Plugin.logger.LogInfo($"Killing connection from {info.m_identityRemote.GetSteamID()}.");
                        SteamNetworkingSockets.CloseConnection(pCallback.m_hConn, 0, null, false);

                        // We take ownership of and kill all the actors that were left behind.
                        if (ServerConnections.Contains(pCallback.m_hConn))
                        {
                            ServerConnections.Remove(pCallback.m_hConn);

                            if (!ConnectionGuidMap.ContainsKey(pCallback.m_hConn))
                                break;

                            var guid = ConnectionGuidMap[pCallback.m_hConn];

                            if (!GuidActorOwnership.ContainsKey(guid))
                                break;

                            var actors = GuidActorOwnership[guid];

                            foreach (var id in actors)
                            {
                                if (!ClientActors.ContainsKey(id))
                                    continue;

                                var actor = ClientActors[id];

                                var controller = actor.controller as NetActorController;

                                if ((controller.Flags & (int)ActorStateFlags.AiControlled) == 0)
                                {
                                    var leaveMsg = $"{actor.name} has left the match.\n";

                                    PushChatMessage(string.Empty, leaveMsg, true, -1);

                                    using MemoryStream memoryStream = new MemoryStream();
                                    var chatPacket = new ChatPacket
                                    {
                                        Id = -1,
                                        Message = leaveMsg,
                                        TeamOnly = false,
                                    };

                                    using (var writer = new ProtocolWriter(memoryStream))
                                    {
                                        writer.Write(chatPacket);
                                    }
                                    byte[] data = memoryStream.ToArray();

                                    RavenM.RSPatch.RavenscriptEventsManagerPatch.events.onPlayerDisconnect.Invoke(actor);
                                    SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
                                }

                                controller.Flags |= (int)ActorStateFlags.Dead;
                                controller.Targets.Position = Vector3.zero;

                                OwnedActors.Add(id);
                            }
                        }

                        break;
                }
            }
            else
            {
                switch (info.m_eState)
                {
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                        Plugin.logger.LogInfo("Connected to server.");
                        break;

                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                        Plugin.logger.LogInfo($"Killing connection from {info.m_identityRemote.GetSteamID()}.");
                        SteamNetworkingSockets.CloseConnection(pCallback.m_hConn, 0, null, false);

                        GameManager.ReturnToMenu();
                        break;
                }
            }
        }

        void FixedUpdate()
        {
            if (!IsClient)
                return;

            SteamNetworkingSockets.RunCallbacks();

            if (IsClient)
            {
                var msg_ptr = new IntPtr[PACKET_SLACK];
                int msg_count = SteamNetworkingSockets.ReceiveMessagesOnConnection(C2SConnection, msg_ptr, PACKET_SLACK);

                for (int msg_index = 0; msg_index < msg_count; msg_index++)
                {
                    var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msg_ptr[msg_index]);

                    var msg_data = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, msg_data, 0, msg.m_cbSize);

                    using var memStream = new MemoryStream(msg_data);
                    using var packetReader = new ProtocolReader(memStream);

                    var packet = packetReader.ReadPacket();

                    if (packet.sender != OwnGUID)
                    {
                        _total++;

                        using MemoryStream compressedStream = new MemoryStream(packet.data);
                        using DeflateStream decompressStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
                        using var dataStream = new ProtocolReader(decompressStream);
                        switch (packet.Id)
                        {
                            case PacketType.ActorUpdate:
                                {
                                    var bulkActorPacket = dataStream.ReadBulkActorUpdate();

                                    foreach (ActorPacket actor_packet in bulkActorPacket.Updates)
                                    {
                                        if (OwnedActors.Contains(actor_packet.Id))
                                            continue;

                                        Actor actor;

                                        if (ClientActors.ContainsKey(actor_packet.Id))
                                        {
                                            actor = ClientActors[actor_packet.Id];
                                        }
                                        else
                                        {
                                            Plugin.logger.LogInfo($"New actor registered with ID {actor_packet.Id} name {actor_packet.Name}");

                                            if (IsHost)
                                            {
                                                if (!GuidActorOwnership.ContainsKey(packet.sender))
                                                    GuidActorOwnership[packet.sender] = new List<int>();

                                                GuidActorOwnership[packet.sender].Add(actor_packet.Id);

                                                if ((actor_packet.Flags & (int)ActorStateFlags.AiControlled) == 0)
                                                {
                                                    var enterMsg = $"{actor_packet.Name} has joined the match.\n";

                                                    PushChatMessage(string.Empty, enterMsg, true, -1);

                                                    using MemoryStream memoryStream = new MemoryStream();
                                                    var chatPacket = new ChatPacket
                                                    {
                                                        Id = -1,
                                                        Message = enterMsg,
                                                        TeamOnly = false,
                                                    };

                                                    using (var writer = new ProtocolWriter(memoryStream))
                                                    {
                                                        writer.Write(chatPacket);
                                                    }
                                                    byte[] data = memoryStream.ToArray();

                                                    SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
                                                }
                                            }

                                            // FIXME: Another better lock needed.
                                            ClientCanSpawnBot = true;
                                            actor = ActorManager.instance.CreateAIActor(actor_packet.Team);
                                            ClientCanSpawnBot = false;

                                            actor.gameObject.AddComponent<GuidComponent>().guid = actor_packet.Id;

                                            if ((actor_packet.Flags & (int)ActorStateFlags.Dead) == 0)
                                                actor.SpawnAt(actor_packet.Position, Quaternion.identity);

                                            var weapon_parent = actor.controller.WeaponParent();
                                            var loadout = actor.controller.GetLoadout();

                                            Destroy(actor.controller);

                                            var net_controller = actor.gameObject.AddComponent<NetActorController>();
                                            net_controller.actor = actor;
                                            net_controller.FakeWeaponParent = weapon_parent;
                                            net_controller.FakeLoadout = loadout;
                                            net_controller.ActualRotation = actor_packet.FacingDirection;

                                            actor.controller = net_controller;

                                            // Here we set up the voice audio source for the player.
                                            // The audio packets are buffered and played only once enough 
                                            // data is recieved to prevent the audio from "cutting out"
                                            if ((actor_packet.Flags & (int)ActorStateFlags.AiControlled) == 0)
                                            {
                                                var state = new AudioContainer();
                                                PlayVoiceQueue[actor_packet.Id] = state;

                                                int id = actor_packet.Id;

                                                // Adapted from https://forum.unity.com/threads/example-voicechat-with-unet-and-steamworks.482721/
                                                void OnAudioRead(float[] data)
                                                {
                                                    if (state.Buffering)
                                                        state.Buffering = state.VoiceQueue.Count < 3;

                                                    if (state.Buffering)
                                                    {
                                                        for (int i = 0; i < data.Length; i++)
                                                            data[i] = 0f;
                                                        return;
                                                    }

                                                    Plugin.logger.LogInfo($"Requesting audio of size: {data.Length}");

                                                    int count = 0;
                                                    while (count < data.Length)
                                                    {
                                                        float sample = 0;

                                                        if (state.CurrentData == null)
                                                        {
                                                            GrabNextPacket();
                                                        }
                                                        // Looks silly but is needed.
                                                        if (state.CurrentData != null)
                                                        {
                                                            sample = state.CurrentData[state.SampleIndex++];

                                                            if (state.SampleIndex >= state.CurrentData.Length)
                                                            {
                                                                GrabNextPacket();
                                                            }
                                                        }

                                                        data[count] = sample;
                                                        count++;
                                                    }
                                                }

                                                void GrabNextPacket()
                                                {
                                                    if (state.VoiceQueue.Count > 0)
                                                    {
                                                        var data = state.VoiceQueue[0];
                                                        state.CurrentData = data;
                                                        state.VoiceQueue.RemoveAt(0);
                                                    }
                                                    else
                                                    {
                                                        state.CurrentData = null;
                                                        state.Buffering = true;
                                                    }

                                                    state.SampleIndex = 0;
                                                }

                                                var voiceSource = actor.gameObject.AddComponent<AudioSource>();
                                                voiceSource.loop = true;
                                                voiceSource.clip = AudioClip.Create(actor_packet.Name + " voice", 11025 * 10, 1, 11025, true, OnAudioRead);
                                                voiceSource.transform.parent = actor.transform;
                                                voiceSource.spatialBlend = 1f;
                                                voiceSource.outputAudioMixerGroup = GameManager.instance.sfxMixer.outputAudioMixerGroup;
                                                voiceSource.Play();
                                            }
                                            ClientActors[actor_packet.Id] = actor;
                                        }

                                        actor.name = actor_packet.Name;
                                        actor.scoreboardEntry.UpdateNameLabel();

                                        actor.health = actor_packet.Health;

                                        var controller = actor.controller as NetActorController;

                                        controller.Targets = actor_packet;
                                        controller.Flags = actor_packet.Flags;
                                         RavenM.RSPatch.RavenscriptEventsManagerPatch.events.onPlayerJoin.Invoke(actor);
                                    }
                                }
                                break;
                            case PacketType.ActorFlags:
                                {
                                    var bulkFlagPacket = dataStream.ReadBulkFlagsUpdate();

                                    if (bulkFlagPacket.Updates == null)
                                        break;

                                    foreach (ActorFlagsPacket flagPacket in bulkFlagPacket.Updates)
                                    {
                                        if (OwnedActors.Contains(flagPacket.Id))
                                            continue;

                                        if (!ClientActors.ContainsKey(flagPacket.Id))
                                            continue;

                                        Actor actor = ClientActors[flagPacket.Id];

                                        var controller = actor.controller as NetActorController;

                                        controller.Flags = flagPacket.StateVector;
                                    }
                                }
                                break;
                            case PacketType.VehicleUpdate:
                                {
                                    var bulkVehiclePacket = dataStream.ReadBulkVehicleUpdate();

                                    if (bulkVehiclePacket.Updates == null)
                                        break;

                                    foreach (VehiclePacket vehiclePacket in bulkVehiclePacket.Updates)
                                    {
                                        if (OwnedVehicles.Contains(vehiclePacket.Id))
                                            continue;

                                        Vehicle vehicle;

                                        if (ClientVehicles.ContainsKey(vehiclePacket.Id))
                                        {
                                            vehicle = ClientVehicles[vehiclePacket.Id];
                                        }
                                        else
                                        {
                                            Plugin.logger.LogInfo($"New vehicle registered with ID {vehiclePacket.Id} name {vehiclePacket.NameHash} mod {vehiclePacket.Mod}");

                                            var tag = new Tuple<int, ulong>(vehiclePacket.NameHash, vehiclePacket.Mod);

                                            if (!PrefabCache.ContainsKey(tag))
                                            {
                                                Plugin.logger.LogError($"Cannot find prefab with this tagging.");
                                                continue;
                                            }

                                            var prefab = PrefabCache[tag];
                                            vehicle = Instantiate(prefab, vehiclePacket.Position, vehiclePacket.Rotation).GetComponent<Vehicle>();
                                            vehicle.isTurret = vehiclePacket.IsTurret;

                                            vehicle.gameObject.AddComponent<GuidComponent>().guid = vehiclePacket.Id;
                                            //RavenM.RSPatch.Wrapper.WLobby.networkGameObjects.Add(prefab.GetHashCode().ToString(), prefab);

                                            ClientVehicles[vehiclePacket.Id] = vehicle;
                                        }

                                        if (vehicle == null)
                                        {
                                            if (vehiclePacket.Dead)
                                                continue;

                                            Plugin.logger.LogError($"Vehicle with id {vehiclePacket.Id} has somehow dissapeared. Skipping this update for now.");
                                            ClientVehicles.Remove(vehiclePacket.Id);
                                            continue;
                                        }

                                        vehicle.gameObject.SetActive(vehiclePacket.Active);

                                        TargetVehicleStates[vehiclePacket.Id] = vehiclePacket;

                                        vehicle.health = vehiclePacket.Health;

                                        if (vehiclePacket.Dead)
                                        {
                                            RemoteDeadVehicles.Add(vehiclePacket.Id);
                                            if (!vehicle.dead)
                                                vehicle.Die(DamageInfo.Default);
                                        }
                                        else if (vehicle.health <= 0)
                                            vehicle.Damage(DamageInfo.Default);
                                        else if (vehicle.burning)
                                            typeof(Vehicle).GetMethod("StopBurning", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vehicle, new object[] { });
                                        else if (vehicle.dead)
                                            vehicle.GetType().GetMethod("Ressurect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vehicle, new object[] { });
                                    }
                                }
                                break;
                            case PacketType.Damage:
                                {
                                    Plugin.logger.LogInfo("Damage packet.");
                                    DamagePacket damage_packet = dataStream.ReadDamagePacket();

                                    if (!ClientActors.ContainsKey(damage_packet.Target))
                                        break;

                                    Actor sourceActor = damage_packet.SourceActor == -1 ? null : ClientActors[damage_packet.SourceActor];
                                    Actor targetActor = ClientActors[damage_packet.Target];

                                    Plugin.logger.LogInfo($"Got damage from {targetActor.name}!");

                                    DamageInfo damage_info = new DamageInfo
                                    {
                                        type = damage_packet.Type,
                                        healthDamage = damage_packet.HealthDamage,
                                        balanceDamage = damage_packet.BalanceDamage,
                                        isSplashDamage = damage_packet.IsSplashDamage,
                                        isPiercing = damage_packet.IsPiercing,
                                        isCriticalHit = damage_packet.IsCriticalHit,
                                        point = damage_packet.Point,
                                        direction = damage_packet.Direction,
                                        impactForce = damage_packet.ImpactForce,
                                        sourceActor = sourceActor,
                                        sourceWeapon = null,
                                    };

                                    targetActor.Damage(damage_info);
                                }
                                break;
                            case PacketType.Death:
                                {
                                    Plugin.logger.LogInfo("Death packet.");
                                    DamagePacket damage_packet = dataStream.ReadDamagePacket();

                                    if (!ClientActors.ContainsKey(damage_packet.Target))
                                        break;

                                    Actor sourceActor = damage_packet.SourceActor == -1 ? null : ClientActors[damage_packet.SourceActor];
                                    Actor targetActor = ClientActors[damage_packet.Target];

                                    Plugin.logger.LogInfo($"Got death from {targetActor.name}!");

                                    DamageInfo damage_info = new DamageInfo
                                    {
                                        type = damage_packet.Type,
                                        healthDamage = damage_packet.HealthDamage,
                                        balanceDamage = damage_packet.BalanceDamage,
                                        isSplashDamage = damage_packet.IsSplashDamage,
                                        isPiercing = damage_packet.IsPiercing,
                                        isCriticalHit = damage_packet.IsCriticalHit,
                                        point = damage_packet.Point,
                                        direction = damage_packet.Direction,
                                        impactForce = damage_packet.ImpactForce,
                                        sourceActor = sourceActor,
                                        sourceWeapon = null,
                                    };

                                    if (damage_packet.Silent)
                                        targetActor.KillSilently();
                                    else
                                        targetActor.Kill(damage_info);
                                }
                                break;
                            case PacketType.EnterSeat:
                                {
                                    Plugin.logger.LogInfo("Enter packet.");
                                    var enterSeatPacket = dataStream.ReadEnterSeatPacket();

                                    if (OwnedActors.Contains(enterSeatPacket.ActorId))
                                        break;

                                    if (!ClientVehicles.ContainsKey(enterSeatPacket.VehicleId))
                                    {
                                        Plugin.logger.LogError($"The vehicle with ID: {enterSeatPacket.VehicleId} does not exist.");
                                        break;
                                    }

                                    var actor = ClientActors[enterSeatPacket.ActorId];
                                    var vehicle = ClientVehicles[enterSeatPacket.VehicleId];

                                    if (actor == null || vehicle == null)
                                    {
                                        Plugin.logger.LogError($"Invalid actor or vehicle. Actor ID: {enterSeatPacket.ActorId} Vehicle ID: {enterSeatPacket.VehicleId}");
                                        break;
                                    }

                                    if (enterSeatPacket.SeatId < 0 || enterSeatPacket.SeatId >= vehicle.seats.Count)
                                    {
                                        Plugin.logger.LogError($"Attempted to enter invalid seat with index: {enterSeatPacket.SeatId}");
                                        break;
                                    }

                                    var seat = vehicle.seats[enterSeatPacket.SeatId];

                                    // TODO: Is it possible to switch into vehicles without getting out first?
                                    if (actor.IsSeated())
                                    {
                                        // Same as if the actor fully left the seat.
                                        if (actor.seat.IsDriverSeat() && IsHost)
                                            OwnedVehicles.Add(actor.seat.vehicle.GetComponent<GuidComponent>().guid);

                                        typeof(Actor).GetMethod("LeaveSeatForSwap", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, new object[] { });
                                    }

                                    actor.EnterSeat(seat, true);
                                    if (!OwnedActors.Contains(enterSeatPacket.ActorId))
                                        (actor.controller as NetActorController).SeatResolverCooldown.Start();

                                    // If an Actor that we do not own wants to take control of a vehicle,
                                    // then let's give up ownership temporarily.
                                    if (seat.IsDriverSeat())
                                        OwnedVehicles.Remove(enterSeatPacket.VehicleId);
                                }
                                break;
                            case PacketType.LeaveSeat:
                                {
                                    Plugin.logger.LogInfo("Leave packet.");
                                    var leaveSeatPacket = dataStream.ReadLeaveSeatPacket();

                                    if (OwnedActors.Contains(leaveSeatPacket.Id))
                                        break;

                                    var actor = ClientActors[leaveSeatPacket.Id];

                                    if (actor == null)
                                        break;

                                    if (actor.seat == null)
                                        break;

                                    // On the converse, if a foreign Actor releases control, then let's
                                    // take it back if we are the host.
                                    if (actor.seat.IsDriverSeat() && IsHost)
                                    {
                                        OwnedVehicles.Add(actor.seat.vehicle.GetComponent<GuidComponent>().guid);
                                    }   

                                    actor.LeaveSeat(false);
                                    if (!OwnedActors.Contains(leaveSeatPacket.Id))
                                        (actor.controller as NetActorController).SeatResolverCooldown.Start();
                                }
                                break;
                            case PacketType.GameStateUpdate:
                                {
                                    switch (GameModeBase.instance.gameModeType)
                                    {
                                        case GameModeType.Battalion:
                                            {
                                                var gameUpdatePacket = dataStream.ReadBattleStatePacket();

                                                var battleObj = GameModeBase.instance as BattleMode;

                                                var currentBattalions = (int[])typeof(BattleMode).GetField("remainingBattalions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battleObj);

                                                for (int i = 0; i < 2; i++)
                                                {
                                                    if (currentBattalions[i] > gameUpdatePacket.RemainingBattalions[i])
                                                        typeof(BattleMode).GetMethod("OnNoTicketsRemaining", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battleObj, new object[] { i });
                                                }

                                                typeof(BattleMode).GetField("remainingBattalions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battleObj, gameUpdatePacket.RemainingBattalions);
                                                typeof(BattleMode).GetField("tickets", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battleObj, gameUpdatePacket.Tickets);

                                                for (int i = 0; i < 2; i++)
                                                {
                                                    if (gameUpdatePacket.RemainingBattalions[i] != 0)
                                                        typeof(BattleMode).GetMethod("UpdateTicketLabel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battleObj, new object[] { i });
                                                }

                                                for (int i = 0; i < gameUpdatePacket.SpawnPointOwners.Length; i++)
                                                {
                                                    if (ActorManager.instance.spawnPoints[i].owner != gameUpdatePacket.SpawnPointOwners[i])
                                                        ActorManager.instance.spawnPoints[i].SetOwner(gameUpdatePacket.SpawnPointOwners[i]);
                                                }
                                            }
                                            break;
                                        default:
                                            Plugin.logger.LogError("Got game mode update for unsupported type?");
                                            break;
                                    }
                                }
                                break;
                            case PacketType.SpawnProjectile:
                                {
                                    var spawnPacket = dataStream.ReadSpawnProjectilePacket();

                                    if (OwnedActors.Contains(spawnPacket.SourceId))
                                        break;

                                    var actor = ClientActors[spawnPacket.SourceId];

                                    if (actor == null)
                                        break;

                                    var tag = new Tuple<int, ulong>(spawnPacket.NameHash, spawnPacket.Mod);

                                    if (!PrefabCache.ContainsKey(tag))
                                    {
                                        Plugin.logger.LogError($"Cannot find projectile prefab with this tagging.");
                                        continue;
                                    }

                                    var prefab = PrefabCache[tag];
                                    var projectile = ProjectilePoolManager.InstantiateProjectile(prefab, spawnPacket.Position, spawnPacket.Rotation);

                                    projectile.source = actor;
                                    projectile.sourceWeapon = null;
                                    projectile.performInfantryInitialMuzzleTravel = spawnPacket.performInfantryInitialMuzzleTravel;
                                    projectile.initialMuzzleTravelDistance = spawnPacket.initialMuzzleTravelDistance;

                                    // Save the old configuration for when the object is pooled.
                                    ReleaseProjectilePatch.ConfigCache[projectile] = new ReleaseProjectilePatch.Config
                                    {
                                        damage = projectile.configuration.damage,
                                        balanceDamage = projectile.configuration.balanceDamage,
                                        autoAssignArmorDamage = projectile.autoAssignArmorDamage,
                                        armorDamage = projectile.armorDamage,
                                    };

                                    // Disable any form of damage from this projectile.
                                    projectile.configuration.damage = 0f;
                                    projectile.configuration.balanceDamage = 0f;
                                    projectile.autoAssignArmorDamage = false;
                                    projectile.armorDamage = Vehicle.ArmorRating.SmallArms;

                                    if (projectile.gameObject.TryGetComponent(out GuidComponent guid))
                                        guid.guid = spawnPacket.ProjectileId;
                                    else
                                        projectile.gameObject.AddComponent<GuidComponent>().guid = spawnPacket.ProjectileId;

                                    ClientProjectiles[spawnPacket.ProjectileId] = projectile;

                                    projectile.StartTravelling();
                                }
                                break;
                            case PacketType.UpdateProjectile:
                                {
                                    var bulkProjectilePacket = dataStream.ReadBulkProjectileUpdate();

                                    if (bulkProjectilePacket.Updates == null)
                                        break;

                                    foreach (UpdateProjectilePacket projectilePacket in bulkProjectilePacket.Updates)
                                    {
                                        if (OwnedProjectiles.Contains(projectilePacket.Id))
                                            continue;

                                        if (!ClientProjectiles.ContainsKey(projectilePacket.Id))
                                            continue;

                                        Projectile projectile = ClientProjectiles[projectilePacket.Id];

                                        if (projectile == null)
                                            continue;

                                        projectile.transform.position = projectilePacket.Position;
                                        projectile.velocity = projectilePacket.Velocity;

                                        if (projectilePacket.Boom && projectile.enabled)
                                        {
                                            var Explode = projectile.GetType().GetMethod("Explode", BindingFlags.Instance | BindingFlags.NonPublic);
                                            // This shouldn't ever not exist, since we send only for ExplodingProjectiles.
                                            if (Explode != null)
                                            {
                                                var up = -projectile.transform.forward;

                                                if (projectile.velocity != Vector3.zero &&
                                                    projectile.ProjectileRaycast(new Ray(projectile.transform.position, projectile.velocity.normalized),
                                                    out var hitInfo,
                                                    Mathf.Infinity,
                                                    (int)projectile.GetType().GetField("hitMask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(projectile)))
                                                {
                                                    up = hitInfo.normal;
                                                }

                                                Explode.Invoke(projectile, new object[] { projectile.transform.position, up });
                                            }
                                        }
                                    }
                                }
                                break;
                            case PacketType.Chat:
                                {
                                    var chatPacket = dataStream.ReadChatPacket();

                                    var actor = ClientActors.ContainsKey(chatPacket.Id) ? ClientActors[chatPacket.Id] : null;

                                    if (actor == null)
                                        PushChatMessage(string.Empty, chatPacket.Message, true, -1);
                                    else
                                        PushChatMessage(actor.name, chatPacket.Message, !chatPacket.TeamOnly, actor.team);
                                }
                                break;
                            case PacketType.Voip:
                                {
                                    var voicePacket = dataStream.ReadVoicePacket();

                                    int bufferSize = 22050;
                                    byte[] voiceBuffer = null;
                                    EVoiceResult res = EVoiceResult.k_EVoiceResultNoData;
                                    uint nBytesWritten = 0;
                                    do
                                    {
                                        bufferSize *= 2;
                                        voiceBuffer = new byte[bufferSize];
                                        res = SteamUser.DecompressVoice(voicePacket.Voice, (uint)voicePacket.Voice.Length, voiceBuffer, (uint)voiceBuffer.Length, out nBytesWritten, 11025);
                                    } while (res == EVoiceResult.k_EVoiceResultBufferTooSmall);

                                    if (res != EVoiceResult.k_EVoiceResultOK)
                                    {
                                        Plugin.logger.LogError($"Failed to decompress voice. Reason: {res}");
                                        break;
                                    }

                                    if (nBytesWritten == 0)
                                        break;

                                    var decodedData = new float[nBytesWritten / 2];
                                    for (int i = 0; i < decodedData.Length; i++)
                                    {
                                        float value = BitConverter.ToInt16(voiceBuffer, i * 2);
                                        decodedData[i] = value * 15f / short.MaxValue;
                                    }

                                    var state = PlayVoiceQueue[voicePacket.Id];

                                    if (state == null)
                                        break;

                                    state.VoiceQueue.Add(decodedData);
                                }
                                break;
                            case PacketType.VehicleDamage:
                                {
                                    Plugin.logger.LogInfo("Vehicle damage packet.");
                                    DamagePacket damage_packet = dataStream.ReadDamagePacket();

                                    if (!ClientVehicles.ContainsKey(damage_packet.Target))
                                        break;

                                    Actor sourceActor = damage_packet.SourceActor == -1 ? null : ClientActors[damage_packet.SourceActor];
                                    Vehicle targetVehicle = ClientVehicles[damage_packet.Target];

                                    Plugin.logger.LogInfo($"Got vehicle damage from {targetVehicle.name}!");

                                    DamageInfo damage_info = new DamageInfo
                                    {
                                        type = damage_packet.Type,
                                        healthDamage = damage_packet.HealthDamage,
                                        balanceDamage = damage_packet.BalanceDamage,
                                        isSplashDamage = damage_packet.IsSplashDamage,
                                        isPiercing = damage_packet.IsPiercing,
                                        isCriticalHit = damage_packet.IsCriticalHit,
                                        point = damage_packet.Point,
                                        direction = damage_packet.Direction,
                                        impactForce = damage_packet.ImpactForce,
                                        sourceActor = sourceActor,
                                        sourceWeapon = null,
                                    };

                                    targetVehicle.Damage(damage_info);
                                }
                                break;
                            default:
                                RSPatch.RSPatch.FixedUpdate(packet, dataStream);
                                break;
                        }
                    }

                    // SR7, pls update Steamworks.NET.
                    SteamAPI_SteamNetworkingMessage_t_Release.Invoke(null, new object[] { msg_ptr[msg_index] });
                }
            }

            // The "server" acts as a P2P relay station.
            if (IsHost)
            {
                var msg_ptr = new IntPtr[PACKET_SLACK];
                int msg_count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(PollGroup, msg_ptr, PACKET_SLACK);

                for (int msg_index = 0; msg_index < msg_count; msg_index++)
                {
                    var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msg_ptr[msg_index]);

                    if (!ConnectionGuidMap.ContainsKey(msg.m_conn))
                    {
                        // We do a really quick read of the packet data.
                        unsafe
                        {
                            using var memoryStream = new UnmanagedMemoryStream((byte*)msg.m_pData.ToPointer(), msg.m_cbSize);
                            using var reader = new ProtocolReader(memoryStream);
                            reader.ReadInt32();
                            var guid = new Guid(reader.ReadBytes(16));
                            ConnectionGuidMap[msg.m_conn] = guid;
                        }
                    }

                    for (int i = ServerConnections.Count - 1; i >= 0; i--)
                    {
                        var connection = ServerConnections[i];

                        if (connection == msg.m_conn)
                            continue;

                        var res = SteamNetworkingSockets.SendMessageToConnection(connection, msg.m_pData, (uint)msg.m_cbSize, Constants.k_nSteamNetworkingSend_Reliable, out long msg_num);

                        if (res != EResult.k_EResultOK)
                        {
                            Plugin.logger.LogError($"Failure {res}");
                            ServerConnections.RemoveAt(i);
                            SteamNetworkingSockets.CloseConnection(connection, 0, null, false);
                        }
                    }

                    Marshal.DestroyStructure<SteamNetworkingMessage_t>(msg_ptr[msg_index]);
                }
            }

            if (MainSendTick.TrueDone())
            {
                MainSendTick.Start();

                SendGameState();

                SendActorStates();

                SendVehicleStates();

                SendProjectileUpdates();
            }
        }

        public void SendGameState()
        {
            if (!IsHost)
                return;

            byte[] data = null;

            switch (GameModeBase.instance.gameModeType)
            {
                case GameModeType.Battalion:
                    {
                        var battleObj = GameModeBase.instance as BattleMode;

                        var gamePacket = new BattleStatePacket
                        {
                            RemainingBattalions = (int[])typeof(BattleMode).GetField("remainingBattalions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battleObj),
                            Tickets = (int[])typeof(BattleMode).GetField("tickets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battleObj),
                            SpawnPointOwners = new int[ActorManager.instance.spawnPoints.Length],
                        };

                        for (int i = 0; i < ActorManager.instance.spawnPoints.Length; i++)
                        {
                            gamePacket.SpawnPointOwners[i] = ActorManager.instance.spawnPoints[i].owner;
                        }

                        using MemoryStream memoryStream = new MemoryStream();

                        using (var writer = new ProtocolWriter(memoryStream))
                        {
                            writer.Write(gamePacket);
                        }
                        data = memoryStream.ToArray();
                    }
                    break;
            }

            if (data == null)
                return;

            SendPacketToServer(data, PacketType.GameStateUpdate, Constants.k_nSteamNetworkingSend_Unreliable);
        }

        private int GenerateFlags(Actor actor)
        {
            int flags = 0;
            if (!actor.dead && actor.controller.Aiming()) flags |= (int)ActorStateFlags.Aiming;
            if (!actor.dead && actor.controller.Countermeasures()) flags |= (int)ActorStateFlags.Countermeasures;
            if (!actor.dead && actor.controller.Crouch()) flags |= (int)ActorStateFlags.Crouch;
            if (!actor.dead && actor.controller.Fire()) flags |= (int)ActorStateFlags.Fire;
            if (!actor.dead && actor.controller.HoldingSprint()) flags |= (int)ActorStateFlags.HoldingSprint;
            if (!actor.dead && actor.controller.IdlePose()) flags |= (int)ActorStateFlags.IdlePose;
            if (!actor.dead && actor.controller.IsAirborne()) flags |= (int)ActorStateFlags.IsAirborne;
            if (!actor.dead && actor.controller.IsAlert()) flags |= (int)ActorStateFlags.IsAlert;
            if (!actor.dead && actor.controller.IsMoving()) flags |= (int)ActorStateFlags.IsMoving;
            if (actor.controller.IsOnPlayerSquad()) flags |= (int)ActorStateFlags.IsOnPlayerSquad;
            if (!actor.dead && actor.controller.IsReadyToPickUpPassengers()) flags |= (int)ActorStateFlags.IsReadyToPickUpPassengers;
            if (!actor.dead && actor.controller.IsSprinting()) flags |= (int)ActorStateFlags.IsSprinting;
            if (!actor.dead && actor.controller.IsTakingFire()) flags |= (int)ActorStateFlags.IsTakingFire;
            if (!actor.dead && actor.controller.Jump()) flags |= (int)ActorStateFlags.Jump;
            if (!actor.dead && actor.controller.OnGround()) flags |= (int)ActorStateFlags.OnGround;
            if (!actor.dead && actor.controller.ProjectToGround()) flags |= (int)ActorStateFlags.ProjectToGround;
            if (!actor.dead && actor.controller.Prone()) flags |= (int)ActorStateFlags.Prone;
            if (!actor.dead && actor.controller.Reload()) flags |= (int)ActorStateFlags.Reload;
            if (actor.dead) flags |= (int)ActorStateFlags.Dead;
            if (actor.aiControlled) flags |= (int)ActorStateFlags.AiControlled;
            if (!actor.dead && actor.controller.DeployParachute()) flags |= (int)ActorStateFlags.DeployParachute;

            return flags;
        }

        public void SendActorFlags()
        {
            var bulkActorUpdate = new BulkFlagsUpdate
            {
                Updates = new List<ActorFlagsPacket>(),
            };

            foreach (var owned_actor in OwnedActors)
            {
                var actor = ClientActors[owned_actor];

                int flags = GenerateFlags(actor);

                if (ActorStateCache.TryGetValue(owned_actor, out int saved_flags) && saved_flags == flags)
                    continue;

                ActorFlagsPacket net_actor = new ActorFlagsPacket
                {
                    Id = owned_actor,
                    StateVector = flags,
                };

                bulkActorUpdate.Updates.Add(net_actor);

                ActorStateCache[owned_actor] = flags;
            }

            if (bulkActorUpdate.Updates.Count == 0)
                return;

            using MemoryStream memoryStream = new MemoryStream();

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(bulkActorUpdate);
            }
            byte[] data = memoryStream.ToArray();

            SendPacketToServer(data, PacketType.ActorFlags, Constants.k_nSteamNetworkingSend_Unreliable);
        }

        public void SendActorStates()
        {
            var bulkActorUpdate = new BulkActorUpdate
            {
                Updates = new List<ActorPacket>(),
            };

            foreach (var owned_actor in OwnedActors)
            {
                var actor = ClientActors[owned_actor];

                ActorPacket net_actor = new ActorPacket
                {
                    Id = owned_actor,
                    Name = actor.name,
                    Position = actor.Position(),
                    Lean = actor.dead ? 0f : actor.controller.Lean(),
                    AirplaneInput = actor.seat != null ? (Vector4?)actor.controller.AirplaneInput() : null,
                    BoatInput = actor.seat != null ? (Vector2?)actor.controller.BoatInput() : null,
                    CarInput = actor.seat != null ? (Vector2?)actor.controller.CarInput() : null,
                    // Dirty conditional, but it is needed to properly update the
                    // turret direction when the user is a player.
                    //
                    // ...Mortars actually use the player's facing direction.
                    // Because why not.
                    FacingDirection = (!actor.aiControlled && actor.IsSeated() && actor.seat.HasActiveWeapon() && actor.seat.activeWeapon.GetType() != typeof(Mortar)) ?
                                        actor.seat.activeWeapon.CurrentMuzzle().forward :
                                        actor.controller.FacingDirection(),
                    HelicopterInput = actor.seat != null ? (Vector4?)actor.controller.HelicopterInput() : null,
                    LadderInput = actor.dead ? 0f : actor.controller.LadderInput(),
                    ParachuteInput = actor.dead ? Vector2.zero : actor.controller.ParachuteInput(),
                    // Not the real controller.RangeInput(), but what the mortar sees. Otherwise,
                    // the other clients cant keep up with the fast mouse scrolls.
                    RangeInput = !actor.dead && actor.IsSeated() && actor.seat.HasActiveWeapon() && actor.seat.activeWeapon.GetType() == typeof(Mortar)
                                    ? (float)typeof(Mortar).GetField("range", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor.seat.activeWeapon)
                                    : 0f,
                    Velocity = actor.dead ? Vector3.zero : actor.controller.Velocity(),
                    ActiveWeaponHash = actor.activeWeapon != null
                                        ? actor.IsSeated()
                                            ? actor.seat.ActiveWeaponSlot()
                                            : actor.activeWeapon.name.GetHashCode()
                                        : 0,
                    Team = actor.team,
                    MarkerPosition = actor.aiControlled ? null : (Vector3?)MarkerPosition,
                    Flags = GenerateFlags(actor),
                    Ammo = !actor.dead && actor.activeWeapon != null ? actor.activeWeapon.ammo : 0,
                    Health = actor.health,
                    VehicleId = actor.IsSeated() && actor.seat.vehicle.TryGetComponent(out GuidComponent vguid) ? vguid.guid : 0,
                    Seat = actor.IsSeated() ? actor.seat.vehicle.seats.IndexOf(actor.seat) : -1,
                };

                bulkActorUpdate.Updates.Add(net_actor);
            }

            using MemoryStream memoryStream = new MemoryStream();

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(bulkActorUpdate);
            }
            byte[] data = memoryStream.ToArray();

            SendPacketToServer(data, PacketType.ActorUpdate, Constants.k_nSteamNetworkingSend_Unreliable);
        }

        public void SendVehicleStates()
        {
            var bulkVehicleUpdate = new BulkVehicleUpdate
            {
                Updates = new List<VehiclePacket>(),
            };

            foreach (var owned_vehicle in OwnedVehicles)
            {
                Vehicle vehicle = ClientVehicles[owned_vehicle];

                if (vehicle == null)
                    continue;

                var tag = vehicle.gameObject.GetComponent<PrefabTag>();

                if (tag == null)
                {
                    Plugin.logger.LogError($"Vehicle {vehicle.name} is somehow untagged!");
                    continue;
                }

                var net_vehicle = new VehiclePacket
                {
                    Id = owned_vehicle,
                    NameHash = tag.NameHash,
                    Mod = tag.Mod,
                    Position = vehicle.transform.position,
                    Rotation = vehicle.transform.rotation,
                    Health = vehicle.health,
                    Dead = vehicle.dead,
                    IsTurret = vehicle.isTurret,
                    Active = vehicle.gameObject.activeSelf,
                };

                bulkVehicleUpdate.Updates.Add(net_vehicle);
            }

            if (bulkVehicleUpdate.Updates.Count == 0)
                return;

            using MemoryStream memoryStream = new MemoryStream();

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(bulkVehicleUpdate);
            }
            byte[] data = memoryStream.ToArray();

            SendPacketToServer(data, PacketType.VehicleUpdate, Constants.k_nSteamNetworkingSend_Unreliable);
        }

        public void SendProjectileUpdates()
        {
            var bulkProjectileUpdate = new BulkProjectileUpdate
            {
                Updates = new List<UpdateProjectilePacket>(),
            };

            var cleanup = new List<int>();

            foreach (var owned_projectile in OwnedProjectiles)
            {
                var projectile = ClientProjectiles[owned_projectile];

                if (projectile == null)
                {
                    cleanup.Add(owned_projectile);
                    continue;
                }

                // We should only update projectiles where it is obvious
                // there is a desync, like rockets++. OR the one's that
                // explode. It's hard to predict how they behave.
                if (!typeof(ExplodingProjectile).IsAssignableFrom(projectile.GetType()))
                    continue;

                // There are a lot of these to be honest. It's probably better to only
                // update these when they actually do something (i.e. explode)
                if (projectile.GetType() == typeof(ExplodingProjectile) && projectile.enabled)
                    continue;
                
                if (!projectile.enabled)
                    cleanup.Add(owned_projectile);

                var net_projectile = new UpdateProjectilePacket
                {
                    Id = owned_projectile,
                    Position = projectile.transform.position,
                    Velocity = projectile.velocity,
                    Boom = !projectile.enabled,
                };

                bulkProjectileUpdate.Updates.Add(net_projectile);
            }

            foreach (var projectile in cleanup)
                OwnedProjectiles.Remove(projectile);

            if (bulkProjectileUpdate.Updates.Count == 0)
                return;

            using MemoryStream memoryStream = new MemoryStream();

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(bulkProjectileUpdate);
            }
            byte[] data = memoryStream.ToArray();

            // TODO: This needs to be reliable because of the explosion state.
            // But, the rest of the data does not. Therefore we should have a seperate
            // packet like the ActorFlags just for this state change.
            SendPacketToServer(data, PacketType.UpdateProjectile, Constants.k_nSteamNetworkingSend_Reliable);
        }

        public void SendVoiceData()
        {
            if (SteamUser.GetAvailableVoice(out uint pcbCompressed) == EVoiceResult.k_EVoiceResultOK)
            {
                var voiceBuffer = new byte[pcbCompressed];
                if (SteamUser.GetVoice(true, voiceBuffer, pcbCompressed, out uint nBytesWritten) == EVoiceResult.k_EVoiceResultOK && nBytesWritten == pcbCompressed)
                {
                    using MemoryStream memoryStream = new MemoryStream();
                    var voicePacket = new VoicePacket
                    {
                        Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                        Voice = voiceBuffer,
                    };

                    using (var writer = new ProtocolWriter(memoryStream))
                    {
                        writer.Write(voicePacket);
                    }
                    byte[] data = memoryStream.ToArray();

                    SendPacketToServer(data, PacketType.Voip, Constants.k_nSteamNetworkingSend_UnreliableNoDelay);
                }
            }
        }

        /// <summary>
        /// Clean up an actor's presence as much as possible.
        /// </summary>
        public void DestroyActor(Actor actor)
        {
            switch (actor.team)
            {
                case 0:
                    var t0field = typeof(ActorManager).GetField("team0Bots", BindingFlags.Instance | BindingFlags.NonPublic);
                    int team0Bots = (int)t0field.GetValue(ActorManager.instance);
                    t0field.SetValue(ActorManager.instance, team0Bots - 1);
                    break;
                case 1:
                    var t1field = typeof(ActorManager).GetField("team1Bots", BindingFlags.Instance | BindingFlags.NonPublic);
                    int team1Bots = (int)t1field.GetValue(ActorManager.instance);
                    t1field.SetValue(ActorManager.instance, team1Bots - 1);
                    break;
            }

            var scoreboard = typeof(ScoreboardUi).GetField("entriesOfTeam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ScoreboardUi.instance) as Dictionary<int, List<ScoreboardActorEntry>>;
            scoreboard[actor.team].Remove(actor.scoreboardEntry);

            if (actor.IsSeated())
                actor.LeaveSeat(false);

            var controller = actor.controller as AiActorController;
            if (controller.squad != null)
                controller.squad.SplitSquad(new List<ActorController>() { controller });

            ActorManager.instance.aiActorControllers.Remove(controller);

            var actorsOnTeam = typeof(ActorManager).GetField("actorsOnTeam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ActorManager.instance) as List<Actor>[];
            actorsOnTeam[actor.team].Remove(actor);

            var nextActorIndexTeam = typeof(ActorManager).GetField("nextActorIndexTeam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ActorManager.instance) as int[];
            nextActorIndexTeam[actor.team]--;

            var nextActorIndexF = typeof(ActorManager).GetField("nextActorIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            int nextActorIndex = (int)nextActorIndexF.GetValue(ActorManager.instance);
            nextActorIndexF.SetValue(ActorManager.instance, nextActorIndex - 1);

            ActorManager.Drop(actor);
            Destroy(actor.controller);
            Destroy(actor);
        }
    }
}