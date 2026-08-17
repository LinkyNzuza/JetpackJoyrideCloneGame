// Unarine's slice. Bridges Ako's RunManager to the presentation layer, without editing
// RunManager.cs at all — it only reads RunManager's public State and calls its public
// Retry(). RunManager stays the single owner of Playing/Dead and the retry mechanism
// (including its own keypress/tap handling); this class only reacts to what RunManager
// decides, the same way ScoreManager reacts to PlayerController's events rather than being
// told what to do by an external controller.
//
// Watching State via Update() (rather than subscribing to OnPlayerDeath directly) is
// deliberate: by the time this script observes RunManager.State == Dead, ScoreManager and
// DistanceTracker have already run their own OnPlayerDeath handlers (same frame, same event),
// so FinalScore/FinalDistance are guaranteed to be frozen and ready to read here — no
// ordering race between independent subscribers.
using UnityEngine;
using Game.Run;
using Game.Progression;
using Game.UI;

namespace Game.Core
{
    [DisallowMultipleComponent]
    public sealed class GameOverPresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RunManager _runManager;
        [SerializeField] private ScoreManager _scoreManager;
        [SerializeField] private HighScoreManager _highScoreManager;
        [SerializeField] private HUDController _hud;
        [SerializeField] private GameOverUIController _gameOverUI;

        [Header("Diagnostics")]
        [SerializeField] private bool _logStateChanges = true;

        private RunManager.RunState _previousState;

        private void Awake()
        {
            if (_runManager == null) _runManager = FindFirstObjectByType<RunManager>();
            if (_scoreManager == null) _scoreManager = FindFirstObjectByType<ScoreManager>();
            if (_highScoreManager == null) _highScoreManager = FindFirstObjectByType<HighScoreManager>();
            if (_hud == null) _hud = FindFirstObjectByType<HUDController>();
            if (_gameOverUI == null) _gameOverUI = FindFirstObjectByType<GameOverUIController>();

            if (_runManager == null)
                Debug.LogError("[GameOverPresenter] No RunManager found. Game Over panel will never appear.", this);
            if (_gameOverUI == null)
                Debug.LogError("[GameOverPresenter] No GameOverUIController found/wired.", this);
        }

        private void Start()
        {
            _previousState = _runManager != null ? _runManager.State : RunManager.RunState.Playing;
            if (_hud != null) _hud.Show();
            if (_gameOverUI != null) _gameOverUI.Hide();
        }

        private void Update()
        {
            if (_runManager == null) return;

            RunManager.RunState current = _runManager.State;
            if (current == _previousState) return;

            if (current == RunManager.RunState.Dead) HandleRunEnded();
            else if (current == RunManager.RunState.Playing) HandleRunStarted();

            _previousState = current;
        }

        private void HandleRunEnded()
        {
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

            if (_hud != null) _hud.Hide();
            if (_gameOverUI != null) _gameOverUI.Show(finalDistance, finalScore, highScore, isNewRecord);

            if (_logStateChanges)
                Debug.Log(
                    $"[GameOverPresenter] Run ended. Distance {finalDistance}m, Score {finalScore}, " +
                    $"NewRecord {isNewRecord}.", this);
        }

        private void HandleRunStarted()
        {
            if (_gameOverUI != null) _gameOverUI.Hide();
            if (_hud != null) _hud.Show();

            if (_logStateChanges)
                Debug.Log("[GameOverPresenter] Run restarted.", this);
        }

        /// <summary>Called by the Game Over UI's Restart button. Forwards to RunManager's own retry.</summary>
        public void RequestRestart()
        {
            if (_runManager != null) _runManager.Retry();
        }
    }
}
