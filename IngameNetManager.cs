using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    [HarmonyPatch(typeof(FpsActorController), "Update")]
    public class NoSlowmoPatch
    {
        // We patch the target slow motion speed with the normal execution speed (1.0f)
        // which results in slow-mo being a no-op.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == 0.2f)
                    instruction.operand = 1.0f;

                yield return instruction;
            }
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

        public int BotIdGen = 0;

        public Guid OwnGUID = Guid.NewGuid();

        public TimedAction MainSendTick = new TimedAction(1.0f / 10);

        public Dictionary<int, int> ActorStateCache = new Dictionary<int, int>();

        public HashSet<int> OwnedActors = new HashSet<int>();

        public Dictionary<int, Actor> ClientActors = new Dictionary<int, Actor>();

        public HashSet<int> OwnedVehicles = new HashSet<int>();

        public Dictionary<int, Vehicle> ClientVehicles = new Dictionary<int, Vehicle>();

        public List<TurretSpawner> TurretSpawners = new List<TurretSpawner>();

        public HashSet<int> RemoteDeadVehicles = new HashSet<int>();

        public Dictionary<int, VehiclePacket> TargetVehicleStates = new Dictionary<int, VehiclePacket>();

        public HashSet<int> OwnedProjectiles = new HashSet<int>();

        public Dictionary<int, Projectile> ClientProjectiles = new Dictionary<int, Projectile>();

        public int ActorToSpawnProjectile = 0;

        public bool ClientCanSpawnBot = false;

        public HSteamNetConnection C2SConnection;

        /// Server owned
        public HSteamListenSocket ServerSocket;

        public HSteamNetPollGroup PollGroup;

        public List<HSteamNetConnection> ServerConnections = new List<HSteamNetConnection>();
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
        }

        private void Start()
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();

            Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatus);
        }

        private void LateUpdate()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Time.timeScale / 60f;
            GameManager.instance?.sfxMixer?.SetFloat("pitch", Time.timeScale);
        }

        private void Update()
        {
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

        private void DrawMarker(Vector3 worldPos)
        {
            if (worldPos != Vector3.zero)
            {
                var camera = FpsActorController.instance.GetActiveCamera();
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

                DrawMarker(controller.Targets.MarkerPosition ?? Vector3.zero);

                Vector3 vector = FpsActorController.instance.GetActiveCamera().WorldToScreenPoint(actor.CenterPosition() + new Vector3(0, 1f, 0));

                if (vector.z < 0f || actor.team != GameManager.PlayerTeam())
                {
                    continue;
                }

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

            string color = !global ? "green" : (team == 0 ? "blue" : "red");

            FullChatLink += $"<color={color}><b><{name}></b></color> {message}\n";
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
            TurretSpawners.Clear();
            TargetVehicleStates.Clear();
            OwnedProjectiles.Clear();
            ClientProjectiles.Clear();

            ActorToSpawnProjectile = 0;
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
        }

        public void OpenRelay()
        {
            Plugin.logger.LogInfo("Starting server socket for connections.");

            ServerConnections.Clear();
            ServerSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);

            PollGroup = SteamNetworkingSockets.CreatePollGroup();

            IsHost = true;
        }

        public void StartAsServer()
        {
            Plugin.logger.LogInfo("Starting server and client.");

            ResetState();

            IsHost = true;

            IsClient = true;

            foreach (var actor in FindObjectsOfType<Actor>())
            {
                int id = actor.aiControlled ? BotIdGen++ : RandomGen.Next(13337, int.MaxValue);

                actor.gameObject.AddComponent<GuidComponent>().guid = id;

                ClientActors.Add(id, actor);
                OwnedActors.Add(id);
            }

            foreach (var vehicle in FindObjectsOfType<Vehicle>(includeInactive: true))
            {
                int id = RandomGen.Next(0, int.MaxValue);

                vehicle.gameObject.AddComponent<GuidComponent>().guid = id;

                ClientVehicles.Add(id, vehicle);
                OwnedVehicles.Add(id);
            }

            foreach (var turretSpawner in FindObjectsOfType<TurretSpawner>())
            {
                TurretSpawners.Add(turretSpawner);
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

            ResetState();

            IsClient = true;

            var player = ActorManager.instance.player;
            {
                int id = RandomGen.Next(13337, int.MaxValue);

                player.gameObject.AddComponent<GuidComponent>().guid = id;

                ClientActors.Add(id, player);
                OwnedActors.Add(id);
            }

            foreach (var vehicle in FindObjectsOfType<Vehicle>(includeInactive: true))
            {
                ActorManager.DropVehicle(vehicle);
                typeof(Vehicle).GetMethod("Cleanup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vehicle, new object[] { });
                vehicle.gameObject.SetActive(false);
            }

            foreach (var vehicle_spawner in FindObjectsOfType<VehicleSpawner>())
            {
                Destroy(vehicle_spawner);
            }

            foreach (var spawn in ActorManager.instance.spawnPoints)
            {
                spawn.turretSpawners.Clear();
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
                C2SConnection = SteamNetworkingSockets.ConnectP2P(ref iden, 0, 0, null);

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

                        if (ServerConnections.Contains(pCallback.m_hConn))
                            ServerConnections.Remove(pCallback.m_hConn);

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

                        ResetState();
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
                                            if (!vehiclePacket.IsTurret)
                                            {
                                                Plugin.logger.LogInfo($"New vehicle registered with ID {vehiclePacket.Id} type {vehiclePacket.Type}");
                                                vehicle = VehicleSpawner.SpawnVehicleAt(vehiclePacket.Position, vehiclePacket.Rotation, vehiclePacket.Team, vehiclePacket.Type);

                                                var fakeSpawner = vehicle.gameObject.AddComponent<VehicleSpawner>();
                                                fakeSpawner.typeToSpawn = vehiclePacket.Type;
                                                vehicle.spawner = fakeSpawner;

                                                vehicle.gameObject.AddComponent<GuidComponent>().guid = vehiclePacket.Id;

                                                ClientVehicles[vehiclePacket.Id] = vehicle;
                                            }
                                            else
                                            {
                                                Plugin.logger.LogInfo($"New turret with ID {vehiclePacket.Id} and type {vehiclePacket.TurretType}");
                                                vehicle = TurretSpawner.SpawnTurretAt(vehiclePacket.Position, vehiclePacket.Rotation, vehiclePacket.Team, vehiclePacket.TurretType);

                                                vehicle.isTurret = true;

                                                vehicle.gameObject.AddComponent<GuidComponent>().guid = vehiclePacket.Id;

                                                ClientVehicles[vehiclePacket.Id] = vehicle;
                                            }
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
                                        else if (vehicle.health > 0 && vehicle.burning)
                                            typeof(Vehicle).GetMethod("StopBurning", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vehicle, new object[] { });
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
                                        actor.seat.vehicle.isInvulnerable = false;
                                    }   

                                    actor.LeaveSeat(false);
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

                                    var weapon = actor.activeWeapon;

                                    if (weapon == null)
                                        break;

                                    // Maybe move to a scope guard?
                                    ActorToSpawnProjectile = spawnPacket.SourceId;

                                    var projectile = (Projectile)weapon.GetType().GetMethod("SpawnProjectile", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, new object[] 
                                    {
                                        spawnPacket.Direction,
                                        spawnPacket.MuzzlePosition,
                                    });

                                    // Disable any form of damage from this projectile.
                                    projectile.configuration.damage = 0f;
                                    projectile.configuration.balanceDamage = 0f;
                                    projectile.autoAssignArmorDamage = false;
                                    projectile.armorDamage = Vehicle.ArmorRating.SmallArms;

                                    ActorToSpawnProjectile = 0;

                                    projectile.gameObject.AddComponent<GuidComponent>().guid = spawnPacket.ProjectileId;

                                    ClientProjectiles[spawnPacket.ProjectileId] = projectile;
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
                                            // This shouldn't ever not exist, since we send only for Rockets++
                                            if (Explode != null)
                                            {
                                                Explode.Invoke(projectile, new object[] { projectile.transform.position, projectile.transform.up });
                                            }
                                        }
                                    }
                                }
                                break;
                            case PacketType.Chat:
                                {
                                    var chatPacket = dataStream.ReadChatPacket();

                                    var actor = ClientActors[chatPacket.Id];

                                    if (actor == null)
                                        break;

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
                        }
                    }

                    // SR7, pls update Steamworks.NET.
                    var NativeMethods = Type.GetType("Steamworks.NativeMethods, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                    NativeMethods.GetMethod("SteamAPI_SteamNetworkingMessage_t_Release", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { msg_ptr[msg_index] });
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

                SendTurretStates();

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
                                            : actor.activeWeapon.name.GetHashCode()
                                        : 0,
                    Team = actor.team,
                    MarkerPosition = actor.aiControlled ? null : (Vector3?)MarkerPosition,
                    Flags = GenerateFlags(actor),
                    Ammo = !actor.dead && actor.activeWeapon != null ? actor.activeWeapon.ammo : 0,
                    Health = actor.health,
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

                // This pass is for normal vehicles, i.e. not turrets.
                if (vehicle.spawner == null)
                    continue;

                var net_vehicle = new VehiclePacket
                {
                    Id = owned_vehicle,
                    Position = vehicle.transform.position,
                    Rotation = vehicle.transform.rotation,
                    Type = vehicle.spawner.typeToSpawn,
                    Team = vehicle.spawner.GetOwner(),
                    Health = vehicle.health,
                    Dead = vehicle.dead,
                    IsTurret = false,
                    TurretType = 0,
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

        public void SendTurretStates()
        {
            // Turret updates use the same packets as regular vehicle updates.
            var bulkTurretUpdate = new BulkVehicleUpdate
            {
                Updates = new List<VehiclePacket>(),
            };

            foreach (var turret_spawner in TurretSpawners)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vehicle vehicle = turret_spawner.spawnedTurret[i];

                    if (vehicle == null)
                        continue;

                    var turret_id = vehicle.GetComponent<GuidComponent>().guid;

                    if (!OwnedVehicles.Contains(turret_id))
                        continue;

                    var net_turret = new VehiclePacket
                    {
                        Id = turret_id,
                        Position = vehicle.transform.position,
                        Rotation = vehicle.transform.rotation,
                        Type = 0,
                        Team = turret_spawner.spawnedTurret[0] != null ? 0 : 1,
                        Health = vehicle.health,
                        Dead = vehicle.dead,
                        IsTurret = true,
                        TurretType = turret_spawner.typeToSpawn,
                        Active = vehicle.gameObject.activeSelf,
                    };

                    bulkTurretUpdate.Updates.Add(net_turret);
                }
            }

            if (bulkTurretUpdate.Updates.Count == 0)
                return;

            using MemoryStream memoryStream = new MemoryStream();

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(bulkTurretUpdate);
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
                // there is a desync, like rockets++
                if (!typeof(Rocket).IsAssignableFrom(projectile.GetType()))
                    continue;

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

            SendPacketToServer(data, PacketType.UpdateProjectile, Constants.k_nSteamNetworkingSend_Unreliable);
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
            nextActorIndexF.SetValue(ActorManager.instance,  nextActorIndex - 1);

            ActorManager.Drop(actor);
            Destroy(actor.controller);
            Destroy(actor);
        }
    }
}
