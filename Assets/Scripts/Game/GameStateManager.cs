// UExtends Ako's  RunManager baseline into the full state + reset
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
            EnterStart();
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
            if (State != GameState.Playing) return;

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

