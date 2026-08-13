// Playtest scaffolding. Remove this folder before final submission.
// Stands in for the world/obstacles slice (scrolling + spawning) so the player
// slice can be tuned by feel. Sprites are generated in code, so this depends on
// no art assets and cannot break when real prefabs arrive.

using System.Collections.Generic;
using UnityEngine;
using Game.Player;

namespace Game.Sandbox
{
    /// <summary>
    /// Spawns tagged obstacles, coins and power-ups off the right edge and scrolls them
    /// left, simulating the world scroll that the environment slice will own.
    /// <para>
    /// Everything is built procedurally with flat colour sprites: red bars are obstacles,
    /// gold squares are coins, blue is Shield, purple is Magnet.
    /// </para>
    /// </summary>
    public sealed class SandboxSpawner : MonoBehaviour
    {
        [Header("Scroll (stands in for World_Scroller)")]
        [Tooltip("World units per second the spawned objects travel left.")]
        [SerializeField, Range(0.5f, 20f)] private float _scrollSpeed = 4f;

        [Header("Spawning")]
        [Tooltip("Seconds between spawns.")]
        [SerializeField, Range(0.15f, 4f)] private float _spawnInterval = 1f;

        [SerializeField] private float _spawnX = 11f;
        [SerializeField] private float _despawnX = -12f;

        [Tooltip("Vertical spawn range. Keep inside the player's Play_Bounds to stay reachable.")]
        [SerializeField] private float _minY = -3.5f;
        [SerializeField] private float _maxY = 3.5f;

        [Header("Spawn weights")]
        [SerializeField, Range(0, 10)] private int _obstacleWeight = 5;
        [SerializeField, Range(0, 10)] private int _coinWeight = 4;
        [SerializeField, Range(0, 10)] private int _shieldWeight = 1;
        [SerializeField, Range(0, 10)] private int _magnetWeight = 1;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private PlayerController _player;
        private float _timer;

        private static Sprite _obstacleSprite;
        private static Sprite _coinSprite;
        private static Sprite _shieldSprite;
        private static Sprite _magnetSprite;

        /// <summary>Current scroll speed, so the debug HUD can display it.</summary>
        public float ScrollSpeed => _scrollSpeed;

        /// <summary>Number of live spawned objects, for the debug HUD.</summary>
        public int LiveCount => _spawned.Count;

        private void Awake()
        {
            _player = FindFirstObjectByType<PlayerController>();
            if (_player == null)
                Debug.LogWarning("[SandboxSpawner] No PlayerController in scene; spawning anyway.", this);

            _obstacleSprite = _obstacleSprite ? _obstacleSprite : MakeSprite(new Color(0.85f, 0.15f, 0.15f));
            _coinSprite     = _coinSprite     ? _coinSprite     : MakeSprite(new Color(1f, 0.82f, 0.1f));
            _shieldSprite   = _shieldSprite   ? _shieldSprite   : MakeSprite(new Color(0.2f, 0.55f, 1f));
            _magnetSprite   = _magnetSprite   ? _magnetSprite   : MakeSprite(new Color(0.7f, 0.25f, 0.9f));
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Scroll and cull. Iterated backwards so removal is safe.
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                GameObject go = _spawned[i];
                if (go == null) { _spawned.RemoveAt(i); continue; }

                go.transform.position += Vector3.left * (_scrollSpeed * dt);
                if (go.transform.position.x < _despawnX)
                {
                    _spawned.RemoveAt(i);
                    Destroy(go);
                }
            }

            // Only spawn while the run is live, so a death freezes the field for inspection.
            if (_player != null && !_player.IsAlive) return;

            _timer += dt;
            if (_timer < _spawnInterval) return;
            _timer = 0f;
            SpawnOne();
        }

        private void SpawnOne()
        {
            int total = _obstacleWeight + _coinWeight + _shieldWeight + _magnetWeight;
            if (total <= 0) return;

            int roll = Random.Range(0, total);
            float y = Random.Range(_minY, _maxY);

            if ((roll -= _obstacleWeight) < 0) SpawnObstacle(y);
            else if ((roll -= _coinWeight) < 0) SpawnCoin(y);
            else if ((roll -= _shieldWeight) < 0) SpawnPowerUp(y, "PowerUp_Shield", _shieldSprite);
            else SpawnPowerUp(y, "PowerUp_Magnet", _magnetSprite);
        }

        private void SpawnObstacle(float y)
        {
            // Tall thin bar, echoing a Jetpack Joyride zapper.
            GameObject go = NewTagged("SandboxObstacle", "Obstacle", _obstacleSprite, y,
                                      new Vector3(0.35f, 2.2f, 1f));
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
        }

        private void SpawnCoin(float y)
        {
            GameObject go = NewTagged("SandboxCoin", "Coin", _coinSprite, y,
                                      new Vector3(0.4f, 0.4f, 1f));
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;

            // Vary the declared value so the clamp and fallback paths get exercised.
            go.AddComponent<SandboxCoin>().SetValue(Random.Range(1, 4) * 10);
        }

        private void SpawnPowerUp(float y, string tag, Sprite sprite)
        {
            GameObject go = NewTagged("Sandbox_" + tag, tag, sprite, y,
                                      new Vector3(0.6f, 0.6f, 1f));
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
        }

        private GameObject NewTagged(string name, string tag, Sprite sprite, float y, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(_spawnX, y, 0f);
            go.transform.localScale = scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            // Setting an unregistered tag throws, so fail loudly but keep the object usable.
            try { go.tag = tag; }
            catch (UnityException)
            {
                Debug.LogError($"[SandboxSpawner] Tag '{tag}' is not registered in the Tag Manager.", go);
            }

            _spawned.Add(go);
            return go;
        }

        /// <summary>Destroys every spawned object. Called by the debug HUD on reset.</summary>
        public void ClearAll()
        {
            foreach (GameObject go in _spawned)
                if (go != null) Destroy(go);

            _spawned.Clear();
            _timer = 0f;
        }

        /// <summary>Builds a 1x1 flat colour sprite at the project's 100 pixels-per-unit.</summary>
        private static Sprite MakeSprite(Color c)
        {
            var tex = new Texture2D(16, 16) { filterMode = FilterMode.Bilinear };
            var px = new Color[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
