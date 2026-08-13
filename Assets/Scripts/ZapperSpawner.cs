using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ZapperSpawner : MonoBehaviour
{
    [Header("Zapper Setup")]
    [SerializeField] private GameObject zapperPrefab;
    [SerializeField] private Transform playerTransform; // spawn point tracks this, same as LaserSpawner/MissileSpawner
    [SerializeField] private float spawnAheadDistance = 12f;
    [Header("Y-Axis Range")]
    [SerializeField] private float minY = -2.7f;
    [SerializeField] private float maxY = 2.7f;
    [SerializeField] private int totalLanes = 8; // MUST match LaserSpawner's totalLanes/minY/maxY for lane indices to line up
    [Header("Difficulty Scaling")]
    [SerializeField] private float timeBetweenZappers = 3f;
    [SerializeField] private float minTimeBetweenZappers = 1f;
    [SerializeField] private float intervalDecay = 0.94f; // spawns get more frequent over time
    private float currentInterval;
    private void Start()
    {
        currentInterval = timeBetweenZappers;
        StartCoroutine(SpawnLoop());
    }
    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(currentInterval);
            SpawnZapper();
            currentInterval = Mathf.Max(minTimeBetweenZappers, currentInterval * intervalDecay);
        }
    }
    private void SpawnZapper()
    {
        List<int> freeLanes = new List<int>();
        for (int i = 0; i < totalLanes; i++)
        {
            if (!LaneOccupancy.IsOccupied(i)) freeLanes.Add(i);
        }
        // Every lane currently has a laser in it - skip this spawn rather than overlap.
        if (freeLanes.Count == 0) return;
        int lane = freeLanes[Random.Range(0, freeLanes.Count)];
        float laneHeight = (maxY - minY) / totalLanes;
        float y = minY + laneHeight * lane + laneHeight * 0.5f;
        float spawnX = playerTransform != null
            ? playerTransform.position.x + spawnAheadDistance
            : transform.position.x;
        GameObject zapper = Instantiate(zapperPrefab, new Vector3(spawnX, y, 0f), Quaternion.identity);
        ZapperBehaviour behaviour = zapper.GetComponent<ZapperBehaviour>();
        if (behaviour != null)
        {
            behaviour.Init(playerTransform);
        }
    }
}