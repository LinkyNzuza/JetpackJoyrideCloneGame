using System.Collections;
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
        float spawnX = playerTransform != null
            ? playerTransform.position.x + spawnAheadDistance
            : transform.position.x;

        float y = Random.Range(minY, maxY);

        GameObject zapper = Instantiate(zapperPrefab, new Vector3(spawnX, y, 0f), Quaternion.identity);

        ZapperBehaviour behaviour = zapper.GetComponent<ZapperBehaviour>();
        if (behaviour != null)
        {
            behaviour.Init(playerTransform);
        }
    }
}
