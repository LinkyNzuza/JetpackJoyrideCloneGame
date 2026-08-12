// Feature: player-core-gameplay
// Jetpack exhaust presentation. Reads PlayerController.IsThrusting only — never input,
// never physics — the same discipline as PlayerAnimation.

using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Drives the jetpack flame on a child <see cref="SpriteRenderer"/>. The flame is shown
    /// and cycled while the player is thrusting, and hidden otherwise.
    /// <para>
    /// State is derived solely from <see cref="PlayerController.IsThrusting"/>, which is
    /// already gated on <c>IsAlive</c>, so a dead player never shows a flame.
    /// </para>
    /// </summary>
    public sealed class PlayerJetpackFlame : MonoBehaviour
    {
        [Header("Flame frames")]
        [Tooltip("Frames cycled while thrusting, in order. Assign the fire00..fire07 sprites.")]
        [SerializeField] private Sprite[] _frames;

        // Tuning: raise for a more frantic flame, lower for a lazier plume.
        [Tooltip("Frames per second the flame cycles.")]
        [SerializeField, Range(2f, 60f)] private float _framesPerSecond = 18f;

        [Header("Placement")]
        [Tooltip("Local offset from the player's origin. Negative Y puts the flame below.")]
        [SerializeField] private Vector2 _localOffset = new Vector2(-0.18f, -0.34f);

        [SerializeField, Range(0.05f, 3f)] private float _scale = 0.55f;

        [Tooltip("Sorting order relative to the player sprite. Negative draws behind.")]
        [SerializeField] private int _sortingOrder = -1;

        private PlayerController _controller;
        private SpriteRenderer _renderer;
        private float _frameTimer;
        private int _frameIndex;
        private bool _warned;

        private void Awake()
        {
            _controller = GetComponentInParent<PlayerController>();
            _renderer = GetComponent<SpriteRenderer>();

            if (_controller == null)
                Debug.LogError("[PlayerJetpackFlame] No PlayerController on this object or its parents.", this);

            if (_renderer == null)
            {
                Debug.LogError("[PlayerJetpackFlame] No SpriteRenderer on this object.", this);
                return;
            }

            transform.localPosition = _localOffset;
            transform.localScale = new Vector3(_scale, _scale, 1f);
            _renderer.sortingOrder = _sortingOrder;
            _renderer.enabled = false; // start hidden; Falling is the initial state
        }

        private void Update()
        {
            if (_renderer == null || _controller == null) return;

            if (_frames == null || _frames.Length == 0)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogError("[PlayerJetpackFlame] No flame frames assigned; flame disabled.", this);
                }
                _renderer.enabled = false;
                return;
            }

            bool thrusting = _controller.IsThrusting;

            if (!thrusting)
            {
                // Reset so every burst starts on the same frame, which reads as a fresh ignition.
                _renderer.enabled = false;
                _frameIndex = 0;
                _frameTimer = 0f;
                return;
            }

            _renderer.enabled = true;

            _frameTimer += Time.deltaTime;
            float step = 1f / _framesPerSecond;
            while (_frameTimer >= step)
            {
                _frameTimer -= step;
                _frameIndex = (_frameIndex + 1) % _frames.Length;
            }

            _renderer.sprite = _frames[_frameIndex];
        }
    }
}
