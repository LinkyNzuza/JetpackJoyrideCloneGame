// Feature: player-core-gameplay
// Pure core type. This file MUST NOT reference UnityEngine.

using System;

namespace Game.Player
{
    /// <summary>
    /// A minimal, immutable 2D float vector used by every pure core so the core
    /// never references <c>UnityEngine.Vector2</c>. Conversion to and from the
    /// Unity type happens only in the adapter layer (<c>Rigidbody2DBody</c>).
    /// </summary>
    public readonly struct Vector2Core : IEquatable<Vector2Core>
    {
        /// <summary>Horizontal component.</summary>
        public readonly float X;

        /// <summary>Vertical component.</summary>
        public readonly float Y;

        public Vector2Core(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>The origin, <c>(0, 0)</c>.</summary>
        public static Vector2Core Zero => new Vector2Core(0f, 0f);

        /// <summary>Returns this vector with <see cref="X"/> replaced.</summary>
        public Vector2Core WithX(float x) => new Vector2Core(x, Y);

        /// <summary>Returns this vector with <see cref="Y"/> replaced.</summary>
        public Vector2Core WithY(float y) => new Vector2Core(X, y);

        public bool Equals(Vector2Core other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object obj) => obj is Vector2Core other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(Vector2Core left, Vector2Core right) => left.Equals(right);

        public static bool operator !=(Vector2Core left, Vector2Core right) => !left.Equals(right);

        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
