using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using RavenM.RSPatch.Packets;

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

        public void Write(int[] value)
        {
            Write(value.Length);
            foreach (int n in value)
            {
                Write(n);
            }
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
            Write(value.Direction);
            Write(value.MuzzlePosition);
            Write(value.ProjectileId);
        }

        public void Write(UpdateProjectilePacket value)
        {
            Write(value.Id);
            Write(value.Position);
            Write(value.Velocity);
            Write(value.Boom);
        }
        public void Write(CustomObjectUpdatePacket value)
        {
            Write(value.Id);
            Write(value.Position);
            Write(value.Rotation);
            Write(value.Active);
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
            Write((int)value.Type);
            Write(value.Position);
            Write(value.Rotation);
            Write(value.Team);
            Write(value.Health);
            Write(value.Dead);
            Write(value.IsTurret);
            Write((int)value.TurretType);
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
                Direction = ReadVector3(),
                MuzzlePosition = ReadVector3(),
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
                Boom = ReadBoolean(),
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
                Type = (VehicleSpawner.VehicleSpawnType)ReadInt32(),
                Position = ReadVector3(),
                Rotation = ReadQuaternion(),
                Team = ReadInt32(),
                Health = ReadSingle(),
                Dead = ReadBoolean(),
                IsTurret = ReadBoolean(),
                TurretType = (TurretSpawner.TurretSpawnType)ReadInt32(),
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
    }
}
