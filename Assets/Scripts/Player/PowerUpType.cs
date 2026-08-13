
namespace Game.Player
{
    /// <summary>
    /// The power-up kinds the Player tracks. Each value gets its own independent
    /// active flag and remaining-seconds timer in <c>PowerUpTimerSet</c>, so any
    /// combination can be active at the same time.
    /// </summary>
    public enum PowerUpType
    {
        Shield = 0,
        Magnet = 1
    }
}
