// Feature: player-core-gameplay
// Contract only: no UnityEngine types appear here. The single implementation
// that touches Rigidbody2D.linearVelocity is Rigidbody2DBody (Requirement 14.2).

namespace Game.Player
{
    /// <summary>
    /// The physics abstraction that makes the player logic portable across Unity
    /// versions. Every position, velocity, gravity, and force access in the slice
    /// goes through this interface, so the Physics_Velocity_API member name has
    /// exactly one call site.
    /// </summary>
    public interface IPlayerBody
    {
        /// <summary>World-space position of the Player.</summary>
        Vector2Core Position { get; set; }

        /// <summary>
        /// Linear velocity of the Player. Maps to <c>Rigidbody2D.linearVelocity</c>
        /// for the locked editor version 6000.0.53f1.
        /// </summary>
        Vector2Core Velocity { get; set; }

        /// <summary>Whether the body participates in physics simulation.</summary>
        bool Simulated { get; set; }

        /// <summary>Gravity multiplier applied to the body.</summary>
        float GravityScale { get; set; }

        /// <summary>
        /// Applies <paramref name="magnitude"/> straight up as a continuous force
        /// for the current tick.
        /// </summary>
        /// <param name="magnitude">Force magnitude in the body's force units.</param>
        void AddUpwardForce(float magnitude);
    }
}
