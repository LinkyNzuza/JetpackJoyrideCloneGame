using UnityEngine;

public class CameraFollowX : MonoBehaviour
{
    public Transform player; // drag your player/ball GameObject here
    public float smoothSpeed = 5f; // higher = snappier, lower = smoother/laggier

    private float startY;
    private float startZ;

    void Start()
    {
        // lock in the camera's current Y and Z so we never touch them
        startY = transform.position.y;
        startZ = transform.position.z;
    }

    void LateUpdate()
    {
        Vector3 targetPosition = new Vector3(player.position.x, startY, startZ);
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }
}