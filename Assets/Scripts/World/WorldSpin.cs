// Rotates a world object at a constant rate. Purely cosmetic.
//
// It exists because a saw that does not turn reads as a decorative cog rather than a threat, and the
// only verb in this game is deciding when to move, so a hazard has to announce itself instantly.
//
// Freeze-aware for the same reason ObstacleDirector is: on death the world holds still so the player
// can see what killed them, and a blade still merrily spinning next to a corpse undoes that.
// It reads the director's published state rather than being told, so it stays a listener.

using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Spins a transform about Z while the world is moving.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpin : MonoBehaviour
    {
        [Tooltip("Degrees per second about Z. Negative spins the other way.")]
        [SerializeField] private float _degreesPerSecond = 180f;

        [Tooltip("Director whose frozen state pauses the spin. Found automatically if left empty.")]
        [SerializeField] private ObstacleDirector _director;

        private bool _searched;

        /// <summary>Sets the spin rate at spawn time.</summary>
        public void SetRate(float degreesPerSecond) => _degreesPerSecond = degreesPerSecond;

        /// <summary>Supplies the director directly, so a spawner need not make this search for one.</summary>
        public void SetDirector(ObstacleDirector director)
        {
            _director = director;
            _searched = true;
        }

        private void Update()
        {
            if (_degreesPerSecond == 0f) return;

            // Searched once rather than every frame. Objects of this kind are spawned constantly, and a
            // scene-wide search per instance per frame would be the most expensive thing in the game.
            if (_director == null && !_searched)
            {
                _searched = true;
                _director = FindFirstObjectByType<ObstacleDirector>();
            }

            if (_director != null && _director.IsFrozen) return;

            transform.Rotate(0f, 0f, _degreesPerSecond * Time.deltaTime);
        }
    }
}
