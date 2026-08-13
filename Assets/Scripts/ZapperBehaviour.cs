using System.Collections;
using UnityEngine;

// Put this on the zapper prefab. Needs a SpriteRenderer and a trigger Collider2D.
[RequireComponent(typeof(SpriteRenderer))]
public class ZapperBehaviour : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Collider2D hitbox;

    [SerializeField] private Sprite onSprite; // dangerous - the only sprite you have

    [SerializeField] private float onDuration = 1f;
    [SerializeField] private float offDuration = 1f;
    [SerializeField] private float despawnDistanceBehindPlayer = 15f; // cleanup once the player's well past it

    private Transform playerTransform;

    private void Reset()
    {
        sr = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
    }

    // Call this right after Instantiate so it knows who to watch for cleanup.
    public void Init(Transform player)
    {
        playerTransform = player;
        StartCoroutine(PulseLoop());
    }

    private IEnumerator PulseLoop()
    {
        // Unlike the laser (fires once) or missile (destroys on hit/timeout), a zapper
        // is a persistent trap - it keeps toggling on/off forever until it's cleaned up.
        while (true)
        {
            // No off sprite to swap to, so just hide it entirely during the safe phase.
            hitbox.enabled = false;
            sr.enabled = false;
            yield return new WaitForSeconds(offDuration);

            sr.sprite = onSprite;
            sr.enabled = true;
            hitbox.enabled = true;
            yield return new WaitForSeconds(onDuration);
        }
    }

    private void Update()
    {
        // Since it never destroys itself on its own, clean it up once the player
        // has run well past it - otherwise these would pile up behind the player forever.
        if (playerTransform != null && playerTransform.position.x - transform.position.x > despawnDistanceBehindPlayer)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(1);
        }
    }
}