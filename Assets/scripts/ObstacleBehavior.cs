using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshObstacle))]
public class ObstacleBehaviour : MonoBehaviour
{
    private NavMeshObstacle obstacle;

    void Start()
    {
        obstacle = GetComponent<NavMeshObstacle>();

        // Disable carving while falling so agents don't try to path around an airborne box
        obstacle.carving = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check if the obstacle hit the ground
        if (collision.gameObject.CompareTag("Ground"))
        {
            // Enabling Carve cuts a hole in the NavMesh, forcing agents to route around it
            obstacle.carving = true;

            // Optional: Freeze the Rigidbody so agents don't push the box around 
            // once it has landed, which can cause NavMesh stuttering.
            GetComponent<Rigidbody>().isKinematic = true;
        }
    }
}