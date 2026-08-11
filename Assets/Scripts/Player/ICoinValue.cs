// Feature: player-core-gameplay
// Contract only: no UnityEngine types appear here.

namespace Game.Player
{
    /// <summary>
    /// Implemented by the coin prefab script owned by the collectibles slice.
    /// Absence of this component on a coin is a valid state: routing then falls
    /// back to the default value (Requirement 6.7). The member name is recorded
    /// in the Agreements_Document.
    /// </summary>
    public interface ICoinValue
    {
        /// <summary>
        /// The score value this coin declares. Routing normalises the value into
        /// the accepted range before it reaches subscribers.
        /// </summary>
        int CoinValue { get; }
    }
}
