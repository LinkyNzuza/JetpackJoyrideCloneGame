// The value carrier for coins the world spawns.
//
// Why it has to exist. PlayerCollision reads ICoinValue off the coin it touched and falls back to its
// own serialized default when the coin carries none. Every code-built coin carried none, so every
// coin in the game was worth exactly 1 no matter which sprite it wore. Three coin sprites that all
// score the same are three coin sprites that mean nothing.
//
// SandboxCoin already implements this interface, but it lives in _Sandbox, whose header says to
// delete the folder before submission. Shipping gameplay cannot depend on scaffolding.
//
// Deliberately tiny: it holds a number and answers when asked. PlayerCollision still owns collection
// and still clamps to 1..1000, so a bad value here cannot corrupt the score.

using UnityEngine;
using Game.Player;

namespace Game.World
{
    /// <summary>
    /// Declares a coin's score value so <see cref="PlayerCollision"/> can read it instead of falling
    /// back to its default.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldCoin : MonoBehaviour, ICoinValue
    {
        [Tooltip("Score value. PlayerCollision clamps to 1..1000, so values outside that are corrected " +
                 "and reported rather than trusted.")]
        [SerializeField, Range(1, 1000)] private int _value = 1;

        /// <inheritdoc />
        public int CoinValue => _value;

        /// <summary>Sets the value at spawn time, before the coin can be touched.</summary>
        public void SetValue(int value) => _value = value;
    }
}
