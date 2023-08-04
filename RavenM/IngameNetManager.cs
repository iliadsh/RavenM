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
using Ravenfield.SpecOps;
using RavenM.Commands;

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

            // And the destructibles.
            foreach (var destructible in Resources.FindObjectsOfTypeAll<Destructible>())
            {
                var prefab = DestructiblePacket.Root(destructible);

                if (!prefab.TryGetComponent(out PrefabTag _))
                {
                    Plugin.logger.LogInfo($"Detected destructible prefab with name: {prefab.name}, and from mod: {contentInfo.sourceMod.workshopItemId}");

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

            foreach (var destructible in Resources.FindObjectsOfTypeAll<Destructible>())
            {
                var prefab = DestructiblePacket.Root(destructible);
                Plugin.logger.LogInfo($"Tagging default destructible: {prefab.name}");

                IngameNetManager.TagPrefab(prefab);
            }
        }
    }

    [HarmonyPatch(typeof(TurretSpawner), nameof(TurretSpawner.SpawnTurrets))]
    public class SpawnTurretDetachPatch
    {
        // Turrets created through spawners will have their transform parent be the CapturePoint,
        // which totally messes things up for the destructible syncing logic. We unparent them
        // here to avoid that. Not a great solution but oh well.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                // Pop the third argument from the evaluation stack (transform.parent) and push a null.
                if (instruction.opcode == OpCodes.Call && ((MethodInfo)instruction.operand).Name == nameof(UnityEngine.Object.Instantiate)) 
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldnull);
                }

                yield return instruction;
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

    [HarmonyPatch(typeof(Destructible), "Start")]
    public class DestructibleCreatedPatch
    {
        static bool Prefix(Destructible __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            var root = DestructiblePacket.Root(__instance);
            Plugin.logger.LogInfo($"D: {root.name}");

            if (IngameNetManager.instance.IsHost)
            {
                if (IngameNetManager.instance.ClientDestructibles.ContainsValue(root))
                    return true;

                int id = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);

                if (root.TryGetComponent(out GuidComponent guid))
                    id = guid.guid;
                else
                    root.AddComponent<GuidComponent>().guid = id;

                IngameNetManager.instance.ClientDestructibles.Add(id, root);

                Plugin.logger.LogInfo($"Registered new destructible root with name: {root.name} and id: {id}");
            }
            else if (!root.TryGetComponent(out GuidComponent guid) ||
                (!root.TryGetComponent(out Vehicle _) && !IngameNetManager.instance.ClientDestructibles.ContainsKey(guid.guid)))
            {
                Plugin.logger.LogInfo($"Cleaning up unwanted destructible with name: {root.name}");
                UnityEngine.Object.Destroy(root);
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
            if (__instance.killCredit != null && __instance.killCredit.TryGetComponent(out GuidComponent aguid))
                sourceId = aguid.guid;

            if (IngameNetManager.instance.OwnedActors.Contains(sourceId) || (sourceId == -1 && IngameNetManager.instance.IsHost))
            {
                int id = typeof(ExplodingProjectile).IsAssignableFrom(__instance.GetType()) ? IngameNetManager.instance.RandomGen.Next(0, int.MaxValue) : 0;

                if (__instance.TryGetComponent(out GuidComponent guid))
                    id = guid.guid;
                else
                    __instance.gameObject.AddComponent<GuidComponent>().guid = id;

                IngameNetManager.instance.ClientProjectiles[id] = __instance;
                if (id != 0)
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
                    initialMuzzleTravelDistance = __instance.initialMuzzleTravelDistance,
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

        public Dictionary<int, GameObject> ClientDestructibles = new Dictionary<int, GameObject>();

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

        public bool UsingMicrophone = false;

        public Texture2D MicTexture = new Texture2D(2, 2);

        public Dictionary<int, AudioContainer> PlayVoiceQueue = new Dictionary<int, AudioContainer>();

        public static readonly Dictionary<Tuple<int, ulong>, GameObject> PrefabCache = new Dictionary<Tuple<int, ulong>, GameObject>();

        public CommandManager commandManager;

        public Type Steamworks_NativeMethods;

        public MethodInfo SteamAPI_SteamNetworkingMessage_t_Release;

        public float VoiceChatVolume = 1f;

        public KeyCode VoiceChatKeybind = KeyCode.CapsLock;

        public KeyCode PlaceMarkerKeybind = KeyCode.BackQuote;

        private void Awake()
        {
            instance = this;

            using var markerResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.marker.png");
            using var resourceMemory = new MemoryStream();
            markerResource.CopyTo(resourceMemory);
            var imageBytes = resourceMemory.ToArray();

            MarkerTexture.LoadImage(imageBytes);

            ChatManager.instance.GreyBackground.SetPixel(0, 0, Color.grey * 0.3f);
            ChatManager.instance.GreyBackground.Apply();

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

            var kickAnimationBundleStream =
                Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.kickanimcontroller");
            var kickAnimationBundle =
                AssetBundle.LoadFromStream(kickAnimationBundleStream);

            KickAnimation.KickController = kickAnimationBundle.LoadAsset<RuntimeAnimatorController>("Actor NEW 1");
            KickAnimation.KickSound = kickAnimationBundle.LoadAsset<AudioClip>("kickSound");

            Plugin.logger.LogWarning(KickAnimation.KickController == null ? "Kick AnimationController couldn't be loaded" : "Kick AnimationController loaded");
            Plugin.logger.LogWarning(KickAnimation.KickSound == null ? "Kick AudioClip couldn't be loaded" : "Kick AudioClip loaded");

            kickAnimationBundle.Unload(false);

            commandManager = new CommandManager();
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
            if (Input.GetKeyDown(PlaceMarkerKeybind)
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

            foreach (var kv in RSPatch.RSPatch.TargetGameObjectState)
            {
                int gameObjectID = kv.Key;
                var gameObjectPacket = kv.Value;
                if (!RSPatch.RSPatch.ClientObjects.ContainsKey(gameObjectID))
                    continue;

                if (RSPatch.RSPatch.OwnedObjects.Contains(gameObjectID))
                    continue;
                var networkGameObject = RSPatch.RSPatch.ClientObjects[gameObjectID];
                if (networkGameObject.SourceID != gameObjectPacket.SourceID)
                    break;

                if (networkGameObject == null)
                    continue;

                networkGameObject.transform.position = Vector3.Lerp(networkGameObject.transform.position, gameObjectPacket.Position, 5f * Time.deltaTime);

                networkGameObject.transform.rotation = Quaternion.Slerp(networkGameObject.transform.rotation, gameObjectPacket.Rotation, 5f * Time.deltaTime);
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

            if (Input.GetKeyDown(VoiceChatKeybind))
            {
                SteamUser.StartVoiceRecording();
                UsingMicrophone = true;
            }

            if (Input.GetKeyUp(VoiceChatKeybind))
            {
                SteamUser.StopVoiceRecording();
                UsingMicrophone = false;
            }

            SendVoiceData();
        }

        public static void TagPrefab(GameObject prefab, ulong mod = 0)
        {
            if (prefab.TryGetComponent(out PrefabTag _))
                return;

            var tag = prefab.AddComponent<PrefabTag>();
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
            if (!IsClient || !OptionsPatch.showHUD)
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
            }
            
            if (ChatManager.instance.SelectedChatPosition == 1) // Position to the right
            {
                ChatManager.instance.CreateChatArea(false, 500f, 200f, 370f, Screen.width - 510f);
            }
            else
            {
                ChatManager.instance.CreateChatArea(false);
            }
            
            // ChatManager.instance.CreateChatArea(false);

            if (UsingMicrophone)
                GUI.DrawTexture(new Rect(315f, Screen.height - 60f, 50f, 50f), MicTexture);
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
            ClientDestructibles.Clear();

            ClientCanSpawnBot = false;

            IsHost = false;

            IsClient = false;

            MarkerPosition = Vector3.zero;

            ChatManager.instance.CurrentChatMessage = string.Empty;
            ChatManager.instance.ChatScrollPosition = Vector2.zero;
            ChatManager.instance.JustFocused = false;
            ChatManager.instance.TypeIntention = false;
            ChatManager.instance.ChatMode = false;

            UsingMicrophone = false;
            PlayVoiceQueue.Clear();

            ReleaseProjectilePatch.ConfigCache.Clear();

            RSPatch.RSPatch.OwnedObjects.Clear();
            RSPatch.RSPatch.ClientObjects.Clear();
            RSPatch.RSPatch.TargetGameObjectState.Clear();
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

        public List<Actor> GetPlayers()
        {
            List<Actor> actors = new List<Actor>();
            foreach (var kv in IngameNetManager.instance.ClientActors)
            {
                var id = kv.Key;
                var actor = kv.Value;

                if (IngameNetManager.instance.OwnedActors.Contains(id))
                    continue;

                var controller = actor.controller as NetActorController;

                if ((controller.Flags & (int)ActorStateFlags.AiControlled) != 0)
                    continue;
                actors.Add(actor);
            }
            actors.Add(ActorManager.instance.player);
            return actors;
        }

        public void SendPacketToServer(byte[] data, PacketType type, int send_flags)
        {
            _totalOut++;

            using MemoryStream compressOut = new MemoryStream();
            using (DeflateStream deflateStream = new DeflateStream(compressOut, System.IO.Compression.CompressionLevel.Optimal))
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
                        foreach (var memberId in LobbySystem.instance.GetLobbyMembers())
                        {
                            if (info.m_identityRemote.GetSteamID() == memberId)
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

                        // We destroy all the actors that were left behind.
                        if (ServerConnections.Contains(pCallback.m_hConn))
                        {
                            ServerConnections.Remove(pCallback.m_hConn);

                            if (!ConnectionGuidMap.ContainsKey(pCallback.m_hConn))
                                break;

                            var guid = ConnectionGuidMap[pCallback.m_hConn];

                            if (!GuidActorOwnership.ContainsKey(guid))
                                break;

                            var actors = GuidActorOwnership[guid];
                            GuidActorOwnership.Remove(guid);

                            foreach (var id in actors)
                            {
                                if (!ClientActors.ContainsKey(id))
                                    continue;

                                var actor = ClientActors[id];

                                var controller = actor.controller as NetActorController;

                                if ((controller.Flags & (int)ActorStateFlags.AiControlled) == 0)
                                {
                                    var leaveMsg = $"{actor.name} has left the match.";

                                    ChatManager.instance.PushChatMessage(null, leaveMsg, true, -1);

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

                                {
                                    // Assume ownership so that we are allowed to kill the actor,
                                    // then release it so we don't try and send updates.
                                    OwnedActors.Add(id);
                                    DestroyActor(actor);
                                    OwnedActors.Remove(id);

                                    using MemoryStream memoryStream = new MemoryStream();
                                    var removeActorPacket = new RemoveActorPacket()
                                    {
                                        Id = id,
                                    };

                                    using (var writer = new ProtocolWriter(memoryStream))
                                    {
                                        writer.Write(removeActorPacket);
                                    }
                                    byte[] data = memoryStream.ToArray();

                                    SendPacketToServer(data, PacketType.RemoveActor, Constants.k_nSteamNetworkingSend_Reliable);
                                }
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
                                                    var enterMsg = $"{actor_packet.Name} has joined the match.";

                                                    ChatManager.instance.PushChatMessage(null, enterMsg, true, -1);

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
                                            if (!actor.dead)
                                                net_controller.SpawnedOnce = true;

                                            actor.controller = net_controller;

                                            actor.name = actor_packet.Name;
                                            actor.scoreboardEntry.UpdateNameLabel();

                                            // Here we set up the voice audio source for the player.
                                            // The audio packets are buffered and played only once enough 
                                            // data is recieved to prevent the audio from "cutting out"
                                            if ((actor_packet.Flags & (int)ActorStateFlags.AiControlled) == 0)
                                            {
                                                // As well as the nametag.
                                                UI.GameUI.instance.AddToNameTagQueue(actor);

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
                                                voiceSource.volume = VoiceChatVolume;
                                                voiceSource.Play();
                                            }
                                            ClientActors[actor_packet.Id] = actor;
                                            RSPatch.RavenscriptEventsManagerPatch.events.onPlayerJoin.Invoke(actor);
                                        }

                                        var controller = actor.controller as NetActorController;

                                        // Delay any possible race on the health value as much as possible.
                                        if (controller.DamageCooldown.TrueDone())
                                            actor.health = actor_packet.Health;

                                        controller.Targets = actor_packet;
                                        controller.Flags = actor_packet.Flags;
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
                                            ClientDestructibles[vehiclePacket.Id] = vehicle.gameObject;
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
                                    switch (GameModeBase.activeGameMode.gameModeType)
                                    {
                                        case GameModeType.Battalion:
                                            {
                                                var gameUpdatePacket = dataStream.ReadBattleStatePacket();

                                                var battleObj = GameModeBase.activeGameMode as BattleMode;

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
                                        case GameModeType.Domination:
                                            {
                                                var gameUpdatePacket = dataStream.ReadDominationStatePacket();

                                                var dominationObj = GameModeBase.activeGameMode as DominationMode;

                                                var currentBattalions = (int[])typeof(DominationMode).GetField("remainingBattalions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dominationObj);

                                                for (int i = 0; i < 2; i++)
                                                {
                                                    if (currentBattalions[i] > gameUpdatePacket.RemainingBattalions[i])
                                                    {
                                                        EndDominationRoundPatch.CanEndRound = true;
                                                        typeof(DominationMode).GetMethod("EndDominationRound", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dominationObj, new object[] { 1 - i });
                                                        EndDominationRoundPatch.CanEndRound = false;
                                                    }
                                                }

                                                typeof(DominationMode).GetField("remainingBattalions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dominationObj, gameUpdatePacket.RemainingBattalions);
                                                typeof(DominationMode).GetField("dominationRatio", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dominationObj, gameUpdatePacket.DominationRatio);

                                                for (int i = 0; i < gameUpdatePacket.SpawnPointOwners.Length; i++)
                                                {
                                                    if (ActorManager.instance.spawnPoints[i].owner != gameUpdatePacket.SpawnPointOwners[i])
                                                        ActorManager.instance.spawnPoints[i].SetOwner(gameUpdatePacket.SpawnPointOwners[i]);
                                                }

                                                var spawns = new CapturePoint[gameUpdatePacket.ActiveFlagSet.Length];
                                                for (int i = 0; i < gameUpdatePacket.ActiveFlagSet.Length; i++)
                                                {
                                                    spawns[i] = ActorManager.instance.spawnPoints[gameUpdatePacket.ActiveFlagSet[i]] as CapturePoint;
                                                }

                                                var flagSet = new DominationMode.FlagSet(spawns);
                                                typeof(DominationMode).GetField("activeFlagSet", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dominationObj, flagSet);

                                                var startDominationAction = (TimedAction)typeof(DominationMode).GetField("startDominationAction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dominationObj);

                                                if (gameUpdatePacket.TimeToStart > 0 && startDominationAction.Remaining() != gameUpdatePacket.TimeToStart)
                                                {
                                                    startDominationAction.StartLifetime(gameUpdatePacket.TimeToStart);
                                                    typeof(DominationMode).GetField("startDominationAction", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dominationObj, startDominationAction);
                                                }
                                            }
                                            break;
                                        case GameModeType.PointMatch:
                                            {
                                                var gameUpdatePacket = dataStream.ReadPointMatchStatePacket();

                                                var pointMatchObj = GameModeBase.activeGameMode as PointMatch;

                                                typeof(PointMatch).GetField("blueScore", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pointMatchObj, gameUpdatePacket.BlueScore);
                                                typeof(PointMatch).GetField("redScore", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pointMatchObj, gameUpdatePacket.RedScore);

                                                for (int i = 0; i < gameUpdatePacket.SpawnPointOwners.Length; i++)
                                                {
                                                    if (ActorManager.instance.spawnPoints[i].owner != gameUpdatePacket.SpawnPointOwners[i])
                                                        ActorManager.instance.spawnPoints[i].SetOwner(gameUpdatePacket.SpawnPointOwners[i]);
                                                }

                                                typeof(PointMatch).GetMethod("AddScore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pointMatchObj, new object[] { 0, 0 });
                                            }
                                            break;
                                        case GameModeType.Skirmish:
                                            {
                                                var gameUpdatePacket = dataStream.ReadSkirmishStatePacket();

                                                var skirmishObj = GameModeBase.activeGameMode as SkirmishMode;

                                                for (int i = 0; i < 2; i++)
                                                {
                                                    if (gameUpdatePacket.SpawningReinforcements[i])
                                                    {
                                                        SkirmishWavePatch.CanSpawnWave = true;
                                                        typeof(SkirmishMode).GetMethod("SpawnReinforcementWave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skirmishObj, new object[] { i });
                                                        SkirmishWavePatch.CanSpawnWave = false;
                                                    }
                                                }

                                                typeof(SkirmishMode).GetField("domination", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skirmishObj, gameUpdatePacket.Domination);
                                                typeof(SkirmishMode).GetField("reinforcementWavesRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skirmishObj, gameUpdatePacket.WavesRemaining);

                                                for (int i = 0; i < gameUpdatePacket.SpawnPointOwners.Length; i++)
                                                {
                                                    if (ActorManager.instance.spawnPoints[i].owner != gameUpdatePacket.SpawnPointOwners[i])
                                                        ActorManager.instance.spawnPoints[i].SetOwner(gameUpdatePacket.SpawnPointOwners[i]);
                                                }

                                                var lockDominationAction = (TimedAction)typeof(SkirmishMode).GetField("lockDominationAction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skirmishObj);

                                                if (gameUpdatePacket.TimeToDominate > 0 && lockDominationAction.Remaining() != gameUpdatePacket.TimeToDominate)
                                                {
                                                    lockDominationAction.StartLifetime(gameUpdatePacket.TimeToDominate);
                                                    typeof(SkirmishMode).GetField("lockDominationAction", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skirmishObj, lockDominationAction);
                                                }
                                            }
                                            break;
                                        case GameModeType.SpecOps:
                                            {
                                                var gameUpdatePacket = dataStream.ReadSpecOpsStatePacket();

                                                var specOpsObj = GameModeBase.activeGameMode as SpecOpsMode;

                                                for (int i = 0; i < gameUpdatePacket.SpawnPointOwners.Length; i++)
                                                {
                                                    if (ActorManager.instance.spawnPoints[i].owner != gameUpdatePacket.SpawnPointOwners[i])
                                                        ActorManager.instance.spawnPoints[i].SetOwner(gameUpdatePacket.SpawnPointOwners[i]);
                                                }

                                                if (specOpsObj.activeScenarios == null)
                                                {
                                                    specOpsObj.activeScenarios = new List<SpecOpsScenario>();
                                                    specOpsObj.activePatrols = new List<SpecOpsPatrol>();
                                                    specOpsObj.activeObjectives = new List<SpecOpsObjective>();
                                                    specOpsObj.scenarioAtSpawn = new Dictionary<SpawnPoint, SpecOpsScenario>();

                                                    var ActivateScenario = typeof(SpecOpsMode).GetMethod("ActivateScenario", BindingFlags.Instance | BindingFlags.NonPublic);

                                                    foreach (var scenario in gameUpdatePacket.Scenarios)
                                                    {
                                                        SpecOpsScenario actualScenario;

                                                        if (scenario is AssassinateScenarioPacket)
                                                        {
                                                            var newScenario = new AssassinateScenario();
                                                            actualScenario = newScenario;
                                                        }
                                                        else if (scenario is ClearScenarioPacket)
                                                        {
                                                            var newScenario = new ClearScenario();
                                                            actualScenario = newScenario;
                                                        }
                                                        else if (scenario is DestroyScenarioPacket)
                                                        {
                                                            var newScenario = new DestroyScenario();
                                                            typeof(DestroyScenario).GetField("targetVehicle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newScenario, ClientVehicles[(scenario as DestroyScenarioPacket).TargetVehicle]);
                                                            actualScenario = newScenario;
                                                        }
                                                        else
                                                        {
                                                            var newScenario = new SabotageScenario();
                                                            var targets = new List<Destructible>();
                                                            foreach (var id in (scenario as SabotageScenarioPacket).Targets)
                                                            {
                                                                targets.Add(ClientDestructibles[id].GetComponentInChildren<Destructible>());
                                                            }
                                                            typeof(SabotageScenario).GetField("targets", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newScenario, targets);
                                                            typeof(SabotageScenario).GetField("targetsToDestroy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newScenario, targets.Count - 2);
                                                            actualScenario = newScenario;
                                                        }

                                                        var spawn = ActorManager.instance.spawnPoints[scenario.Spawn];

                                                        actualScenario.actors = new List<Actor>(scenario.Actors.Count);
                                                        foreach (var actor in scenario.Actors)
                                                        {
                                                            actualScenario.actors.Add(ClientActors[actor]);
                                                        }
                                                        Order order = new Order(Order.OrderType.PatrolBase, spawn, spawn, enabled: true);
                                                        actualScenario.squad = new Squad(actualScenario.actors, spawn, order, null, 0f)
                                                        {
                                                            allowRequestNewOrders = false
                                                        };
                                                        actualScenario.squad.SetNotAlert(limitSpeed: true);

                                                        typeof(SpecOpsMode).GetMethod("ActivateScenario", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(specOpsObj, new object[] { actualScenario, spawn });
                                                    }
                                                    ObjectiveUi.SortEntries();

                                                    //typeof(SpecOpsMode).GetField("gameIsRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(specOpsObj, gameUpdatePacket.GameIsRunning);
                                                }

                                                if (specOpsObj.attackerSpawnPosition == default)
                                                {
                                                    typeof(SpecOpsMode).GetMethod("InitializeAttackerSpawn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(specOpsObj, new object[] { gameUpdatePacket.AttackerSpawn });
                                                }

                                                if (specOpsObj.attackerSquad == null && FpsActorController.instance.playerSquad != null)
                                                {
                                                    var attackers = new List<Actor>
                                                    {
                                                        FpsActorController.instance.actor
                                                    };
                                                    foreach (var actor in ClientActors.Values)
                                                    {
                                                        if (actor != FpsActorController.instance.actor && actor.team == FpsActorController.instance.actor.team)
                                                        {
                                                            attackers.Add(actor);
                                                            FpsActorController.instance.playerSquad.AddMember(actor.controller);
                                                        }
                                                    }
                                                    specOpsObj.attackerActors = attackers.ToArray();
                                                    specOpsObj.attackerSquad = FpsActorController.instance.playerSquad;
                                                }
                                            }
                                            break;
                                        case GameModeType.Haunted:
                                            {
                                                var gameUpdatePacket = dataStream.ReadHauntedStatePacket();

                                                var hauntedObj = GameModeBase.activeGameMode as SpookOpsMode;

                                                typeof(SpookOpsMode).GetField("skeletonCountModifier", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hauntedObj, gameUpdatePacket.SkeletonCountModifier);

                                                typeof(SpookOpsMode).GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hauntedObj, gameUpdatePacket.CurrentPhase);

                                                var newSpawn = ActorManager.instance.spawnPoints[gameUpdatePacket.CurrentSpawnPoint];
                                                var currentSpawn = (SpawnPoint)typeof(SpookOpsMode).GetField("currentSpawnPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj);

                                                if (currentSpawn != newSpawn)
                                                {
                                                    var PreparePhase = typeof(SpookOpsMode).GetMethod("PreparePhase", BindingFlags.Instance | BindingFlags.NonPublic);
                                                    var WAVE_SKELETON_COUNT = (int[])typeof(SpookOpsMode).GetField("WAVE_SKELETON_COUNT", BindingFlags.Static | BindingFlags.NonPublic).GetValue(hauntedObj);
                                                    var WAVE_SKELETON_TICKETS = (int[])typeof(SpookOpsMode).GetField("WAVE_SKELETON_TICKETS", BindingFlags.Static | BindingFlags.NonPublic).GetValue(hauntedObj);

                                                    PreparePhase.Invoke(hauntedObj, new object[] { newSpawn, WAVE_SKELETON_COUNT[gameUpdatePacket.CurrentPhase], WAVE_SKELETON_TICKETS[gameUpdatePacket.CurrentPhase] });
                                                }

                                                typeof(SpookOpsMode).GetField("killCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hauntedObj, gameUpdatePacket.KillCount);
                                                if (gameUpdatePacket.PhaseEnded && !(bool)typeof(SpookOpsMode).GetField("phaseEnded", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj))
                                                {
                                                    typeof(SpookOpsMode).GetMethod("EndPhase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hauntedObj, null);
                                                }
                                                else
                                                {
                                                    typeof(SpookOpsMode).GetMethod("UpdateUi", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hauntedObj, null);
                                                }

                                                if (!gameUpdatePacket.AwaitingNextPhase && (bool)typeof(SpookOpsMode).GetField("awaitingNextPhase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj))
                                                {
                                                    StartPhasePatch.CanPerform = true;
                                                    typeof(SpookOpsMode).GetMethod("StartPhase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hauntedObj, null);
                                                    StartPhasePatch.CanPerform = false;
                                                }

                                                var newPlayerSpawn = ActorManager.instance.spawnPoints[gameUpdatePacket.PlayerSpawn];
                                                var playerSpawn = (SpawnPoint)typeof(SpookOpsMode).GetField("playerSpawn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj);

                                                if (playerSpawn != newPlayerSpawn)
                                                {
                                                    typeof(SpookOpsMode).GetField("playerSpawn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hauntedObj, newPlayerSpawn);
                                                    FpsActorController.instance.actor.SpawnAt(newPlayerSpawn.GetSpawnPosition(), Quaternion.identity);
                                                }

                                                HauntedActorDiedPatch.CheckLoseCondition();
                                            }
                                            break;
                                        default:
                                            Plugin.logger.LogError("Got game mode update for unsupported type?");
                                            break;
                                    }
                                }
                                break;
                            case PacketType.SpecOpsFlare:
                                {
                                    var flarePacket = dataStream.ReadFireFlarePacket();

                                    if (GameModeBase.activeGameMode.gameModeType != GameModeType.SpecOps)
                                    {
                                        Plugin.logger.LogError("Attempted to fire flare while not in spec ops.");
                                        break;
                                    }

                                    var actor = ClientActors[flarePacket.Actor];
                                    var spawn = ActorManager.instance.spawnPoints[flarePacket.Spawn];

                                    var specOpsObj = GameModeBase.activeGameMode as SpecOpsMode;
                                    specOpsObj.FireFlare(actor, spawn);
                                }
                                break;
                            case PacketType.SpecOpsSequence:
                                {
                                    var sequencePacket = dataStream.ReadSpecOpsSequencePacket();

                                    switch (sequencePacket.Sequence)
                                    {
                                        case SpecOpsSequencePacket.SequenceType.ExfiltrationVictory:
                                            ExfiltrationVictorySequencePatch.CanPerform = true;
                                            StartCoroutine(typeof(SpecOpsMode).GetMethod("ExfiltrationVictorySequence", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(GameModeBase.activeGameMode, null) as IEnumerator);
                                            ExfiltrationVictorySequencePatch.CanPerform = false;
                                            break;
                                        case SpecOpsSequencePacket.SequenceType.StealthVictory:
                                            StealthVictorySequencePatch.CanPerform = true;
                                            StartCoroutine(typeof(SpecOpsMode).GetMethod("StealthVictorySequence", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(GameModeBase.activeGameMode, null) as IEnumerator);
                                            StealthVictorySequencePatch.CanPerform = false;
                                            break;
                                        case SpecOpsSequencePacket.SequenceType.Defeat:
                                            DefeatSequencePatch.CanPerform = true;
                                            StartCoroutine(typeof(SpecOpsMode).GetMethod("DefeatSequence", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(GameModeBase.activeGameMode, null) as IEnumerator);
                                            DefeatSequencePatch.CanPerform = false;
                                            break;
                                    }
                                }
                                break;
                            case PacketType.SpecOpsDialog:
                                {
                                    var dialogPacket = dataStream.ReadSpecOpsDialogPacket();

                                    if (dialogPacket.Hide)
                                        IngameDialog.Hide();
                                    else
                                        IngameDialog.PrintActorText(dialogPacket.ActorPose, dialogPacket.Text, dialogPacket.OverrideName);
                                }
                                break;
                            case PacketType.SpawnProjectile:
                                {
                                    var spawnPacket = dataStream.ReadSpawnProjectilePacket();

                                    if (OwnedActors.Contains(spawnPacket.SourceId))
                                        break;

                                    var actor = spawnPacket.SourceId == -1 ? null : ClientActors[spawnPacket.SourceId];

                                    var tag = new Tuple<int, ulong>(spawnPacket.NameHash, spawnPacket.Mod);

                                    if (!PrefabCache.ContainsKey(tag))
                                    {
                                        Plugin.logger.LogError($"Cannot find projectile prefab with this tagging.");
                                        continue;
                                    }

                                    var prefab = PrefabCache[tag];
                                    var projectile = ProjectilePoolManager.InstantiateProjectile(prefab, spawnPacket.Position, spawnPacket.Rotation);

                                    projectile.killCredit = actor;
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
                                    OwnedProjectiles.Remove(spawnPacket.ProjectileId);

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
                                        projectile.enabled = projectilePacket.Enabled;
                                    }
                                }
                                break;
                            case PacketType.Explode:
                                {
                                    var explodePacket = dataStream.ReadExplodeProjectilePacket();

                                    if (OwnedProjectiles.Contains(explodePacket.Id))
                                        continue;

                                    if (!ClientProjectiles.ContainsKey(explodePacket.Id))
                                        continue;

                                    Projectile projectile = ClientProjectiles[explodePacket.Id];

                                    if (projectile == null)
                                        continue;

                                    // RemoteDetonatedProjectiles don't explode like normal projectiles.
                                    if (projectile.GetType() == typeof(RemoteDetonatedProjectile))
                                    {
                                        Plugin.logger.LogInfo($"Detonate.");
                                        (projectile as RemoteDetonatedProjectile).Detonate();
                                    }
                                    else
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
                                break;
                            case PacketType.Chat:
                                {
                                    var chatPacket = dataStream.ReadChatPacket();

                                    var actor = ClientActors.ContainsKey(chatPacket.Id) ? ClientActors[chatPacket.Id] : null;
                                    if (actor == null)
                                        ChatManager.instance.PushChatMessage(null, chatPacket.Message, true, -1);
                                    else
                                        ChatManager.instance.PushChatMessage(actor, chatPacket.Message, !chatPacket.TeamOnly, actor.team);
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
                            case PacketType.KickAnimation:
                                {
                                    Plugin.logger.LogDebug("Kick Animation Packet");
                                    var kickPacket = dataStream.ReadKickAnimationPacket();

                                    var actor = ClientActors[kickPacket.Id];

                                    if (actor == null)
                                        break;

                                    Plugin.logger.LogDebug($"Receiving Kick Animation Packet from: {actor.name}");

                                    StartCoroutine(KickAnimation.PerformKick(actor));
                                }
                                break;
                            case PacketType.UpdateDestructible:
                                {
                                    var bulkDestructiblePacket = dataStream.ReadBulkDestructiblePacket();

                                    if (bulkDestructiblePacket.Updates == null)
                                        break;

                                    foreach (DestructiblePacket destructiblePacket in bulkDestructiblePacket.Updates)
                                    {
                                        if (!ClientDestructibles.ContainsKey(destructiblePacket.Id))
                                        {
                                            if (!destructiblePacket.FullUpdate)
                                                continue;

                                            var tag = new Tuple<int, ulong>(destructiblePacket.NameHash, destructiblePacket.Mod);

                                            if (!PrefabCache.ContainsKey(tag))
                                            {
                                                Plugin.logger.LogError($"Cannot find destructible prefab with this tagging.");
                                                continue;
                                            }

                                            var prefab = PrefabCache[tag];
                                            var newRoot = Instantiate(prefab, destructiblePacket.Position, destructiblePacket.Rotation);
                                            newRoot.AddComponent<GuidComponent>().guid = destructiblePacket.Id;

                                            ClientDestructibles.Add(destructiblePacket.Id, newRoot);
                                        }

                                        var root = ClientDestructibles[destructiblePacket.Id];

                                        if (root == null)
                                            continue;

                                        if (destructiblePacket.FullUpdate)
                                        {
                                            root.transform.position = destructiblePacket.Position;
                                            root.transform.rotation = destructiblePacket.Rotation;
                                        }

                                        var destructibles = DestructiblePacket.GetDestructibles(root);
                                        for (int i = 0; i < destructibles.Length; i++)
                                        {
                                            var destructible = destructibles[i];

                                            if (!destructible.isDead && destructiblePacket.States[i])
                                                destructible.Shatter();
                                        }
                                    }
                                }
                                break;
                            case PacketType.DestroyDestructible:
                                {
                                    var destroyPacket = dataStream.ReadDestructibleDiePacket();

                                    var root = ClientDestructibles[destroyPacket.Id];
                                    var destructible = DestructiblePacket.GetDestructibles(root)[destroyPacket.Index];

                                    if (destructible == null || destructible.isDead)
                                        break;

                                    destructible.Shatter();
                                }
                                break;
                            case PacketType.ChatCommand:
                                {
                                    var commandPacket = dataStream.ReadChatCommandPacket();

                                    var actor = ClientActors.ContainsKey(commandPacket.Id) ? ClientActors[commandPacket.Id] : null;
                                    bool inLobby = LobbySystem.instance.InLobby;

                                    if (!inLobby && actor != null)
                                    {
                                         ChatManager.instance.ProcessChatCommand(commandPacket.Command, actor, commandPacket.SteamID, false);
                                    }
                                    else
                                    {
                                        ChatManager.instance.ProcessLobbyChatCommand(commandPacket.Command, commandPacket.SteamID, false);
                                    }

                                }
                                break;
                            case PacketType.Countermeasures:
                                {
                                    var countermeasuresPacket = dataStream.ReadCountermeasuresPacket();

                                    if (!ClientVehicles.ContainsKey(countermeasuresPacket.VehicleId))
                                        break;

                                    Vehicle targetVehicle = ClientVehicles[countermeasuresPacket.VehicleId];
                                    if (targetVehicle == null)
                                        break;

                                    typeof(Vehicle).GetMethod("PopCountermeasures", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(targetVehicle, null);
                                }
                                break;
                            case PacketType.RemoveActor:
                                {
                                    var removeActorPacket = dataStream.ReadRemoveActorPacket();

                                    if (!ClientActors.ContainsKey(removeActorPacket.Id) || OwnedActors.Contains(removeActorPacket.Id))
                                        break;

                                    Actor actor = ClientActors[removeActorPacket.Id];
                                    if (actor == null)
                                        break;

                                    // Assume ownership so that we are allowed to kill the actor,
                                    // then release it so we don't try and send updates.
                                    OwnedActors.Add(removeActorPacket.Id);
                                    DestroyActor(actor);
                                    OwnedActors.Remove(removeActorPacket.Id);
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

                SendActorStates();

                SendVehicleStates();

                SendProjectileUpdates();

                SendDestructibleUpdates();

                SendGameState();
            }
        }

        public void SendGameState()
        {
            if (!IsHost)
                return;

            byte[] data = null;

            switch (GameModeBase.activeGameMode.gameModeType)
            {
                case GameModeType.Battalion:
                    {
                        var battleObj = GameModeBase.activeGameMode as BattleMode;

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
                case GameModeType.Domination:
                    {
                        var dominationObj = GameModeBase.activeGameMode as DominationMode;

                        var flagSet = (DominationMode.FlagSet)typeof(DominationMode).GetField("activeFlagSet", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dominationObj);

                        var gamePacket = new DominationStatePacket
                        {
                            RemainingBattalions = (int[])typeof(DominationMode).GetField("remainingBattalions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dominationObj),
                            DominationRatio = (float[])typeof(DominationMode).GetField("dominationRatio", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dominationObj),
                            SpawnPointOwners = new int[ActorManager.instance.spawnPoints.Length],
                            ActiveFlagSet = new int[flagSet.flags.Length],
                            TimeToStart = Mathf.CeilToInt(((TimedAction)typeof(DominationMode).GetField("startDominationAction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dominationObj)).Remaining()),
                        };

                        for (int i = 0; i < ActorManager.instance.spawnPoints.Length; i++)
                        {
                            gamePacket.SpawnPointOwners[i] = ActorManager.instance.spawnPoints[i].owner;
                        }

                        for (int i = 0; i < flagSet.flags.Length; i++)
                        {
                            gamePacket.ActiveFlagSet[i] = Array.IndexOf(ActorManager.instance.spawnPoints, flagSet.flags[i]);
                        }

                        using MemoryStream memoryStream = new MemoryStream();

                        using (var writer = new ProtocolWriter(memoryStream))
                        {
                            writer.Write(gamePacket);
                        }
                        data = memoryStream.ToArray();
                    }
                    break;
                case GameModeType.PointMatch:
                    {
                        var pointMatchObj = GameModeBase.activeGameMode as PointMatch;

                        var gamePacket = new PointMatchStatePacket
                        {
                            BlueScore = (int)typeof(PointMatch).GetField("blueScore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointMatchObj),
                            RedScore = (int)typeof(PointMatch).GetField("redScore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointMatchObj),
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
                case GameModeType.Skirmish:
                    {
                        var skirmishObj = GameModeBase.activeGameMode as SkirmishMode;

                        var gamePacket = new SkirmishStatePacket
                        {
                            Domination = (float)typeof(SkirmishMode).GetField("domination", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skirmishObj),
                            SpawningReinforcements = (bool[])typeof(SkirmishMode).GetField("spawningReinforcements", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skirmishObj),
                            WavesRemaining = (int[])typeof(SkirmishMode).GetField("reinforcementWavesRemaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skirmishObj),
                            SpawnPointOwners = new int[ActorManager.instance.spawnPoints.Length],
                            TimeToDominate = Mathf.CeilToInt(((TimedAction)typeof(SkirmishMode).GetField("lockDominationAction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skirmishObj)).Remaining()),
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
                case GameModeType.SpecOps:
                    {
                        var specOpsObj = GameModeBase.activeGameMode as SpecOpsMode;

                        var gamePacket = new SpecOpsStatePacket
                        {
                            AttackerSpawn = specOpsObj.attackerSpawnPosition,
                            SpawnPointOwners = new int[ActorManager.instance.spawnPoints.Length],
                            Scenarios = new List<ScenarioPacket>(specOpsObj.activeScenarios.Count),
                            GameIsRunning = (bool)typeof(SpecOpsMode).GetField("gameIsRunning", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(specOpsObj),
                        };

                        for (int i = 0; i < ActorManager.instance.spawnPoints.Length; i++)
                        {
                            gamePacket.SpawnPointOwners[i] = ActorManager.instance.spawnPoints[i].owner;
                        }

                        foreach (var scenario in specOpsObj.activeScenarios)
                        {
                            ScenarioPacket packet;

                            if (scenario is AssassinateScenario)
                            {
                                var scenarioPacket = new AssassinateScenarioPacket();
                                packet = scenarioPacket;
                            }
                            else if (scenario is ClearScenario)
                            {
                                var scenarioPacket = new ClearScenarioPacket();
                                packet = scenarioPacket;
                            }
                            else if (scenario is DestroyScenario)
                            {
                                var scenarioPacket = new DestroyScenarioPacket();
                                var target = (Vehicle)typeof(DestroyScenario).GetField("targetVehicle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scenario);
                                scenarioPacket.TargetVehicle = target != null ? target.GetComponent<GuidComponent>().guid : 0;
                                packet = scenarioPacket;
                            }
                            else
                            {
                                var scenarioPacket = new SabotageScenarioPacket();
                                var targets = (List<Destructible>)typeof(SabotageScenario).GetField("targets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scenario);
                                scenarioPacket.Targets = new List<int>(targets.Count);
                                foreach (var target in targets)
                                {
                                    scenarioPacket.Targets.Add(DestructiblePacket.Root(target).GetComponent<GuidComponent>().guid);
                                }
                                packet = scenarioPacket;
                            }

                            packet.Spawn = Array.IndexOf(ActorManager.instance.spawnPoints, scenario.spawn);
                            packet.Actors = new List<int>(scenario.actors.Count);
                            foreach (var actor in scenario.actors)
                            {
                                packet.Actors.Add(actor.GetComponent<GuidComponent>().guid);
                            }

                            gamePacket.Scenarios.Add(packet);
                        }

                        using MemoryStream memoryStream = new MemoryStream();

                        using (var writer = new ProtocolWriter(memoryStream))
                        {
                            writer.Write(gamePacket);
                        }
                        data = memoryStream.ToArray();
                    }
                    break;
                case GameModeType.Haunted:
                    {
                        var hauntedObj = GameModeBase.activeGameMode as SpookOpsMode;

                        var gamePacket = new HauntedStatePacket
                        {
                            CurrentSpawnPoint = Array.IndexOf(ActorManager.instance.spawnPoints, (SpawnPoint)typeof(SpookOpsMode).GetField("currentSpawnPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj)),
                            PlayerSpawn = Array.IndexOf(ActorManager.instance.spawnPoints, (SpawnPoint)typeof(SpookOpsMode).GetField("playerSpawn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj)),
                            CurrentPhase = (int)typeof(SpookOpsMode).GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj),
                            KillCount = (int)typeof(SpookOpsMode).GetField("killCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj),
                            AwaitingNextPhase = (bool)typeof(SpookOpsMode).GetField("awaitingNextPhase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj),
                            PhaseEnded = (bool)typeof(SpookOpsMode).GetField("phaseEnded", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj),
                            SkeletonCountModifier = (float)typeof(SpookOpsMode).GetField("skeletonCountModifier", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hauntedObj),
                        };

                        HauntedActorDiedPatch.CheckLoseCondition();

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
            if (!actor.dead && actor.controller.Crouch()) flags |= (int)ActorStateFlags.Crouch;
            if (!actor.dead && actor.controller.WantsToFire()) flags |= (int)ActorStateFlags.Fire;
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

            SendPacketToServer(data, PacketType.ActorFlags, Constants.k_nSteamNetworkingSend_Reliable);
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
                                            : actor.activeWeapon.weaponEntry?.name.GetHashCode() ?? 0
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
                if (projectile.GetType() == typeof(ExplodingProjectile) || !projectile.enabled)
                    continue;

                var net_projectile = new UpdateProjectilePacket
                {
                    Id = owned_projectile,
                    Position = projectile.transform.position,
                    Velocity = projectile.velocity,
                    Enabled = projectile.enabled,
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

        public void SendDestructibleUpdates()
        {
            // Host has complete ownership.
            if (!IsHost)
                return;

            var bulkDestructiblePacket = new BulkDestructiblePacket
            {
                Updates = new List<DestructiblePacket>(),
            };

            foreach (var kv in ClientDestructibles)
            {
                var id = kv.Key;
                var root = kv.Value;

                if (root == null)
                    continue;

                var tag = root.GetComponent<PrefabTag>();

                var destructibles = DestructiblePacket.GetDestructibles(root);
                var states = new BitArray(destructibles.Length);
                for (int i = 0; i < destructibles.Length; i++)
                {
                    var destructible = destructibles[i];

                    states.Set(i, destructible.isDead);
                }

                var net_destructible = new DestructiblePacket
                {
                    Id = id,
                    FullUpdate = !root.TryGetComponent(out Vehicle _),
                    NameHash = tag.NameHash,
                    Mod = tag.Mod,
                    Position = root.transform.position,
                    Rotation = root.transform.rotation,
                    States = states,
                };

                bulkDestructiblePacket.Updates.Add(net_destructible);
            }

            if (bulkDestructiblePacket.Updates.Count == 0)
                return;

            using MemoryStream memoryStream = new MemoryStream();

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(bulkDestructiblePacket);
            }
            byte[] data = memoryStream.ToArray();

            SendPacketToServer(data, PacketType.UpdateDestructible, Constants.k_nSteamNetworkingSend_Unreliable);
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
            Destroy(actor.scoreboardEntry.gameObject);

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

            UI.GameUI.instance.RemoveNameTag(actor);

            actor.Deactivate();
            ActorManager.Drop(actor);
            Destroy(actor.controller);
            Destroy(actor);
        }
    }
}