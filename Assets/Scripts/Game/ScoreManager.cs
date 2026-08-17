// Interprets CurrentDistance + coin events into the displayed score.
//
// Score = DisplayedDistance + (coins collected * coin multiplier). Coin events are received
// from Ako's PlayerController.OnCoinCollected and folded into a running coin tally WITHOUT
// ever touching DistanceTracker's CurrentDistance — the two measurements stay independent
// so a UI/scoring decision can never contaminate the value the difficulty system relies on.
using UnityEngine;
using Game.Player;
using Game.Run;

namespace Game.Progression
{
    [DisallowMultipleComponent]
    public sealed class ScoreManager : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private DistanceTracker _distanceTracker;
        [SerializeField] private PlayerController _player;
        [SerializeField] private RunManager _runManager;

        [Header("Diagnostics")]
        [Tooltip("Logs every coin event and the resulting coin tally, to help debug wiring issues.")]
        [SerializeField] private bool _logCoinEvents = false;

        [Header("Scoring")]
        [Tooltip("Points awarded per unit of coin value collected.")]
        [SerializeField, Range(1, 10)] private int _coinMultiplier = 1;

        private int _coinScore;
        private bool _isFrozen;
        private int _frozenScore;
        private RunManager.RunState _previousRunState;

        /// <summary>Coins collected so far this run, already multiplied.</summary>
        public int CoinScore => _coinScore;

        /// <summary>Live score while playing; holds steady once frozen after death.</summary>
        public int CurrentScore =>
            _isFrozen ? _frozenScore : (_distanceTracker != null ? _distanceTracker.DisplayedDistance : 0) + _coinScore;

        /// <summary>Set once by FreezeFinalResult() on death. Read by HighScoreManager and the Game Over UI.</summary>
        public int FinalScore { get; private set; }

        /// <summary>Set once by FreezeFinalResult() on death.</summary>
        public int FinalDistance { get; private set; }

        private void Awake()
        {
            if (_distanceTracker == null) _distanceTracker = FindFirstObjectByType<DistanceTracker>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_runManager == null) _runManager = FindFirstObjectByType<RunManager>();

            if (_distanceTracker == null)
                Debug.LogError("[ScoreManager] No DistanceTracker found. Score will read as coins only.", this);
            if (_player == null)
                Debug.LogError("[ScoreManager] No PlayerController found or wired. Coins will NEVER be added to score.", this);
            if (_runManager == null)
                Debug.LogWarning("[ScoreManager] No RunManager found. Score will never reset between runs.", this);
        }

        private void OnEnable()
        {
            //if (_player != null) _player.OnCoinCollected += HandleCoinCollected;
            if (_player != null)
            {
                _player.OnCoinCollected += HandleCoinCollected;
                if (_logCoinEvents)
                    Debug.Log($"[ScoreManager] Subscribed to OnCoinCollected on '{_player.name}'.", this);
                _player.OnPlayerDeath += HandlePlayerDeath;
            }
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.OnCoinCollected -= HandleCoinCollected;
                _player.OnPlayerDeath -= HandlePlayerDeath;
            }
            //if (_player != null) _player.OnCoinCollected -= HandleCoinCollected;
        }

        private void Start()
        {
            _previousRunState = _runManager != null ? _runManager.State : RunManager.RunState.Playing;
        }

        private void Update()
        {
            if (_runManager == null) return;

            RunManager.RunState current = _runManager.State;
            if (current == _previousRunState) return;

            if (current == RunManager.RunState.Playing)
            {
                // Dead -> Playing: a retry just happened, however it was triggered.
                ResetScore();
            }

            _previousRunState = current;
        }

        private void HandleCoinCollected(int coinValue)
        {
            // Ignore stray collection events after the run has already ended — a coin whose
            // trigger fires the same physics step as death must not sneak into a frozen score.
            //if (_isFrozen) return;
            // _coinScore += coinValue * _coinMultiplier;
            if (_isFrozen)
            {
                if (_logCoinEvents)
                    Debug.Log($"[ScoreManager] Coin worth {coinValue} ignored — score already frozen.", this);
                return;
            }

            _coinScore += coinValue * _coinMultiplier;

            if (_logCoinEvents)
                Debug.Log($"[ScoreManager] Coin collected (value {coinValue}). CoinScore is now {_coinScore}.", this);
        }

        private void HandlePlayerDeath()
        {
            FreezeFinalResult();
        }

        /// <summary>Called by the Game State Manager as step 3 of the reset sequence.</summary>
        public void ResetScore()
        {
            _coinScore = 0;
            _isFrozen = false;
            _frozenScore = 0;
            FinalScore = 0;
            FinalDistance = 0;
        }

        /// <summary>
        /// Called by the Game State Manager immediately after DistanceTracker.StopRun(), before
        /// anything else reacts to death. Locks in FinalScore/FinalDistance so later frames
        /// (e.g. a coin trigger still resolving) can never change the recorded result.
        /// </summary>
        public void FreezeFinalResult()
        {
            FinalDistance = _distanceTracker != null ? _distanceTracker.DisplayedDistance : 0;
            FinalScore = FinalDistance + _coinScore;
            _frozenScore = FinalScore;
            _isFrozen = true;
        }
    }
}


