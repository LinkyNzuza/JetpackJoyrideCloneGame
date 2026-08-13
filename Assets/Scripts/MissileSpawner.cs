using System.Collections;
using UnityEngine;

public class MissileSpawner : MonoBehaviour
{
    [Header("Missile Setup")]
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private Transform playerTransform; // spawn point tracks this, same as LaserSpawner
    [SerializeField] private float spawnAheadDistance = 12f;

    [Header("Y-Axis Range")]
    [SerializeField] private float minY = -2.7f;
    [SerializeField] private float maxY = 2.7f;

    [Header("Spawn Timing")]
    [SerializeField] private float gapBetweenMissilesInBurst = 0.3f; // spacing between missiles within the same burst

    [Header("Difficulty Scaling")]
    [SerializeField] private float missileSpeedStart = 5f;
    [SerializeField] private float missileSpeedMax = 10f;
    [SerializeField] private float speedIncreasePerCycle = 0.5f; // speed goes up once per full cycle, not per missile

    private float currentSpeed;

    private void Start()
    {
        currentSpeed = missileSpeedStart;
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        int cycle = 0;

        while (true)
        {
            // Cycle N: N missiles (1, 2, 3...), spaced out slightly, then a wait
            // that grows by 2 each cycle (5, 7, 9, 11...).
            int missileCount = cycle + 1;
            float waitAfterBurst = 5 + cycle * 2;

            for (int i = 0; i < missileCount; i++)
            {
                SpawnMissile();
                yield return new WaitForSeconds(gapBetweenMissilesInBurst);
            }

            yield return new WaitForSeconds(waitAfterBurst);

            currentSpeed = Mathf.Min(missileSpeedMax, currentSpeed + speedIncreasePerCycle);
            cycle++;
        }
    }

    private void SpawnMissile()
    {
        float spawnX = playerTransform != null
            ? playerTransform.position.x + spawnAheadDistance
            : transform.position.x;

        float y = Random.Range(minY, maxY);

        GameObject missile = Instantiate(
            missilePrefab,
            new Vector3(spawnX, y, 0f),
            Quaternion.identity
        );

        MissileBehaviour behaviour = missile.GetComponent<MissileBehaviour>();
        if (behaviour != null)
        {
            behaviour.SetSpeed(currentSpeed);
        }
    }
}