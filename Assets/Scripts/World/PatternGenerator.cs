// Ported from the 3D runner's PatternLibrary, which generated a fresh lane layout every time
// instead of picking from a fixed list, and capped obstacles at two of three lanes so a clear
// path always existed.
//
// That cap is necessary here and not sufficient. In the 3D game a lane change took 0.2 s, so a
// clear lane was always reachable and spacing never entered the design. In this game crossing
// the play area takes over a second, so a clear band the player cannot get to in time is the
// same thing as no clear band at all. This generator therefore checks reachability against the
// player's own numbers before it accepts a layout.
//
// Pure logic, seeded through System.Random rather than UnityEngine.Random, so a run can be
// reproduced and so this can be checked without the engine.

using System;

namespace Game.World
{
    /// <summary>
    /// Produces obstacle patterns that stay possible for the player to pass.
    /// </summary>
    public sealed class PatternGenerator
    {
        private readonly Random _random;

        /// <summary>Fewest bands blocked in a set. Zero produces occasional empty sets, which is breathing room.</summary>
        public int MinObstacleBands { get; set; } = 1;

        /// <summary>
        /// Most bands blocked in a set. Hard capped at two below, because blocking all three leaves no
        /// way through whatever the spacing is.
        /// </summary>
        public int MaxObstacleBands { get; set; } = 2;

        /// <summary>Chance that a band with no obstacle receives a coin.</summary>
        public float CoinChanceOnClearBand { get; set; } = 0.5f;

        /// <summary>Chance that a band which would have received a coin receives a power-up instead.</summary>
        public float PowerUpChanceInsteadOfCoin { get; set; } = 0.08f;

        /// <summary>
        /// How many layouts to try before giving up and using the guaranteed-safe fallback. Bounded so a
        /// hostile combination of speed and spacing cannot spin this loop.
        /// </summary>
        public int MaxAttempts { get; set; } = 12;

        /// <summary>
        /// Counts how many times the reachability check rejected a layout. Worth watching: a high number
        /// means the difficulty settings are pushing against what the player can physically do.
        /// </summary>
        public int RejectedForReachability { get; private set; }

        /// <summary>Counts how many times every attempt failed and the fallback was used.</summary>
        public int FallbacksUsed { get; private set; }

        public PatternGenerator(int seed)
        {
            _random = new Random(seed);
        }

        public PatternGenerator() : this(Environment.TickCount)
        {
        }

        /// <summary>
        /// Builds the next pattern in a sequence.
        /// </summary>
        /// <param name="previous">The set immediately before this one, so reachability can be checked.</param>
        /// <param name="reach">The player's movement capability.</param>
        /// <param name="geometry">Band heights for the current play area.</param>
        /// <param name="spacing">Horizontal gap to the previous set, in metres.</param>
        /// <param name="scrollSpeed">Current world speed, in metres per second.</param>
        public ObstaclePattern Next(
            ObstaclePattern previous,
            PlayerReach reach,
            BandGeometry geometry,
            float spacing,
            float scrollSpeed)
        {
            int lowerBound = Clamp(MinObstacleBands, 0, 2);
            int upperBound = Clamp(MaxObstacleBands, lowerBound, 2);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int blocked = lowerBound + _random.Next(upperBound - lowerBound + 1);
                ObstaclePattern candidate = BuildCandidate(blocked);

                if (IsPassable(previous, candidate, reach, geometry, spacing, scrollSpeed))
                {
                    FillClearBands(ref candidate);
                    return candidate;
                }

                RejectedForReachability++;
            }

            // Nothing random worked, so place one obstacle in a band the player is least likely to be
            // stuck in. Reporting this rather than hiding it, because a fallback means the world is
            // asking for something the character cannot deliver.
            FallbacksUsed++;
            ObstaclePattern safe = BuildFallback(previous, reach, geometry, spacing, scrollSpeed);
            FillClearBands(ref safe);
            return safe;
        }

        /// <summary>
        /// True when the player can be somewhere safe in <paramref name="candidate"/>, starting from
        /// somewhere safe in <paramref name="previous"/>, within the time the spacing allows.
        /// <para>
        /// The check is deliberately generous about where the player was: any safe band in the previous
        /// set counts as a possible starting point, because the world cannot know which one they chose.
        /// It only requires that at least one route exists.
        /// </para>
        /// </summary>
        public static bool IsPassable(
            ObstaclePattern previous,
            ObstaclePattern candidate,
            PlayerReach reach,
            BandGeometry geometry,
            float spacing,
            float scrollSpeed)
        {
            if (!candidate.HasSafeBand()) return false;

            for (int from = 0; from < ObstaclePattern.BandCount; from++)
            {
                if (!previous.IsSafe(from)) continue;

                for (int to = 0; to < ObstaclePattern.BandCount; to++)
                {
                    if (!candidate.IsSafe(to)) continue;

                    float fromY = geometry.CentreOf(from);
                    float toY = geometry.CentreOf(to);

                    if (reach.IsReachable(fromY, toY, spacing, scrollSpeed)) return true;
                }
            }

            return false;
        }

        private ObstaclePattern BuildCandidate(int blockedBands)
        {
            var pattern = ObstaclePattern.Empty;
            if (blockedBands <= 0) return pattern;

            int[] order = { 0, 1, 2 };
            Shuffle(order);

            for (int i = 0; i < blockedBands && i < order.Length; i++)
                pattern[order[i]] = BandContent.Obstacle;

            return pattern;
        }

        // One obstacle only, placed in whichever band leaves the cheapest escape from where the player
        // might have been. Cheapest means the shortest travel time, and falling is quicker than climbing,
        // so this naturally prefers leaving an escape below the player.
        private static ObstaclePattern BuildFallback(
            ObstaclePattern previous,
            PlayerReach reach,
            BandGeometry geometry,
            float spacing,
            float scrollSpeed)
        {
            int bestBand = -1;
            float bestCost = float.PositiveInfinity;

            for (int blocked = 0; blocked < ObstaclePattern.BandCount; blocked++)
            {
                var candidate = ObstaclePattern.Empty;
                candidate[blocked] = BandContent.Obstacle;

                float cost = CheapestRoute(previous, candidate, reach, geometry);
                if (cost >= bestCost) continue;

                bestCost = cost;
                bestBand = blocked;
            }

            var result = ObstaclePattern.Empty;

            // If even a single obstacle cannot be escaped in the time available, the honest answer is an
            // empty set. A gap in the obstacle rhythm is a far smaller design problem than a wall.
            if (bestBand >= 0 && bestCost * scrollSpeed <= spacing)
                result[bestBand] = BandContent.Obstacle;

            return result;
        }

        private static float CheapestRoute(
            ObstaclePattern previous,
            ObstaclePattern candidate,
            PlayerReach reach,
            BandGeometry geometry)
        {
            float best = float.PositiveInfinity;

            for (int from = 0; from < ObstaclePattern.BandCount; from++)
            {
                if (!previous.IsSafe(from)) continue;

                for (int to = 0; to < ObstaclePattern.BandCount; to++)
                {
                    if (!candidate.IsSafe(to)) continue;

                    float cost = reach.TravelTime(geometry.CentreOf(from), geometry.CentreOf(to));
                    if (cost < best) best = cost;
                }
            }

            return best;
        }

        private void FillClearBands(ref ObstaclePattern pattern)
        {
            for (int band = 0; band < ObstaclePattern.BandCount; band++)
            {
                if (pattern[band] != BandContent.Empty) continue;
                if (NextFloat() >= CoinChanceOnClearBand) continue;

                pattern[band] = NextFloat() < PowerUpChanceInsteadOfCoin
                    ? BandContent.PowerUp
                    : BandContent.Coin;
            }
        }

        private float NextFloat() => (float)_random.NextDouble();

        private void Shuffle(int[] values)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
