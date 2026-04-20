using UnityEngine;

public class MoveBackAndForth : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("How fast the object moves.")]
    public float speed = 2.0f;

    [Tooltip("How far the object moves from the start position.")]
    public float distance = 5.0f;

    [Tooltip("The direction of movement (e.g., 1,0,0 for X-axis, 0,1,0 for Y-axis).")]
    public Vector3 direction = Vector3.right;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;

        // Normalize the direction so that the 'distance' value remains accurate
        // regardless of how large the direction vector components are.
        direction = direction.normalized;
    }

    void Update()
    {
        // Calculate the smooth oscillating offset
        float offset = Mathf.Sin(Time.time * speed) * distance;

        // Apply the offset along the chosen direction
        transform.position = startPosition + (direction * offset);
    }
}
