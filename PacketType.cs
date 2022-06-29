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
    }
}
