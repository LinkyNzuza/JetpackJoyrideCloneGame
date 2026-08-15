// The 2D port of the 3D runner's track and spawning stack: TrackManager streamed segments ahead
// of the player and recycled them behind, SegmentGameplay picked a layout per set, and
// ObstacleSpawner and PointSpawner turned that layout into enabled objects.
//
// Two lessons came across with it. TrackManager's look-ahead existed because building only 50
// units ahead gave about a second of warning at speed, and an obstacle the player cannot see
// coming is not a challenge. Here the same idea is a spawn edge placed off-screen rather than at
// the camera edge. The second lesson is the one this file is built around: spacing is not a
// constant, it is whatever the player's climb time demands at the current speed.
//
// This reads only the player's published state and never writes to it.

using System.Collections.Generic;
using UnityEngine;
using Game.Player;

namespace Game.World
{
    /// <summary>
    /// Drives distance, scroll speed and obstacle spawning for a run.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObstacleDirector : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Player to read state and movement capability from. Found automatically if left empty.")]
        [SerializeField] private PlayerController _player;

        [Header("Difficulty")]
        [Tooltip("Which of the three configurations to run. Constant is the control condition.")]
        [SerializeField] private DifficultyProfile _profile = DifficultyProfile.Progressive;

        [Header("Spawning")]
        [Tooltip("How far right of the camera edge sets appear, so the player sees them arrive.")]
        [SerializeField, Range(1f, 30f)] private float _spawnAheadOfCamera = 4f;

        [Tooltip("How far left of the camera edge objects are removed.")]
        [SerializeField, Range(1f, 30f)] private float _despawnBehindCamera = 4f;

        [Tooltip("Spacing the chunks were authored for, in metres. The reach bound below can raise it " +
                 "but never lowers it.")]
        [SerializeField, Range(1f, 60f)] private float _authoredSpacing = 6f;

        [Tooltip("Multiplier on the minimum spacing the player's climb time demands. Above 1 leaves the " +
                 "player margin rather than exactly enough time.")]
        [SerializeField, Range(1f, 3f)] private float _spacingSafetyFactor = 1.15f;

        [Header("Prefabs (optional)")]
        [Tooltip("Obstacle prefab. Must carry the Obstacle tag. A flat sprite is generated if empty.")]
        [SerializeField] private GameObject _obstaclePrefab;

        [Tooltip("Coin prefab. Must carry the Coin tag. A flat sprite is generated if empty.")]
        [SerializeField] private GameObject _coinPrefab;

        [Tooltip("Power-up prefab. Must carry a PowerUp tag. A flat sprite is generated if empty.")]
        [SerializeField] private GameObject _powerUpPrefab;

        [Header("Diagnostics")]
        [Tooltip("Report each tier change, and every time the reach bound raises the spacing.")]
        [SerializeField] private bool _logPaceChanges = true;

        private PaceCurve _curve;
        private PatternGenerator _generator;
        private PlayerReach _reach;
        private BandGeometry _geometry;

        private readonly List<Transform> _live = new List<Transform>();
        private ObstaclePattern _previousPattern = ObstaclePattern.Empty;

        private float _distance;
        private float _speed;
        private float _metresSinceLastSet;
        private float _currentSpacing;
        private int _reportedTier = -1;
        private bool _reportedSpacingRaise;
        private Camera _camera;
        private System.Action _handleRunReset;

        private static Sprite _obstacleSprite;
        private static Sprite _coinSprite;
        private static Sprite _powerUpSprite;

        // ── Published state, for the interface system ──────────────────────────────────

        /// <summary>Metres travelled in the current run. This is the group's progression measure.</summary>
        public float Distance => _distance;

        /// <summary>Current scroll speed, in metres per second.</summary>
        public float ScrollSpeed => _speed;

        /// <summary>Zero-based difficulty tier the run is currently in.</summary>
        public int TierIndex => _curve != null ? _curve.TierAt(_distance) : 0;

        /// <summary>Spacing being used between sets, after the reach bound has been applied.</summary>
        public float CurrentSpacing => _currentSpacing;

        /// <summary>Objects currently alive in the world.</summary>
        public int LiveCount => _live.Count;

        /// <summary>How many candidate layouts the reachability check has rejected this run.</summary>
        public int RejectedForReachability => _generator != null ? _generator.RejectedForReachability : 0;

        /// <summary>Which difficulty configuration is running.</summary>
        public DifficultyProfile Profile => _profile;

        private void Awake()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            _camera = Camera.main;

            if (_player == null)
                Debug.LogError("[ObstacleDirector] No PlayerController found. Nothing will spawn.", this);

            _handleRunReset = ResetRun;
            Rebuild();
        }

        private void OnEnable()
        {
            if (_player != null) _player.OnPlayerDeath += _handleRunReset;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnPlayerDeath -= _handleRunReset;
        }

        /// <summary>
        /// Rebuilds the pace curve and the reach model from the player's current values. Called at
        /// startup and safe to call after tuning, since every value is re-read rather than cached.
        /// </summary>
        public void Rebuild()
        {
            _curve = PaceCurve.Default(_profile);
            _generator = new PatternGenerator();

            if (_player != null)
            {
                _reach = new PlayerReach(
                    _player.ThrustForce,
                    _player.GravityScale,
                    _player.BodyMass,
                    _player.MaxRiseSpeed,
                    _player.MaxFallSpeed,
                    _player.PlayBoundsMinY,
                    _player.PlayBoundsMaxY);

                _geometry = new BandGeometry(_player.PlayBoundsMinY, _player.PlayBoundsMaxY);
            }

            _speed = _curve.SpeedAt(0f);
            _currentSpacing = ResolveSpacing();

            if (!_reach.CanClimb)
            {
                Debug.LogError(
                    "[ObstacleDirector] Thrust does not exceed weight, so the player cannot gain height. " +
                    "Every pattern would be impossible. Raise thrust or lower gravity scale.", this);
            }
            else if (_logPaceChanges)
            {
                Debug.Log(
                    $"[ObstacleDirector] {_profile}: thrust-to-weight {_reach.ThrustToWeight:0.00}, " +
                    $"worst crossing {_reach.WorstCaseTraversal():0.00} s over {_reach.BandHeight:0.0} m. " +
                    $"Spacing starts at {_currentSpacing:0.0} m.", this);
            }
        }

        /// <summary>
        /// Clears the world and puts distance and speed back to the start of a run. Wired to the
        /// player's death event, and safe to call directly from the interface system's retry path.
        /// </summary>
        public void ResetRun()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i] != null) Destroy(_live[i].gameObject);

            _live.Clear();
            _previousPattern = ObstaclePattern.Empty;
            _distance = 0f;
            _metresSinceLastSet = 0f;
            _reportedTier = -1;
            _reportedSpacingRaise = false;
            _speed = _curve != null ? _curve.SpeedAt(0f) : 0f;
            _currentSpacing = ResolveSpacing();
        }

        private void Update()
        {
            if (_player == null || _curve == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Distance only accrues while the player is alive, so the game-over screen does not inflate
            // the distance the run is recorded at.
            if (_player.IsAlive)
            {
                _speed = _curve.SpeedAt(_distance);
                _distance += _speed * dt;
                _metresSinceLastSet += _speed * dt;

                ReportTierChange();

                _currentSpacing = ResolveSpacing();
                if (_metresSinceLastSet >= _currentSpacing)
                {
                    _metresSinceLastSet = 0f;
                    SpawnSet();
                }
            }

            ScrollAndRecycle(dt);
        }

        // The spacing the world uses is the larger of what the chunks were authored for and what the
        // player's climb time demands at the current speed. This is the whole point of the port: in the
        // 3D game spacing was a constant because a lane change was instant. Here it has to follow speed.
        private float ResolveSpacing()
        {
            float authored = _authoredSpacing;
            if (!_reach.CanClimb) return authored;

            float required = _reach.WorstCaseTraversal() * _speed * _spacingSafetyFactor;

            if (required > authored)
            {
                if (_logPaceChanges && !_reportedSpacingRaise)
                {
                    _reportedSpacingRaise = true;
                    Debug.Log(
                        $"[ObstacleDirector] Authored spacing of {authored:0.0} m is too tight at " +
                        $"{_speed:0.0} m/s. Using {required:0.0} m so a full crossing stays possible.",
                        this);
                }

                return required;
            }

            return authored;
        }

        private void ReportTierChange()
        {
            if (!_logPaceChanges) return;

            int tier = _curve.TierAt(_distance);
            if (tier == _reportedTier) return;

            _reportedTier = tier;
            Debug.Log(
                $"[ObstacleDirector] Tier {tier + 1} at {_distance:0} m: speed {_speed:0.0} m/s, " +
                $"spacing {_currentSpacing:0.0} m, layouts rejected as unreachable so far " +
                $"{_generator.RejectedForReachability}.", this);
        }

        private void SpawnSet()
        {
            ObstaclePattern pattern = _generator.Next(
                _previousPattern, _reach, _geometry, _currentSpacing, _speed);

            float spawnX = RightEdge() + _spawnAheadOfCamera;

            for (int band = 0; band < ObstaclePattern.BandCount; band++)
            {
                BandContent content = pattern[band];
                if (content == BandContent.Empty) continue;

                var position = new Vector3(spawnX, _geometry.CentreOf(band), 0f);
                Transform spawned = CreateFor(content, position);
                if (spawned != null) _live.Add(spawned);
            }

            _previousPattern = pattern;
        }

        private void ScrollAndRecycle(float dt)
        {
            float step = _speed * dt;
            float cutoff = LeftEdge() - _despawnBehindCamera;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Transform t = _live[i];
                if (t == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                t.position += Vector3.left * step;

                if (t.position.x < cutoff)
                {
                    _live.RemoveAt(i);
                    Destroy(t.gameObject);
                }
            }
        }

        private Transform CreateFor(BandContent content, Vector3 position)
        {
            switch (content)
            {
                case BandContent.Obstacle:
                    return _obstaclePrefab != null
                        ? Instantiate(_obstaclePrefab, position, Quaternion.identity).transform
                        : BuildStandIn("Obstacle", position, new Color(0.85f, 0.2f, 0.2f),
                            new Vector2(0.8f, 1.6f), ref _obstacleSprite, false);

                case BandContent.Coin:
                    return _coinPrefab != null
                        ? Instantiate(_coinPrefab, position, Quaternion.identity).transform
                        : BuildStandIn("Coin", position, new Color(0.95f, 0.8f, 0.15f),
                            new Vector2(0.5f, 0.5f), ref _coinSprite, true);

                case BandContent.PowerUp:
                    return _powerUpPrefab != null
                        ? Instantiate(_powerUpPrefab, position, Quaternion.identity).transform
                        : BuildStandIn("PowerUp_Shield", position, new Color(0.25f, 0.6f, 0.95f),
                            new Vector2(0.6f, 0.6f), ref _powerUpSprite, true);

                default:
                    return null;
            }
        }

        // Stand-in objects so the world runs before any art is imported, following the same approach as
        // the sandbox scene. Colour coded rather than pretty: red blocks, gold coins, blue power-ups.
        private Transform BuildStandIn(
            string tagName,
            Vector3 position,
            Color colour,
            Vector2 size,
            ref Sprite cached,
            bool trigger)
        {
            if (cached == null) cached = BuildSprite(colour);

            var go = new GameObject($"World_{tagName}");
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = cached;
            renderer.color = colour;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = trigger;

            if (IsTagRegistered(tagName)) go.tag = tagName;
            else Debug.LogError($"[ObstacleDirector] Tag '{tagName}' is not registered, so the player " +
                                "will ignore this object. Add it in Tags and Layers.", this);

            return go.transform;
        }

        private static Sprite BuildSprite(Color colour)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
        }

        private static bool IsTagRegistered(string tagName)
        {
            try
            {
                GameObject.FindWithTag(tagName);
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private float RightEdge()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return 10f;

            return _camera.orthographic
                ? _camera.transform.position.x + _camera.orthographicSize * _camera.aspect
                : _camera.transform.position.x + 10f;
        }

        private float LeftEdge()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return -10f;

            return _camera.orthographic
                ? _camera.transform.position.x - _camera.orthographicSize * _camera.aspect
                : _camera.transform.position.x - 10f;
        }
    }
}
