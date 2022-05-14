namespace RavenM
{
    /// <summary>
    /// Handshake is currently not used.
    /// </summary>
    public enum PacketType
    {
        Handshake,
        ActorUpdate,
        VehicleUpdate,
        Damage,
        Death,
        EnterSeat,
        LeaveSeat,
    }
}
