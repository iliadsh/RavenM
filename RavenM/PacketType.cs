namespace RavenM
{
    /// <summary>
    /// Handshake is currently not used.
    /// </summary>
    public enum PacketType
    {
        Handshake,
        ActorUpdate,
        ActorFlags,
        VehicleUpdate,
        Damage,
        Death,
        EnterSeat,
        LeaveSeat,
        GameStateUpdate,
        SpawnProjectile,
        UpdateProjectile,
        Chat,
        Voip,
        VehicleDamage,
        CreateCustomGameObject,
        NetworkGameObjectsHashes,
        ScriptedPacket,
        KickAnimation,
        Explode,
        SpecOpsFlare,
        SpecOpsSequence,
        SpecOpsDialog,
        UpdateDestructible,
        DestroyDestructible,
        ChatCommand,
        Countermeasures
    }
}
