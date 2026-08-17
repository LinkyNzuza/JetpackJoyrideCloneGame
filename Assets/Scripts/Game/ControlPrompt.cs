// On-screen instructions: how to fly, and how to restart once you have not.
//
// Needed because the game teaches nothing. A player is dropped into a scene already falling, with one
// verb that is not obvious - the jetpack fires while a button is HELD, and altitude is lost the moment
// it is released - and after dying they are looking at a frozen field with no indication that the run
// can be started again. Both were things a playtester had to be told out loud, which is a poor use of
// the person running the session and makes remote testing impossible.
//
// Two prompts, each shown only when it is useful:
//
//   The control hint is shown until the player thrusts for the first time, then never again. Once
//   somebody has flown they have understood, and leaving the text up would be clutter over the part
//   of the screen they need to watch.
//
//   The restart prompt appears only once a retry will actually be accepted. RunManager holds a short
//   lockout after death so the player has a moment to see what killed them, and advertising a key
//   before it works trains people to press it twice.
//
// Drawn with OnGUI and kept in its own file, like RunTestOverlay, so it cannot fight the interface
// slice's Canvas over layout and can be removed in one action when a designed version replaces it.
// The wording is device-aware rather than hardcoded, so a build tested with a gamepad or on a phone
// does not tell the player to press a key they do not have.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Player;

namespace Game.Run
{
    /// <summary>
    /// Shows how to fly until the player has flown, and how to restart once they have died.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControlPrompt : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RunManager _run;
        [SerializeField] private PlayerController _player;

        [Header("Display")]
        [Tooltip("Draw the prompts at all.")]
        [SerializeField] private bool _visible = true;

        [Tooltip("Hide the control hint once the player has thrusted for the first time.")]
        [SerializeField] private bool _hideHintAfterFirstThrust = true;

        [Tooltip("Seconds the control hint stays up if the player never thrusts. Zero keeps it forever.")]
        [SerializeField, Range(0f, 30f)] private float _hintTimeout = 0f;

        [SerializeField, Range(10, 48)] private int _primaryFontSize = 22;
        [SerializeField, Range(8, 36)] private int _secondaryFontSize = 15;

        private GUIStyle _primary;
        private GUIStyle _secondary;
        private bool _hasThrust;
        private float _shownSince;

        private void Awake()
        {
            if (_run == null) _run = FindFirstObjectByType<RunManager>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            _shownSince = Time.unscaledTime;
        }

        private void Update()
        {
            // Latched rather than read live, because IsThrusting is only true while the button is down
            // and the hint has to stay gone after a tap, not flicker back.
            if (_player != null && _player.IsThrusting) _hasThrust = true;
        }

        private bool ShowHint
        {
            get
            {
                if (_hideHintAfterFirstThrust && _hasThrust) return false;
                if (_hintTimeout > 0f && Time.unscaledTime - _shownSince > _hintTimeout) return false;

                // Never over the death screen; the restart prompt owns that moment.
                return _run == null || _run.State == RunManager.RunState.Playing;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            if (_run != null && _run.State == RunManager.RunState.Dead)
            {
                if (_run.CanRetry) DrawRestart();
                return;
            }

            if (ShowHint) DrawHint();
        }

        private void DrawHint()
        {
            // Low on the screen, so it sits under the play area rather than over the obstacles the
            // player is being asked to read.
            float y = Screen.height * 0.74f;
            DrawCentred($"Hold {ThrustLabel()} to fly up", _primary, y, 340f, 44f);
            DrawCentred("Release to drop", _secondary, y + 30f, 340f, 0f);
        }

        private void DrawRestart()
        {
            float y = Screen.height * 0.46f;
            DrawCentred(RestartLabel(), _primary, y, 300f, 44f);
            DrawCentred("Watch what hit you before you go again", _secondary, y + 30f, 380f, 0f);
        }

        private static void DrawCentred(string text, GUIStyle style, float y, float width, float boxHeight)
        {
            float x = (Screen.width - width) * 0.5f;

            // A backing panel only behind the primary line. The parallax layers are pale, so unbacked
            // light text on them is genuinely hard to read.
            if (boxHeight > 0f)
                GUI.Box(new Rect(x, y - 8f, width, boxHeight), GUIContent.none);

            GUI.Label(new Rect(x, y, width, 28f), text, style);
        }

        /// <summary>
        /// Names only the devices actually present, so the prompt cannot instruct a player to press
        /// something they do not have. The bindings mirror those in PlayerController.
        /// </summary>
        private static string ThrustLabel()
        {
            // A touch device with no keyboard gets phrasing rather than a key name, because "SPACE" is
            // meaningless on a phone.
            if (Touchscreen.current != null && Keyboard.current == null)
                return "anywhere on screen";

            var parts = new List<string>(3);
            if (Keyboard.current != null) parts.Add("SPACE");
            if (Mouse.current != null) parts.Add("LEFT CLICK");
            if (Gamepad.current != null) parts.Add("A");

            // Falls back to the keyboard binding rather than an empty sentence if no device is detected.
            if (parts.Count == 0) return "SPACE";

            return string.Join(" or ", parts);
        }

        private static string RestartLabel()
        {
            if (Keyboard.current != null) return "Press R to restart";
            if (Touchscreen.current != null) return "Tap to restart";
            return "Click to restart";
        }

        private void EnsureStyles()
        {
            if (_primary == null)
                _primary = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _primaryFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    richText = false
                };

            if (_secondary == null)
                _secondary = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _secondaryFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    richText = false
                };
        }
    }
}
