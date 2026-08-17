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

        // Tuning: a floor on how long the hint is up, which beats the first-thrust rule. Without it a
        // player who taps immediately - which is most people, since falling prompts a reaction - erases
        // the instruction before they have read it, and then has no idea that releasing is what drops
        // them. Long enough to read two short lines, short enough not to sit over the first obstacles.
        [Tooltip("Seconds the control hint stays visible no matter what, before the first-thrust rule " +
                 "is allowed to hide it.")]
        [SerializeField, Range(0f, 10f)] private float _minimumHintSeconds = 3.5f;

        [Tooltip("Seconds the control hint stays up if the player never thrusts. Zero keeps it forever.")]
        [SerializeField, Range(0f, 30f)] private float _hintTimeout = 0f;

        [SerializeField, Range(10, 48)] private int _primaryFontSize = 22;
        [SerializeField, Range(8, 36)] private int _secondaryFontSize = 15;

        [Header("Panel")]
        // The panel is measured from the text rather than given a fixed width. The label is device
        // aware, so its length changes with what is plugged in - adding a gamepad turns it into "Hold
        // SPACE or LEFT CLICK or A to fly up" - and any hardcoded width is a guess that one device
        // combination will overflow. Measuring cannot be wrong.
        [SerializeField, Range(8f, 60f)] private float _horizontalPadding = 26f;
        [SerializeField, Range(4f, 40f)] private float _verticalPadding = 12f;

        [Tooltip("Panels never narrower than this, so a short line still reads as deliberate.")]
        [SerializeField, Range(120f, 600f)] private float _minimumPanelWidth = 300f;

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
                // Never over the death screen; the restart prompt owns that moment. Checked first so the
                // minimum display time cannot force the hint on top of it.
                if (_run != null && _run.State != RunManager.RunState.Playing) return false;

                float shown = Time.unscaledTime - _shownSince;

                // The floor wins over the first-thrust rule. Everything below it is a reason to hide,
                // and none of them apply until the player has had time to read.
                if (shown < _minimumHintSeconds) return true;

                if (_hideHintAfterFirstThrust && _hasThrust) return false;
                if (_hintTimeout > 0f && shown > _hintTimeout) return false;

                return true;
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
            DrawPanel($"Hold {ThrustLabel()} to fly up", "Release to drop", Screen.height * 0.72f);
        }

        private void DrawRestart()
        {
            DrawPanel(RestartLabel(), "Watch what hit you before you go again", Screen.height * 0.44f);
        }

        /// <summary>
        /// Draws one panel containing both lines, sized to whichever is wider.
        /// <para>
        /// One panel rather than a box behind the first line and a floating second line. The two lines
        /// are a single message, so they should sit in a single shape, and it means the backing covers
        /// both - the parallax layers are pale, and unbacked light text on them is genuinely hard to
        /// read.
        /// </para>
        /// </summary>
        private void DrawPanel(string primaryText, string secondaryText, float topY)
        {
            Vector2 primarySize = _primary.CalcSize(new GUIContent(primaryText));

            bool hasSecondary = !string.IsNullOrEmpty(secondaryText);
            Vector2 secondarySize = hasSecondary
                ? _secondary.CalcSize(new GUIContent(secondaryText))
                : Vector2.zero;

            float lineGap = hasSecondary ? 6f : 0f;
            float contentWidth = Mathf.Max(primarySize.x, secondarySize.x);
            float width = Mathf.Max(_minimumPanelWidth, contentWidth + _horizontalPadding * 2f);
            float height = _verticalPadding * 2f + primarySize.y + lineGap + secondarySize.y;

            float x = (Screen.width - width) * 0.5f;
            GUI.Box(new Rect(x, topY, width, height), GUIContent.none);

            // Labels span the full panel width and the styles are centre aligned, so each line centres
            // itself within the shape regardless of how long it is.
            float lineY = topY + _verticalPadding;
            GUI.Label(new Rect(x, lineY, width, primarySize.y), primaryText, _primary);

            if (hasSecondary)
                GUI.Label(
                    new Rect(x, lineY + primarySize.y + lineGap, width, secondarySize.y),
                    secondaryText, _secondary);
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
