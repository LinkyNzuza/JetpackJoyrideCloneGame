// Feature: player-core-gameplay
// Contract only: no UnityEngine types appear here.

using System;

namespace Game.Player
{
    /// <summary>
    /// Diagnostics sink for the player slice. The once-per-key contract is what
    /// keeps per-tick failure paths from spamming the console (Requirements 3.6,
    /// 3.9, 5.10, 6.11, 13.7). Implementations MUST swallow their own failures so
    /// a broken logger can never propagate into gameplay code (Requirement 11.7).
    /// </summary>
    public interface IPlayerLog
    {
        /// <summary>
        /// Logs one warning for <paramref name="key"/> and stays silent for every
        /// later call with the same key in the same session.
        /// </summary>
        /// <param name="key">Suppression key, typically the offending field or component plus name.</param>
        /// <param name="message">Human-readable description of the condition.</param>
        void LogWarningOnce(string key, string message);

        /// <summary>
        /// Logs one error for <paramref name="key"/> and stays silent for every
        /// later call with the same key in the same session.
        /// </summary>
        /// <param name="key">Suppression key, typically the offending field or component plus name.</param>
        /// <param name="message">Human-readable description of the condition.</param>
        void LogErrorOnce(string key, string message);

        /// <summary>
        /// Logs one entry identifying <paramref name="context"/> together with
        /// <paramref name="exception"/>. Not suppressed, because each subscriber
        /// failure is a distinct event that callers need to see
        /// (Requirement 11.6).
        /// </summary>
        /// <param name="context">What was running when the exception surfaced, for example the event name.</param>
        /// <param name="exception">The caught exception. Implementations MUST tolerate <c>null</c>.</param>
        void LogException(string context, Exception exception);
    }
}
