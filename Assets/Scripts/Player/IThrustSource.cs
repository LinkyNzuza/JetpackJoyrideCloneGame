// Feature: player-core-gameplay
// Contract only: no UnityEngine types appear here, so the Input System stays
// behind the adapter and ThrustGate can be tested with a fake source.

namespace Game.Player
{
    /// <summary>
    /// The Thrust_Input device abstraction. Two signals are exposed because one is
    /// not enough: polling "held now" alone would drop a press and release that
    /// both land between two fixed ticks, while the edge signal alone would drop a
    /// sustained hold. Together they let <c>ThrustGate</c> produce thrust on
    /// exactly one tick for a sub-tick pulse (Requirement 2.9) and on every tick of
    /// a hold (Requirement 2.4).
    /// </summary>
    public interface IThrustSource
    {
        /// <summary>True while the thrust control is held at the moment of sampling.</summary>
        bool IsHeld { get; }

        /// <summary>
        /// True when the thrust control was pressed at any point since the previous
        /// tick sample, even if it was released again before this sample.
        /// </summary>
        bool PressedSinceLastTick { get; }

        /// <summary>
        /// Clears <see cref="PressedSinceLastTick"/>. The shell calls this once per
        /// tick, after both signals have been read and handed to the gate.
        /// </summary>
        void ConsumeTick();
    }
}
