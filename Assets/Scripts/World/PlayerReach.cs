// Ported from the 3D runner, where lane changes took a fixed 0.2 s and spacing never
// mattered. Here it does. Climbing the play area takes over a second, so the world has to
// know how long the player needs before it decides how close two obstacles may sit.
//
// Pure maths, no Unity types beyond the struct itself, so it can be checked without an
// engine and without a scene.

using System;

namespace Game.World
{
    /// <summary>
    /// Works out how long the player needs to travel a given vertical distance, from the
    /// player's own thrust, gravity and speed limits.
    /// <para>
    /// The point of this type is that the pattern generator asks a question rather than
    /// assuming an answer. In the 3D version I hard-coded a lane-change duration because it
    /// was a constant. Here the answer depends on tuning, so hard-coding it would silently
    /// go stale the moment somebody drags a thrust slider.
    /// </para>
    /// </summary>
    public readonly struct PlayerReach
    {
        /// <summary>Standard gravity used by the 2D physics engine, in m/s^2.</summary>
        public const float EngineGravity = 9.81f;

        private readonly float _riseAcceleration;
        private readonly float _fallAcceleration;
        private readonly float _maxRiseSpeed;
        private readonly float _maxFallSpeed;

        /// <summary>Height of the play area, in metres.</summary>
        public float BandHeight { get; }

        /// <summary>Net upward acceleration while thrusting, in m/s^2. Negative if thrust cannot lift the body.</summary>
        public float RiseAcceleration => _riseAcceleration;

        /// <summary>Downward acceleration while released, in m/s^2.</summary>
        public float FallAcceleration => _fallAcceleration;

        /// <summary>Thrust divided by weight. Above 1 the player can climb; at or below 1 they cannot.</summary>
        public float ThrustToWeight => _fallAcceleration <= 0f ? 0f : (_riseAcceleration + _fallAcceleration) / _fallAcceleration;

        /// <summary>True when thrust exceeds weight, so the player can actually gain height.</summary>
        public bool CanClimb => _riseAcceleration > 0f;

        /// <summary>
        /// Builds a reach model from raw player values.
        /// </summary>
        /// <param name="thrustForce">Upward force while held, in newtons.</param>
        /// <param name="gravityScale">Multiplier on engine gravity.</param>
        /// <param name="mass">Body mass, in kilograms.</param>
        /// <param name="maxRiseSpeed">Upward speed limit, in m/s.</param>
        /// <param name="maxFallSpeed">Downward speed limit, in m/s.</param>
        /// <param name="boundsMinY">Floor of the play area.</param>
        /// <param name="boundsMaxY">Ceiling of the play area.</param>
        public PlayerReach(
            float thrustForce,
            float gravityScale,
            float mass,
            float maxRiseSpeed,
            float maxFallSpeed,
            float boundsMinY,
            float boundsMaxY)
        {
            if (mass <= 0f || !IsFinite(mass)) mass = 1f;
            if (!IsFinite(gravityScale) || gravityScale < 0f) gravityScale = 0f;
            if (!IsFinite(thrustForce) || thrustForce < 0f) thrustForce = 0f;

            _fallAcceleration = gravityScale * EngineGravity;
            _riseAcceleration = (thrustForce / mass) - _fallAcceleration;

            _maxRiseSpeed = IsFinite(maxRiseSpeed) && maxRiseSpeed > 0f ? maxRiseSpeed : float.MaxValue;
            _maxFallSpeed = IsFinite(maxFallSpeed) && maxFallSpeed > 0f ? maxFallSpeed : float.MaxValue;

            float height = boundsMaxY - boundsMinY;
            BandHeight = IsFinite(height) && height > 0f ? height : 0f;
        }

        /// <summary>
        /// Seconds needed to climb <paramref name="distance"/> metres from a standing start.
        /// Returns <see cref="float.PositiveInfinity"/> when the player cannot climb at all,
        /// which the caller must treat as "this pattern is impossible" rather than as a large
        /// number.
        /// </summary>
        public float ClimbTime(float distance)
        {
            if (distance <= 0f) return 0f;
            if (!CanClimb) return float.PositiveInfinity;
            return TimeUnderCap(distance, _riseAcceleration, _maxRiseSpeed);
        }

        /// <summary>Seconds needed to fall <paramref name="distance"/> metres from a standing start.</summary>
        public float FallTime(float distance)
        {
            if (distance <= 0f) return 0f;
            if (_fallAcceleration <= 0f) return float.PositiveInfinity;
            return TimeUnderCap(distance, _fallAcceleration, _maxFallSpeed);
        }

        /// <summary>
        /// Seconds needed to move from <paramref name="fromY"/> to <paramref name="toY"/>. This is
        /// the question the pattern generator actually asks, and it is asymmetric: climbing takes
        /// roughly twice as long as falling, which is the whole character of the movement.
        /// </summary>
        public float TravelTime(float fromY, float toY)
        {
            float delta = toY - fromY;
            if (delta > 0f) return ClimbTime(delta);
            if (delta < 0f) return FallTime(-delta);
            return 0f;
        }

        /// <summary>
        /// The slowest move the player can be asked to make inside the play area, which is a climb
        /// from the floor to the ceiling. Everything the world spawns has to leave at least this
        /// much time if it wants every pattern to stay possible.
        /// </summary>
        public float WorstCaseTraversal() => ClimbTime(BandHeight);

        /// <summary>
        /// Smallest horizontal gap, in metres, that leaves the player time to travel
        /// <paramref name="verticalDistance"/> metres while the world scrolls past at
        /// <paramref name="scrollSpeed"/>.
        /// <para>
        /// This is the rule the 3D version never needed and the reason this file exists. An
        /// obstacle pair closer than this is not difficult, it is impossible, and a player reads
        /// impossible as broken rather than as hard.
        /// </para>
        /// </summary>
        public float MinimumSpacing(float verticalDistance, float scrollSpeed)
        {
            if (scrollSpeed <= 0f || !IsFinite(scrollSpeed)) return 0f;

            float seconds = verticalDistance >= 0f
                ? ClimbTime(verticalDistance)
                : FallTime(-verticalDistance);

            if (float.IsPositiveInfinity(seconds)) return float.PositiveInfinity;
            return seconds * scrollSpeed;
        }

        /// <summary>
        /// True when a player at <paramref name="fromY"/> can reach <paramref name="toY"/> before an
        /// obstacle <paramref name="spacing"/> metres away arrives at <paramref name="scrollSpeed"/>.
        /// </summary>
        public bool IsReachable(float fromY, float toY, float spacing, float scrollSpeed)
        {
            if (scrollSpeed <= 0f) return true;
            float available = spacing / scrollSpeed;
            float needed = TravelTime(fromY, toY);
            return needed <= available;
        }

        // Constant acceleration up to a speed cap, then constant speed for the remainder.
        private static float TimeUnderCap(float distance, float acceleration, float speedCap)
        {
            float distanceToCap = (speedCap * speedCap) / (2f * acceleration);

            if (distance <= distanceToCap)
                return (float)Math.Sqrt(2f * distance / acceleration);

            float timeToCap = speedCap / acceleration;
            return timeToCap + (distance - distanceToCap) / speedCap;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
