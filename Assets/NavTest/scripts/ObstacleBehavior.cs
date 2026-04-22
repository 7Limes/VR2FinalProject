using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshObstacle))]
public class ObstacleBehaviour : MonoBehaviour
{
    private NavMeshObstacle obstacle;
    private Rigidbody rb;
    private bool landed;

    void Start()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        rb = GetComponent<Rigidbody>();

        // Disable carving while falling so agents don't try to path around an airborne box
        obstacle.carving = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (landed) return;

        if (collision.gameObject.CompareTag("Ground"))
        {
            landed = true;

            // Carve the NavMesh so racers reroute around the newly placed obstacle
            obstacle.carving = true;

            // Freeze so racers can't shove the obstacle around (NavMesh stutter)
            // but keep the collider active so they still physically collide
            if (rb != null) rb.isKinematic = true;
        }
    }
}
