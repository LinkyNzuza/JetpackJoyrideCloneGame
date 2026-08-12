// Feature: player-core-gameplay
// PlayerCollision routes physics contacts to the appropriate player response.
// ReleaseCollectible is the ONLY method that calls Destroy — change it when pooling is decided.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Player
{
    /// <summary>
    /// Routes <see cref="OnTriggerEnter2D"/> and <see cref="OnCollisionEnter2D"/> to the
    /// appropriate player response using case-sensitive tag matching.
    /// <para>Recognised tags: <c>Obstacle</c>, <c>Coin</c>, <c>PowerUp_Shield</c>, <c>PowerUp_Magnet</c>.</para>
    /// <para>All callbacks are silently ignored while the player is not alive.</para>
    /// <para>
    /// <see cref="ReleaseCollectible"/> is the single point of collectible release — the only
    /// place to change when the pooling strategy is decided. Nothing else calls <c>Destroy</c>.
    /// </para>
    /// </summary>
    public sealed class PlayerCollision : MonoBehaviour
    {
        // ──────────────────────────── Tag Constants (case-sensitive) ───────────────────

        private const int MinCoinValue = 1;
        private const int MaxCoinValue = 1000;

        private const string TagObstacle       = "Obstacle";
        private const string TagCoin           = "Coin";
        private const string TagPowerUpShield  = "PowerUp_Shield";
        private const string TagPowerUpMagnet  = "PowerUp_Magnet";

        // ──────────────────────────── Serialized Fields ────────────────────────────────

        [Header("Cue (Optional)")]
        [Tooltip("Invoked when a shield absorbs an obstacle — wire audio/VFX responses here in the Inspector.")]
        [SerializeField] private UnityEvent _onShieldBreakCue;

        [Tooltip("Coin value raised via OnCoinCollected when a coin is picked up.")]
        [SerializeField] private int _coinValue = 1;

        // ──────────────────────────── Runtime State ────────────────────────────────────

        private PlayerController _controller;
        private PlayerDeath _death;

        // HashSets prevent double-counting when overlapping/rapid callbacks fire for the same instance.
        private readonly HashSet<int> _consumedCoins     = new HashSet<int>();
        private readonly HashSet<int> _absorbedObstacles = new HashSet<int>();

        // Stable delegate — stored so OnEnable/OnDisable use the exact same instance.
        private Action _handleRunReset;

        // ──────────────────────────── Lifecycle ────────────────────────────────────────

        private void Awake()
        {
            _controller   = GetComponent<PlayerController>();
            _death        = GetComponent<PlayerDeath>();
            _handleRunReset = HandleRunReset;

            if (_controller == null)
                Debug.LogError("[PlayerCollision] PlayerController not found on this GameObject.", this);
            if (_death == null)
                Debug.LogError("[PlayerCollision] PlayerDeath not found on this GameObject.", this);

            ValidateTags();
        }

        private void OnEnable()
        {
            if (_controller != null)
                _controller.OnRunReset += _handleRunReset;
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnRunReset -= _handleRunReset;
        }

        // ──────────────────────────── Physics Callbacks ────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            RouteContact(other.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            RouteContact(collision.gameObject);
        }

        // ──────────────────────────── Routing ──────────────────────────────────────────

        private void RouteContact(GameObject go)
        {
            // While not alive, ignore every callback entirely.
            if (_controller == null || !_controller.IsAlive) return;

            string tag = go.tag;

            if (tag == TagObstacle)
                HandleObstacle(go);
            else if (tag == TagCoin)
                HandleCoin(go);
            else if (tag == TagPowerUpShield)
                HandlePowerUp(go, PowerUpType.Shield);
            else if (tag == TagPowerUpMagnet)
                HandlePowerUp(go, PowerUpType.Magnet);
            // Any other or missing tag → do nothing (raise nothing, release nothing).
        }

        private void HandleObstacle(GameObject go)
        {
            int id = go.GetInstanceID();

            if (_controller.IsShielded)
            {
                // Guard: mark ABSORBED on first contact so repeat callbacks from the same
                // obstacle instance never consume a second shield.
                if (_absorbedObstacles.Contains(id)) return;
                _absorbedObstacles.Add(id);

                _controller.ConsumeShield();
                _onShieldBreakCue?.Invoke();
                // Player survives — IsAlive remains true.
            }
            else
            {
                // Request death — PlayerDeath guards on IsAlive, so overlapping contacts are safe.
                _death?.RequestDeath();
            }
        }

        private void HandleCoin(GameObject go)
        {
            int id = go.GetInstanceID();

            // Mark BEFORE raising to prevent double-count on overlapping callbacks.
            if (_consumedCoins.Contains(id)) return;
            _consumedCoins.Add(id);

            _controller.RaiseCoinCollected(ResolveCoinValue(go));
            ReleaseCollectible(go);
        }

        /// <summary>
        /// Reads a coin's own declared value when it carries an <see cref="ICoinValue"/>
        /// component, otherwise falls back to the serialized default. Out-of-range values are
        /// clamped into 1..1000 and reported once, so one malformed prefab cannot spam the log.
        /// </summary>
        private int ResolveCoinValue(GameObject go)
        {
            var declared = go.GetComponent<ICoinValue>();
            if (declared == null) return Mathf.Clamp(_coinValue, MinCoinValue, MaxCoinValue);

            int raw = declared.CoinValue;
            if (raw >= MinCoinValue && raw <= MaxCoinValue) return raw;

            Debug.LogWarning(
                $"[PlayerCollision] Coin '{go.name}' declared value {raw}, outside " +
                $"{MinCoinValue}..{MaxCoinValue}. Clamped.", go);
            return Mathf.Clamp(raw, MinCoinValue, MaxCoinValue);
        }

        private void HandlePowerUp(GameObject go, PowerUpType type)
        {
            _controller.ActivatePowerUp(type);
            ReleaseCollectible(go);
        }

        // ──────────────────────────── Release ──────────────────────────────────────────

        /// <summary>
        /// The single point of collectible release. This is the ONLY method in this class
        /// that calls <c>Destroy</c> or would return an object to a pool.
        /// When the collectibles teammate decides the pooling strategy, change ONLY this method.
        /// </summary>
        private static void ReleaseCollectible(GameObject go)
        {
            Destroy(go);
        }

        // ──────────────────────────── Reset Handler ────────────────────────────────────

        /// <summary>
        /// Subscribed to the internal <see cref="PlayerController.OnRunReset"/> event.
        /// Clears both tracking sets so previous-run contacts don't bleed into the new run.
        /// </summary>
        private void HandleRunReset()
        {
            _consumedCoins.Clear();
            _absorbedObstacles.Clear();
        }

        // ──────────────────────────── Tag Validation ───────────────────────────────────

        private void ValidateTags()
        {
            var missing = new StringBuilder();

            if (!IsTagRegistered(TagObstacle))      missing.Append(' ').Append(TagObstacle);
            if (!IsTagRegistered(TagCoin))           missing.Append(' ').Append(TagCoin);
            if (!IsTagRegistered(TagPowerUpShield))  missing.Append(' ').Append(TagPowerUpShield);
            if (!IsTagRegistered(TagPowerUpMagnet))  missing.Append(' ').Append(TagPowerUpMagnet);

            if (missing.Length > 0)
                Debug.LogError(
                    $"[PlayerCollision] Tag(s) not registered in Tag Manager:{missing}. " +
                    "Add them in Edit > Project Settings > Tags and Layers.", this);
        }

        /// <summary>
        /// Runtime tag existence check. <c>GameObject.FindWithTag</c> throws
        /// <see cref="UnityException"/> when the tag is not registered; returns null (no throw)
        /// when registered but no object carries it.
        /// </summary>
        private static bool IsTagRegistered(string tag)
        {
            try
            {
                GameObject.FindWithTag(tag);
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }
    }
}
