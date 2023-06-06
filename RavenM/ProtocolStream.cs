using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace RavenM
{
    public class ProtocolWriter : BinaryWriter
    {
        public ProtocolWriter(Stream output) : base(output)
        {
        }

        /// <summary>
        /// Minecraft (lol) VarInt encoding. Adapted from https://wiki.vg/Protocol
        /// </summary>
        public override void Write(int value)
        {
            while (true)
            {
                if ((value & ~0x7F) == 0)
                {
                    Write((byte)value);
                    return;
                }

                Write((byte)((value & 0x7F) | 0x80));

                value = (int)((uint)value >> 7);
            }
        }

        public void Write(Vector2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        public void Write(Vector3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        public void Write(Vector4 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        public void Write(Quaternion value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        public void Write(Vector2? value)
        {
            if (value.HasValue)
            {
                Write(true);
                Write(value.Value);
            }
            else
                Write(false);
        }

        public void Write(Vector3? value)
        {
            if (value.HasValue)
            {
                Write(true);
                Write(value.Value);
            }
            else
                Write(false);
        }

        public void Write(Vector4? value)
        {
            if (value.HasValue)
            {
                Write(true);
                Write(value.Value);
            }
            else
                Write(false);
        }

        public void Write(Quaternion? value)
        {
            if (value.HasValue)
            {
                Write(true);
                Write(value.Value);
            }
            else
                Write(false);
        }

        public void Write(int[] value)
        {
            Write(value.Length);
            foreach (int n in value)
            {
                Write(n);
            }
        }

        public void Write(float[] value)
        {
            Write(value.Length);
            foreach (float n in value)
            {
                Write(n);
            }
        }

        public void Write(bool[] value)
        {
            Write(value.Length);
            foreach (bool n in value)
            {
                Write(n);
            }
        }

        public void Write(BitArray value)
        {
            var bytes = new byte[(value.Length - 1) / 8 + 1];
            value.CopyTo(bytes, 0);
            Write(bytes.Length);
            Write(bytes);
        }

        public void Write(ActorPacket value)
        {
            Write(value.Id);
            Write(value.Name);
            Write(value.Position);
            Write(value.Lean);
            Write(value.AirplaneInput);
            Write(value.BoatInput);
            Write(value.CarInput);
            Write(value.FacingDirection);
            Write(value.HelicopterInput);
            Write(value.LadderInput);
            Write(value.ParachuteInput);
            Write(value.RangeInput);
            Write(value.Velocity);
            Write(value.ActiveWeaponHash);
            Write(value.Team);
            Write(value.MarkerPosition);
            Write(value.Flags);
            Write(value.Ammo);
            Write(value.Health);
            Write(value.VehicleId);
            Write(value.Seat);
        }

        public void Write(ActorFlagsPacket value)
        {
            Write(value.Id);
            Write(value.StateVector);
        }

        public void Write(BulkActorUpdate value)
        {
            Write(value.Updates.Count);
            foreach (var update in value.Updates)
            {
                Write(update);
            }
        }

        public void Write(BulkFlagsUpdate value)
        {
            Write(value.Updates.Count);
            foreach (var update in value.Updates)
            {
                Write(update);
            }
        }

        public void Write(BattleStatePacket value)
        {
            Write(value.RemainingBattalions);
            Write(value.Tickets);
            Write(value.SpawnPointOwners);
        }

        public void Write(DamagePacket value)
        {
            Write((int)value.Type);
            Write(value.HealthDamage);
            Write(value.BalanceDamage);
            Write(value.IsSplashDamage);
            Write(value.IsPiercing);
            Write(value.IsCriticalHit);
            Write(value.Point);
            Write(value.Direction);
            Write(value.ImpactForce);
            Write(value.SourceActor);
            Write(value.Target);
            Write(value.Silent);
        }

        public void Write(EnterSeatPacket value)
        {
            Write(value.ActorId);
            Write(value.VehicleId);
            Write(value.SeatId);
        }

        public void Write(LeaveSeatPacket value)
        {
            Write(value.Id);
        }

        public void Write(Packet value)
        {
            Write((int)value.Id);
            Write(value.sender.ToByteArray());
            Write(value.data.Length);
            Write(value.data);
        }

        public void Write(SpawnProjectilePacket value)
        {
            Write(value.SourceId);
            Write(value.NameHash);
            Write(value.Mod);
            Write(value.Position);
            Write(value.Rotation);
            Write(value.performInfantryInitialMuzzleTravel);
            Write(value.initialMuzzleTravelDistance);
            Write(value.ProjectileId);
        }

        public void Write(UpdateProjectilePacket value)
        {
            Write(value.Id);
            Write(value.Position);
            Write(value.Velocity);
            Write(value.Enabled);
        }

        public void Write(BulkProjectileUpdate value)
        {
            Write(value.Updates.Count);
            foreach (var update in value.Updates)
            {
                Write(update);
            }
        }

        public void Write(VehiclePacket value)
        {
            Write(value.Id);
            Write(value.NameHash);
            Write(value.Mod);
            Write(value.Position);
            Write(value.Rotation);
            Write(value.Health);
            Write(value.Dead);
            Write(value.IsTurret);
            Write(value.Active);
        }

        public void Write(BulkVehicleUpdate value)
        {
            Write(value.Updates.Count);
            foreach (var update in value.Updates)
            {
                Write(update);
            }
        }

        public void Write(ChatPacket value)
        {
            Write(value.Id);
            Write(value.Message);
            Write(value.TeamOnly);
        }

        public void Write(VoicePacket value)
        {
            Write(value.Id);
            Write(value.Voice.Length);
            Write(value.Voice);
        }
        public void Write(SpawnCustomGameObjectPacket value)
        {
            Write(value.SourceID);
            Write(value.PrefabHash);
            Write(value.Position);
            Write(value.Rotation);
        }
        public void Write(NetworkGameObjectsHashesPacket value)
        {
            Write(value.Id);
            Write(value.NetworkGameObjectHashes);
        }
        public void Write(ScriptedPacket value)
        {
            Write(value.Id);
            Write(value.PacketId);
            Write(value.Data);
        }
        public void Write(KickAnimationPacket value)
        {
            Write(value.Id);
        }

        public void Write(ExplodeProjectilePacket value)
        {
            Write(value.Id);
        }

        public void Write(DominationStatePacket value)
        {
            Write(value.RemainingBattalions);
            Write(value.DominationRatio);
            Write(value.SpawnPointOwners);
            Write(value.ActiveFlagSet);
            Write(value.TimeToStart);
        }

        public void Write(PointMatchStatePacket value)
        {
            Write(value.BlueScore);
            Write(value.RedScore);
            Write(value.SpawnPointOwners);
        }

        public void Write(SkirmishStatePacket value)
        {
            Write(value.Domination);
            Write(value.SpawningReinforcements);
            Write(value.WavesRemaining);
            Write(value.SpawnPointOwners);
            Write(value.TimeToDominate);
        }

        public void Write(SpecOpsStatePacket value)
        {
            Write(value.AttackerSpawn);
            Write(value.SpawnPointOwners);
            Write(value.Scenarios.Count);
            foreach (var scenario in value.Scenarios)
            {
                if (scenario is AssassinateScenarioPacket)
                {
                    Write(0);
                    Write(scenario as AssassinateScenarioPacket);
                }
                else if (scenario is ClearScenarioPacket)
                {
                    Write(1);
                    Write(scenario as ClearScenarioPacket);
                }
                else if (scenario is DestroyScenarioPacket)
                {
                    Write(2);
                    Write(scenario as DestroyScenarioPacket);
                }
                else if (scenario is SabotageScenarioPacket)
                {
                    Write(3);
                    Write(scenario as SabotageScenarioPacket);
                }
            }
            Write(value.GameIsRunning);
        }

        public void Write(ScenarioPacket value)
        {
            Write(value.Spawn);
            Write(value.Actors.Count);
            foreach (var actor in value.Actors)
            {
                Write(actor);
            }
        }

        public void Write(AssassinateScenarioPacket value)
        {
            Write(value as ScenarioPacket);
        }

        public void Write(ClearScenarioPacket value)
        {
            Write(value as ScenarioPacket);
        }

        public void Write(DestroyScenarioPacket value)
        {
            Write(value as ScenarioPacket);
            Write(value.TargetVehicle);
        }

        public void Write(SabotageScenarioPacket value)
        {
            Write(value as ScenarioPacket);
            Write(value.Targets.Count);
            foreach (var target in value.Targets)
            {
                Write(target);
            }
        }

        public void Write(FireFlarePacket value)
        {
            Write(value.Actor);
            Write(value.Spawn);
        }

        public void Write(DestructiblePacket value)
        {
            Write(value.Id);
            Write(value.FullUpdate);
            if (value.FullUpdate)
            {
                Write(value.NameHash);
                Write(value.Mod);
                Write(value.Position);
                Write(value.Rotation);
            }
            Write(value.States);
        }

        public void Write(BulkDestructiblePacket value)
        {
            Write(value.Updates.Count);
            foreach (var update in value.Updates)
            {
                Write(update);
            }
        }

        public void Write(DestructibleDiePacket value)
        {
            Write(value.Id);
            Write(value.Index);
        }

        public void Write(SpecOpsDialogPacket value)
        {
            Write(value.Hide);
            Write(value.ActorPose);
            Write(value.Text);
            Write(value.OverrideName);
        }

        public void Write(SpecOpsSequencePacket value)
        {
            Write((int)value.Sequence);
        }

        public void Write(HauntedStatePacket value)
        {
            Write(value.CurrentSpawnPoint);
            Write(value.PlayerSpawn);
            Write(value.CurrentPhase);
            Write(value.KillCount);
            Write(value.AwaitingNextPhase);
            Write(value.PhaseEnded);
            Write(value.SkeletonCountModifier);
        }
        public void Write(ChatCommandPacket value)
        {
            Write(value.Id);
            Write(value.SteamID);
            Write(value.Command);
            Write(value.Scripted);
        }

        public void Write(CountermeasuresPacket value) 
        {
            Write(value.VehicleId);
        }
    }

    public class ProtocolReader : BinaryReader
    {
        public ProtocolReader(Stream input) : base(input)
        {
        }

        /// <summary>
        /// Adapted from https://wiki.vg/Protocol
        /// </summary>
        public override int ReadInt32()
        {
            int value = 0;
            int position = 0;
            byte currentByte;

            while (true)
            {
                currentByte = ReadByte();
                value |= (currentByte & 0x7F) << position;

                if ((currentByte & 0x80) == 0) break;

                position += 7;

                if (position >= 32) throw new ArithmeticException("VarInt is too big");
            }

            return value;
        }

        public Vector2 ReadVector2()
        {
            return new Vector2
            {
                x = ReadSingle(),
                y = ReadSingle(),
            };
        }

        public Vector3 ReadVector3()
        {
            return new Vector3
            {
                x = ReadSingle(),
                y = ReadSingle(),
                z = ReadSingle(),
            };
        }

        public Vector4 ReadVector4()
        {
            return new Vector4
            {
                x = ReadSingle(),
                y = ReadSingle(),
                z = ReadSingle(),
                w = ReadSingle(),
            };
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion
            {
                x = ReadSingle(),
                y = ReadSingle(),
                z = ReadSingle(),
                w = ReadSingle(),
            };
        }

        public Vector2? ReadVector2Optional()
        {
            bool hasValue = ReadBoolean();
            if (!hasValue)
                return null;

            return ReadVector2();
        }

        public Vector3? ReadVector3Optional()
        {
            bool hasValue = ReadBoolean();
            if (!hasValue)
                return null;

            return ReadVector3();
        }

        public Vector4? ReadVector4Optional()
        {
            bool hasValue = ReadBoolean();
            if (!hasValue)
                return null;

            return ReadVector4();
        }

        public Quaternion? ReadQuaternionOptional()
        {
            bool hasValue = ReadBoolean();
            if (!hasValue)
                return null;

            return ReadQuaternion();
        }

        public int[] ReadIntArray()
        {
            int len = ReadInt32();
            var o = new int[len];
            for (int i = 0; i < len; i++)
            {
                o[i] = ReadInt32();
            }
            return o;
        }

        public float[] ReadSingleArray()
        {
            int len = ReadInt32();
            var o = new float[len];
            for (int i = 0; i < len; i++)
            {
                o[i] = ReadSingle();
            }
            return o;
        }

        public bool[] ReadBoolArray()
        {
            int len = ReadInt32();
            var o = new bool[len];
            for (int i = 0; i < len; i++)
            {
                o[i] = ReadBoolean();
            }
            return o;
        }

        public BitArray ReadBitArray()
        {
            return new BitArray(ReadBytes(ReadInt32()));
        }

        public ActorPacket ReadActorPacket()
        {
            return new ActorPacket
            {
                Id = ReadInt32(),
                Name = ReadString(),
                Position = ReadVector3(),
                Lean = ReadSingle(),
                AirplaneInput = ReadVector4Optional(),
                BoatInput = ReadVector2Optional(),
                CarInput = ReadVector2Optional(),
                FacingDirection = ReadVector3(),
                HelicopterInput = ReadVector4Optional(),
                LadderInput = ReadSingle(),
                ParachuteInput = ReadVector2(),
                RangeInput = ReadSingle(),
                Velocity = ReadVector3(),
                ActiveWeaponHash = ReadInt32(),
                Team = ReadInt32(),
                MarkerPosition = ReadVector3Optional(),
                Flags = ReadInt32(),
                Ammo = ReadInt32(),
                Health = ReadSingle(),
                VehicleId = ReadInt32(),
                Seat = ReadInt32(),
            };
        }

        public ActorFlagsPacket ReadActorFlagsPacket()
        {
            return new ActorFlagsPacket
            {
                Id = ReadInt32(),
                StateVector = ReadInt32(),
            };
        }

        public BulkActorUpdate ReadBulkActorUpdate()
        {
            int count = ReadInt32();
            var updates = new List<ActorPacket>(count);
            for (int i = 0; i < count; i++)
            {
                updates.Add(ReadActorPacket());
            }
            return new BulkActorUpdate
            {
                Updates = updates,
            };
        }

        public BulkFlagsUpdate ReadBulkFlagsUpdate()
        {
            int count = ReadInt32();
            var updates = new List<ActorFlagsPacket>(count);
            for (int i = 0; i < count; i++)
            {
                updates.Add(ReadActorFlagsPacket());
            }
            return new BulkFlagsUpdate
            {
                Updates = updates,
            };
        }

        public BattleStatePacket ReadBattleStatePacket()
        {
            return new BattleStatePacket
            {
                RemainingBattalions = ReadIntArray(),
                Tickets = ReadIntArray(),
                SpawnPointOwners = ReadIntArray(),
            };
        }

        public DamagePacket ReadDamagePacket()
        {
            return new DamagePacket
            {
                Type = (DamageInfo.DamageSourceType)ReadInt32(),
                HealthDamage = ReadSingle(),
                BalanceDamage = ReadSingle(),
                IsSplashDamage = ReadBoolean(),
                IsPiercing = ReadBoolean(),
                IsCriticalHit = ReadBoolean(),
                Point = ReadVector3(),
                Direction = ReadVector3(),
                ImpactForce = ReadVector3(),
                SourceActor = ReadInt32(),
                Target = ReadInt32(),
                Silent = ReadBoolean(),
            };
        }

        public EnterSeatPacket ReadEnterSeatPacket()
        {
            return new EnterSeatPacket
            {
                ActorId = ReadInt32(),
                VehicleId = ReadInt32(),
                SeatId = ReadInt32(),
            };
        }

        public LeaveSeatPacket ReadLeaveSeatPacket()
        {
            return new LeaveSeatPacket
            {
                Id = ReadInt32(),
            };
        }

        public Packet ReadPacket()
        {
            return new Packet
            {
                Id = (PacketType)ReadInt32(),
                sender = new Guid(ReadBytes(16)),
                data = ReadBytes(ReadInt32()),
            };
        }

        public SpawnProjectilePacket ReadSpawnProjectilePacket()
        {
            return new SpawnProjectilePacket
            {
                SourceId = ReadInt32(),
                NameHash = ReadInt32(),
                Mod = ReadUInt64(),
                Position = ReadVector3(),
                Rotation = ReadQuaternion(),
                performInfantryInitialMuzzleTravel = ReadBoolean(),
                initialMuzzleTravelDistance = ReadSingle(),
                ProjectileId = ReadInt32(),
            };
        }

        public UpdateProjectilePacket ReadUpdateProjectilePacket()
        {
            return new UpdateProjectilePacket
            {
                Id = ReadInt32(),
                Position = ReadVector3(),
                Velocity = ReadVector3(),
                Enabled = ReadBoolean(),
            };
        }

        public BulkProjectileUpdate ReadBulkProjectileUpdate()
        {
            int count = ReadInt32();
            var updates = new List<UpdateProjectilePacket>(count);
            for (int i = 0; i < count; i++)
            {
                updates.Add(ReadUpdateProjectilePacket());
            }
            return new BulkProjectileUpdate
            {
                Updates = updates,
            };
        }

        public VehiclePacket ReadVehiclePacket()
        {
            return new VehiclePacket
            {
                Id = ReadInt32(),
                NameHash = ReadInt32(),
                Mod = ReadUInt64(),
                Position = ReadVector3(),
                Rotation = ReadQuaternion(),
                Health = ReadSingle(),
                Dead = ReadBoolean(),
                IsTurret = ReadBoolean(),
                Active = ReadBoolean(),
            };
        }

        public BulkVehicleUpdate ReadBulkVehicleUpdate()
        {
            int count = ReadInt32();
            var updates = new List<VehiclePacket>(count);
            for (int i = 0; i < count; i++)
            {
                updates.Add(ReadVehiclePacket());
            }
            return new BulkVehicleUpdate
            {
                Updates = updates,
            };
        }

        public ChatPacket ReadChatPacket()
        {
            return new ChatPacket
            {
                Id = ReadInt32(),
                Message = ReadString(),
                TeamOnly = ReadBoolean(),
            };
        }

        public VoicePacket ReadVoicePacket()
        {
            return new VoicePacket
            {
                Id = ReadInt32(),
                Voice = ReadBytes(ReadInt32()),
            };
        }

        public SpawnCustomGameObjectPacket ReadSpawnCustomGameObjectPacket()
        {
            return new SpawnCustomGameObjectPacket
            {
                SourceID = ReadInt32(),
                PrefabHash = ReadString(),
                Position = ReadVector3(),
                Rotation = ReadVector3()
            };
        }
        public NetworkGameObjectsHashesPacket ReadSyncNetworkGameObjectsPacket()
        {
            return new NetworkGameObjectsHashesPacket
            {
                Id = ReadInt32(),
                NetworkGameObjectHashes = ReadString()
            };
        }
        public ScriptedPacket ReadScriptedPacket()
        {
            return new ScriptedPacket
            {
                Id = ReadInt32(),
                PacketId = ReadInt32(),
                Data = ReadString()
            };
        }
        public KickAnimationPacket ReadKickAnimationPacket()
        {
            return new KickAnimationPacket
            {
                Id = ReadInt32(),
            };
        }

        public ExplodeProjectilePacket ReadExplodeProjectilePacket()
        {
            return new ExplodeProjectilePacket
            {
                Id = ReadInt32(),
            };
        }

        public DominationStatePacket ReadDominationStatePacket()
        {
            return new DominationStatePacket
            {
                RemainingBattalions = ReadIntArray(),
                DominationRatio = ReadSingleArray(),
                SpawnPointOwners = ReadIntArray(),
                ActiveFlagSet = ReadIntArray(),
                TimeToStart = ReadInt32(),
            };
        }

        public PointMatchStatePacket ReadPointMatchStatePacket()
        {
            return new PointMatchStatePacket
            {
                BlueScore = ReadInt32(),
                RedScore = ReadInt32(),
                SpawnPointOwners = ReadIntArray(),
            };
        }

        public SkirmishStatePacket ReadSkirmishStatePacket()
        {
            return new SkirmishStatePacket
            {
                Domination = ReadSingle(),
                SpawningReinforcements = ReadBoolArray(),
                WavesRemaining = ReadIntArray(),
                SpawnPointOwners = ReadIntArray(),
                TimeToDominate = ReadInt32(),
            };
        }

        public SpecOpsStatePacket ReadSpecOpsStatePacket()
        {
            var state = new SpecOpsStatePacket
            {
                AttackerSpawn = ReadVector3(),
                SpawnPointOwners = ReadIntArray(),
            };

            int scenarioCount = ReadInt32();
            var scenarios = new List<ScenarioPacket>(scenarioCount);
            for (int i = 0; i < scenarioCount; i++)
            {
                scenarios.Add(ReadScenarioPacket());
            }
            state.Scenarios = scenarios;
            state.GameIsRunning = ReadBoolean();

            return state;
        }

        public ScenarioPacket ReadScenarioPacket()
        {
            int selector = ReadInt32();
            if (selector == 0)
            {
                return ReadAssassinateScenarioPacket();
            }
            else if (selector == 1)
            {
                return ReadClearScenarioPacket();
            }
            else if (selector == 2)
            {
                return ReadDestroyScenarioPacket();
            }
            else
            {
                return ReadSabotageScenarioPacket();
            }
        }

        private void ReadAndPopulateGenericScenario(ScenarioPacket scenario)
        {
            scenario.Spawn = ReadInt32();
            int count = ReadInt32();
            scenario.Actors = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                scenario.Actors.Add(ReadInt32());
            }
        }

        public AssassinateScenarioPacket ReadAssassinateScenarioPacket()
        {
            var scenario = new AssassinateScenarioPacket();
            ReadAndPopulateGenericScenario(scenario);
            return scenario;
        }

        public ClearScenarioPacket ReadClearScenarioPacket()
        {
            var scenario = new ClearScenarioPacket();
            ReadAndPopulateGenericScenario(scenario);
            return scenario;
        }

        public DestroyScenarioPacket ReadDestroyScenarioPacket()
        {
            var scenario = new DestroyScenarioPacket();
            ReadAndPopulateGenericScenario(scenario);
            scenario.TargetVehicle = ReadInt32();
            return scenario;
        }

        public SabotageScenarioPacket ReadSabotageScenarioPacket()
        {
            var scenario = new SabotageScenarioPacket();
            ReadAndPopulateGenericScenario(scenario);
            int count = ReadInt32();
            scenario.Targets = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                scenario.Targets.Add(ReadInt32());
            }
            return scenario;
        }

        public FireFlarePacket ReadFireFlarePacket()
        {
            return new FireFlarePacket
            {
                Actor = ReadInt32(),
                Spawn = ReadInt32(),
            };
        }

        public DestructiblePacket ReadDestructiblePacket()
        {
            var packet =  new DestructiblePacket
            {
                Id = ReadInt32(),
                FullUpdate = ReadBoolean(),
            };

            if (packet.FullUpdate)
            {
                packet.NameHash = ReadInt32();
                packet.Mod = ReadUInt64();
                packet.Position = ReadVector3();
                packet.Rotation = ReadQuaternion();
            }

            packet.States = ReadBitArray();
            return packet;
        }

        public BulkDestructiblePacket ReadBulkDestructiblePacket()
        {
            int count = ReadInt32();
            var updates = new List<DestructiblePacket>(count);
            for (int i = 0; i < count; i++)
            {
                updates.Add(ReadDestructiblePacket());
            }
            return new BulkDestructiblePacket
            {
                Updates = updates,
            };
        }

        public DestructibleDiePacket ReadDestructibleDiePacket()
        {
            return new DestructibleDiePacket
            {
                Id = ReadInt32(),
                Index = ReadInt32(),
            };
        }

        public SpecOpsDialogPacket ReadSpecOpsDialogPacket()
        {
            return new SpecOpsDialogPacket
            {
                Hide = ReadBoolean(),
                ActorPose = ReadString(),
                Text = ReadString(),
                OverrideName = ReadString(),
            };
        }

        public SpecOpsSequencePacket ReadSpecOpsSequencePacket()
        {
            return new SpecOpsSequencePacket
            {
                Sequence = (SpecOpsSequencePacket.SequenceType)ReadInt32(),
            };
        }

        public HauntedStatePacket ReadHauntedStatePacket()
        {
            return new HauntedStatePacket
            {
                CurrentSpawnPoint = ReadInt32(),
                PlayerSpawn = ReadInt32(),
                CurrentPhase = ReadInt32(),
                KillCount = ReadInt32(),
                AwaitingNextPhase = ReadBoolean(),
                PhaseEnded = ReadBoolean(),
                SkeletonCountModifier = ReadSingle(),
            };
        }
        public ChatCommandPacket ReadChatCommandPacket()
        {
            return new ChatCommandPacket
            {
                Id = ReadInt32(),
                SteamID = ReadUInt64(),
                Command = ReadString(),
                Scripted = ReadBoolean(),
            };
        }
        
        public CountermeasuresPacket ReadCountermeasuresPacket()
        {
            return new CountermeasuresPacket
            {
                VehicleId = ReadInt32(),
            };
        }
    }
}