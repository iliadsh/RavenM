using UnityEngine;
using HarmonyLib;
using System.Reflection;

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

            if (!IngameNetManager.instance.IsHost)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Part two of stopping AI coroutines from firing. This should stop all any
    /// any AI interaction if not the host.
    /// </summary>
    [HarmonyPatch(typeof(ActorManager), "UpdateAI")]
    public class NoUpdateAIPatch
    {
        static bool Prefix()
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            if (!IngameNetManager.instance.IsHost)
                return false;

            return true;
        }
    }

    /// <summary>
    /// If a third person renderer does not exist, then we just
    /// use the first person one.
    /// </summary>
    [HarmonyPatch(typeof(Weapon), nameof(Weapon.CullFpsObjects))]
    public class WeaponRenderPatch
    {
        static void Prefix(Weapon __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            if (__instance.thirdPersonTransform != null)
                return;

            // Simple heuristic to pick the sub-mesh with the highest vertex count.
            int bestVertexCount = 0;
            foreach (Transform transform in __instance.transform)
            {
                var child = transform.gameObject;

                if (child.TryGetComponent(out MeshFilter meshFilter))
                {
                    var mesh = meshFilter.mesh;

                    if (mesh.vertexCount > bestVertexCount)
                    {
                        __instance.thirdPersonTransform = transform;
                        bestVertexCount = mesh.vertexCount;
                    }
                }
            }
        }
    }

    /// <summary>
    /// An ActorController for Actors controlled by a remote client. Nothing fancy, just copying
    /// the method results of the actual ActorController on the other end.
    /// </summary>
    public class NetActorController : AiActorController
    {
        public Vector3 ActualRotation;
        public ActorPacket Targets;
        public int Flags;

        public Transform FakeWeaponParent;
        public WeaponManager.LoadoutSet FakeLoadout;

        public TimedAction RespawnCooldown = new TimedAction(3.0f);
        public TimedAction SeatResolverCooldown = new TimedAction(1.5f);

        private void Update()
        {
            if (RespawnCooldown.TrueDone())
            {
                if ((Flags & (int)ActorStateFlags.Dead) != 0 && !actor.dead)
                    actor.Kill(DamageInfo.Default);

                if (!((Flags & (int)ActorStateFlags.Dead) != 0) && actor.dead)
                    actor.SpawnAt(Targets.Position, Quaternion.identity);
            }

            // *tiny* slack. We don't want de-sync, but we also don't want the actor
            // to glitch out with its colliders and such.
            if ((actor.transform.position - Targets.Position).magnitude > 0.005)
            {
                // Let the vehicle move the actor in this case, otherwise there is
                // a very noticible de-sync between actor and vehicle.
                if (!actor.IsSeated())
                {
                    // Fully teleport the actor if they are far away from the target.
                    var new_pos = (actor.transform.position - Targets.Position).magnitude > 5
                                    ? Targets.Position
                                    : Vector3.Lerp(actor.transform.position, Targets.Position, 10f * Time.deltaTime);

                    actor.SetPositionAndRotation(new_pos, actor.transform.rotation);
                }
            }

            ActualRotation = Vector3.Slerp(ActualRotation, Targets.FacingDirection, 5f * Time.deltaTime);

            // Resolve the current seat if we missed the Enter/Leave packets.
            if (SeatResolverCooldown.TrueDone())
            {
                SeatResolverCooldown.Start();

                if (Targets.Seat == -1)
                {
                    if (actor.IsSeated())
                    {
                        if (actor.seat.IsDriverSeat() && IngameNetManager.instance.IsHost && actor.seat.vehicle.TryGetComponent(out GuidComponent guid))
                            IngameNetManager.instance.OwnedVehicles.Add(guid.guid);

                        actor.LeaveSeat(false);
                    }
                }
                else
                {
                    // Are we seated?
                    // Are we in the right vehicle?
                    // Are we in the right seat?
                    if (!actor.IsSeated() || (actor.seat.vehicle.TryGetComponent(out GuidComponent guid) &&
                                                (guid.guid != Targets.VehicleId || actor.seat.vehicle.seats.IndexOf(actor.seat) != Targets.Seat)))
                    {
                        if (actor.IsSeated())
                        {
                            if (actor.seat.IsDriverSeat() && IngameNetManager.instance.IsHost && actor.seat.vehicle.TryGetComponent(out GuidComponent vguid))
                                IngameNetManager.instance.OwnedVehicles.Add(vguid.guid);

                            actor.LeaveSeat(false);
                        }

                        if (IngameNetManager.instance.ClientVehicles.ContainsKey(Targets.VehicleId))
                        {
                            Vehicle vehicle = IngameNetManager.instance.ClientVehicles[Targets.VehicleId];

                            if (vehicle != null && Targets.Seat < vehicle.seats.Count)
                            {
                                var seat = vehicle.seats[Targets.Seat];

                                if (seat.IsDriverSeat())
                                    IngameNetManager.instance.OwnedVehicles.Remove(Targets.VehicleId);

                                actor.EnterSeat(seat, true);
                            }
                        }
                    }
                }
            }

            // For normal hand-held weapons.
            if (!actor.dead && !actor.IsSeated() && Targets.ActiveWeaponHash != 0 && (actor.activeWeapon == null || actor.activeWeapon.name.GetHashCode() != Targets.ActiveWeaponHash))
            {
                var weaponWithName = GetWeaponEntryByHash(Targets.ActiveWeaponHash);

                // ? Perhaps MountedWeapons are sometimes sent?
                if (weaponWithName != null)
                {
                    Plugin.logger.LogInfo($"Changing weapon to: {weaponWithName.name}. current weapon: {actor.activeWeapon?.name}");
                    actor.EquipNewWeaponEntry(weaponWithName, actor.activeWeapon?.slot ?? 0, true);
                }
            }

            // For seats that have multiple mounted weapons.
            if (!actor.dead && actor.IsSeated() && Targets.ActiveWeaponHash >= 0 && Targets.ActiveWeaponHash < actor.seat.weapons.Length && actor.seat.ActiveWeaponSlot() != Targets.ActiveWeaponHash)
            {
                actor.SwitchWeapon(Targets.ActiveWeaponHash);
            }

            if (!actor.dead && actor.IsSeated() && actor.seat.HasActiveWeapon() && actor.seat.activeWeapon.GetType() == typeof(Mortar))
            {
                typeof(Mortar).GetField("range", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor.seat.activeWeapon, Targets.RangeInput);
            }

            if (actor.activeWeapon != null)
            {
                actor.activeWeapon.ammo = Targets.Ammo;

                // This would be broken since we normally set the projectiles it spawns to null.
                actor.activeWeapon.onSpawnProjectiles.RemoveAllListeners();
            }
        }

        public static WeaponManager.WeaponEntry GetWeaponEntryByHash(int hash)
        {
            foreach (var entry in WeaponManager.instance.allWeapons)
                if (entry.name.GetHashCode() == hash)
                    return entry;
            return null;
        }

        public override bool Aiming()
        {
            return (Flags & (int)ActorStateFlags.Aiming) != 0;
        }

        public override Vector2 AimInput()
        {
            return Vector2.zero;
        }

        public override Vector4 AirplaneInput()
        {
            return Targets.AirplaneInput ?? Vector4.zero;
        }

        public override void ApplyRecoil(Vector3 impulse)
        {
        }

        public override Vector2 BoatInput()
        {
            return Targets.BoatInput ?? Vector2.zero;
        }

        public override Vector2 CarInput()
        {
            return Targets.CarInput ?? Vector2.zero;
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
            return (Flags & (int)ActorStateFlags.Countermeasures) != 0;
        }

        public override bool Crouch()
        {
            return (Flags & (int)ActorStateFlags.Crouch) != 0;
        }

        public override bool CurrentWaypoint(out Vector3 waypoint)
        {
            waypoint = Vector3.zero;
            return false;
        }

        public override bool DeployParachute()
        {
            return (Flags & (int)ActorStateFlags.DeployParachute) != 0;
        }

        public override void Die(Actor killer)
        {
            RespawnCooldown.Start();
            
            // Reset the animator Controller in case the PerformKick Coroutine is Interrupted
            if (KickAnimation.OldKickController == null)
                return;
            actor.animator.runtimeAnimatorController = KickAnimation.OldKickController;
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
            return ActualRotation;
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
            return (Flags & (int)ActorStateFlags.Fire) != 0;
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
            return true;
        }

        public override Vector4 HelicopterInput()
        {
            return Targets.HelicopterInput ?? Vector4.zero;
        }

        public override bool HoldingSprint()
        {
            return (Flags & (int)ActorStateFlags.HoldingSprint) != 0;
        }

        public override void HolsteredActiveWeapon()
        {
        }

        public override bool IdlePose()
        {
            return (Flags & (int)ActorStateFlags.IdlePose) != 0;
        }

        public override bool IsAirborne()
        {
            return (Flags & (int)ActorStateFlags.IsAirborne) != 0;
        }

        public override bool IsAlert()
        {
            return (Flags & (int)ActorStateFlags.IsAlert) != 0;
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
            return (Flags & (int)ActorStateFlags.IsMoving) != 0;
        }

        public override bool IsOnPlayerSquad()
        {
            return false;
        }

        public override bool IsReadyToPickUpPassengers()
        {
            return (Flags & (int)ActorStateFlags.IsReadyToPickUpPassengers) != 0;
        }

        public override bool IsSprinting()
        {
            return (Flags & (int)ActorStateFlags.IsSprinting) != 0;
        }

        public override bool IsTakingFire()
        {
            return false;
        }

        public override bool Jump()
        {
            return (Flags & (int)ActorStateFlags.Jump) != 0;
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
            this.squad = squad;
        }

        public override void OnCancelParachute()
        {
        }

        public override void OnDroppedFromSquad()
        {
        }

        public override bool OnGround()
        {
            return (Flags & (int)ActorStateFlags.OnGround) != 0;
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
            return RespawnCooldown.TrueDone() && (Flags & (int)ActorStateFlags.ProjectToGround) != 0;
        }

        public override bool Prone()
        {
            return (Flags & (int)ActorStateFlags.Prone) != 0;
        }

        public override float RangeInput()
        {
            return 0f;
        }

        public override void ReceivedDamage(bool friendlyFire, float damage, float balanceDamage, Vector3 point, Vector3 direction, Vector3 force)
        {
        }

        public override bool Reload()
        {
            return (Flags & (int)ActorStateFlags.Reload) != 0;
        }

        public override SpawnPoint SelectedSpawnPoint()
        {
            return ActorManager.instance.spawnPoints[0];
        }

        public override void SpawnAt(Vector3 position, Quaternion rotation)
        {
            RespawnCooldown.Start();
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