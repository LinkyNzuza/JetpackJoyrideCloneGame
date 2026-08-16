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

using System;
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
        /// <summary>
        /// One grade of coin: its sprite, what it scores, and how often it appears.
        /// <para>
        /// The values are a geometric ladder, 1 / 5 / 25, rather than three numbers close together.
        /// The magnet pulls coins in bunches, so if the grades scored similarly the three sprites
        /// would carry no information and there would be no reason to ever divert for one. At 25 a
        /// gold is worth twenty-five bronze, which makes going out of your way for it a real
        /// decision. Round numbers so a player can total them in their head, and small enough that a
        /// long run stays inside a three or four digit readout. All well within the 1..1000 that
        /// PlayerCollision clamps to.
        /// </para>
        /// </summary>
        /// <summary>Which collider shape an obstacle variant gets.</summary>
        public enum ColliderShape
        {
            /// <summary>Circle when the scaled sprite is roughly square, box otherwise.</summary>
            Auto = 0,
            Circle = 1,
            Box = 2
        }

        /// <summary>
        /// One kind of obstacle: its sprite, how it is sized, how it collides and whether it turns.
        /// <para>
        /// Every variant stays centred on its band rather than anchored to a band edge, and that is a
        /// gameplay requirement rather than a convenience. PatternGenerator guarantees that a blocked
        /// band cannot be passed, and PlayerReach models the player travelling between band centres. A
        /// hazard parked at a band's edge would leave the centre clear, so the player would sail
        /// through a band the generator had already counted as blocked, and the reachability promise
        /// the whole difficulty model rests on would be quietly false.
        /// </para>
        /// <para>
        /// For the same reason each variant is scaled to cover a useful share of the 2.67 m band. A
        /// hazard the player can drift past is not an obstacle.
        /// </para>
        /// </summary>
        [Serializable]
        public sealed class ObstacleVariant
        {
            public string Name;
            public Sprite Sprite;

            [Tooltip("Local scale. Non-uniform is allowed; the collider follows.")]
            public Vector2 Scale = Vector2.one;

            [Tooltip("Relative chance. Weights are normalised, so they need not sum to 1.")]
            [Range(0f, 1f)] public float Weight = 1f;

            public ColliderShape Collider = ColliderShape.Auto;

            [Tooltip("Shrinks the collider against the sprite. Below 1 so a near miss stays a miss " +
                     "rather than dying on transparent corners.")]
            [Range(0.1f, 1.5f)] public float ColliderScale = 0.9f;

            [Tooltip("Degrees per second about Z. Zero adds no spin component at all.")]
            public float SpinDegreesPerSecond;

            public ObstacleVariant(
                string name, Vector2 scale, float weight, ColliderShape collider,
                float colliderScale, float spin)
            {
                Name = name;
                Scale = scale;
                Weight = weight;
                Collider = collider;
                ColliderScale = colliderScale;
                SpinDegreesPerSecond = spin;
            }
        }

        [Serializable]
        public sealed class CoinTier
        {
            public string Name;
            public Sprite Sprite;

            [Range(1, 1000)] public int Value;

            [Tooltip("Relative chance of this tier. Weights are normalised, so they need not sum to 1.")]
            [Range(0f, 1f)] public float Weight;

            public CoinTier(string name, int value, float weight)
            {
                Name = name;
                Value = value;
                Weight = weight;
            }
        }

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
        // Still here, still first in line. When Linky authors real prefabs they take over from the
        // sprite path below without this file changing. Three routes in priority order: prefab, then
        // sprite, then a generated flat rectangle, so the world always spawns something.
        [Tooltip("Obstacle prefab. Must carry the Obstacle tag. Falls back to the sprites below.")]
        [SerializeField] private GameObject _obstaclePrefab;

        [Tooltip("Coin prefab. Must carry the Coin tag. Falls back to the sprites below.")]
        [SerializeField] private GameObject _coinPrefab;

        [Tooltip("Power-up prefab. Must carry a PowerUp tag. Falls back to the sprites below.")]
        [SerializeField] private GameObject _powerUpPrefab;

        [Header("Sprites (used when no prefab is set)")]
        [Tooltip("Obstacle kinds. One is chosen per spawn by weight. Empty falls back to a flat red " +
                 "rectangle.")]
        [SerializeField] private ObstacleVariant[] _obstacleVariants;

        [Tooltip("Shield power-up sprite. Empty falls back to a flat blue square.")]
        [SerializeField] private Sprite _shieldSprite;

        [Tooltip("Magnet power-up sprite. Empty falls back to a flat purple square.")]
        [SerializeField] private Sprite _magnetSprite;

        // Tinted rather than left white, because the source badge carries no magnet pictogram. See the
        // note on MagnetTint below.
        [Tooltip("Tint applied to the magnet sprite so it cannot be mistaken for the shield.")]
        [SerializeField] private Color _magnetTint = new Color(0.65f, 0.42f, 0.95f, 1f);

        [Header("Coins")]
        // Three tiers with a geometric value ladder. See CoinTier for why these numbers.
        [SerializeField]
        private CoinTier[] _coinTiers =
        {
            new CoinTier("Bronze", 1, 0.70f),
            new CoinTier("Silver", 5, 0.25f),
            new CoinTier("Gold", 25, 0.05f)
        };

        [Header("Pickup rates")]
        // These two live on PatternGenerator as plain properties and were never set from anywhere, so
        // they sat at their defaults and could not be tuned without editing code. That is why nobody
        // could find a power-up: the compound chance was four per cent of a clear band, roughly one
        // every thirty-three seconds, in a game where a run lasted ten to twenty.
        [Tooltip("Chance a band with no obstacle receives a pickup at all.")]
        [SerializeField, Range(0f, 1f)] private float _coinChanceOnClearBand = 0.5f;

        [Tooltip("Chance a pickup is a power-up rather than a coin.")]
        [SerializeField, Range(0f, 1f)] private float _powerUpChance = 0.18f;

        // A probability alone still lets an unlucky run go a very long way with nothing, and a player
        // reads that as the feature being absent. This is the backstop: after this many metres without
        // one, the next set is made to carry a power-up. Zero disables it.
        [Tooltip("Metres without a power-up before one is guaranteed. Zero disables the guarantee.")]
        [SerializeField, Range(0f, 500f)] private float _metresBetweenPowerUps = 100f;

        [Header("Power-up mix")]
        [Tooltip("Chance a power-up spawn is a magnet rather than a shield. Before this existed the " +
                 "world only ever tagged PowerUp_Shield, so the magnet never appeared at all.")]
        [SerializeField, Range(0f, 1f)] private float _magnetShare = 0.5f;

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
        private float _metresSincePowerUp;
        private float _currentSpacing;
        private int _reportedTier = -1;
        private bool _reportedSpacingRaise;
        private bool _frozen;
        private Camera _camera;

        // System.Random rather than UnityEngine.Random, matching PatternGenerator, so the visual
        // choices are part of the same reproducible stream as the layout choices.
        private readonly System.Random _visualRandom = new System.Random();

        private static Sprite _obstacleSprite;
        private static Sprite _coinSprite;
        private static Sprite _powerUpSprite;

        // ── Published state, for the interface system ──────────────────────────────────

        /// <summary>Metres travelled in the current run. This is the group's progression measure.</summary>
        public float Distance => _distance;

        /// <summary>
        /// Speed the pace curve prescribes at the current distance, in metres per second. This keeps
        /// its meaning while the world is frozen, because it answers a design question rather than a
        /// rendering one, and the difficulty data will want it.
        /// </summary>
        public float ScrollSpeed => _speed;

        /// <summary>
        /// How fast the world is actually moving this frame: <see cref="ScrollSpeed"/> normally, zero
        /// while frozen.
        /// <para>
        /// Anything that scrolls to match the obstacles must read this and not
        /// <see cref="ScrollSpeed"/>. A parallax background reading the raw curve speed would keep
        /// sliding after death while the obstacles stood still, which reads as more broken than the
        /// obstacles sliding past a corpse did.
        /// </para>
        /// </summary>
        public float EffectiveScrollSpeed => IsFrozen ? 0f : _speed;

        /// <summary>
        /// True while the world is holding still. Frozen means no spawning, no scrolling, no
        /// recycling and no distance accruing: everything stays exactly where it was.
        /// <para>
        /// Two things can freeze the world. The run owner calls <see cref="Freeze"/>, and that is the
        /// intended path. A dead player also freezes it, so the director stays correct on its own in
        /// a scene that has no run owner, which is how it existed before one was written.
        /// </para>
        /// </summary>
        public bool IsFrozen => _frozen || (_player != null && !_player.IsAlive);

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

            Rebuild();
        }

        // This used to subscribe ResetRun to OnPlayerDeath, which was wrong. It wiped the world at the
        // instant of death, so the player's corpse sat on a freshly respawned obstacle field and they
        // never got to see what killed them. Worse, distance went to zero before anything could read
        // the run's final value.
        //
        // Reset now belongs to whoever owns the run, on retry only. Death freezes instead.

        /// <summary>
        /// Stops the world without clearing it. Called by the run owner on death, so the player can
        /// see the obstacle that killed them still sitting where it was.
        /// </summary>
        public void Freeze() => _frozen = true;

        /// <summary>
        /// Lets the world move again. <see cref="ResetRun"/> does this already; this exists for a
        /// pause that is not a death.
        /// </summary>
        public void Unfreeze() => _frozen = false;

        /// <summary>
        /// Rebuilds the pace curve and the reach model from the player's current values. Called at
        /// startup and safe to call after tuning, since every value is re-read rather than cached.
        /// </summary>
        public void Rebuild()
        {
            _curve = PaceCurve.Default(_profile);
            _generator = new PatternGenerator
            {
                CoinChanceOnClearBand = _coinChanceOnClearBand,
                PowerUpChanceInsteadOfCoin = _powerUpChance
            };

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
        /// Clears the world, puts distance and speed back to the start of a run, and unfreezes.
        /// Called by the run owner on retry. Nothing calls this on death any more.
        /// </summary>
        public void ResetRun()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i] != null) Destroy(_live[i].gameObject);

            _live.Clear();
            _previousPattern = ObstaclePattern.Empty;
            _distance = 0f;
            _metresSinceLastSet = 0f;
            _metresSincePowerUp = 0f;
            _reportedTier = -1;
            _reportedSpacingRaise = false;
            _frozen = false;
            _speed = _curve != null ? _curve.SpeedAt(0f) : 0f;
            _currentSpacing = ResolveSpacing();
        }

        private void Update()
        {
            if (_player == null || _curve == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // One gate for the whole tick. Everything below is a switch that a frozen world needs off,
            // and they are all here rather than scattered as separate IsAlive checks, so "what does
            // freezing actually stop" has a single answer someone can read.
            //
            // Advancing distance and speed: off, so a game-over screen cannot inflate the distance the
            // run gets recorded at. Spawning: off, so nothing new arrives behind a dead player.
            // Scrolling and recycling: off, and note those two are one switch rather than two, because
            // ScrollAndRecycle both moves the transforms and destroys whatever passes the despawn edge.
            // Nothing reaches that edge if nothing moves.
            if (IsFrozen) return;

            _speed = _curve.SpeedAt(_distance);
            _distance += _speed * dt;
            _metresSinceLastSet += _speed * dt;
            _metresSincePowerUp += _speed * dt;

            ReportTierChange();

            _currentSpacing = ResolveSpacing();
            if (_metresSinceLastSet >= _currentSpacing)
            {
                _metresSinceLastSet = 0f;
                SpawnSet();
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
            bool force = _metresBetweenPowerUps > 0f && _metresSincePowerUp >= _metresBetweenPowerUps;

            ObstaclePattern pattern = _generator.Next(
                _previousPattern, _reach, _geometry, _currentSpacing, _speed, force);

            float spawnX = RightEdge() + _spawnAheadOfCamera;
            bool spawnedPowerUp = false;

            for (int band = 0; band < ObstaclePattern.BandCount; band++)
            {
                BandContent content = pattern[band];
                if (content == BandContent.Empty) continue;

                if (content == BandContent.PowerUp) spawnedPowerUp = true;

                var position = new Vector3(spawnX, _geometry.CentreOf(band), 0f);
                Transform spawned = CreateFor(content, position);
                if (spawned != null) _live.Add(spawned);
            }

            // Reset on what actually spawned rather than on what was requested, so a pattern that could
            // not fit a power-up does not silently consume the guarantee.
            if (spawnedPowerUp) _metresSincePowerUp = 0f;

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
                    if (_obstaclePrefab != null)
                        return Instantiate(_obstaclePrefab, position, Quaternion.identity).transform;
                    return BuildObstacle(position);

                case BandContent.Coin:
                    if (_coinPrefab != null)
                        return Instantiate(_coinPrefab, position, Quaternion.identity).transform;
                    return BuildCoin(position);

                case BandContent.PowerUp:
                    if (_powerUpPrefab != null)
                        return Instantiate(_powerUpPrefab, position, Quaternion.identity).transform;
                    return BuildPowerUp(position);

                default:
                    return null;
            }
        }

        // A saw is rotationally symmetric, which is why it works floating in mid-air where a spike
        // would look wrong for having no surface to grow from. Several sprites are supported and one is
        // picked per spawn, so a long run is not visually identical.
        private Transform BuildObstacle(Vector3 position)
        {
            ObstacleVariant variant = PickObstacleVariant();
            Sprite sprite = variant != null ? variant.Sprite : null;

            // Solid, not a trigger, as specified. Worth knowing: a shielded player survives the hit
            // but the obstacle is not released, because PlayerCollision only releases coins and
            // power-ups. The player therefore ends up inside a solid collider that this director is
            // translating while PlayerController re-locks X and clamps Y every FixedUpdate, so expect
            // jitter on a shielded hit. Making obstacles triggers, or releasing an absorbed obstacle,
            // would both fix it; both are decisions for whoever owns collision.
            Transform built = Build("Obstacle", position, sprite, Color.white,
                new Color(0.85f, 0.2f, 0.2f), new Vector2(0.8f, 1.6f),
                ref _obstacleSprite, trigger: false,
                colliderScale: variant != null ? variant.ColliderScale : 0.9f,
                value: 0,
                spriteScale: variant != null ? variant.Scale : Vector2.one,
                shape: variant != null ? variant.Collider : ColliderShape.Auto);

            if (built != null && variant != null && variant.SpinDegreesPerSecond != 0f)
            {
                WorldSpin spin = built.gameObject.AddComponent<WorldSpin>();
                spin.SetRate(variant.SpinDegreesPerSecond);
                spin.SetDirector(this);
            }

            return built;
        }

        private Transform BuildCoin(Vector3 position)
        {
            CoinTier tier = PickCoinTier();
            Sprite sprite = tier != null ? tier.Sprite : null;
            int value = tier != null ? tier.Value : 1;

            return Build("Coin", position, sprite, Color.white,
                new Color(0.95f, 0.8f, 0.15f), new Vector2(0.5f, 0.5f),
                ref _coinSprite, trigger: true, colliderScale: 1f, value: value);
        }

        private Transform BuildPowerUp(Vector3 position)
        {
            bool magnet = NextFloat() < _magnetShare;

            // The magnet sprite is a stand-in and is knowingly wrong. There is no magnet pictogram
            // anywhere in the art set, and every alternative asserts something false: wings mean
            // flight, a bunny means jump, the jetpack badge means the pack he is already wearing, a gem
            // or star means collectible. powerup_empty is the same blue badge as the shield, so the two
            // read as one category of pickup, and being blank it says "art pending" rather than lying.
            // The tint is what makes them tellable apart in play, and purple is the colour the sandbox
            // HUD already used for magnet. Replace the sprite, not the tint, when real art arrives.
            string tag = magnet ? "PowerUp_Magnet" : "PowerUp_Shield";
            Sprite sprite = magnet ? _magnetSprite : _shieldSprite;
            Color tint = magnet ? _magnetTint : Color.white;
            Color fallback = magnet
                ? new Color(0.65f, 0.42f, 0.95f)
                : new Color(0.25f, 0.6f, 0.95f);

            return Build(tag, position, sprite, tint, fallback, new Vector2(0.6f, 0.6f),
                ref _powerUpSprite, trigger: true, colliderScale: 1f, value: 0);
        }

        private ObstacleVariant PickObstacleVariant()
        {
            if (_obstacleVariants == null || _obstacleVariants.Length == 0) return null;

            float total = 0f;
            for (int i = 0; i < _obstacleVariants.Length; i++)
                if (_obstacleVariants[i] != null) total += Mathf.Max(0f, _obstacleVariants[i].Weight);

            if (total <= 0f) return _obstacleVariants[0];

            // Weighted from the same System.Random stream as the layout, so a seeded run reproduces its
            // appearance as well as its shape.
            float roll = NextFloat() * total;
            for (int i = 0; i < _obstacleVariants.Length; i++)
            {
                ObstacleVariant variant = _obstacleVariants[i];
                if (variant == null) continue;

                roll -= Mathf.Max(0f, variant.Weight);
                if (roll <= 0f) return variant;
            }

            return _obstacleVariants[_obstacleVariants.Length - 1];
        }

        private CoinTier PickCoinTier()
        {
            if (_coinTiers == null || _coinTiers.Length == 0) return null;

            float total = 0f;
            for (int i = 0; i < _coinTiers.Length; i++)
                if (_coinTiers[i] != null) total += Mathf.Max(0f, _coinTiers[i].Weight);

            if (total <= 0f) return _coinTiers[0];

            float roll = NextFloat() * total;
            for (int i = 0; i < _coinTiers.Length; i++)
            {
                CoinTier tier = _coinTiers[i];
                if (tier == null) continue;

                roll -= Mathf.Max(0f, tier.Weight);
                if (roll <= 0f) return tier;
            }

            return _coinTiers[_coinTiers.Length - 1];
        }

        private float NextFloat() => (float)_visualRandom.NextDouble();

        /// <summary>
        /// Builds one world object. Uses <paramref name="sprite"/> when there is one and falls back to a
        /// generated flat rectangle when there is not, so the world still runs with no art assigned.
        /// <para>
        /// The collider is sized from the sprite rather than from the old hard-coded rectangle, because
        /// real sprites have their own proportions and the previous numbers described a shape that no
        /// longer exists. A circle is used when the sprite is roughly square: a saw and a coin are both
        /// round, and a box around a round sprite kills on a corner the player can see is empty.
        /// </para>
        /// </summary>
        private Transform Build(
            string tagName,
            Vector3 position,
            Sprite sprite,
            Color tint,
            Color fallbackColour,
            Vector2 fallbackSize,
            ref Sprite cachedFallback,
            bool trigger,
            float colliderScale,
            int value,
            Vector2 spriteScale = default,
            ColliderShape shape = ColliderShape.Auto)
        {
            bool usingFallback = sprite == null;
            if (spriteScale == default) spriteScale = Vector2.one;

            if (usingFallback)
            {
                if (cachedFallback == null) cachedFallback = BuildSprite(fallbackColour);
                sprite = cachedFallback;
            }

            var go = new GameObject($"World_{tagName}");
            go.transform.position = position;

            // The generated rectangle is shaped entirely by scale. A real sprite is used at its authored
            // size unless a variant asks otherwise, and coins and power-ups never do, so those are never
            // resampled.
            go.transform.localScale = usingFallback
                ? new Vector3(fallbackSize.x, fallbackSize.y, 1f)
                : new Vector3(spriteScale.x, spriteScale.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = usingFallback ? fallbackColour : tint;

            AddCollider(go, sprite, usingFallback, trigger, colliderScale, spriteScale, shape);

            if (value > 0) go.AddComponent<WorldCoin>().SetValue(value);

            if (IsTagRegistered(tagName)) go.tag = tagName;
            else Debug.LogError($"[ObstacleDirector] Tag '{tagName}' is not registered, so the player " +
                                "will ignore this object. Add it in Tags and Layers.", this);

            return go.transform;
        }

        private static void AddCollider(
            GameObject go, Sprite sprite, bool usingFallback, bool trigger, float colliderScale,
            Vector2 spriteScale, ColliderShape shape)
        {
            // The generated rectangle is shaped by localScale, so a unit box is already the right size.
            if (usingFallback)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = trigger;
                return;
            }

            // Collider sizes are in local space, so the transform's scale applies on top. That means the
            // unscaled sprite size is the right input, and a non-uniform scale still produces a collider
            // that matches what is drawn.
            Vector2 size = sprite.bounds.size;

            // A CircleCollider2D under a non-uniform scale cannot follow the sprite, since a circle has
            // one radius and the two axes disagree. Rather than silently producing a hitbox that does not
            // match the art, that case becomes a box.
            bool uniformScale = Mathf.Abs(spriteScale.x - spriteScale.y) < 0.001f;
            Vector2 scaled = new Vector2(size.x * spriteScale.x, size.y * spriteScale.y);
            bool roughlySquare = Mathf.Abs(scaled.x - scaled.y) <= Mathf.Max(scaled.x, scaled.y) * 0.2f;

            bool useCircle = shape == ColliderShape.Circle
                             || (shape == ColliderShape.Auto && roughlySquare && uniformScale);

            if (useCircle && uniformScale)
            {
                var circle = go.AddComponent<CircleCollider2D>();
                circle.radius = Mathf.Min(size.x, size.y) * 0.5f * colliderScale;
                circle.isTrigger = trigger;
                return;
            }

            var fitted = go.AddComponent<BoxCollider2D>();
            fitted.size = size * colliderScale;
            fitted.isTrigger = trigger;
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
