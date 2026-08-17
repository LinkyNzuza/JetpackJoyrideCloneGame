// Ported from the 3D runner's RunPaceController. The good part of that design was the tier
// table: each tier had a ceiling and a floor, and natural speed climbed smoothly from the
// previous ceiling to the current one, so the ceilings were waypoints on one continuous curve
// rather than caps the run might never approach.
//
// Two things changed in the port. The 3D game drove pace from score and let collected stars
// brake the player, because braking was that group's twist. Here difficulty is driven by
// distance, which is what our hypothesis actually manipulates, and nothing brakes. And the
// curve now reports the minimum obstacle spacing it needs at its own speed, because a speed
// the player cannot dodge at is not difficulty.
//
// Pure logic, no Unity types, so the three difficulty configurations can be compared on paper
// before anybody plays them.

using System;

namespace Game.World
{
    /// <summary>Which of the three configurations our investigation compares.</summary>
    public enum DifficultyProfile
    {
        /// <summary>Control condition. Speed holds near its starting value for the whole run.</summary>
        Constant = 0,

        /// <summary>Our hypothesis condition. Speed rises gradually with distance.</summary>
        Progressive = 1,

        /// <summary>Comparison condition. Speed rises substantially over a much shorter distance.</summary>
        Aggressive = 2
    }

    /// <summary>
    /// One difficulty tier: where it begins, and the speeds it works between.
    /// </summary>
    [Serializable]
    public struct PaceTier
    {
        /// <summary>Distance in metres at which this tier begins.</summary>
        public float DistanceThreshold;

        /// <summary>Fastest the world scrolls in this tier.</summary>
        public float MaximumSpeed;

        /// <summary>Slowest the world scrolls in this tier. Rises with each tier, so the run tightens.</summary>
        public float MinimumSpeed;

        public PaceTier(float distanceThreshold, float maximumSpeed, float minimumSpeed)
        {
            DistanceThreshold = distanceThreshold;
            MaximumSpeed = maximumSpeed;
            MinimumSpeed = minimumSpeed;
        }
    }

    /// <summary>
    /// Turns distance travelled into a scroll speed, and reports what that speed demands of the
    /// obstacle spacing.
    /// </summary>
    public sealed class PaceCurve
    {
        private readonly PaceTier[] _tiers;
        private readonly float _startSpeed;

        /// <summary>Which configuration this curve represents.</summary>
        public DifficultyProfile Profile { get; }

        public PaceCurve(DifficultyProfile profile, float startSpeed, PaceTier[] tiers)
        {
            Profile = profile;
            _startSpeed = startSpeed > 0f ? startSpeed : 1f;
            _tiers = tiers != null && tiers.Length > 0
                ? tiers
                : new[] { new PaceTier(0f, _startSpeed, _startSpeed) };
        }

        /// <summary>Number of tiers in this curve.</summary>
        public int TierCount => _tiers.Length;

        /// <summary>
        /// Builds the default curve for a profile. The three tables share a shape and differ in how far
        /// the player travels before each tier arrives, which is exactly the variable our group is
        /// manipulating: the rate of escalation rather than the amount.
        /// </summary>
        public static PaceCurve Default(DifficultyProfile profile)
        {
            switch (profile)
            {
                case DifficultyProfile.Constant:
                    // Control. One tier, so speed never changes and any behaviour change across a run
                    // cannot be attributed to difficulty.
                    return new PaceCurve(profile, 4f, new[]
                    {
                        new PaceTier(0f, 4f, 4f)
                    });

                case DifficultyProfile.Aggressive:
                    // Same ceilings as progressive, reached in roughly a fifth of the distance.
                    return new PaceCurve(profile, 4f, new[]
                    {
                        new PaceTier(0f,    5f,  4f),
                        new PaceTier(60f,   7f,  5f),
                        new PaceTier(160f,  9f,  6f),
                        new PaceTier(320f,  11f, 7f),
                        new PaceTier(560f,  13f, 8f)
                    });

                default:
                    // Progressive. Thresholds multiply, so each tier takes noticeably longer to reach.
                    return new PaceCurve(profile, 4f, new[]
                    {
                        new PaceTier(0f,    5f,  4f),
                        new PaceTier(300f,  7f,  5f),
                        new PaceTier(800f,  9f,  6f),
                        new PaceTier(1600f, 11f, 7f),
                        new PaceTier(2800f, 13f, 8f)
                    });
            }
        }

        /// <summary>Zero-based index of the tier a distance falls in.</summary>
        public int TierAt(float distance)
        {
            int index = 0;
            for (int i = 0; i < _tiers.Length; i++)
                if (distance >= _tiers[i].DistanceThreshold) index = i;
            return index;
        }

        /// <summary>
        /// Scroll speed at a given distance. Climbs smoothly from the previous tier's ceiling to this
        /// tier's ceiling, arriving exactly as the next threshold is crossed. The final tier holds.
        /// </summary>
        public float SpeedAt(float distance)
        {
            int tier = TierAt(distance);
            float ceiling = _tiers[tier].MaximumSpeed;
            float floor = Math.Min(_tiers[tier].MinimumSpeed, ceiling);

            float from = tier == 0 ? _startSpeed : _tiers[tier - 1].MaximumSpeed;

            if (tier >= _tiers.Length - 1) return Clamp(ceiling, floor, ceiling);

            float spanStart = _tiers[tier].DistanceThreshold;
            float spanEnd = _tiers[tier + 1].DistanceThreshold;
            if (spanEnd <= spanStart) return Clamp(ceiling, floor, ceiling);

            float progress = Clamp01((distance - spanStart) / (spanEnd - spanStart));
            float natural = from + (ceiling - from) * progress;
            return Clamp(natural, floor, ceiling);
        }

        /// <summary>Slowest speed the current tier allows.</summary>
        public float FloorAt(float distance)
        {
            int tier = TierAt(distance);
            return Math.Min(_tiers[tier].MinimumSpeed, _tiers[tier].MaximumSpeed);
        }

        /// <summary>Fastest speed the current tier allows.</summary>
        public float CeilingAt(float distance) => _tiers[TierAt(distance)].MaximumSpeed;

        /// <summary>
        /// Smallest obstacle spacing this curve can honestly ask for at a given distance, given what the
        /// player can physically do. This is the bound my player system imposes on the world system, and
        /// the reason the two have to be tuned together rather than separately.
        /// </summary>
        public float MinimumSpacingAt(float distance, PlayerReach reach)
        {
            float speed = SpeedAt(distance);
            return reach.WorstCaseTraversal() * speed;
        }

        /// <summary>
        /// Largest speed at which a given spacing still leaves the player time for the worst move in the
        /// play area. Useful the other way round: given the spacing a chunk was authored with, this says
        /// how fast the world may scroll before that chunk becomes impossible.
        /// </summary>
        public static float MaximumHonestSpeed(float spacing, PlayerReach reach)
        {
            float worst = reach.WorstCaseTraversal();
            if (worst <= 0f || float.IsPositiveInfinity(worst)) return 0f;
            return spacing / worst;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);
    }
}
