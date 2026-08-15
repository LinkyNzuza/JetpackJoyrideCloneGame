// Ported from the 3D runner's SegmentPattern. Three lanes across the track became three
// height bands up the screen, and jump-or-slide became which band is blocked. Data only,
// no logic and no Unity dependency, so the generator that produces these can be checked
// without an engine.

using System;

namespace Game.World
{
    /// <summary>Which third of the play area a piece of content sits in.</summary>
    public enum HeightBand
    {
        Low = 0,
        Middle = 1,
        High = 2
    }

    /// <summary>What one band holds. Kept deliberately small: the world spawns four things.</summary>
    public enum BandContent
    {
        Empty = 0,
        Obstacle = 1,
        Coin = 2,
        PowerUp = 3
    }

    /// <summary>
    /// One set of content across the three bands, at one point along the chunk. A chunk holds
    /// several of these, exactly as a 3D segment held several lane sets.
    /// </summary>
    [Serializable]
    public struct ObstaclePattern
    {
        /// <summary>Number of bands. Three, matching the three lanes this design came from.</summary>
        public const int BandCount = 3;

        public BandContent Low;
        public BandContent Middle;
        public BandContent High;

        public ObstaclePattern(BandContent low, BandContent middle, BandContent high)
        {
            Low = low;
            Middle = middle;
            High = high;
        }

        /// <summary>An empty set, used as the state before the first pattern of a run.</summary>
        public static ObstaclePattern Empty =>
            new ObstaclePattern(BandContent.Empty, BandContent.Empty, BandContent.Empty);

        /// <summary>Reads one band by index, so callers can loop instead of repeating themselves.</summary>
        public BandContent this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return Low;
                    case 1: return Middle;
                    default: return High;
                }
            }
            set
            {
                switch (index)
                {
                    case 0: Low = value; break;
                    case 1: Middle = value; break;
                    default: High = value; break;
                }
            }
        }

        /// <summary>True when the band does not hold an obstacle, so the player may occupy it.</summary>
        public bool IsSafe(int bandIndex) => this[bandIndex] != BandContent.Obstacle;

        /// <summary>How many bands are blocked. The generator never lets this reach three.</summary>
        public int BlockedCount()
        {
            int blocked = 0;
            for (int i = 0; i < BandCount; i++)
                if (this[i] == BandContent.Obstacle) blocked++;
            return blocked;
        }

        /// <summary>True when at least one band is passable. A pattern failing this is unplayable.</summary>
        public bool HasSafeBand() => BlockedCount() < BandCount;

        public override string ToString() => $"[{Low} | {Middle} | {High}]";
    }

    /// <summary>
    /// Turns band indices into world heights and back. Kept separate from the pattern so the
    /// pattern stays pure data and the geometry lives in one place.
    /// </summary>
    public readonly struct BandGeometry
    {
        private readonly float _minY;
        private readonly float _bandHeight;

        public BandGeometry(float boundsMinY, float boundsMaxY)
        {
            float height = boundsMaxY - boundsMinY;
            if (float.IsNaN(height) || float.IsInfinity(height) || height <= 0f) height = 0f;

            _minY = boundsMinY;
            _bandHeight = height / ObstaclePattern.BandCount;
        }

        /// <summary>Height of one band, in metres.</summary>
        public float BandHeight => _bandHeight;

        /// <summary>World Y of the middle of a band, which is where the player sits to pass it.</summary>
        public float CentreOf(int bandIndex)
        {
            int clamped = bandIndex < 0 ? 0 : (bandIndex > 2 ? 2 : bandIndex);
            return _minY + _bandHeight * (clamped + 0.5f);
        }

        /// <summary>Vertical distance between the centres of two bands. Signed: positive means upward.</summary>
        public float DistanceBetween(int fromBand, int toBand) =>
            CentreOf(toBand) - CentreOf(fromBand);

        /// <summary>Which band a world height falls in.</summary>
        public int BandAt(float worldY)
        {
            if (_bandHeight <= 0f) return 1;
            int index = (int)((worldY - _minY) / _bandHeight);
            return index < 0 ? 0 : (index > 2 ? 2 : index);
        }
    }
}
