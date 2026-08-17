// Shows the completed run's result vs. the player's best.
//
// The Restart button does NOT reset anything itself — it only asks the Game State Manager
// to do so, via RestartRun(). If this class touched player position, distance, score or
// obstacles directly, it would become responsible for systems outside the UI's scope
// (see 4.4). The UI stays a presentation + input layer; the Game State Manager stays the
// single owner of state transitions.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class GameOverUIController : MonoBehaviour
    {
        [Header("Wiring")]
        //[SerializeField] private GameStateManager _gameStateManager;
        [SerializeField] private GameOverPresenter _presenter;

        [Header("Text Targets")]
        [SerializeField] private TMP_Text _finalDistanceText;
        [SerializeField] private TMP_Text _finalScoreText;
        [SerializeField] private TMP_Text _highScoreText;

        [Tooltip("Optional. Any GameObject (badge, label, particle) shown only on a new record.")]
        [SerializeField] private GameObject _newRecordBadge;

        [Header("Buttons")]
        [SerializeField] private Button _restartButton;

        [Tooltip("Optional. If assigned, Show/Hide fade the group instead of toggling the GameObject.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            //if (_gameStateManager == null) _gameStateManager = FindFirstObjectByType<GameStateManager>();
            if (_presenter == null) _presenter = FindFirstObjectByType<GameOverPresenter>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_restartButton != null) _restartButton.onClick.AddListener(HandleRestartClicked);

            Hide();
        }

        private void OnDestroy()
        {
            if (_restartButton != null) _restartButton.onClick.RemoveListener(HandleRestartClicked);
        }

        /// <summary>Called once by the Game State Manager when it enters Game Over.</summary>
        public void Show(int finalDistance, int finalScore, int highScore, bool isNewRecord)
        {
            if (_finalDistanceText != null) _finalDistanceText.text = $"{finalDistance} m";
            if (_finalScoreText != null) _finalScoreText.text = finalScore.ToString();
            if (_highScoreText != null) _highScoreText.text = highScore.ToString();
            if (_newRecordBadge != null) _newRecordBadge.SetActive(isNewRecord);

            SetVisible(true);
        }

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

        private void HandleRestartClicked()
        {
            //if (_gameStateManager != null) _gameStateManager.RestartRun();
            if (_presenter != null) _presenter.RequestRestart();
        }
    }
}
