// PlayerDeath orchestrates the one-shot death sequence; PlayerController owns all state mutation.
// NO UI code, NO SceneManager, NO Time.timeScale, NO GameManager reference — by design.

using UnityEngine;
using UnityEngine.Events;

namespace Game.Player
{
    /// <summary>
    /// Orchestrates the one-shot death sequence. Guards on <see cref="PlayerController.IsAlive"/>
    /// so that multiple overlapping contact callbacks in a single physics step produce exactly one
    /// death — no matter how many colliders fire at once.
    /// <para>
    /// Sequence (in order): state mutation via <see cref="PlayerController.TryBeginDeath"/> →
    /// present Death → death cue → <see cref="PlayerController.RaisePlayerDeath"/>.
    /// </para>
    /// <para>Must NOT write to <c>transform.position</c>; <see cref="PlayerController"/> keeps the X-lock.</para>
    /// </summary>
    public sealed class PlayerDeath : MonoBehaviour
    {
        [Header("Cue (Optional)")]
        [Tooltip("Invoked when the player dies — wire audio/VFX/animator responses here in the Inspector.")]
        [SerializeField] private UnityEvent _onDeathCue;

        private PlayerController _controller;
        private PlayerAnimation _animation;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _animation = GetComponent<PlayerAnimation>();

            if (_controller == null)
                Debug.LogError("[PlayerDeath] PlayerController not found on this GameObject.", this);
        }

        /// <summary>
        /// Requests the death sequence. Safe to call from multiple overlapping contacts in one
        /// physics step — only the first call proceeds; subsequent calls are silently ignored
        /// because <see cref="PlayerController.IsAlive"/> is false after the first execution.
        /// <para>
        /// Sequence: state mutation (IsAlive→false, input off, velocity→0, rb.simulated→false,
        /// power-ups expired) → death cue → <see cref="PlayerController.OnPlayerDeath"/> raised.
        /// </para>
        /// </summary>
        public void RequestDeath()
        {
            if (_controller == null) return;

            // Single guard for the whole sequence. Returns false if already dying or dead,
            // so overlapping contacts in one physics step produce exactly one death.
            // Mutates state atomically: expires power-ups, IsAlive=false, input off,
            // velocity zeroed, rb.simulated=false.
            if (!_controller.TryBeginDeath()) return;

            // Present Death BEFORE the cue and before subscribers run, so the pose is
            // already correct by the time anyone reacts to OnPlayerDeath.
            _animation?.PresentDeath();

            // Play the death cue (audio, VFX, etc. — wired via the Inspector).
            _onDeathCue?.Invoke();

            // Raise the public OnPlayerDeath event with per-subscriber error isolation.
            _controller.RaisePlayerDeath();
        }
    }
}
