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
        public bool Aiming;

        [ProtoMember(6)]
        public Vector2 AimInput;

        [ProtoMember(7)]
        public Vector4 AirplaneInput;

        [ProtoMember(8)]
        public Vector2 BoatInput;

        [ProtoMember(9)]
        public Vector2 CarInput;

        [ProtoMember(10)]
        public bool Countermeasures;

        [ProtoMember(11)]
        public bool Crouch;

        [ProtoMember(12)]
        public Vector3 FacingDirection;

        [ProtoMember(13)]
        public bool Fire;

        [ProtoMember(14)]
        public Vector4 HelicopterInput;

        [ProtoMember(15)]
        public bool HoldingSprint;

        [ProtoMember(16)]
        public bool IdlePose;

        [ProtoMember(17)]
        public bool IsAirborne;

        [ProtoMember(18)]
        public bool IsAlert;

        [ProtoMember(19)]
        public bool IsMoving;

        [ProtoMember(20)]
        public bool IsOnPlayerSquad;

        [ProtoMember(21)]
        public bool IsReadyToPickUpPassengers;

        [ProtoMember(22)]
        public bool IsSprinting;

        [ProtoMember(23)]
        public bool IsTakingFire;

        [ProtoMember(24)]
        public bool Jump;

        [ProtoMember(25)]
        public float LadderInput;

        [ProtoMember(26)]
        public bool OnGround;

        [ProtoMember(27)]
        public Vector2 ParachuteInput;

        [ProtoMember(28)]
        public bool ProjectToGround;

        [ProtoMember(29)]
        public bool Prone;

        [ProtoMember(30)]
        public float RangeInput;

        [ProtoMember(31)]
        public bool Reload;

        [ProtoMember(32)]
        public Vector3 Velocity;

        [ProtoMember(33)]
        public string ActiveWeapon;

        [ProtoMember(34)]
        public int Team;

        [ProtoMember(35)]
        public bool Dead;

        [ProtoMember(36)]
        public bool AiControlled;
    }

    [ProtoContract]
    public class BulkActorUpdate
    {
        [ProtoMember(1)]
        public List<ActorPacket> Updates;
    }
}
