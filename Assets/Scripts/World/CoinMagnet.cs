// The magnet power-up's actual effect. The player has published IsMagnetActive since the start and
// nothing ever acted on it, so the magnet was a flag with a sound and no gameplay.
//
// Why this lives on the world side. The ownership rule is that the player reports and never
// decides, so the player must not move a coin. Coins are world objects: ObstacleDirector spawns
// them, scrolls them and recycles them. So the thing that moves them belongs next to the thing that
// owns them, and it reads the player's published state rather than being told anything.
//
// What this deliberately does NOT do is collect coins. PlayerCollision already counts each coin
// exactly once, keyed on instance id, and that guarantee is what the group's score data rests on.
// A second collection path would double-count silently, which is the worst kind of bug to have in
// data you intend to draw conclusions from. This only closes the distance; contact does the rest.

using System.Collections.Generic;
using UnityEngine;
using Game.Player;

namespace Game.World
{
    /// <summary>
    /// Pulls nearby coins toward the player while the magnet power-up is active.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoinMagnet : MonoBehaviour
    {
        private const string TagCoin = "Coin";

        [Header("Sources")]
        [Tooltip("Player whose magnet state and position drive the pull. Found automatically if left empty.")]
        [SerializeField] private PlayerController _player;

        [Tooltip("Director the current scroll speed is read from, so the pull can outrun the world. " +
                 "Found automatically if left empty.")]
        [SerializeField] private ObstacleDirector _director;

        [Header("Reach")]
        // Tuning: bands are about 2.67 m tall, so 3.5 reaches roughly one band above and below. It is
        // also well under the smallest obstacle spacing the reach bound permits, which at 4 m/s is
        // about 7.9 m. That matters: the magnet can never reach past the neighbouring set into the one
        // beyond it, so it stays a local effect and cannot interact with pattern generation.
        [Tooltip("Radius in metres within which coins are pulled.")]
        [SerializeField, Range(0.5f, 12f)] private float _radius = 3.5f;

        [Header("Pull")]
        // Tuning: 8 m/s matches the player's own rise speed limit and is double the starting scroll
        // speed, so coins visibly overtake the world from the first tier rather than merely drifting.
        [Tooltip("Base pull speed in metres per second.")]
        [SerializeField, Range(1f, 40f)] private float _pullSpeed = 8f;

        // A fixed pull speed breaks at the top of the Progressive curve, where the world scrolls at
        // 13 m/s: a coin behind the player would be carried away faster than it could close. So the
        // floor on the pull is the scroll speed times this, read live from the director rather than
        // hardcoded, because the director is what actually knows how fast the world is moving.
        [Tooltip("Pull speed is at least the current scroll speed multiplied by this. Above 1 so a " +
                 "coin behind the player can still catch up.")]
        [SerializeField, Range(1f, 4f)] private float _scrollSpeedMargin = 1.6f;

        private ContactFilter2D _filter;
        private readonly List<Collider2D> _hits = new List<Collider2D>();

        /// <summary>How many coins were moved on the last frame the magnet ran. Diagnostics only.</summary>
        public int PulledLastFrame { get; private set; }

        /// <summary>Pull speed currently in use, after the scroll-speed floor has been applied.</summary>
        public float EffectivePullSpeed =>
            _director != null
                ? Mathf.Max(_pullSpeed, _director.ScrollSpeed * _scrollSpeedMargin)
                : _pullSpeed;

        private void Awake()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_director == null) _director = FindFirstObjectByType<ObstacleDirector>();

            if (_player == null)
                Debug.LogError("[CoinMagnet] No PlayerController found. Nothing will be pulled.", this);

            if (_director == null)
                Debug.LogWarning(
                    "[CoinMagnet] No ObstacleDirector found, so the pull cannot track scroll speed. " +
                    $"Falling back to a fixed {_pullSpeed:0.0} m/s, which coins behind the player may " +
                    "not be able to beat at higher tiers.", this);

            if (!IsTagRegistered(TagCoin))
                Debug.LogError(
                    $"[CoinMagnet] Tag '{TagCoin}' is not registered, so no coin can ever be matched. " +
                    "Add it in Edit > Project Settings > Tags and Layers.", this);

            // Coins are spawned as triggers, so the filter has to include them. Nothing else is
            // filtered: matching is by tag, exactly as PlayerCollision does it. A layer mask would be
            // cheaper, but a mask set to Nothing fails silently, and this project has lost enough time
            // to configuration that fails quietly.
            _filter = new ContactFilter2D();
            _filter.NoFilter();
            _filter.useTriggers = true;
        }

        private void Update()
        {
            PulledLastFrame = 0;

            if (_player == null) return;

            // Stops on the frame the flag clears, and on death. There is no state to unwind, which is
            // the point of holding none: IsMagnetActive is already false after TryBeginDeath expires
            // every power-up, so the IsAlive test is insurance rather than the mechanism.
            if (!_player.IsAlive || !_player.IsMagnetActive) return;

            float step = EffectivePullSpeed * Time.deltaTime;
            if (step <= 0f) return;

            Vector3 target = _player.transform.position;

            _hits.Clear();
            Physics2D.OverlapCircle(target, _radius, _filter, _hits);

            for (int i = 0; i < _hits.Count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null || !hit.CompareTag(TagCoin)) continue;

                Transform coin = hit.transform;

                // MoveTowards rather than a scaled direction, so a coin can never overshoot the player
                // and oscillate around them. This is additive with the director's own leftward scroll,
                // which is intended: the coin drifts with the world and is pulled on top of it.
                coin.position = Vector3.MoveTowards(coin.position, target, step);
                PulledLastFrame++;
            }
        }

        // ── The obstacle question ──────────────────────────────────────────────────────
        //
        // Coins are pulled THROUGH obstacles. No line-of-sight test. Three reasons.
        //
        // PatternGenerator guarantees a route for the player, using PlayerReach, and it knows nothing
        // about coins. A line-of-sight rule would introduce a second reachability constraint that
        // nothing models and nothing tests, and it would make coin yield quietly depend on layout. In
        // a project whose whole hypothesis is measured in score and distance, a scoring rule nobody
        // wrote down is worse than a slightly unrealistic one.
        //
        // The reward has to be legible. "Coins come to me for five seconds" is a promise a player can
        // read off a single pickup. "Coins come to me unless something is in the way" is not, and it
        // reads as the power-up being broken rather than as a rule being applied.
        //
        // And it would cost a raycast per coin per frame to express a distinction nobody can perceive
        // while the world goes past at 4 to 13 m/s.
        //
        // The honest cost of this choice: a coin will sometimes visibly slide behind an obstacle on
        // its way in. That is accepted, not overlooked.

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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 0.95f, 0.5f);
            Vector3 centre = _player != null ? _player.transform.position : transform.position;
            Gizmos.DrawWireSphere(centre, _radius);
        }
    }
}
