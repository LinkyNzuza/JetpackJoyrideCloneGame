// Unity 6 (6000.0.53f1) — uses Rigidbody2D.linearVelocity exclusively (the pre-Unity-6 API is obsolete).

using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Owns all player physics, input reading, state flags, and the public event surface.
    /// All physics writes occur exclusively in <see cref="FixedUpdate"/>; input is sampled
    /// there via a cached <c>thrustHeld</c> bool — never in Update or input callbacks.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        // ──────────────────────────── Serialized Fields ────────────────────────────────

        [Header("Input")]
        [Tooltip("Reference to the Jump action (Player map) from InputSystem_Actions. Assign in Inspector.")]
        [SerializeField] private InputActionReference _thrustActionReference;

        [Header("Physics — Thrust & Gravity")]
        // Tuning: raise for a punchier jetpack feel, lower for gentle floatiness. Retest after each change.
        [Tooltip("Upward force applied every FixedUpdate tick while the thrust input is held (Newtons).")]
        [SerializeField, Range(0.1f, 500f)] private float _thrustForce = 35f;

        // Tuning: raising gravityScale makes the player fall faster and the game harder to play.
        [Tooltip("Rigidbody2D gravity scale applied once in Awake.")]
        [SerializeField, Range(0.1f, 20f)] private float _gravityScale = 3f;

        [Header("Physics — Speed Clamps")]
        // Tuning: cap rise speed so the player can't rocket into the ceiling uncontrollably.
        [Tooltip("Maximum upward velocity (m/s). Clamped every FixedUpdate regardless of thrust.")]
        [SerializeField, Range(0.1f, 100f)] private float _maxRiseSpeed = 8f;

        // Tuning: cap fall speed so the player has time to react to low obstacles.
        [Tooltip("Maximum downward speed magnitude (m/s, positive value). Clamped every FixedUpdate.")]
        [SerializeField, Range(0.1f, 100f)] private float _maxFallSpeed = 12f;

        [Header("Play Bounds (World Y)")]
        [Tooltip("World-space Y floor. Hitting it snaps Y and zeroes vertical velocity — NON-FATAL.")]
        [SerializeField] private float _playBoundsMinY = -4f;

        [Tooltip("World-space Y ceiling. Hitting it snaps Y and zeroes vertical velocity — NON-FATAL.")]
        [SerializeField] private float _playBoundsMaxY = 4f;

        [Header("Power-Up Durations")]
        [Tooltip("Seconds the Shield power-up remains active.")]
        [SerializeField, Range(1f, 60f)] private float _shieldDuration = 5f;

        [Tooltip("Seconds the Magnet power-up remains active.")]
        [SerializeField, Range(1f, 60f)] private float _magnetDuration = 5f;

        // ──────────────────────────── Runtime State ────────────────────────────────────

        private Rigidbody2D _rb;
        private InputAction _thrustAction;

        // Captured once in Awake; fixedX is NEVER changed, not even on reset.
        private float _fixedX;
        private float _spawnY;

        private bool _isAlive;
        private bool _thrustHeld;
        private bool _inputEnabled;
        private bool _ownsThrustAction;
        private bool _pressedSinceLastTick;
        private bool _requireFreshPress;
        private bool _dying;
        private bool _loggedNonFinite;

        // Power-up state — arrays indexed by (int)PowerUpType.
        private static readonly PowerUpType[] _allPowerUpTypes =
            (PowerUpType[])Enum.GetValues(typeof(PowerUpType));

        private bool[] _powerUpActive;
        private float[] _powerUpTimers;
        private bool _isShielded;
        private bool _isMagnetActive;

        // ──────────────────────────── Public Read-Only Properties ──────────────────────

        /// <summary>True while the player is alive. Restored to true by <see cref="ResetRun"/>.</summary>
        public bool IsAlive => _isAlive;

        /// <summary>True while the Shield power-up is active.</summary>
        public bool IsShielded => _isShielded;

        /// <summary>
        /// True while the player is alive AND the thrust input is held.
        /// <see cref="PlayerAnimation"/> reads this — never the raw device state.
        /// </summary>
        public bool IsThrusting => _isAlive && _thrustHeld;

        /// <summary>True while the Magnet power-up is active.</summary>
        public bool IsMagnetActive => _isMagnetActive;

        // ── Movement capability, published so the world system can respect it ──────────
        // The world owner needs these to work out how long the player takes to cross the
        // play area, because that time is the ceiling on how dense an obstacle pattern can
        // honestly get. Read-only on purpose: publishing the numbers is fine, letting
        // another system write them is not.

        /// <summary>Upward force applied per tick while thrusting, in newtons.</summary>
        public float ThrustForce => _thrustForce;

        /// <summary>Gravity scale applied to the body.</summary>
        public float GravityScale => _gravityScale;

        /// <summary>Upper bound on upward speed, in metres per second.</summary>
        public float MaxRiseSpeed => _maxRiseSpeed;

        /// <summary>Upper bound on downward speed magnitude, in metres per second.</summary>
        public float MaxFallSpeed => _maxFallSpeed;

        /// <summary>World-space Y floor of the play area.</summary>
        public float PlayBoundsMinY => _playBoundsMinY;

        /// <summary>World-space Y ceiling of the play area.</summary>
        public float PlayBoundsMaxY => _playBoundsMaxY;

        /// <summary>Mass of the body, needed to turn thrust in newtons into an acceleration.</summary>
        public float BodyMass => _rb != null ? _rb.mass : 1f;

        // ──────────────────────────── Public Events ────────────────────────────────────

        /// <summary>Raised once when the player dies. Subscribe for game-over or respawn logic.</summary>
        public event Action OnPlayerDeath;

        /// <summary>Raised each time a coin is collected; argument is the coin's point value.</summary>
        public event Action<int> OnCoinCollected;

        /// <summary>Raised once when a power-up becomes active. Every activation is matched by exactly one expiry.</summary>
        public event Action<PowerUpType> OnPowerUpActivated;

        /// <summary>Raised when a power-up expires (timer elapsed, shield consumed, or player died). Matched one-to-one with activations.</summary>
        public event Action<PowerUpType> OnPowerUpExpired;

        // Internal reset notification — consumed by PlayerCollision to clear tracking sets.
        internal event Action OnRunReset;

        // ──────────────────────────── Lifecycle ────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();

            if (_rb.bodyType != RigidbodyType2D.Dynamic)
                Debug.LogWarning("[PlayerController] Rigidbody2D.bodyType is not Dynamic. Set it to Dynamic.", this);

            // Record spawn coords — fixedX never changes after this point.
            _fixedX = transform.position.x;
            _spawnY = transform.position.y;

            ValidateFields(); // Resets any out-of-range fields to their defaults.

            // Allocated before the bounds guard: ResetRun is public, so a caller can reach it
            // even after this component disables itself. Null arrays would throw there.
            int typeCount = _allPowerUpTypes.Length;
            _powerUpActive = new bool[typeCount];
            _powerUpTimers = new float[typeCount];

            if (_playBoundsMinY >= _playBoundsMaxY)
            {
                Debug.LogError(
                    $"[PlayerController] playBoundsMinY ({_playBoundsMinY}) >= playBoundsMaxY ({_playBoundsMaxY}). " +
                    "Component disabled — fix the bounds in the Inspector.", this);
                enabled = false;
                return;
            }

            _rb.gravityScale = _gravityScale;

            ResolveThrustAction();

            _inputEnabled = true;
            _isAlive = true;
        }

        /// <summary>
        /// Resolves the thrust action. Prefers the Inspector-assigned
        /// <see cref="_thrustActionReference"/>; if none is assigned, builds an equivalent
        /// action in code so the prefab is playable straight out of the box. The fallback
        /// bindings mirror the Jump action in InputSystem_Actions.
        /// </summary>
        private void ResolveThrustAction()
        {
            if (_thrustActionReference != null && _thrustActionReference.action != null)
            {
                _thrustAction = _thrustActionReference.action;
                return;
            }

            _thrustAction = new InputAction("Thrust", InputActionType.Button);
            _thrustAction.AddBinding("<Keyboard>/space");
            _thrustAction.AddBinding("<Mouse>/leftButton");
            _thrustAction.AddBinding("<Gamepad>/buttonSouth");
            _thrustAction.AddBinding("<Touchscreen>/primaryTouch/press");
            _ownsThrustAction = true;
        }

        private void OnEnable()
        {
            if (_thrustAction == null) return;
            // Edge signal: 'started' fires on the press itself, so a press and release that
            // both land between two FixedUpdate ticks still produces exactly one thrust tick.
            _thrustAction.started += HandleThrustStarted;
            _thrustAction.Enable();
        }

        private void OnDisable()
        {
            if (_thrustAction == null) return;
            _thrustAction.started -= HandleThrustStarted;
            _thrustAction.Disable();
        }

        private void OnDestroy()
        {
            // Only dispose the action if this component created it.
            if (_ownsThrustAction) _thrustAction?.Dispose();
        }

        private void HandleThrustStarted(InputAction.CallbackContext _)
        {
            _pressedSinceLastTick = true;
            _requireFreshPress = false; // a genuine new press clears the post-reset latch
        }

        private void FixedUpdate()
        {
            // Re-apply gravity scale so Inspector tuning during Play Mode takes effect.
            _rb.gravityScale = _gravityScale;

            // Sample input at top of tick; gated by _inputEnabled (forced false when dead/reset).
            bool deviceHeld = _thrustAction?.IsPressed() ?? false;
            bool pressedThisTick = _pressedSinceLastTick;
            _pressedSinceLastTick = false;

            // A hold carried across ResetRun counts as RELEASED until the player presses again.
            if (_requireFreshPress && !pressedThisTick) deviceHeld = false;

            _thrustHeld = _inputEnabled && (deviceHeld || pressedThisTick);

            // ── Step 1: Apply thrust force ────────────────────────────────────────────
            if (_isAlive && _thrustHeld)
                _rb.AddForce(Vector2.up * _thrustForce, ForceMode2D.Force);

            // ── Step 2: Clamp vertical speed ──────────────────────────────────────────
            Vector2 vel = _rb.linearVelocity;

            // Guard: a non-finite velocity poisons every later clamp and never recovers,
            // because NaN comparisons are always false.
            if (float.IsNaN(vel.y) || float.IsInfinity(vel.y))
            {
                vel.y = 0f;
                if (!_loggedNonFinite)
                {
                    _loggedNonFinite = true;
                    Debug.LogWarning("[PlayerController] Non-finite vertical velocity corrected to 0.", this);
                }
            }

            vel.y = Mathf.Clamp(vel.y, -_maxFallSpeed, _maxRiseSpeed);
            _rb.linearVelocity = vel;

            // ── Step 3: Clamp Y position (NON-FATAL — does NOT change isAlive) ────────
            float yPos = transform.position.y;
            float clampedY = Mathf.Clamp(yPos, _playBoundsMinY, _playBoundsMaxY);
            if (clampedY != yPos)
            {
                Vector3 p = transform.position;
                p.y = clampedY;
                transform.position = p;

                vel = _rb.linearVelocity;
                vel.y = 0f;
                _rb.linearVelocity = vel;
            }

            // ── Step 4: Lock X — unconditional, even when dead or rb.simulated=false ──
            {
                Vector3 p = transform.position;
                p.x = _fixedX;
                transform.position = p;

                vel = _rb.linearVelocity;
                vel.x = 0f;
                _rb.linearVelocity = vel;
            }

            // ── Power-up timer tick — alive only ──────────────────────────────────────
            if (_isAlive)
                TickPowerUpTimers();
        }

        // ──────────────────────────── Input Control ────────────────────────────────────

        /// <summary>Re-enables thrust input polling. Called by <see cref="ResetRun"/>.</summary>
        public void EnableInput()
        {
            _inputEnabled = true;
        }

        /// <summary>
        /// Disables thrust input and immediately forces <c>thrustHeld</c> to false so physics
        /// stops applying force. Called by the death sequence and by <see cref="ResetRun"/> during
        /// the reset-then-re-enable cycle.
        /// </summary>
        public void DisableInput()
        {
            _inputEnabled = false;
            _thrustHeld = false;
        }

        // ──────────────────────────── Power-Ups ────────────────────────────────────────

        /// <summary>
        /// Activates a power-up. If the type is already active, resets its timer to full without
        /// raising a second <see cref="OnPowerUpActivated"/>. Otherwise sets the flag, starts the
        /// timer, and raises <see cref="OnPowerUpActivated"/> exactly once.
        /// </summary>
        /// <param name="type">Which power-up to activate.</param>
        public void ActivatePowerUp(PowerUpType type)
        {
            int idx = (int)type;
            float duration = GetDuration(type);

            if (_powerUpActive[idx])
            {
                // Already active — reset timer only, no second event
                _powerUpTimers[idx] = duration;
            }
            else
            {
                _powerUpActive[idx] = true;
                _powerUpTimers[idx] = duration;
                UpdatePowerUpFlags();
                RaiseEvent(OnPowerUpActivated, type);
            }
        }

        /// <summary>
        /// Consumes the Shield when an obstacle contact is blocked. Deactivates Shield,
        /// clears its timer, and raises <see cref="OnPowerUpExpired"/> once. No-op if
        /// Shield is not currently active.
        /// </summary>
        public void ConsumeShield()
        {
            int idx = (int)PowerUpType.Shield;
            if (!_powerUpActive[idx]) return;

            _powerUpActive[idx] = false;
            _powerUpTimers[idx] = 0f;
            UpdatePowerUpFlags();
            RaiseEvent(OnPowerUpExpired, PowerUpType.Shield);
        }

        // ──────────────────────────── Internal Death API ───────────────────────────────

        /// <summary>
        /// Called by <see cref="PlayerDeath"/> — atomically mutates all player state and expires
        /// any active power-ups. After this returns, PlayerDeath plays its cue and then calls
        /// <see cref="RaisePlayerDeath"/> to fire the public event.
        /// </summary>
        internal bool TryBeginDeath()
        {
            // Single guard for the whole sequence. _dying covers re-entrancy: expiring
            // power-ups raises events while still alive, and a subscriber could call back
            // into RequestDeath. Without this flag that would produce a second OnPlayerDeath.
            if (_dying || !_isAlive) return false;
            _dying = true;

            ExpireAllActivePowerUps();
            _isAlive = false;
            DisableInput();
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
            return true;
        }

        /// <summary>
        /// Called by <see cref="PlayerDeath"/> after the death cue is played — raises
        /// <see cref="OnPlayerDeath"/> with per-subscriber error isolation.
        /// </summary>
        internal void RaisePlayerDeath()
        {
            RaiseEvent(OnPlayerDeath);
        }

        /// <summary>
        /// Called by <see cref="PlayerCollision"/> to raise <see cref="OnCoinCollected"/>
        /// with per-subscriber error isolation.
        /// </summary>
        /// <param name="value">Point value of the collected coin.</param>
        internal void RaiseCoinCollected(int value)
        {
            RaiseEvent(OnCoinCollected, value);
        }

        // ──────────────────────────── ResetRun ────────────────────────────────────────

        /// <summary>
        /// Fully restores the player to a fresh-run state and re-enables physics and input.
        /// <para>
        /// Idempotent: calling twice in a row leaves identical state; if no power-ups were
        /// active at the second call, no events are raised.
        /// </para>
        /// <para>
        /// Raises <see cref="OnPowerUpExpired"/> once for each type that was active at the
        /// moment of the call. Never raises death or coin events.
        /// </para>
        /// </summary>
        public void ResetRun()
        {
            // Capture which types were active BEFORE clearing (determines what to expire).
            bool[] wasActive = new bool[_powerUpActive.Length];
            for (int i = 0; i < _powerUpActive.Length; i++)
                wasActive[i] = _powerUpActive[i];

            // Restore physics state.
            _rb.simulated = true;
            _rb.linearVelocity = Vector2.zero;
            transform.position = new Vector3(_fixedX, _spawnY, transform.position.z);

            // Restore logic state.
            _isAlive = true;
            _dying = false;
            _isShielded = false;
            _isMagnetActive = false;
            _thrustHeld = false;
            _pressedSinceLastTick = false;
            // A device held across the reset must not resume thrust until released and pressed.
            _requireFreshPress = true;
            EnableInput();

            // Clear all power-up state.
            for (int i = 0; i < _powerUpActive.Length; i++)
            {
                _powerUpActive[i] = false;
                _powerUpTimers[i] = 0f;
            }

            // Notify internal consumers (e.g. PlayerCollision clears tracking sets).
            OnRunReset?.Invoke();

            // Raise OnPowerUpExpired for types that were active at reset time.
            for (int i = 0; i < _allPowerUpTypes.Length; i++)
            {
                if (wasActive[(int)_allPowerUpTypes[i]])
                    RaiseEvent(OnPowerUpExpired, _allPowerUpTypes[i]);
            }
        }

        // ──────────────────────────── Private Helpers ──────────────────────────────────

        private void TickPowerUpTimers()
        {
            foreach (PowerUpType type in _allPowerUpTypes)
            {
                int idx = (int)type;
                if (!_powerUpActive[idx]) continue;

                _powerUpTimers[idx] -= Time.fixedDeltaTime;
                if (_powerUpTimers[idx] <= 0f)
                {
                    _powerUpActive[idx] = false;
                    _powerUpTimers[idx] = 0f;
                    UpdatePowerUpFlags();
                    RaiseEvent(OnPowerUpExpired, type);
                }
            }
        }

        private void ExpireAllActivePowerUps()
        {
            foreach (PowerUpType type in _allPowerUpTypes)
            {
                int idx = (int)type;
                if (!_powerUpActive[idx]) continue;

                _powerUpActive[idx] = false;
                _powerUpTimers[idx] = 0f;
                UpdatePowerUpFlags();
                RaiseEvent(OnPowerUpExpired, type);
            }
        }

        private void UpdatePowerUpFlags()
        {
            _isShielded = _powerUpActive[(int)PowerUpType.Shield];
            _isMagnetActive = _powerUpActive[(int)PowerUpType.Magnet];
        }

        private float GetDuration(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: return _shieldDuration;
                case PowerUpType.Magnet: return _magnetDuration;
                default: return _shieldDuration;
            }
        }

        private void ValidateFields()
        {
            var outOfRange = new StringBuilder();

            if (_thrustForce < 0.1f || _thrustForce > 500f)
            { outOfRange.Append(" thrustForce"); _thrustForce = 35f; }

            if (_gravityScale < 0.1f || _gravityScale > 20f)
            { outOfRange.Append(" gravityScale"); _gravityScale = 3f; }

            if (_maxRiseSpeed < 0.1f || _maxRiseSpeed > 100f)
            { outOfRange.Append(" maxRiseSpeed"); _maxRiseSpeed = 8f; }

            if (_maxFallSpeed < 0.1f || _maxFallSpeed > 100f)
            { outOfRange.Append(" maxFallSpeed"); _maxFallSpeed = 12f; }

            if (_shieldDuration < 1f || _shieldDuration > 60f)
            { outOfRange.Append(" shieldDuration"); _shieldDuration = 5f; }

            if (_magnetDuration < 1f || _magnetDuration > 60f)
            { outOfRange.Append(" magnetDuration"); _magnetDuration = 5f; }

            if (outOfRange.Length > 0)
                Debug.LogWarning(
                    $"[PlayerController] Field(s) out of range — reset to defaults:{outOfRange}", this);
        }

        /// <summary>Invokes every subscriber of <paramref name="evt"/> in its own try/catch so one throwing subscriber never silences the rest.</summary>
        private static void RaiseEvent(Action evt)
        {
            if (evt == null) return;
            foreach (Delegate d in evt.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        /// <summary>Invokes every subscriber of <paramref name="evt"/> in its own try/catch so one throwing subscriber never silences the rest.</summary>
        private static void RaiseEvent<T>(Action<T> evt, T arg)
        {
            if (evt == null) return;
            foreach (Delegate d in evt.GetInvocationList())
            {
                try { ((Action<T>)d)(arg); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
    }
}
