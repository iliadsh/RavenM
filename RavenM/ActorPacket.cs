using System.Collections.Generic;
using UnityEngine;

namespace RavenM
{
    /// <summary>
    /// A massive Actor state packet. In general, most fields correspond
    /// to the results of the methods in the controller.
    /// </summary>
    public class ActorPacket
    {
        public int Id;

        public string Name;

        public Vector3 Position;

        public float Lean;

        public Vector4? AirplaneInput;

        public Vector2? BoatInput;

        public Vector2? CarInput;

        public Vector3 FacingDirection;

        public Vector4? HelicopterInput;

        public float LadderInput;

        public Vector2 ParachuteInput;

        public float RangeInput;

        public Vector3 Velocity;

        public int ActiveWeaponHash;

        public int Team;

        public Vector3? MarkerPosition;

        public int Flags;

        public int Ammo;

        public float Health;

        public int VehicleId;

        public int Seat;
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
        Crouch                      = 1 << 11,
        IsAirborne                  = 1 << 12,
        IsOnPlayerSquad             = 1 << 13,
        IsReadyToPickUpPassengers   = 1 << 14,
        IsTakingFire                = 1 << 15,
        Jump                        = 1 << 16,
        Prone                       = 1 << 17,
        Reload                      = 1 << 18,
        Dead                        = 1 << 19,
    }

    public class ActorFlagsPacket
    {
        public int Id;

        public int StateVector;
    }

    public class BulkActorUpdate
    {
        public List<ActorPacket> Updates;
    }

    public class BulkFlagsUpdate
    {
        public List<ActorFlagsPacket> Updates;
    }

    public class RemoveActorPacket
    {
        public int Id;
    }
}
