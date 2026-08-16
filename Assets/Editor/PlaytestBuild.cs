// Editor-only. Builds a standalone Windows player of the sandbox scene for playtesting
// away from the editor. Lives in an Editor folder, so it is excluded from player builds.

using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Produces a 64-bit Windows player containing only the sandbox scene.
    /// <para>
    /// The scene list is passed explicitly rather than read from Build Settings, so building
    /// never requires editing <c>EditorBuildSettings.asset</c>. That file is shared with the
    /// other two project members and every change to it is a potential merge conflict.
    /// </para>
    /// </summary>
    public static class PlaytestBuild
    {
        private const string SandboxScene = "Assets/Scenes/PlayerSandbox.unity";
        private const string OutputDirectory = "Build/Windows";
        private const string ExecutableName = "PlayerSandbox.exe";

        [MenuItem("Tools/Playtest Build/Build Windows Player", priority = 10)]
        public static void BuildFromMenu()
        {
            string path = Build(out string message);

            if (path == null)
            {
                EditorUtility.DisplayDialog("Playtest build failed", message, "Close");
                return;
            }

            bool reveal = EditorUtility.DisplayDialog(
                "Playtest build succeeded", message, "Show me the folder", "Close");

            if (reveal) EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/Playtest Build/Open Build Folder", priority = 20)]
        public static void OpenBuildFolder()
        {
            string full = Path.GetFullPath(OutputDirectory);
            if (Directory.Exists(full)) EditorUtility.RevealInFinder(full);
            else EditorUtility.DisplayDialog("No build yet", $"{full} does not exist.", "Close");
        }

        /// <summary>
        /// Entry point for a command-line build:
        /// <c>Unity.exe -quit -batchmode -projectPath &lt;path&gt;
        /// -executeMethod Game.EditorTools.PlaytestBuild.BuildFromCommandLine</c>
        /// <para>Exits with code 0 on success and 1 on failure, so a script can test the result.</para>
        /// </summary>
        public static void BuildFromCommandLine()
        {
            string path = Build(out string message);
            Debug.Log(message);
            EditorApplication.Exit(path == null ? 1 : 0);
        }

        /// <summary>
        /// Runs the build. Returns the full path of the executable, or null on failure, and
        /// always writes a human-readable outcome to <paramref name="message"/>.
        /// </summary>
        private static string Build(out string message)
        {
            if (!File.Exists(SandboxScene))
            {
                message = $"Scene not found: {SandboxScene}";
                return null;
            }

            Directory.CreateDirectory(OutputDirectory);
            string target = Path.Combine(OutputDirectory, ExecutableName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SandboxScene },
                locationPathName = target,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                message = $"Build {summary.result} with {summary.totalErrors} error(s). " +
                          "See the Console for detail.";
                return null;
            }

            double megabytes = summary.totalSize / (1024.0 * 1024.0);
            string full = Path.GetFullPath(target);

            message = $"Built {ExecutableName} in {summary.totalTime.TotalSeconds:0} s, " +
                      $"{megabytes:0.0} MB.\n\n{full}";
            return full;
        }
    }
}
