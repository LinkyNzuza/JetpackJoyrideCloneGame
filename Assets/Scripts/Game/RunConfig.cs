// Owns which difficulty condition is being played.
//
// The three profiles have existed in PaceCurve since the world systems were ported, and all three
// tier tables are real. What did not exist was any way to choose between them: ObstacleDirector's
// _profile was a serialized field with no setter, so switching condition meant opening the scene,
// changing a dropdown and saving. That has three problems for a study that compares conditions. It
// is easy to forget, so a whole playtest session can silently run one condition. It cannot change
// between runs, so a tester cannot do an A/B/C sitting. And nothing records which condition produced
// a given run, so the data cannot be grouped afterwards.
//
// One rule governs when a change takes effect: at the START OF THE NEXT RUN, never mid-run. Pressing
// a key while alive queues the change and leaves the current run alone, so no run is ever played
// under two conditions and every record describes exactly one. Because a change requested while dead
// takes effect on the retry that follows, the rule feels immediate when you are actually using it.
//
// Selection persists between sessions, so a tester assigned one condition does not have to remember
// to re-pick it every time they open the game.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.World;

namespace Game.Run
{
    /// <summary>
    /// Chooses the difficulty condition, applies it at run boundaries, and remembers it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunConfig : MonoBehaviour
    {
        private const string PrefsKey = "JJ.DifficultyProfile";

        /// <summary>
        /// Command line switch for a controlled session, for example <c>-difficulty Aggressive</c>.
        /// Overrides the remembered choice, so a supervised playtest can pin a condition without
        /// trusting anyone to press the right key.
        /// </summary>
        private const string CommandLineFlag = "-difficulty";

        [Header("Wiring")]
        [Tooltip("Director the profile is applied to. Found automatically if left empty.")]
        [SerializeField] private ObstacleDirector _director;

        [Header("Selection")]
        [Tooltip("Condition used on a machine that has never chosen one.")]
        [SerializeField] private DifficultyProfile _defaultProfile = DifficultyProfile.Progressive;

        [Tooltip("Allow keys 1, 2 and 3 to pick a condition. Turn off for an unsupervised playtest " +
                 "so a participant cannot change condition by leaning on the keyboard.")]
        [SerializeField] private bool _allowKeyboardSelection = true;

        [Tooltip("Remember the choice between sessions.")]
        [SerializeField] private bool _remember = true;

        [Header("Diagnostics")]
        [SerializeField] private bool _logChanges = true;

        /// <summary>Condition the current run is being played under.</summary>
        public DifficultyProfile Active { get; private set; }

        /// <summary>Condition the next run will use. Equal to <see cref="Active"/> when nothing is queued.</summary>
        public DifficultyProfile Pending { get; private set; }

        /// <summary>True when a change has been requested and is waiting for the next run to begin.</summary>
        public bool HasPendingChange => Pending != Active;

        /// <summary>True when the condition was pinned from the command line and keys are ignored.</summary>
        public bool LockedByCommandLine { get; private set; }

        /// <summary>Raised when the condition actually changes, after it has been applied.</summary>
        public event Action<DifficultyProfile> OnProfileApplied;

        private void Awake()
        {
            if (_director == null) _director = FindFirstObjectByType<ObstacleDirector>();

            if (_director == null)
                Debug.LogError(
                    "[RunConfig] No ObstacleDirector found, so the difficulty condition cannot be " +
                    "applied and every run will use whatever the director was serialized with.", this);

            Active = ResolveStartingProfile();
            Pending = Active;

            // Applied here rather than waiting for the first run to start, so run one is played under
            // the chosen condition rather than under the director's serialized default.
            ApplyToDirector(Active);
        }

        /// <summary>
        /// Resolution order: command line first, because a supervised session must win; then the
        /// remembered choice; then the serialized default.
        /// </summary>
        private DifficultyProfile ResolveStartingProfile()
        {
            if (TryReadCommandLine(out DifficultyProfile fromArgs))
            {
                LockedByCommandLine = true;
                if (_logChanges)
                    Debug.Log($"[RunConfig] Condition pinned to {fromArgs} by {CommandLineFlag}. " +
                              "Keyboard selection is ignored for this session.", this);
                return fromArgs;
            }

            if (_remember && PlayerPrefs.HasKey(PrefsKey))
            {
                string stored = PlayerPrefs.GetString(PrefsKey);
                if (Enum.TryParse(stored, out DifficultyProfile remembered))
                    return remembered;

                Debug.LogWarning(
                    $"[RunConfig] Remembered condition '{stored}' is not a known profile, so the " +
                    $"default of {_defaultProfile} is used instead.", this);
            }

            return _defaultProfile;
        }

        private static bool TryReadCommandLine(out DifficultyProfile profile)
        {
            profile = default;
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], CommandLineFlag, StringComparison.OrdinalIgnoreCase))
                    continue;

                return Enum.TryParse(args[i + 1], true, out profile);
            }

            return false;
        }

        private void Update()
        {
            if (!_allowKeyboardSelection || LockedByCommandLine) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) Request(DifficultyProfile.Constant);
            else if (keyboard.digit2Key.wasPressedThisFrame) Request(DifficultyProfile.Progressive);
            else if (keyboard.digit3Key.wasPressedThisFrame) Request(DifficultyProfile.Aggressive);
        }

        /// <summary>
        /// Queues a condition for the next run. Public so a menu can call it once there is one.
        /// </summary>
        public void Request(DifficultyProfile profile)
        {
            if (Pending == profile) return;

            Pending = profile;

            if (_logChanges)
            {
                string when = profile == Active
                    ? "which cancels the queued change"
                    : "and takes effect when the next run starts";
                Debug.Log($"[RunConfig] {profile} requested, {when}.", this);
            }
        }

        /// <summary>
        /// Promotes any queued condition to the active one. Called by the run owner at the moment a run
        /// begins, which is the only point at which changing the pace curve cannot corrupt a run.
        /// </summary>
        public void ApplyPending()
        {
            if (!HasPendingChange) return;

            Active = Pending;
            ApplyToDirector(Active);

            if (_logChanges) Debug.Log($"[RunConfig] Condition is now {Active}.", this);

            OnProfileApplied?.Invoke(Active);
        }

        private void ApplyToDirector(DifficultyProfile profile)
        {
            if (_director != null) _director.SetProfile(profile);

            if (_remember && !LockedByCommandLine)
            {
                PlayerPrefs.SetString(PrefsKey, profile.ToString());
                PlayerPrefs.Save();
            }
        }
    }
}
