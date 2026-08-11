// Feature: player-core-gameplay
// Contract only: no UnityEngine types appear here.

namespace Game.Player
{
    /// <summary>
    /// A one-shot feedback hook for the shield-break and death cues
    /// (Requirements 8.6, 10.5). No audio assets exist yet, so the shipped
    /// implementations are an animator trigger and a no-op; keeping the contract
    /// abstract makes cue plays countable in tests.
    /// </summary>
    public interface ICue
    {
        /// <summary>Plays the cue once. Implementations MUST NOT throw.</summary>
        void Play();
    }
}
