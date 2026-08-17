// Writes one CSV row per completed run.
//
// Without this, selecting a condition is only worth half of what it should be: you can play all three
// but you cannot compare them, because every run's numbers vanish the moment the player dies. The
// values were already published - ObstacleDirector exposes Distance, TierIndex, ScrollSpeed,
// CurrentSpacing, RejectedForReachability, FallbacksUsed and GuaranteedPowerUps - so this records what
// already exists rather than measuring anything new.
//
// Deliberately raw and not summarised. It logs coins collected and total coin value separately and
// does not compute a score, because how distance and coins combine into a score is a design decision
// nobody has made yet, and inventing a formula here would bake it into the data. Raw columns can be
// turned into any score later; a pre-combined score cannot be taken apart.
//
// Writes to Application.persistentDataPath rather than anywhere inside the project. A file under
// Assets would be imported by Unity, tracked by git and reimported on every write, and a research log
// has no business in version control.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Game.World;

namespace Game.Run
{
    /// <summary>
    /// Appends a row of run data to a CSV file for later analysis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunLog : MonoBehaviour
    {
        /// <summary>Everything recorded about one run.</summary>
        public struct Record
        {
            public int RunIndex;
            public DifficultyProfile Profile;
            public float DistanceMetres;
            public float DurationSeconds;
            public int CoinsCollected;
            public int CoinValueTotal;
            public int TierReached;
            public float ScrollSpeedAtDeath;
            public float SpacingAtDeath;
            public int LayoutsRejected;
            public int FallbacksUsed;
            public int GuaranteedPowerUps;
        }

        [Header("Output")]
        [Tooltip("File name inside the persistent data path.")]
        [SerializeField] private string _fileName = "run_log.csv";

        [Tooltip("Write a row when a run ends. Turn off to play without recording.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("Report the file path once on the first write, so a tester can find the data.")]
        [SerializeField] private bool _announcePath = true;

        private bool _announced;

        /// <summary>Full path of the file being written.</summary>
        public string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

        /// <summary>How many rows this component has written this session.</summary>
        public int RowsWritten { get; private set; }

        /// <summary>The most recent record, for a diagnostic readout.</summary>
        public Record Last { get; private set; }

        /// <summary>True once at least one run has been recorded.</summary>
        public bool HasRecord { get; private set; }

        private const string Header =
            "utc_timestamp,run_index,profile,distance_m,duration_s,coins_collected,coin_value_total," +
            "tier_reached,scroll_speed_at_death,spacing_at_death,layouts_rejected,fallbacks_used," +
            "guaranteed_powerups";

        /// <summary>
        /// Records one run. Never throws: a playtest must not be interrupted because a disk was busy or
        /// a path was not writable, so a failure is reported and the game carries on.
        /// </summary>
        public void Append(Record record)
        {
            Last = record;
            HasRecord = true;

            if (!_enabled) return;

            try
            {
                string path = FilePath;
                bool needsHeader = !File.Exists(path);

                var row = new StringBuilder();
                if (needsHeader) row.AppendLine(Header);

                // InvariantCulture throughout. On a machine with a comma decimal separator the default
                // formatting would write "12,34" into a comma separated file and silently shift every
                // column after it.
                row.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.RunIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.Profile).Append(',')
                   .Append(record.DistanceMetres.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.DurationSeconds.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.CoinsCollected.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.CoinValueTotal.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.TierReached.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.ScrollSpeedAtDeath.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.SpacingAtDeath.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.LayoutsRejected.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.FallbacksUsed.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(record.GuaranteedPowerUps.ToString(CultureInfo.InvariantCulture));

                File.AppendAllText(path, row.ToString() + Environment.NewLine, Encoding.UTF8);
                RowsWritten++;

                if (_announcePath && !_announced)
                {
                    _announced = true;
                    Debug.Log($"[RunLog] Recording runs to {path}", this);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunLog] Could not write the run log: {ex.Message}", this);
            }
        }
    }
}
