// TEMPORARY PLAYTEST SCAFFOLDING — delete this folder before submission.
// Debug readout only. This is NOT the game HUD; that belongs to the UI slice.
// It subscribes to the player's four public events, which also proves the
// event surface works for the GameManager that will consume it.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Player;

namespace Game.Sandbox
{
    /// <summary>
    /// On-screen state readout plus playtest hotkeys. Reads only the player's public
    /// surface and public operations, exactly as another slice would.
    /// </summary>
    public sealed class SandboxDebugHud : MonoBehaviour
    {
        private PlayerController _player;
        private PlayerDeath _death;
        private SandboxSpawner _spawner;

        private int _coins;
        private int _score;
        private int _deaths;
        private int _powerUpsActivated;
        private int _powerUpsExpired;
        private string _lastEvent = "-";

        // Stable delegates so += and -= act on the same instances.
        private Action _onDeath;
        private Action<int> _onCoin;
        private Action<PowerUpType> _onActivated;
        private Action<PowerUpType> _onExpired;

        private GUIStyle _style;

        private void Awake()
        {
            _player = FindFirstObjectByType<PlayerController>();
            _death = _player != null ? _player.GetComponent<PlayerDeath>() : null;
            _spawner = FindFirstObjectByType<SandboxSpawner>();

            _onDeath = () => { _deaths++; _lastEvent = "OnPlayerDeath"; };
            _onCoin = v => { _coins++; _score += v; _lastEvent = $"OnCoinCollected({v})"; };
            _onActivated = t => { _powerUpsActivated++; _lastEvent = $"OnPowerUpActivated({t})"; };
            _onExpired = t => { _powerUpsExpired++; _lastEvent = $"OnPowerUpExpired({t})"; };
        }

        private void OnEnable()
        {
            if (_player == null) return;
            _player.OnPlayerDeath += _onDeath;
            _player.OnCoinCollected += _onCoin;
            _player.OnPowerUpActivated += _onActivated;
            _player.OnPowerUpExpired += _onExpired;
        }

        private void OnDisable()
        {
            if (_player == null) return;
            _player.OnPlayerDeath -= _onDeath;
            _player.OnCoinCollected -= _onCoin;
            _player.OnPowerUpActivated -= _onActivated;
            _player.OnPowerUpExpired -= _onExpired;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || _player == null) return;

            if (kb.rKey.wasPressedThisFrame)
            {
                _spawner?.ClearAll();
                _player.ResetRun();
                _lastEvent = "ResetRun()";
            }

            if (kb.digit1Key.wasPressedThisFrame) _player.ActivatePowerUp(PowerUpType.Shield);
            if (kb.digit2Key.wasPressedThisFrame) _player.ActivatePowerUp(PowerUpType.Magnet);
            if (kb.kKey.wasPressedThisFrame) _death?.RequestDeath();
        }

        private void OnGUI()
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = false };

            if (_player == null)
            {
                GUI.Label(new Rect(12, 12, 600, 24), "SANDBOX: no PlayerController found", _style);
                return;
            }

            GUI.Box(new Rect(8, 8, 330, 232), GUIContent.none);

            float y = 14f;
            void Line(string s) { GUI.Label(new Rect(16, y, 320, 20), s, _style); y += 19f; }

            Line("── PLAYER SANDBOX ──");
            Line($"Alive: {_player.IsAlive}    Thrusting: {_player.IsThrusting}");
            Line($"Shielded: {_player.IsShielded}    Magnet: {_player.IsMagnetActive}");
            Line($"Coins: {_coins}    Score: {_score}");
            Line($"Deaths: {_deaths}");
            Line($"PowerUps  activated: {_powerUpsActivated}  expired: {_powerUpsExpired}");
            Line($"Last event: {_lastEvent}");
            if (_spawner != null)
                Line($"Scroll: {_spawner.ScrollSpeed:0.0}   live objects: {_spawner.LiveCount}");
            y += 6f;
            Line("SPACE thrust   R reset   K kill");
            Line("1 shield   2 magnet");
            Line("Red=obstacle Gold=coin Blue=shield");
            Line("Purple=magnet");
        }
    }
}
