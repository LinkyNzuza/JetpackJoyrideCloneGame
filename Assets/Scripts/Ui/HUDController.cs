// Presentation-only: reads CurrentDistance/score, never calculates them.
//
// Refresh is decoupled from physics on purpose (see 4.2, objective 3). Underlying data can
// change every FixedUpdate, but rebuilding TMP text that often is wasted work — and more
// importantly, lowering the refresh rate must never be able to affect the measurement itself,
// only how often the player SEES it change.
using UnityEngine;
using TMPro;
using Game.Progression;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class HUDController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private DistanceTracker _distanceTracker;
        [SerializeField] private ScoreManager _scoreManager;

        [Header("Text Targets")]
        [SerializeField] private TMP_Text _distanceText;
        [SerializeField] private TMP_Text _scoreText;

        [Header("Refresh")]
        [Tooltip("Seconds between HUD text rebuilds. Lower = smoother, higher = cheaper. Never affects gameplay data.")]
        [SerializeField, Range(0.02f, 0.5f)] private float _refreshInterval = 0.1f;

        [Tooltip("Optional. If assigned, Show/Hide fade the group instead of toggling the GameObject.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        private float _timer;

        private void Awake()
        {
            if (_distanceTracker == null) _distanceTracker = FindFirstObjectByType<DistanceTracker>();
            if (_scoreManager == null) _scoreManager = FindFirstObjectByType<ScoreManager>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < _refreshInterval) return;
            _timer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            int distance = _distanceTracker != null ? _distanceTracker.DisplayedDistance : 0;
            int score = _scoreManager != null ? _scoreManager.CurrentScore : 0;

            if (_distanceText != null) _distanceText.text = $"{distance} m";
            if (_scoreText != null) _scoreText.text = score.ToString();
        }

        /// <summary>Called by the Game State Manager when entering Playing.</summary>
        public void Show()
        {
            SetVisible(true);
            Refresh();
        }

        /// <summary>Called by the Game State Manager on Start and Game Over.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}

