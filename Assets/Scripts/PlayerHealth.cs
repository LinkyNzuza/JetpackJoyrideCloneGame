using System.Collections;
using UnityEngine;

// Put this on the player GameObject. Make sure the player is tagged "Player"
// so LaserBehaviour's OnTriggerEnter2D can find it.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float invincibilityDuration = 1f; // brief immunity after a hit, so overlapping lasers don't stack damage

    private int currentLives;
    private bool isInvincible;

    private void Awake()
    {
        currentLives = maxLives;
    }

    public void TakeDamage(int amount)
    {
        if (isInvincible) return;

        currentLives -= amount;
        Debug.Log($"Player hit. Lives remaining: {currentLives}");

        if (currentLives <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvincibilityFrames());
        }
    }

    private IEnumerator InvincibilityFrames()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    private void Die()
    {
        // Hook up your game-over / respawn logic here.
        Debug.Log("Player died.");
    }
}
