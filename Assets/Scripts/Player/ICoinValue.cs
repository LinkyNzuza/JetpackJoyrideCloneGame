// Contract implemented by the coin prefab script owned by the collectibles slice.

namespace Game.Player
{
    /// <summary>
    /// Implemented by a component on a coin prefab to declare that coin's score value.
    /// <para>
    /// A coin that carries no component implementing this interface is a valid case:
    /// <see cref="PlayerCollision"/> falls back to its serialized default value. Values
    /// outside the accepted range are clamped and reported once per instance.
    /// </para>
    /// </summary>
    public interface ICoinValue
    {
        /// <summary>
        /// This coin's score value. Accepted range is 1 to 1000 inclusive; values outside
        /// that range are clamped by the collision handler.
        /// </summary>
        int CoinValue { get; }
    }
}
