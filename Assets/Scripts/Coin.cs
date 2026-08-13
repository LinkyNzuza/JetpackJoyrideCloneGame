using UnityEngine;


public class Coin : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        
        if (!other.CompareTag("Player")) return;

        // Tell the spawner a coin was collected so it can track count / spawn more
        if (CoinSpawner.Instance != null)
        {
            CoinSpawner.Instance.CoinCollected(this);
        }

        // Deactivate rather than Destroy so the spawner can pool/reuse this object
        gameObject.SetActive(false);
    }
}