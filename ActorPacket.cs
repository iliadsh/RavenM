using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;

namespace RavenM
{
    /// <summary>
    /// A massive Actor state packet. In general, most fields correspond
    /// to the results of the methods in the controller.
    /// </summary>
    [ProtoContract]
    public class ActorPacket
    {
        [ProtoMember(1)]
        public int Id;

        [ProtoMember(2)]
        public string Name;

        [ProtoMember(3)]
        public Vector3 Position;

        [ProtoMember(4)]
        public float Lean;

        [ProtoMember(5)]
        public Vector4? AirplaneInput;

        [ProtoMember(6)]
        public Vector2? BoatInput;

        [ProtoMember(7)]
        public Vector2? CarInput;

        [ProtoMember(8)]
        public Vector3 FacingDirection;

        [ProtoMember(9)]
        public Vector4? HelicopterInput;

        [ProtoMember(10)]
        public float LadderInput;

        [ProtoMember(11)]
        public Vector2 ParachuteInput;

        [ProtoMember(12)]
        public float RangeInput;

        [ProtoMember(13)]
        public Vector3 Velocity;

        [ProtoMember(14, DataFormat = DataFormat.FixedSize)]
        public int ActiveWeaponHash;

        [ProtoMember(15)]
        public int Team;

        [ProtoMember(16)]
        public Vector3? MarkerPosition;
    }

    public enum ActorStateFlags
    {
        AiControlled                = 1 << 0,
        DeployParachute             = 1 << 1,
        Fire                        = 1 << 2,
        Aiming                      = 1 << 3,
        IsMoving                    = 1 << 4,
        IdlePose                    = 1 << 5,
        OnGround                    = 1 << 6,
        ProjectToGround             = 1 << 7,
        IsAlert                     = 1 << 8,
        HoldingSprint               = 1 << 9,
        IsSprinting                 = 1 << 10,
        Countermeasures             = 1 << 11,
        Crouch                      = 1 << 12,
        IsAirborne                  = 1 << 13,
        IsOnPlayerSquad             = 1 << 14,
        IsReadyToPickUpPassengers   = 1 << 15,
        IsTakingFire                = 1 << 16,
        Jump                        = 1 << 17,
        Prone                       = 1 << 18,
        Reload                      = 1 << 19,
        Dead                        = 1 << 20,
    }

    [ProtoContract]
    public class ActorFlagsPacket
    {
        [ProtoMember(1)]
        public int Id;

        [ProtoMember(2)]
        public int StateVector;
    }

    [ProtoContract]
    public class BulkActorUpdate
    {
        [ProtoMember(1)]
        public List<ActorPacket> Updates;
    }

    [ProtoContract]
    public class BulkFlagsUpdate
    {
        [ProtoMember(1)]
        public List<ActorFlagsPacket> Updates;
    }
}
