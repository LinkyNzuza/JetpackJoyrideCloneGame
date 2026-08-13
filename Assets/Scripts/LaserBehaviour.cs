using System.Collections;
using UnityEngine;

// Put this on the laser prefab itself.
// Needs a SpriteRenderer (visual) and a Collider2D set as Trigger (the actual hazard).
[RequireComponent(typeof(SpriteRenderer))]
public class LaserBehaviour : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Collider2D hitbox;

    [SerializeField] private Sprite inactiveSprite; // shown during the telegraph/warning phase
    [SerializeField] private Sprite activeSprite;   // shown once the hitbox is actually on

    private void Reset()
    {
        sr = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
    }

    public void Activate(float telegraphTime, float activeTime)
    {
        StartCoroutine(Sequence(telegraphTime, activeTime));
    }

    // Only fires while hitbox.enabled is true (the "active" phase), so touching the
    // laser during its harmless warning phase never costs a life.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(1);
        }
    }

    private IEnumerator Sequence(float telegraphTime, float activeTime)
    {
        // Inactive phase: visible warning, but collider is off so it can't hurt the player yet.
        // This is what gives the player their dodge window.
        hitbox.enabled = false;
        sr.sprite = inactiveSprite;
        yield return new WaitForSeconds(telegraphTime);

        // Active phase: now it's a real threat.
        sr.sprite = activeSprite;
        hitbox.enabled = true;
        yield return new WaitForSeconds(activeTime);

        Destroy(gameObject);
    }
}
