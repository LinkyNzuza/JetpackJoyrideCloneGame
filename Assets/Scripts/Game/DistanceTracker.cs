using UnityEngine;

namespace Game.Progression
{
    /// <summary>
    /// Anything that can report the world's current scroll speed. Implement this on
    /// Linky's ObstacleDirector (or whatever owns world scrolling) so DistanceTracker can
    /// read it without depending on that class's internals.
    /// </summary>
    public interface IScrollSpeedProvider
    {
        float ScrollSpeed { get; }
    }

    /// <summary>
    /// Owns and accumulates the authoritative CurrentDistance value.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistanceTracker : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Component implementing IScrollSpeedProvider (e.g. the ObstacleDirector). Found automatically if left empty and the found object implements the interface.")]
        [SerializeField] private MonoBehaviour _scrollSpeedSource;

        [Tooltip("Used only if no IScrollSpeedProvider is wired or found, so the tracker still runs during standalone testing.")]
        [SerializeField, Range(0.1f, 50f)] private float _fallbackScrollSpeed = 5f;

        [Header("Diagnostics")]
        [SerializeField] private bool _logWarnings = true;

        private IScrollSpeedProvider _speedProvider;

        /// <summary>The raw, unrounded distance value. This is the number every other system should read.</summary>
        public float CurrentDistance { get; private set; }

        /// <summary>
        /// The presentation value. Floored, never rounded — flooring prevents the interface
        /// from showing progress the player has not physically reached yet (see 2.4).
        /// </summary>
        public int DisplayedDistance => Mathf.FloorToInt(CurrentDistance);

        /// <summary>True while distance is accumulating (i.e. the game is in the Playing state).</summary>
        public bool IsAccumulating { get; private set; }

        private void Awake()
        {
            ResolveSpeedProvider();
        }

        private void ResolveSpeedProvider()
        {
            if (_scrollSpeedSource == null)
            {
                // Try to find ANY MonoBehaviour in the scene implementing the interface.
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (behaviour is IScrollSpeedProvider provider)
                    {
                        _speedProvider = provider;
                        _scrollSpeedSource = behaviour;
                        break;
                    }
                }
            }
            else
            {
                _speedProvider = _scrollSpeedSource as IScrollSpeedProvider;
            }

            if (_speedProvider == null && _logWarnings)
                Debug.LogWarning(
                    "[DistanceTracker] No IScrollSpeedProvider found or wired. Falling back to a constant " +
                    $"speed of {_fallbackScrollSpeed} m/s. Ask Linky to implement IScrollSpeedProvider on " +
                    "ObstacleDirector for accurate distance.", this);
        }

        /// <summary>Called by the Game State Manager when entering Playing.</summary>
        public void BeginRun()
        {
            IsAccumulating = true;
        }

        /// <summary>
        /// Called by the Game State Manager on death. Distance stops accumulating the instant
        /// the game leaves the Playing state — the final value is what scoring and the high
        /// score system evaluate.
        /// </summary>
        public void StopRun()
        {
            IsAccumulating = false;
        }

        /// <summary>Called by the Game State Manager as step 2 of the reset sequence.</summary>
        public void ResetDistance()
        {
            CurrentDistance = 0f;
            IsAccumulating = false;
        }

        private void FixedUpdate()
        {
            if (!IsAccumulating) return;

            float speed = _speedProvider != null ? _speedProvider.ScrollSpeed : _fallbackScrollSpeed;
            if (speed < 0f) speed = 0f; // distance never runs backwards, even if scroll speed briefly goes negative

            CurrentDistance += speed * Time.fixedDeltaTime;
        }
    }
}

