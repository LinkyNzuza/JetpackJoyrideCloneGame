using System.Collections.Generic;
using UnityEngine;

// Spawns coins in square or line arrangements, restricted to y between -2.9 and 2.9.
// Split into three vertical zones (top, middle, low) so patterns don't overlap.
// Spawn X is computed relative to the player's current position so coins
// always appear a fixed distance ahead, regardless of how far the player has moved.
public class CoinSpawner : MonoBehaviour
{
    public static CoinSpawner Instance { get; private set; }

    [Header("Prefab & Spawn Point")]
    [SerializeField] private GameObject coinPrefab;

    [Header("Player Reference")]
    [Tooltip("The player's transform. Coins spawn aheadDistance to the right of this.")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("How far ahead of the player (in world units) coins spawn.")]
    [SerializeField] private float aheadDistance = 12f;

    [Header("Y Range")]
    [SerializeField] private float yMax = 2.9f;
    [SerializeField] private float yMin = -2.9f;

    [Header("Pattern Spacing")]
    [SerializeField] private float lineSpacing = 1.2f;   // gap between coins in a line
    [SerializeField] private float squareSpacing = 1.0f; // gap between coins in a square grid
    [SerializeField] private int squareSize = 3;          // NxN grid for square pattern

    [Header("Spawn Timing")]
    [SerializeField] private float spawnInterval = 4f;

    private float timer;
    private readonly List<Coin> activeCoins = new List<Coin>();

    private enum YZone { Top, Middle, Low }
    private enum Pattern { Line, Square }

    private void Awake()
    {
        // Simple singleton, same pattern as your AudioManager
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Fallback if you forget to wire up the player in the inspector,
        // so this doesn't silently spawn at world origin.
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                Debug.LogWarning("CoinSpawner: No playerTransform assigned and no GameObject tagged 'Player' found. Coins will spawn ahead of world origin.");
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Remove entries for coins destroyed off-screen (uncollected), otherwise
        // the list never empties and the "spawn when empty" check below never fires
        activeCoins.RemoveAll(c => c == null);

        // Spawn on the regular timer, OR immediately if the field is empty
        // (covers the case where the player clears a pattern fast and coins
        // would otherwise stop until the next timer tick)
        if (timer >= spawnInterval || activeCoins.Count == 0)
        {
            timer = 0f;
            SpawnPattern();
        }
    }

    // Current world-space x to start spawning at: player's x plus a fixed lead distance.
    // Falls back to just aheadDistance from world origin if no player is assigned.
    private float GetSpawnX()
    {
        float playerX = playerTransform != null ? playerTransform.position.x : 0f;
        return playerX + aheadDistance;
    }

    // Picks a random zone and pattern, then builds it
    private void SpawnPattern()
    {
        YZone zone = (YZone)Random.Range(0, 3);
        Pattern pattern = (Pattern)Random.Range(0, 2);
        float centerY = GetZoneY(zone);
        float spawnX = GetSpawnX();

        if (pattern == Pattern.Line)
            SpawnLine(spawnX, centerY);
        else
            SpawnSquare(spawnX, centerY);
    }

    // Maps each zone to a y value, clamped inside yMin/yMax
    private float GetZoneY(YZone zone)
    {
        switch (zone)
        {
            case YZone.Top: return yMax;
            case YZone.Low: return yMin;
            default: return 0f; // Middle
        }
    }

    // Horizontal row of coins at a fixed y
    private void SpawnLine(float startX, float centerY)
    {
        int count = 5;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(startX + i * lineSpacing, centerY, 0f);
            SpawnCoin(pos);
        }
    }

    // Square/grid of coins centered on centerY, clamped so it never exceeds yMin/yMax
    private void SpawnSquare(float startX, float centerY)
    {
        float halfExtent = (squareSize - 1) * squareSpacing * 0.5f;
        float clampedCenterY = Mathf.Clamp(centerY, yMin + halfExtent, yMax - halfExtent);

        for (int row = 0; row < squareSize; row++)
        {
            for (int col = 0; col < squareSize; col++)
            {
                float x = startX + col * squareSpacing;
                float y = clampedCenterY - halfExtent + row * squareSpacing;
                SpawnCoin(new Vector3(x, y, 0f));
            }
        }
    }

    private void SpawnCoin(Vector3 position)
    {
        GameObject coinObj = Instantiate(coinPrefab, position, Quaternion.identity);
        Coin coin = coinObj.GetComponent<Coin>();
        activeCoins.Add(coin);

        // If you have a shared mover script for scrolling obstacles, attach/configure it here
        // e.g. coinObj.AddComponent<ScrollMover>();
    }

    // Called by Coin.cs when the player picks one up
    public void CoinCollected(Coin coin)
    {
        activeCoins.Remove(coin);
        // Hook score increment here, e.g. GameManager.Instance.AddScore(1);
    }
}