// The Start screen's only job is to hand control to the Game State Manager.
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class StartScreenController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameStateManager _gameStateManager;
        [SerializeField] private Button _startButton;

        [Tooltip("Optional. If assigned, Show/Hide fade the group instead of toggling the GameObject.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (_gameStateManager == null) _gameStateManager = FindFirstObjectByType<GameStateManager>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_startButton != null) _startButton.onClick.AddListener(HandleStartClicked);
        }

        private void OnDestroy()
        {
            if (_startButton != null) _startButton.onClick.RemoveListener(HandleStartClicked);
        }

        private void HandleStartClicked()
        {
            if (_gameStateManager != null) _gameStateManager.StartGame();
        }

        /// <summary>Called by the Game State Manager on entering the Start state.</summary>
        public void Show()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>Called by the Game State Manager when leaving Start.</summary>
        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}

