// The worn jetpack. Reads PlayerController.IsAlive only — never input, never physics — the same
// discipline as PlayerJetpackFlame and PlayerShieldVisual.
//
// The difference from the flame is what drives visibility. The flame is an event in sprite form:
// it exists only while thrust is held. The pack is equipment, so it is present for the whole run
// and goes away only when the run does. That makes this the simplest of the three effect
// components, and deliberately so — a backpack that flickers would read as a bug.
//
// The sprite is a single frame, but it still loads through SpriteFrameLoader rather than a
// serialized reference. Two reasons. It survives whatever the importer decides, which is the
// failure that silenced both other effects. And it keeps all three effect components loading the
// same way, so there is one place to look when art goes missing rather than three.

using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Shows the jetpack worn on the player's back for as long as the player is alive.
    /// <para>
    /// Visibility follows <see cref="PlayerController.IsAlive"/>. Nothing else about the pack
    /// changes at runtime: it does not animate, and it does not react to thrust, because the
    /// flame already carries that information.
    /// </para>
    /// </summary>
    public sealed class PlayerJetpackVisual : MonoBehaviour
    {
        /// <summary>
        /// Folder under any Resources directory holding the pack sprite. Its contents are derived
        /// from Kenney's power-up badge by <c>JetpackSpriteBuilder</c>, which lives in
        /// <c>Assets/Editor</c>.
        /// </summary>
        private const string FramesResourcePath = "PlayerGear/Jetpack";

        [Header("Pack sprite")]
        [Tooltip("Optional. Leave empty to auto-load from Resources/PlayerGear/Jetpack.")]
        [SerializeField] private Sprite _sprite;

        [Header("Placement")]
        [Tooltip("Local offset from the player's origin. Negative X puts the pack behind him.")]
        [SerializeField] private Vector2 _localOffset = new Vector2(-0.22f, -0.13f);

        // Tuning: 1 is the source art's native 20x31, which neither stretches nor softens. Above
        // that the sprite is resampled, and because the texture imports with bilinear filtering the
        // edges go soft rather than blocky. 2 was chosen by eye over crispness.
        [SerializeField, Range(0.05f, 3f)] private float _scale = 2f;

        [Tooltip("Sorting order relative to the player sprite. Must sit between the body at 0 and " +
                 "the flame at -2, so the pack hides behind the body and the flame behind the pack.")]
        [SerializeField] private int _sortingOrder = -1;

        // Tuning: the source pictogram is pure white so it can be tinted to anything. Grey reads as
        // metal; pure white reads as paper.
        [Tooltip("Pack tint. The source art is white, so this is the pack's actual colour.")]
        [SerializeField] private Color _tint = new Color(0.62f, 0.65f, 0.7f, 1f);

        private PlayerController _controller;
        private SpriteRenderer _renderer;
        private bool _warned;

        private void Awake()
        {
            _controller = GetComponentInParent<PlayerController>();
            _renderer = GetComponent<SpriteRenderer>();

            if (_controller == null)
                Debug.LogError("[PlayerJetpackVisual] No PlayerController on this object or its parents.", this);

            if (_renderer == null)
            {
                Debug.LogError("[PlayerJetpackVisual] No SpriteRenderer on this object.", this);
                return;
            }

            // Loaded by name, through the loader, so the pack survives whatever the texture
            // imported as. Only the first frame is used; the pack does not animate.
            if (_sprite == null)
            {
                Sprite[] loaded = SpriteFrameLoader.Load(FramesResourcePath, out string route);
                if (loaded != null && loaded.Length > 0)
                {
                    _sprite = loaded[0];
                    Debug.Log($"[PlayerJetpackVisual] Loaded {route} from Resources/{FramesResourcePath}.", this);
                }
            }

            transform.localPosition = _localOffset;
            transform.localScale = new Vector3(_scale, _scale, 1f);
            _renderer.sortingOrder = _sortingOrder;
            _renderer.color = _tint;
            _renderer.sprite = _sprite;

            // Visible from the first frame, because the player spawns alive and already wearing it.
            _renderer.enabled = _sprite != null;
        }

        private void Update()
        {
            if (_renderer == null || _controller == null) return;

            if (_sprite == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogError(
                        "[PlayerJetpackVisual] No pack sprite. Expected one in " +
                        $"Resources/{FramesResourcePath} or assigned in the Inspector. Run " +
                        "Tools > Jetpack > Rebuild worn jetpack sprite to regenerate it.", this);
                }
                _renderer.enabled = false;
                return;
            }

            // Equipment, not an effect: on for the whole run, off once the run ends.
            _renderer.enabled = _controller.IsAlive;
        }
    }
}
