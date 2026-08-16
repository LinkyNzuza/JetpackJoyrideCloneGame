// Records the player's strongest performance across completed runs.
//
// Evaluates ONLY after a run has ended  — never mid-run — so an unfinished run can
// never be mistaken for a completed performance measurement. Persists via PlayerPrefs so the
// record survives between sessions, which matters for the group's replay-behaviour hypothesis.
using UnityEngine;

namespace Game.Progression
{
    [DisallowMultipleComponent]
    public sealed class HighScoreManager : MonoBehaviour
    {
        private const string KeyHighScore = "JetpackClone_HighScore";
        private const string KeyHighDistance = "JetpackClone_HighDistance";

        /// <summary>Best score recorded across all sessions on this device.</summary>
        public int HighScore { get; private set; }

        /// <summary>Best distance recorded across all sessions on this device.</summary>
        public int HighDistance { get; private set; }

        /// <summary>True if the most recently evaluated run beat the previous best.</summary>
        public bool IsNewRecord { get; private set; }

        private void Awake()
        {
            HighScore = PlayerPrefs.GetInt(KeyHighScore, 0);
            HighDistance = PlayerPrefs.GetInt(KeyHighDistance, 0);
        }

        /// <summary>
        /// Called once by the Game State Manager after ScoreManager.FreezeFinalResult(), with
        /// the frozen values. Compares against the stored best and persists a new record.
        /// </summary>
        public void EvaluateRun(int finalScore, int finalDistance)
        {
            IsNewRecord = finalScore > HighScore;

            if (IsNewRecord)
            {
                HighScore = finalScore;
                HighDistance = Mathf.Max(HighDistance, finalDistance);
                PlayerPrefs.SetInt(KeyHighScore, HighScore);
                PlayerPrefs.SetInt(KeyHighDistance, HighDistance);
                PlayerPrefs.Save();
            }
        }
    }
}

