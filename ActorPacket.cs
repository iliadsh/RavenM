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
}
