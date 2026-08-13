using UnityEngine;

// Put this on the missile prefab. Needs a SpriteRenderer and a trigger Collider2D.
[RequireComponent(typeof(SpriteRenderer))]
public class MissileBehaviour : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private float lifetime = 6f; // safety net so stray missiles don't live forever if they miss the player

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Travels toward -x to meet a player running in +x. Flip to Vector3.right
        // if your player runs toward -x instead.
        transform.position += Vector3.left * speed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(1);
        }

        Destroy(gameObject);
    }
}
