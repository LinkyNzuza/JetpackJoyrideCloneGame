// Extends Ako's  RunManager baseline into the full state + reset
// orchestration layer described in sections 5 and 6 of the technical document.
//
// Single source of truth for whether gameplay is active (5.2, objective 1). No other system
// — not the player, not the world, not the UI — decides this independently. On death, the
// Player Controller only raises OnPlayerDeath; THIS class decides what that means for the
// rest of the game (5.4):
//     
//
// The reset sequence is explicit and ordered rather than a scene reload, because a reload
// would hide whether individual systems can actually return to a clean initial state (5.5):

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Player;
using Game.World;
using Game.Progression;
using Game.UI;
using Game.Run;

namespace Game.Core
{
    public enum GameState
    {
        Start = 0,
        Playing = 1,
        Paused = 2,
        GameOver = 3
    }

    [DisallowMultipleComponent]
    public sealed class GameStateManager : MonoBehaviour
    {
        [Header("Wiring — Gameplay")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private ObstacleDirector _world;
        [SerializeField] private DistanceTracker _distanceTracker;
        [SerializeField] private ScoreManager _scoreManager;
        [SerializeField] private HighScoreManager _highScoreManager;

        [Header("Wiring — UI")]
        [SerializeField] private HUDController _hud;
        [SerializeField] private GameOverUIController _gameOverUI;
        [SerializeField] private StartScreenController _startScreen;

        [Header("Start Behaviour")]
        [Tooltip("If true, the game waits at a Start screen for StartGame() (e.g. a button click). " +
                "If false, the game skips straight into Playing on scene load — no Start screen needed.")]
        [SerializeField] private bool _useStartScreen = false;

        [Header("Legacy Integration")]
        [Tooltip("Ako's RunManager baseline handles death/retry on its own and will conflict with this " +
                "class if both are active (duplicate freezes, duplicate retries, mismatched distance " +
                "readings). If true, GameStateManager disables any RunManager found in the scene at " +
                "startup — without editing RunManager.cs — so it becomes the single source of truth.")]
        [SerializeField] private bool _takeOverFromRunManager = true;


        [Header("Retry")]
        [Tooltip("Mirrors Ako's RunManager lockout: seconds after death before a keypress/tap is accepted as a retry, so the input that killed the player can't instantly retry it.")]
        [SerializeField, Range(0f, 3f)] private float _retryLockout = 0.4f;

        [Header("Diagnostics")]
        [SerializeField] private bool _logStateChanges = true;

        private float _deadSince;
        private Action _onPlayerDeath;

        public GameState State { get; private set; } = GameState.Start;

        public bool CanRetry =>
            State == GameState.GameOver && Time.unscaledTime - _deadSince >= _retryLockout;

        private void Awake()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_world == null) _world = FindFirstObjectByType<ObstacleDirector>();
            if (_distanceTracker == null) _distanceTracker = FindFirstObjectByType<DistanceTracker>();
            if (_scoreManager == null) _scoreManager = FindFirstObjectByType<ScoreManager>();
            if (_highScoreManager == null) _highScoreManager = FindFirstObjectByType<HighScoreManager>();
            if (_hud == null) _hud = FindFirstObjectByType<HUDController>();
            if (_gameOverUI == null) _gameOverUI = FindFirstObjectByType<GameOverUIController>();
            if (_startScreen == null) _startScreen = FindFirstObjectByType<StartScreenController>();

            _onPlayerDeath = HandlePlayerDeath;

            if (_player == null)
                Debug.LogError("[GameStateManager] No PlayerController found. Game Over cannot trigger.", this);
            if (_distanceTracker == null)
                Debug.LogError("[GameStateManager] No DistanceTracker found/wired. Distance will never accumulate.", this);
            if (_scoreManager == null)
                Debug.LogWarning("[GameStateManager] No ScoreManager found/wired. Score/coins will not be tracked.", this);
            if (_highScoreManager == null)
                Debug.LogWarning("[GameStateManager] No HighScoreManager found/wired. High score will not update.", this);
            if (_world == null)
                Debug.LogWarning("[GameStateManager] No ObstacleDirector (World) found/wired. World won't freeze on death or clear on restart.", this);
            if (_hud == null)
                Debug.LogWarning("[GameStateManager] No HUDController found/wired. Live distance/score won't display.", this);
            if (_gameOverUI == null)
                Debug.LogError("[GameStateManager] No GameOverUIController found/wired. The Game Over panel will never appear.", this);

            if (!_useStartScreen && _startScreen != null)
                _startScreen.Hide();
            if (_takeOverFromRunManager)
                TakeOverFromLegacyRunManager();
        }

        /// Disables Ako's RunManager (if present) so it stops reacting to OnPlayerDeath and stops
        /// handling its own retry input. This only flips the built-in Component.enabled flag —
        /// it does not modify RunManager.cs. If RunManager hasn't run its own OnEnable yet, this
        /// prevents it from ever subscribing in the first place; if it already has, disabling it
        /// triggers RunManager's own OnDisable, which unsubscribes it cleanly.
        /// </summary>
        private void TakeOverFromLegacyRunManager()
        {
            RunManager legacyRunManager = FindFirstObjectByType<RunManager>();
            if (legacyRunManager == null) return;

            if (legacyRunManager.enabled)
            {
                legacyRunManager.enabled = false;
                if (_logStateChanges)
                    Debug.Log(
                        "[GameStateManager] Found an active RunManager and disabled it — " +
                        "GameStateManager is now the single source of truth for run state.", this);
            }
        }


        private void OnEnable()
        {
            if (_player != null) _player.OnPlayerDeath += _onPlayerDeath;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnPlayerDeath -= _onPlayerDeath;
        }

        private void Start()
        {
            if (_useStartScreen)
                EnterStart();
            else
                BeginPlaying(); // auto-start: no Start screen, input enabled immediately

            //EnterStart();
        }

        private void Update()
        {
            if (State != GameState.GameOver) return;
            if (!CanRetry) return;
            if (!RetryRequested()) return;
            RestartRun();
        }

        // ───────────────────────────── Start ─────────────────────────────

        private void EnterStart()
        {
            if (_player != null) _player.DisableInput();
            if (_hud != null) _hud.Hide();
            if (_gameOverUI != null) _gameOverUI.Hide();
            if (_startScreen != null) _startScreen.Show();

            SetState(GameState.Start);
        }

        /// <summary>Called by the Start screen's button.</summary>
        public void StartGame()
        {
            if (State != GameState.Start) return;
            if (_startScreen != null) _startScreen.Hide();
            BeginPlaying();
        }

        // ──────────────────────────── Playing ────────────────────────────

        private void BeginPlaying()
        {
            if (_player != null) _player.EnableInput();
            if (_distanceTracker != null) _distanceTracker.BeginRun();
            if (_hud != null) _hud.Show();

            SetState(GameState.Playing);
        }

        // ──────────────────────────── Paused ─────────────────────────────
        // Note: Pause freezes via Time.timeScale, deliberately unlike Game Over. Game Over
        // freezes the world object itself (world.Freeze()) so the obstacle that killed the
        // player stays visible and readable (per Ako's RunManager). Pause is a menu-level
        // freeze and is expected to stop animation/physics entirely, so timeScale is correct here.

        public void PauseGame()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 0f;
            if (_player != null) _player.DisableInput();
            SetState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (State != GameState.Paused) return;
            Time.timeScale = 1f;
            if (_player != null) _player.EnableInput();
            SetState(GameState.Playing);
        }

        // ─────────────────────────── Game Over ───────────────────────────

        private void HandlePlayerDeath()
        {
            if (_logStateChanges)
                Debug.Log($"[GameStateManager] OnPlayerDeath received. Current state: {State}.", this);

            if (State != GameState.Playing)
            {
                if (_logStateChanges)
                    Debug.LogWarning($"[GameStateManager] Death ignored because state was {State}, not Playing.", this);
                return;
            }


            //if (State != GameState.Playing) return;

            // Order matters: stop measuring before freezing the final result, freeze the
            // result before evaluating the high score, evaluate before presenting the UI.
            if (_distanceTracker != null) _distanceTracker.StopRun();
            if (_scoreManager != null) _scoreManager.FreezeFinalResult();

            int finalDistance = _scoreManager != null ? _scoreManager.FinalDistance : 0;
            int finalScore = _scoreManager != null ? _scoreManager.FinalScore : 0;

            bool isNewRecord = false;
            int highScore = finalScore;
            if (_highScoreManager != null)
            {
                _highScoreManager.EvaluateRun(finalScore, finalDistance);
                isNewRecord = _highScoreManager.IsNewRecord;
                highScore = _highScoreManager.HighScore;
            }

            if (_world != null) _world.Freeze();
            if (_hud != null) _hud.Hide();
            if (_gameOverUI != null) _gameOverUI.Show(finalDistance, finalScore, highScore, isNewRecord);

            _deadSince = Time.unscaledTime;
            SetState(GameState.GameOver);

            if (_logStateChanges)
                Debug.Log(
                    $"[GameStateManager] Game Over. Distance {finalDistance}m, Score {finalScore}, " +
                    $"NewRecord {isNewRecord}. Retry in {_retryLockout:0.00}s.", this);
        }

        // ──────────────────────────── Restart ────────────────────────────

        /// <summary>
        /// The explicit, ordered reset from 5.5. Called by the Game Over UI's Restart button
        /// or by a retry keypress/tap. Every step is deliberate — see the class header.
        /// </summary>
        public void RestartRun()
        {
            if (_player != null) _player.ResetRun();               // Reset Player
            if (_distanceTracker != null) _distanceTracker.ResetDistance(); // Reset Distance
            if (_scoreManager != null) _scoreManager.ResetScore();  // Reset Score + Reset Coins
                                                                    // (coin tally lives inside ScoreManager)
            if (_world != null) _world.ResetRun();                  // Clear World + Generate Initial Chunk

            if (_gameOverUI != null) _gameOverUI.Hide();            // Reset UI
            if (_hud != null) _hud.Show();

            BeginPlaying();                                          // Enter Playing

            if (_logStateChanges)
                Debug.Log("[GameStateManager] Run restarted.", this);
        }

        // ──────────────────────────── Helpers ────────────────────────────

        private void SetState(GameState newState)
        {
            if (_logStateChanges && State != newState)
                Debug.Log($"[GameStateManager] {State} -> {newState}", this);
            State = newState;
        }

        /// <summary>Same press-edge detection as Ako's RunManager, so retry never fires the instant the lockout ends from a held input.</summary>
        private static bool RetryRequested()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
        }
    }
}

