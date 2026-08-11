// Feature: player-core-gameplay
// Pure core type. This file MUST NOT reference UnityEngine.

namespace Game.Player
{
    /// <summary>
    /// Severity of a <see cref="Diagnostic"/>. <see cref="Info"/> exists for the
    /// design's resolution C1 note (a configured thrust force above 200 is legal
    /// but worth surfacing).
    /// </summary>
    public enum DiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// A single machine-checkable report produced by a pure core instead of a
    /// console call, so "exactly one warning naming that field" is assertable in
    /// an edit-mode test. The shell forwards these to <see cref="IPlayerLog"/>.
    /// </summary>
    public readonly struct Diagnostic
    {
        /// <summary>How serious the report is.</summary>
        public readonly DiagnosticSeverity Severity;

        /// <summary>
        /// The name of the offending field, used both as the human-facing name
        /// and as the once-per-key suppression key.
        /// </summary>
        public readonly string Field;

        /// <summary>Human-readable explanation of what was wrong and what was done instead.</summary>
        public readonly string Message;

        public Diagnostic(DiagnosticSeverity severity, string field, string message)
        {
            Severity = severity;
            Field = field;
            Message = message;
        }

        public static Diagnostic Info(string field, string message)
            => new Diagnostic(DiagnosticSeverity.Info, field, message);

        public static Diagnostic Warning(string field, string message)
            => new Diagnostic(DiagnosticSeverity.Warning, field, message);

        public static Diagnostic Error(string field, string message)
            => new Diagnostic(DiagnosticSeverity.Error, field, message);

        public override string ToString() => Severity + " [" + Field + "] " + Message;
    }
}
