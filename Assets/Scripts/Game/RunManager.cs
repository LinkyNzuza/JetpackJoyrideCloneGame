// Unarine's slice. This is a minimal baseline built by Ako to unblock playability, not a finished
// interface system. It is hers to take over and extend.
//
// Why it exists at all. Until something owned the run, one death ended the session: the player sat
// looking at a corpse with no way back. Retry is the difference between a prototype you can play and
// a prototype you can look at, so the smallest thing that provides it was worth writing now.
//
// What is deliberately NOT here, so extending it is not a matter of unpicking my guesses: no score,
// no distance readout, no high score, no Canvas, no UI of any kind. Those serve the group's data
// collection rather than the question of whether the game feels good, so they can wait for the person
// who owns them. ObstacleDirector already publishes Distance and TierIndex when a HUD wants them.
//
// It also does not touch Time.timeScale. Freezing the world is the world's job, and it does it by
// holding still rather than by stopping time, so a retry prompt could animate over a frozen field.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Player;
using Game.World;

namespace Game.Run
{
    /// <summary>
    /// Owns run state and the retry path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunManager : MonoBehaviour
    {
        /// <summary>The only two states this baseline has.</summary>
        public enum RunState
        {
            /// <summary>The player is alive and the world is moving.</summary>
            Playing = 0,

            /// <summary>The player has died. The world is frozen and a keypress will retry.</summary>
            Dead = 1
        }

        [Header("Wiring")]
        [Tooltip("Player this run belongs to. Found automatically if left empty.")]
        [SerializeField] private PlayerController _player;

        [Tooltip("World director to freeze and reset. Found automatically if left empty.")]
        [SerializeField] private ObstacleDirector _director;

        [Tooltip("Audio director, so a retry is audible. Optional; found automatically if left empty.")]
        [SerializeField] private PlayerAudioDirector _audio;

        [Tooltip("Owner of the difficulty condition. Optional; found automatically if left empty.")]
        [SerializeField] private RunConfig _config;

        [Tooltip("Per-run data recorder. Optional; found automatically if left empty.")]
        [SerializeField] private RunLog _log;

        [Header("Retry")]
        // Tuning: the player very often dies with thrust held, and they will be pressing things. Without
        // a short lockout the first stray input skips straight past the frozen world, which defeats the
        // reason for freezing it. Long enough to read the obstacle, short enough not to feel like a wait.
        [Tooltip("Seconds after death before a keypress is accepted as a retry.")]
        [SerializeField, Range(0f, 3f)] private float _retryLockout = 0.4f;

        [Header("Diagnostics")]
        [SerializeField] private bool _logStateChanges = true;

        private Action _onPlayerDeath;
        private Action<int> _onCoinCollected;
        private float _deadSince;
        private float _runStarted;

        /// <summary>Coins picked up in the current run.</summary>
        public int Coins { get; private set; }

        /// <summary>
        /// Sum of the values of those coins. Kept separate from the count because the tiers are worth
        /// 1, 5 and 25, so eight coins can mean anything between eight and two hundred.
        /// </summary>
        public int CoinValue { get; private set; }

        /// <summary>Seconds the current run has lasted, frozen once the player is dead.</summary>
        public float RunDuration =>
            State == RunState.Dead ? _deadSince - _runStarted : Time.unscaledTime - _runStarted;

        /// <summary>Current run state.</summary>
        public RunState State { get; private set; } = RunState.Playing;

        /// <summary>True once the lockout has elapsed and a keypress would retry.</summary>
        public bool CanRetry =>
            State == RunState.Dead && Time.unscaledTime - _deadSince >= _retryLockout;

        /// <summary>How many runs have ended this session. Useful to a HUD later; nothing reads it yet.</summary>
        public int DeathCount { get; private set; }

        private void Awake()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_director == null) _director = FindFirstObjectByType<ObstacleDirector>();
            if (_audio == null) _audio = FindFirstObjectByType<PlayerAudioDirector>();
            if (_config == null) _config = FindFirstObjectByType<RunConfig>();
            if (_log == null) _log = FindFirstObjectByType<RunLog>();

            if (_player == null)
                Debug.LogError("[RunManager] No PlayerController found. Retry cannot work.", this);

            if (_director == null)
                Debug.LogWarning(
                    "[RunManager] No ObstacleDirector found. Retry will reset the player but the world " +
                    "will neither freeze nor clear.", this);

            _onPlayerDeath = HandlePlayerDeath;
            _onCoinCollected = HandleCoinCollected;
        }

        private void Start()
        {
            // Run one starts here rather than in Awake, so RunConfig has already resolved and applied
            // its condition by the time the clock begins.
            BeginRun();
        }

        private void OnEnable()
        {
            if (_player == null) return;

            _player.OnPlayerDeath += _onPlayerDeath;
            _player.OnCoinCollected += _onCoinCollected;
        }

        private void OnDisable()
        {
            if (_player == null) return;

            _player.OnPlayerDeath -= _onPlayerDeath;
            _player.OnCoinCollected -= _onCoinCollected;
        }

        private void HandleCoinCollected(int value)
        {
            Coins++;
            CoinValue += value;
        }

        // Counters and the clock, in one place so a run cannot start with half of the previous one's
        // numbers still attached.
        private void BeginRun()
        {
            Coins = 0;
            CoinValue = 0;
            _runStarted = Time.unscaledTime;
            State = RunState.Playing;
        }

        private void Update()
        {
            if (State != RunState.Dead) return;
            if (!CanRetry) return;
            if (!RetryRequested()) return;

            Retry();
        }

        /// <summary>
        /// Restarts the run. Order matters and is fixed: the player goes first so it is alive and its
        /// power-up state is cleared before the world starts handing it obstacles, then the world
        /// clears and unfreezes, then the run-start cue plays over an already-valid scene.
        /// <para>
        /// Public so a retry button can call it directly once there is UI, without going through input.
        /// </para>
        /// </summary>
        public void Retry()
        {
            // The condition is promoted first, before anything else is touched. SetProfile rebuilds the
            // pace curve, so doing it here means the new run is laid out under the new condition from
            // its first metre, and the run that just ended keeps the condition it was actually played
            // under. This is the only moment at which changing the curve cannot corrupt a run.
            if (_config != null) _config.ApplyPending();

            if (_player != null) _player.ResetRun();
            if (_director != null) _director.ResetRun();
            if (_audio != null) _audio.PlayRunStart();

            BeginRun();

            if (_logStateChanges)
            {
                string profile = _config != null ? _config.Active.ToString() : "unknown condition";
                Debug.Log($"[RunManager] Retry. Run {DeathCount + 1} starting under {profile}.", this);
            }
        }

        private void HandlePlayerDeath()
        {
            // Guard rather than assume: PlayerDeath fires exactly once per run, but this stays correct
            // if that ever changes, and it keeps DeathCount honest.
            if (State == RunState.Dead) return;

            State = RunState.Dead;
            DeathCount++;
            _deadSince = Time.unscaledTime;

            // Freeze rather than clear. The obstacle that killed them stays on screen, which is the
            // only way a player learns anything from dying.
            if (_director != null) _director.Freeze();

            // Recorded before anything is reset. Every value here is read from the frozen world, so the
            // row describes the run as it actually ended rather than as it was rebuilt.
            RecordRun();

            if (_logStateChanges)
                Debug.Log(
                    $"[RunManager] Dead after {(_director != null ? _director.Distance : 0f):0} m " +
                    $"under {(_config != null ? _config.Active.ToString() : "unknown condition")}. " +
                    $"World frozen. Retry in {_retryLockout:0.00} s.", this);
        }

        private void RecordRun()
        {
            if (_log == null) return;

            _log.Append(new RunLog.Record
            {
                RunIndex = DeathCount,
                Profile = _config != null ? _config.Active : (_director != null ? _director.Profile : default),
                DistanceMetres = _director != null ? _director.Distance : 0f,
                DurationSeconds = RunDuration,
                CoinsCollected = Coins,
                CoinValueTotal = CoinValue,
                TierReached = _director != null ? _director.TierIndex : 0,
                ScrollSpeedAtDeath = _director != null ? _director.ScrollSpeed : 0f,
                SpacingAtDeath = _director != null ? _director.CurrentSpacing : 0f,
                LayoutsRejected = _director != null ? _director.RejectedForReachability : 0,
                FallbacksUsed = _director != null ? _director.FallbacksUsed : 0,
                GuaranteedPowerUps = _director != null ? _director.GuaranteedPowerUps : 0
            });
        }

        /// <summary>
        /// True on the frame any key goes down. Uses the press edge rather than the held state, so a
        /// player who died with thrust held does not retry the instant the lockout expires; they have
        /// to actually press something.
        /// </summary>
        private static bool RetryRequested()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // The condition keys are excluded, because retry accepts any key and those three would
                // otherwise both pick a condition and immediately consume it on the same press. Checked
                // here rather than by asking RunConfig, so it cannot depend on which component's Update
                // happens to run first.
                if (keyboard.digit1Key.wasPressedThisFrame
                    || keyboard.digit2Key.wasPressedThisFrame
                    || keyboard.digit3Key.wasPressedThisFrame)
                    return false;

                if (keyboard.anyKey.wasPressedThisFrame) return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

            return false;
        }
    }
}
