using UnityEngine;
using HarmonyLib;

namespace RavenM
{
    /// <summary>
    /// Not entirely sure why a NetActor sometimes has coroutines called but hopefully this patch
    /// takes care of it.
    /// </summary>
    [HarmonyPatch(typeof(AiActorController), nameof(AiActorController.TickAiCoroutines))]
    public class NoTickPatch
    {
        static bool Prefix(AiActorController __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            if (!IngameNetManager.instance.OwnedActors.Contains(__instance.actor.GetComponent<GuidComponent>().guid))
                return false;

            return true;
        }
    }

    /// <summary>
    /// An ActorController for Actors controlled by a remote client. Nothing fancy, just copying
    /// the method results of the actual ActorController on the other end.
    /// </summary>
    public class NetActorController : AiActorController
    {
        public ActorPacket ActualState;
        public ActorPacket Targets;

        public Transform FakeWeaponParent;
        public WeaponManager.LoadoutSet FakeLoadout;

        public TimedAction respawn_action = new TimedAction(3.0f);

        private void Update()
        {
            if (respawn_action.TrueDone())
            {
                if (Targets.Dead && !actor.dead)
                    actor.KillSilently();

                if (!Targets.Dead && actor.dead)
                    actor.SpawnAt(Targets.Position, Quaternion.identity);
            }

            // *tiny* slack. We don't want de-sync, but we also don't want the actor
            // to glitch out with its colliders and such.
            if ((actor.transform.position - Targets.Position).magnitude > 0.005)
            {
                var new_pos = Vector3.Lerp(actor.transform.position, Targets.Position, 10f * Time.deltaTime);

                actor.SetPositionAndRotation(new_pos, actor.transform.rotation);
            }

            ActualState.FacingDirection = Vector3.Slerp(ActualState.FacingDirection, Targets.FacingDirection, 5f * Time.deltaTime);

            // Ugly, but I'm tired of exceptions. 
            if (actor.seat == null && actor.activeWeapon != null && actor.activeWeapon.GetType() != typeof(MountedWeapon) && Targets.ActiveWeapon != string.Empty && actor.activeWeapon.name != Targets.ActiveWeapon)
            {
                var weaponWithName = WeaponManager.GetWeaponEntryByName(Targets.ActiveWeapon);

                // ? Perhaps MountedWeapons are sometimes sent?
                if (weaponWithName != null)
                {
                    Plugin.logger.LogInfo($"Changing weapon to: {Targets.ActiveWeapon}. current weapon: {actor.activeWeapon.name}");
                    actor.EquipNewWeaponEntry(weaponWithName, actor.activeWeapon.slot, true);
                }
            }
        }

        public override bool Aiming()
        {
            return Targets.Aiming;
        }

        public override Vector2 AimInput()
        {
            return Targets.AimInput;
        }

        public override Vector4 AirplaneInput()
        {
            return Targets.AirplaneInput;
        }

        public override void ApplyRecoil(Vector3 impulse)
        {
        }

        public override Vector2 BoatInput()
        {
            return Targets.BoatInput;
        }

        public override Vector2 CarInput()
        {
            return Targets.CarInput;
        }

        public override void ChangeAimFieldOfView(float fov)
        {
        }

        public override bool ChangeStance(Actor.Stance stance)
        {
            return true;
        }

        public override void ChangeToSquad(Squad squad)
        {
        }

        public override bool Countermeasures()
        {
            return Targets.Countermeasures;
        }

        public override bool Crouch()
        {
            return Targets.Crouch;
        }

        public override bool CurrentWaypoint(out Vector3 waypoint)
        {
            waypoint = Vector3.zero;
            return false;
        }

        public override bool DeployParachute()
        {
            return true;
        }

        public override void Die(Actor killer)
        {
            respawn_action.Start();
        }

        public override void DisableInput()
        {
        }

        public override void DisableMovement()
        {
        }

        public override void EnableInput()
        {
        }

        public override void EnableMovement()
        {
        }

        public override void EndLadder(Vector3 exitPosition, Quaternion flatFacing)
        {
        }

        public override void EndRagdoll()
        {
        }

        public override void EndSeated(Seat leftSeat, Vector3 exitPosition, Quaternion flatFacing, bool forcedByFallingOver)
        {
        }

        public override void EndSeatedSwap(Seat leftSeat)
        {
        }

        public override bool EnterCover(CoverPoint coverPoint)
        {
            return false;
        }

        public override Vector3 FacingDirection()
        {
            return ActualState.FacingDirection;
        }

        public override void FillDriverSeat()
        {
        }

        public override bool FindCover()
        {
            return false;
        }

        public override bool FindCoverAtPoint(Vector3 point)
        {
            return false;
        }

        public override bool FindCoverAwayFrom(Vector3 point)
        {
            return false;
        }

        public override bool FindCoverTowards(Vector3 direction)
        {
            return false;
        }

        public override bool Fire()
        {
            return Targets.Fire;
        }

        public override void ForceChangeStance(Actor.Stance stance)
        {
        }

        public override bool ForceStopVehicle()
        {
            return false;
        }

        public override WeaponManager.LoadoutSet GetLoadout()
        {
            return FakeLoadout;
        }

        public override Squad GetSquad()
        {
            return null;
        }

        public override Actor GetTarget()
        {
            return null;
        }

        public override void GettingUp()
        {
        }

        public override bool HasPath()
        {
            return true;
        }

        public override bool HasSpottedTarget()
        {
            return false;
        }

        public override Vector4 HelicopterInput()
        {
            return Targets.HelicopterInput;
        }

        public override bool HoldingSprint()
        {
            return Targets.HoldingSprint;
        }

        public override void HolsteredActiveWeapon()
        {
        }

        public override bool IdlePose()
        {
            return Targets.IdlePose;
        }

        public override bool IsAirborne()
        {
            return Targets.IsAirborne;
        }

        public override bool IsAlert()
        {
            return Targets.IsAlert;
        }

        public override bool IsGroupedUp()
        {
            return false;
        }

        public override bool IsMovementEnabled()
        {
            return false;
        }

        public override bool IsMoving()
        {
            return Targets.IsMoving;
        }

        public override bool IsOnPlayerSquad()
        {
            return false;
        }

        public override bool IsReadyToPickUpPassengers()
        {
            return Targets.IsReadyToPickUpPassengers;
        }

        public override bool IsSprinting()
        {
            return Targets.IsSprinting;
        }

        public override bool IsTakingFire()
        {
            return false;
        }

        public override bool Jump()
        {
            return Targets.Jump;
        }

        public override float LadderInput()
        {
            return Targets.LadderInput;
        }

        public override float Lean()
        {
            return Targets.Lean;
        }

        public override void LeaveCover()
        {
        }

        public override void LookAt(Vector3 position)
        {
        }

        public override void MarkReachedWaypoint()
        {
        }

        public override void Move(Vector3 movement)
        {
        }

        public override bool NextSightMode()
        {
            return true;
        }

        public override void OnAssignedToSquad(Squad squad)
        {
        }

        public override void OnCancelParachute()
        {
        }

        public override void OnDroppedFromSquad()
        {
        }

        public override bool OnGround()
        {
            return Targets.OnGround;
        }

        public override void OnVehicleWasDamaged(Actor source, float damage)
        {
        }

        public override Vector2 ParachuteInput()
        {
            return Targets.ParachuteInput;
        }

        public override Vector3 PathEndPoint()
        {
            return Vector3.zero;
        }

        public override bool PreviousSightMode()
        {
            return false;
        }

        public override bool ProjectToGround()
        {
            return Targets.ProjectToGround;
        }

        public override bool Prone()
        {
            return Targets.Prone;
        }

        public override float RangeInput()
        {
            return Targets.RangeInput;
        }

        public override void ReceivedDamage(bool friendlyFire, float damage, float balanceDamage, Vector3 point, Vector3 direction, Vector3 force)
        {
        }

        public override bool Reload()
        {
            return Targets.Reload;
        }

        public override SpawnPoint SelectedSpawnPoint()
        {
            return ActorManager.instance.spawnPoints[0];
        }

        public override void SpawnAt(Vector3 position, Quaternion rotation)
        {
        }

        public override void StartClimbingSlope()
        {
        }

        public override void StartLadder(Ladder ladder)
        {
        }

        public override void StartRagdoll()
        {
        }

        public override void StartSeated(Seat seat)
        {
        }

        public override Vector3 SwimInput()
        {
            return Vector3.zero;
        }

        public override void SwitchedToWeapon(Weapon weapon)
        {
        }

        public override bool SwitchFireMode()
        {
            return false;
        }

        public override Transform TpWeaponParent()
        {
            return null;
        }

        public override bool UseEyeMuzzle()
        {
            return false;
        }

        public override bool UseMuzzleDirection()
        {
            return false;
        }

        public override bool UseSprintingAnimation()
        {
            return IsSprinting();
        }

        public override Vector3 Velocity()
        {
            return Targets.Velocity;
        }

        public override Transform WeaponParent()
        {
            return FakeWeaponParent;
        }
    }
}
