using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
public class NPCMovement : MonoBehaviour
{
    public Transform goal;

    [Header("Timing")]
    public float startDelay = 3f;
    private float timer = 0f;
    private bool isReadyToMove = false;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float frictionGrip = 5f;

    [Header("Personality (Randomization)")]
    public float speedVariation = 1.5f;
    public float wanderForce = 4f;
    public float wanderChangeInterval = 1.5f;

    private float currentWanderTimer;
    private Vector3 currentWanderDirection;

    [Header("Physics & Avoidance Settings")]
    public float lookAheadDistance = 5f;
    public float dodgeForce = 15f;
    public LayerMask obstacleLayer;

    [Header("Race Progress")]
    public int currentLap = 0; // Starts at 0, becomes 1 when race starts
    public bool isFinished = false;
    public float distanceToGoal; // Used for real-time ranking

    private NavMeshAgent agent;
    private Rigidbody rb;

    // Tracks where to send the NPC if they escape
    private Vector3 initialSpawnPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        agent.updatePosition = false;
        agent.updateRotation = false;

        // Save the start position for respawning
        initialSpawnPosition = transform.position;

        moveSpeed += Random.Range(-speedVariation, speedVariation);
        agent.avoidancePriority = Random.Range(1, 100);
        currentWanderTimer = Random.Range(0f, wanderChangeInterval);
        agent.Warp(transform.position);
    }

    void Update()
    {
        if (transform.position.y < -20f)
        {
            Respawn();
        }

        // --- NEW: wait for the Race Manager before thinking ---
        if (!RaceManager.Instance.raceHasStarted) return;

        if (agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.nextPosition = transform.position;
            distanceToGoal = agent.remainingDistance;

            if (goal != null && agent.destination != goal.position)
            {
                agent.SetDestination(goal.position);
            }
        }
    }

    void FixedUpdate()
    {
        // --- NEW: wait for the Race Manager before moving ---
        if (!RaceManager.Instance.raceHasStarted) return;

        if (agent.isOnNavMesh)
        {
            MoveWithPhysics();
            ApplyWanderForce();
        }

        ProactiveAvoidance();
    }

    void MoveWithPhysics()
    {
        Vector3 targetVelocity = agent.desiredVelocity.normalized * moveSpeed;
        Vector3 targetVelocityWithGravity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocityWithGravity, Time.fixedDeltaTime * frictionGrip);

        if (targetVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetVelocity.normalized);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
        }
    }

    void ApplyWanderForce()
    {
        currentWanderTimer -= Time.fixedDeltaTime;

        if (currentWanderTimer <= 0)
        {
            float randomX = Random.Range(-1f, 1f);
            float randomZ = Random.Range(-1f, 1f);

            currentWanderDirection = new Vector3(randomX, 0, randomZ).normalized;
            currentWanderTimer = wanderChangeInterval;
        }

        rb.AddForce(currentWanderDirection * wanderForce, ForceMode.Acceleration);
    }

    void ProactiveAvoidance()
    {
        Vector3 rayOrigin = transform.position + Vector3.up;
        RaycastHit hit;

        if (Physics.Raycast(rayOrigin, transform.forward, out hit, lookAheadDistance, obstacleLayer))
        {
            Vector3 dodgeDirection = Vector3.Cross(Vector3.up, transform.forward).normalized;
            Vector3 directionToObstacle = hit.point - transform.position;

            if (Vector3.Dot(transform.right, directionToObstacle) > 0)
            {
                dodgeDirection = -dodgeDirection;
            }

            rb.AddForce(dodgeDirection * dodgeForce, ForceMode.Acceleration);
        }
    }

    // --- RESPAWN LOGIC ---
    void OnTriggerExit(Collider other)
    {
        // If they leave the safe zone, reset them
        if (other.CompareTag("Boundary"))
        {
            Respawn();
        }
    }

    void Respawn()
    {
        // Strip away momentum so they don't keep flying
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Move physical body back to start
        transform.position = initialSpawnPosition;

        // Snap the NavMesh agent brain back to start
        agent.Warp(initialSpawnPosition);

        // Reset the startup timer so they get a moment to settle again
        isReadyToMove = false;
        timer = 0f;
    }
}