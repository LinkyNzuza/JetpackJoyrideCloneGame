// Shield bubble presentation. Reads PlayerController.IsShielded only — never input,
// never physics, never collision. Purely a mirror of published player state.

using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Shows a shield bubble around the player while <see cref="PlayerController.IsShielded"/>
    /// is true, and hides it otherwise.
    /// <para>
    /// The bubble pulses gently so an active shield reads clearly, and flashes faster once the
    /// shield is close to expiring is deliberately NOT done here: the controller does not
    /// publish remaining time, and this component must not duplicate timer state.
    /// </para>
    /// </summary>
    public sealed class PlayerShieldVisual : MonoBehaviour
    {
        /// <summary>Folder under any Resources directory holding the bubble frames.</summary>
        private const string FramesResourcePath = "PlayerFX/Shield";

        [Header("Bubble frames")]
        [Tooltip("Optional. Leave empty to auto-load every sprite in Resources/PlayerFX/Shield.")]
        [SerializeField] private Sprite[] _frames;

        [Tooltip("Frames per second the bubble cycles. Low values read as a slow shimmer.")]
        [SerializeField, Range(1f, 30f)] private float _framesPerSecond = 8f;

        [Header("Appearance")]
        [SerializeField, Range(0.1f, 4f)] private float _scale = 1.15f;

        [Tooltip("Sorting order relative to the player sprite. Positive draws in front.")]
        [SerializeField] private int _sortingOrder = 1;

        [Tooltip("Bubble tint and opacity.")]
        [SerializeField] private Color _tint = new Color(0.45f, 0.75f, 1f, 0.72f);

        // Tuning: how much the bubble breathes. 0 disables the pulse entirely.
        [Tooltip("Scale pulse amplitude, as a fraction of base scale.")]
        [SerializeField, Range(0f, 0.3f)] private float _pulseAmount = 0.06f;

        [SerializeField, Range(0.1f, 8f)] private float _pulseSpeed = 2.5f;

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
                Debug.LogError("[PlayerShieldVisual] No PlayerController on this object or its parents.", this);

            if (_renderer == null)
            {
                Debug.LogError("[PlayerShieldVisual] No SpriteRenderer on this object.", this);
                return;
            }

            // Loaded by name so the effect survives any re-import of the source textures.
            if (_frames == null || _frames.Length == 0)
            {
                Sprite[] loaded = Resources.LoadAll<Sprite>(FramesResourcePath);
                if (loaded != null && loaded.Length > 0)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    _frames = loaded;
                }
            }

            transform.localPosition = Vector3.zero;
            _renderer.sortingOrder = _sortingOrder;
            _renderer.color = _tint;
            _renderer.enabled = false; // no shield at spawn
            ApplyScale(1f);
        }

        private void Update()
        {
            if (_renderer == null || _controller == null) return;

            if (_frames == null || _frames.Length == 0)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogError(
                        "[PlayerShieldVisual] No bubble frames. Expected sprites in " +
                        $"Resources/{FramesResourcePath} or assigned in the Inspector.", this);
                }
                _renderer.enabled = false;
                return;
            }

            if (!_controller.IsShielded)
            {
                _renderer.enabled = false;
                _frameIndex = 0;
                _frameTimer = 0f;
                ApplyScale(1f);
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

            // Breathe, so an active shield is obvious even against a busy background.
            float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmount;
            ApplyScale(pulse);
        }

        private void ApplyScale(float multiplier)
        {
            float s = _scale * multiplier;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
