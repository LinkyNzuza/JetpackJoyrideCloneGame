// TEMPORARY PLAYTEST SCAFFOLDING — delete this folder before submission.
// Stands in for the collectibles slice so the player's coin path can be exercised.

using UnityEngine;
using Game.Player;

namespace Game.Sandbox
{
    /// <summary>
    /// Minimal stand-in for the real Coin component owned by the world/obstacles slice.
    /// Exists so <c>PlayerCollision</c>'s per-coin value path is actually exercised during
    /// playtesting rather than always falling back to the serialized default.
    /// </summary>
    public sealed class SandboxCoin : MonoBehaviour, ICoinValue
    {
        [Tooltip("Score value this coin declares. Values outside 1..1000 are clamped by PlayerCollision.")]
        [SerializeField] private int _value = 10;

        /// <inheritdoc />
        public int CoinValue => _value;

        /// <summary>Sets the declared value at spawn time.</summary>
        public void SetValue(int value) => _value = value;
    }
}
