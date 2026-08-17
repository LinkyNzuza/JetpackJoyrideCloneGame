// Test instrumentation, not the game's HUD.
//
// This exists so a difficulty condition can be verified rather than assumed. Selecting a condition is
// worthless if the tester cannot see which one is active, and "I think I was on Aggressive" is not
// data. It shows the active condition, any queued change, the live run, and the row that was just
// written, so a session can be run and checked without opening a log file.
//
// Deliberately drawn with OnGUI rather than a Canvas, and deliberately in its own file. The real HUD
// belongs to the interface slice and will be a Canvas; keeping this on the immediate-mode API means the
// two cannot fight over layout, and this component can be deleted in one action when the real one
// arrives. Nothing else references it.
//
// Turn it off for an unsupervised playtest: a participant should be told which condition they are in
// by the person running the session, not read it off the screen and start theorising about it.

using UnityEngine;
using Game.World;

namespace Game.Run
{
    /// <summary>
    /// On-screen readout of the difficulty condition and the current run, for controlled testing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunTestOverlay : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RunManager _run;
        [SerializeField] private RunConfig _config;
        [SerializeField] private RunLog _log;
        [SerializeField] private ObstacleDirector _director;

        [Header("Display")]
        [Tooltip("Draw the overlay. Turn off for an unsupervised playtest.")]
        [SerializeField] private bool _visible = true;

        [SerializeField] private int _fontSize = 14;

        private GUIStyle _style;

        private void Awake()
        {
            if (_run == null) _run = FindFirstObjectByType<RunManager>();
            if (_config == null) _config = FindFirstObjectByType<RunConfig>();
            if (_log == null) _log = FindFirstObjectByType<RunLog>();
            if (_director == null) _director = FindFirstObjectByType<ObstacleDirector>();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = _fontSize, richText = false };

            GUI.Box(new Rect(8f, 8f, 340f, 196f), GUIContent.none);

            float y = 14f;
            void Line(string text)
            {
                GUI.Label(new Rect(16f, y, 330f, 20f), text, _style);
                y += 18f;
            }

            Line("── DIFFICULTY TEST ──");

            if (_config == null)
            {
                Line("No RunConfig in the scene.");
                Line("Condition cannot be selected or reported.");
                return;
            }

            string pending = _config.HasPendingChange
                ? $"  ->  {_config.Pending} next run"
                : string.Empty;

            Line($"Condition: {_config.Active}{pending}");
            Line(_config.LockedByCommandLine
                ? "Pinned by -difficulty, keys ignored"
                : "1 Constant   2 Progressive   3 Aggressive");

            y += 4f;

            if (_director != null)
            {
                Line($"Distance {_director.Distance:0} m    tier {_director.TierIndex + 1}");
                Line($"Speed {_director.ScrollSpeed:0.0} m/s    spacing {_director.CurrentSpacing:0.0} m");
            }

            if (_run != null)
            {
                Line($"Coins {_run.Coins}  value {_run.CoinValue}   {_run.RunDuration:0.0} s");
                Line($"State {_run.State}    runs ended {_run.DeathCount}");
            }

            y += 4f;

            if (_log != null && _log.HasRecord)
            {
                RunLog.Record last = _log.Last;
                Line($"Last: {last.Profile} {last.DistanceMetres:0} m " +
                     $"{last.CoinValueTotal} pts {last.DurationSeconds:0.0} s");
                Line($"Rows written {_log.RowsWritten}");
            }
            else
            {
                Line("No run recorded yet.");
            }
        }
    }
}
