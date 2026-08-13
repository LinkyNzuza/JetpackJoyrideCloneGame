using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserSpawner : MonoBehaviour
{
    [Header("Laser Setup")]
    [SerializeField] private GameObject laserPrefab;
    [SerializeField] private Transform spawnParent; // optional, keeps hierarchy tidy
    [SerializeField] private Transform playerTransform; // spawn point tracks this so waves keep appearing ahead as the player advances
    [SerializeField] private float spawnAheadDistance = 12f; // how far along x, ahead of the player, lasers spawn
    [SerializeField] private float minSpawnGapX = 10f; // waves are never placed closer than this to the previous wave along x, even if the player is slow or waves fire in quick succession

    private float lastWaveSpawnX = float.NegativeInfinity; // x position the previous wave spawned at, used to enforce minSpawnGapX

    [Header("Y-Axis Range")]
    [SerializeField] private float minY = -2.7f;
    [SerializeField] private float maxY = 2.7f;
    [SerializeField] private int totalLanes = 8; // how many horizontal slots exist across the range

    [Header("Wave Timing")]
    [SerializeField] private float timeBetweenWaves = 2.5f; // fixed interval between waves

    [Header("Lane Spacing")]
    [SerializeField] private int minLaneGap = 1; // minimum empty lanes required between two active lanes in the same wave, so there's always room to dodge through

    // Explicit spawn-count pattern, cycled through one value per wave.
    private static readonly int[] laserCountPattern =
    {
        1,3,2,1,2,2,3,3,3,1,1,2,1,1,1,1,3,2,2,3,2,1,2,1,2,1,3,3,3,3,2,3,2,1,1,1,3,2,3,1,2,3,1,2,3
    };
    private int patternIndex = 0;
    [SerializeField] private float laserTelegraphTime = 0.8f; // fixed warning time before a laser becomes dangerous
    [SerializeField] private float laserActiveTime = 1.0f;    // how long it stays dangerous

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(timeBetweenWaves);
            SpawnWave();
        }
    }

    private void SpawnWave()
    {
        // Pull this wave's laser count from the fixed pattern, wrapping back to the
        // start once we reach the end. Still capped by totalLanes as a safety net.
        int activeCount = Mathf.Min(laserCountPattern[patternIndex], totalLanes);
        patternIndex = (patternIndex + 1) % laserCountPattern.Length;

        List<int> activeLanes = SelectLanesWithGap(activeCount, minLaneGap);

        // Wave x is normally gap-based off the previous wave, so spacing stays
        // exactly minSpawnGapX. But that alone doesn't account for player speed -
        // if the player closes the distance faster than waves are spaced out,
        // this also floors spawnX at spawnAheadDistance in front of the player,
        // so every wave (not just the first) stays ahead of them.
        float gapBasedX = lastWaveSpawnX == float.NegativeInfinity
            ? float.NegativeInfinity
            : lastWaveSpawnX + minSpawnGapX;
        float playerAheadX = playerTransform != null
            ? playerTransform.position.x + spawnAheadDistance
            : transform.position.x;
        float spawnX = Mathf.Max(gapBasedX, playerAheadX);
        lastWaveSpawnX = spawnX;

        float laneHeight = (maxY - minY) / totalLanes;

        foreach (int lane in activeLanes)
        {
            float y = minY + laneHeight * lane + laneHeight * 0.5f; // center of the lane, always within [minY, maxY]
            LaneOccupancy.Occupy(lane);
            SpawnLaser(spawnX, y, lane);
        }
    }

    // Picks 'count' lanes out of totalLanes such that no two picks are within
    // minGap of each other, guaranteeing an open corridor to dodge through.
    // Falls back to filling remaining slots without the gap rule if the
    // constraint can't be satisfied (e.g. count is too high for the gap size),
    // so we never silently spawn fewer lasers than the pattern calls for.
    private List<int> SelectLanesWithGap(int count, int minGap)
    {
        List<int> laneOrder = new List<int>(totalLanes);
        for (int i = 0; i < totalLanes; i++) laneOrder.Add(i);

        for (int i = 0; i < laneOrder.Count; i++)
        {
            int swapIndex = Random.Range(i, laneOrder.Count);
            (laneOrder[i], laneOrder[swapIndex]) = (laneOrder[swapIndex], laneOrder[i]);
        }

        List<int> selected = new List<int>(count);
        foreach (int lane in laneOrder)
        {
            bool tooClose = false;
            foreach (int chosen in selected)
            {
                if (Mathf.Abs(chosen - lane) <= minGap)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                selected.Add(lane);
                if (selected.Count == count) break;
            }
        }

        if (selected.Count < count)
        {
            foreach (int lane in laneOrder)
            {
                if (selected.Count == count) break;
                if (!selected.Contains(lane)) selected.Add(lane);
            }
        }

        return selected;
    }

    private void SpawnLaser(float spawnX, float y, int lane)
    {
        GameObject laser = Instantiate(
            laserPrefab,
            new Vector3(spawnX, y, 0f),
            Quaternion.identity,
            spawnParent
        );

        LaserBehaviour behaviour = laser.GetComponent<LaserBehaviour>();
        if (behaviour != null)
        {
            behaviour.Activate(laserTelegraphTime, laserActiveTime, lane);
        }
    }
}