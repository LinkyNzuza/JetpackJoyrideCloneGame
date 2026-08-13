using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserSpawner : MonoBehaviour
{
    [Header("Laser Setup")]
    [SerializeField] private GameObject laserPrefab;
    [SerializeField] private Transform playerTransform; // spawn point tracks this so waves keep appearing ahead as the player advances
    [SerializeField] private float spawnAheadDistance = 12f; // how far along x, ahead of the player, lasers spawn

    [Header("Y-Axis Range")]
    [SerializeField] private float minY = -2.7f;
    [SerializeField] private float maxY = 2.7f;
    [SerializeField] private int totalLanes = 8; // how many horizontal slots exist across the range

    [Header("Difficulty Scaling")]
    [SerializeField] private float timeBetweenWaves = 2.5f;
    [SerializeField] private float minTimeBetweenWaves = 0.6f;
    [SerializeField] private float waveIntervalDecay = 0.92f;     // interval shrinks by this factor each wave
    [SerializeField] private int maxActiveLasers = 6;              // caps the exponential growth so late waves stay dodgeable
    [SerializeField] private float laserTelegraphTime = 0.8f;      // warning time before it becomes dangerous
    [SerializeField] private float laserActiveTime = 1.0f;         // how long it stays dangerous
    [SerializeField] private float minTelegraphTime = 0.35f;
    [SerializeField] private float telegraphDecay = 0.95f;         // telegraph also shrinks over time -> less reaction time

    private int waveIndex = 0;
    private float currentInterval;
    private float currentTelegraph;

    private void Start()
    {
        currentInterval = timeBetweenWaves;
        currentTelegraph = laserTelegraphTime;
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(currentInterval);
            SpawnWave();

            waveIndex++;
            // Ramp difficulty: waves come faster and give less warning as waveIndex grows.
            currentInterval = Mathf.Max(minTimeBetweenWaves, currentInterval * waveIntervalDecay);
            currentTelegraph = Mathf.Max(minTelegraphTime, currentTelegraph * telegraphDecay);
        }
    }

    private void SpawnWave()
    {
        // Binary exponentiation growth: wave 0 -> 1 active laser, wave 1 -> 2, wave 2 -> 4, wave 3 -> 8...
        // Capped by maxActiveLasers/totalLanes so it never demands dodging every lane at once.
        int activeCount = Mathf.Min((int)Mathf.Pow(2, waveIndex), maxActiveLasers, totalLanes);

        List<int> lanes = new List<int>(totalLanes);
        for (int i = 0; i < totalLanes; i++) lanes.Add(i);

        // Shuffle so which lanes are "active" this wave is random - the rest stay open as dodge gaps.
        for (int i = 0; i < lanes.Count; i++)
        {
            int swapIndex = Random.Range(i, lanes.Count);
            (lanes[i], lanes[swapIndex]) = (lanes[swapIndex], lanes[i]);
        }

        float laneHeight = (maxY - minY) / totalLanes;

        for (int i = 0; i < activeCount; i++)
        {
            int lane = lanes[i];
            float y = minY + laneHeight * lane + laneHeight * 0.5f; // center of the lane, always within [minY, maxY]
            SpawnLaser(y);
        }
    }

    private void SpawnLaser(float y)
    {
        // Spawn x tracks the player + a fixed lead distance, so as the player moves
        // along the x axis the spawn point continuously moves with them, always
        // placing new waves the same distance ahead. Flip the sign on spawnAheadDistance
        // if your player runs toward -x instead of +x.
        float spawnX = playerTransform != null
            ? playerTransform.position.x + spawnAheadDistance
            : transform.position.x;

        GameObject laser = Instantiate(
            laserPrefab,
            new Vector3(spawnX, y, 0f),
            Quaternion.identity);

        LaserBehaviour behaviour = laser.GetComponent<LaserBehaviour>();
        if (behaviour != null)
        {
            behaviour.Activate(currentTelegraph, laserActiveTime);
        }
    }
}