using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
public class NPCMovement : MonoBehaviour
{
    public Transform goal;

    [Header("Rolling Physics")]
    [Tooltip("Force applied toward the NavMesh desired direction")]
    public float rollForce = 14f;
    [Tooltip("Maximum velocity magnitude")]
    public float maxSpeed = 10f;
    [Tooltip("Base drift correction strength on straight paths")]
    public float steeringSharpness = 8f;
    [Tooltip("Multiplier on steeringSharpness when the ball deviates from the " +
             "NavMesh direction (obstacle avoidance). Higher = follows the NavMesh " +
             "path more tightly around obstacles.")]
    public float obstacleGripMultiplier = 4f;

    [Header("Turn Anticipation")]
    [Tooltip("How far ahead along the path to scan for turns (meters)")]
    public float turnLookAheadDistance = 10f;
    [Tooltip("Turns sharper than this angle trigger early braking (degrees)")]
    public float brakeAngleThreshold = 30f;
    [Tooltip("Braking counter-force applied before sharp turns")]
    public float brakingForce = 8f;
    [Tooltip("Blend between immediate NavMesh direction and the look-ahead point (0 = immediate only, 1 = look-ahead only)")]
    public float anticipationWeight = 0.5f;
    [Tooltip("How much to relax drift correction during turns (0 = full drift, 1 = full grip)")]
    public float turnDriftRelax = 0.3f;

    [Header("Personality (Randomization)")]
    public float speedVariation = 3f;
    public float wanderForce = 3f;
    public float wanderChangeInterval = 1.5f;

    private float currentWanderTimer;
    private Vector3 currentWanderDirection;

    [Header("Physics & Avoidance Settings")]
    [Tooltip("How far ahead to scan for obstacles (meters)")]
    public float lookAheadDistance = 8f;
    public float dodgeForce = 15f;
    public LayerMask obstacleLayer;

    [Header("Slope Handling")]
    [Tooltip("Extra force along downhill slopes for natural rolling")]
    public float slopeAssist = 4f;

    [Header("Stuck Recovery")]
    [Tooltip("How often to check if the racer has made real progress (seconds)")]
    public float progressCheckInterval = 2f;
    [Tooltip("Minimum distance the racer must move between checks to count as progressing")]
    public float minimumProgressDistance = 1.5f;
    [Tooltip("After this many failed progress checks, attempt smart redirect")]
    public int redirectAfterFailedChecks = 1;
    [Tooltip("After this many failed progress checks, force respawn at last checkpoint")]
    public int respawnAfterFailedChecks = 3;
    [Tooltip("Force applied when redirecting around an obstacle")]
    public float redirectForce = 10f;

    private float progressCheckTimer = 0f;
    private Vector3 lastProgressPosition;
    private int failedProgressChecks = 0;

    [Header("NavMesh Sync")]
    [Tooltip("How often to force a full path recalculation (seconds)")]
    public float pathRefreshInterval = 0.25f;
    private float pathRefreshTimer = 0f;

    [Header("Race Progress")]
    public int currentLap = 0;
    public bool isFinished = false;
    public float distanceToGoal;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Vector3 initialSpawnPosition;
    private bool hasStarted = false;

    // Allows RacerProgress to set a blended world position as the
    // NavMesh destination instead of using goal.position
    private bool hasDestinationOverride = false;
    private Vector3 destinationOverride;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // NavMeshAgent is used for pathfinding ONLY — not for moving the transform
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.autoRepath = true;

        // --- Standardize NavMeshAgent settings for all racers ---
        // These override whatever the prefab has so every racer
        // (champion and regular) pathfinds identically.
        agent.autoBraking = false;      // CRITICAL: prevents desiredVelocity from
                                        // dropping near the destination, which was
                                        // causing racers to lose steering force
                                        // and stop at every checkpoint
        agent.stoppingDistance = 0f;     // Don't stop short of the target
        agent.acceleration = 100f;       // Instant response to path changes
        agent.angularSpeed = 360f;       // Can face any direction immediately
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.radius = 0.5f;            // Tight paths around geometry

        initialSpawnPosition = transform.position;
        lastProgressPosition = transform.position;

        // Freeze the Rigidbody until the race starts so racers
        // don't push each other apart while waiting at the start line
        rb.isKinematic = true;

        // Personality: randomize roll force within a range
        rollForce += Random.Range(-speedVariation, speedVariation);
        agent.avoidancePriority = Random.Range(1, 100);
        currentWanderTimer = Random.Range(0f, wanderChangeInterval);
        agent.Warp(transform.position);
    }

    void Update()
    {
        // Fall-off-map respawn
        if (transform.position.y < -20f)
        {
            Respawn();
        }

        // Wait for race to begin
        if (!RaceManager.Instance.raceHasStarted) return;

        // First frame the race starts: unfreeze physics so they can roll
        if (!hasStarted)
        {
            rb.isKinematic = false;
            hasStarted = true;
        }
    }

    void FixedUpdate()
    {
        if (!RaceManager.Instance.raceHasStarted) return;

        // --- Sync NavMesh to physics position FIRST, then apply forces ---
        SyncNavMesh();

        if (agent.isOnNavMesh)
        {
            RollTowardGoal();
            ApplyWanderForce();
            ApplySlopeAssist();
        }

        CheckProgressAndRecover();
        ProactiveAvoidance();
        ClampSpeed();
    }

    /// <summary>
    /// Syncs the NavMeshAgent's position with the Rigidbody every physics tick.
    /// Uses nextPosition (which preserves the current path and desiredVelocity)
    /// as the normal sync method. Only uses Warp as a recovery when the agent
    /// has fallen off the NavMesh — Warp clears the path, so we avoid it
    /// during normal operation to keep pathfinding and the PathDrawer working.
    /// </summary>
    void SyncNavMesh()
    {
        if (agent.isOnNavMesh)
        {
            // Normal sync: tell the agent where physics put us
            // This preserves the existing path and desiredVelocity
            agent.nextPosition = transform.position;
            agent.speed = maxSpeed;
            distanceToGoal = agent.remainingDistance;

            // Periodically force a full path recalculation since
            // physics is constantly shifting the ball's position
            pathRefreshTimer -= Time.fixedDeltaTime;
            if (pathRefreshTimer <= 0f)
            {
                if (hasDestinationOverride)
                {
                    agent.SetDestination(destinationOverride);
                }
                else if (goal != null)
                {
                    agent.SetDestination(goal.position);
                }
                pathRefreshTimer = pathRefreshInterval;
            }
        }
        else
        {
            // Recovery: ball got knocked off the NavMesh by physics.
            // Find the nearest valid NavMesh point and Warp there.
            // Warp clears the path, so we immediately request a new one.
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 10f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
                agent.speed = maxSpeed;

                if (hasDestinationOverride)
                {
                    agent.SetDestination(destinationOverride);
                }
                else if (goal != null)
                {
                    agent.SetDestination(goal.position);
                }

                // Reset the refresh timer so we don't immediately
                // call SetDestination again next tick
                pathRefreshTimer = pathRefreshInterval;
            }
            // If no NavMesh within 10 units, the ball is truly lost —
            // fall-off-map check or boundary trigger will handle respawn
        }
    }

    /// <summary>
    /// Called externally (by RacerProgress) whenever the goal Transform changes.
    /// Clears any position override and paths to the goal Transform.
    /// </summary>
    public void ForcePathRefresh()
    {
        hasDestinationOverride = false;

        if (agent == null || goal == null) return;

        if (agent.isOnNavMesh)
        {
            agent.SetDestination(goal.position);
            pathRefreshTimer = pathRefreshInterval;
        }
    }

    /// <summary>
    /// Sets a direct world position as the NavMesh destination.
    /// Used by RacerProgress for blended checkpoint targeting —
    /// the position slides smoothly between checkpoints each frame.
    /// </summary>
    public void SetDestinationDirect(Vector3 position)
    {
        destinationOverride = position;
        hasDestinationOverride = true;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(position);
        }
    }

    /// <summary>
    /// Adaptive rolling: follows the NavMesh path with variable grip.
    /// 
    /// On straight, clear paths: gentle steering, look-ahead anticipation active,
    ///   drift allowed — feels like natural rolling momentum.
    /// Near obstacles (NavMesh direction differs from current velocity):
    ///   steering grip scales up dramatically, look-ahead influence drops,
    ///   the ball actively redirects to follow the NavMesh path around obstacles.
    /// 
    /// The key insight: agent.desiredVelocity ALREADY knows how to go around
    /// obstacles. We just need to follow it tightly enough when it matters.
    /// </summary>
    void RollTowardGoal()
    {
        Vector3 immediateDir = agent.desiredVelocity.normalized;
        if (immediateDir.sqrMagnitude < 0.01f) return;
        immediateDir.y = 0f;
        immediateDir.Normalize();

        // --- How much is the ball deviating from where the NavMesh says to go? ---
        Vector3 currentVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float currentSpeed = currentVel.magnitude;
        Vector3 currentDir = currentSpeed > 0.1f ? currentVel.normalized : immediateDir;

        // alignment: 1 = perfectly aligned, 0 = perpendicular, -1 = opposite
        float alignment = Vector3.Dot(currentDir, immediateDir);
        // deviationFactor: 0 = aligned (no obstacle issue), 1 = fully deviating
        float deviationFactor = Mathf.Clamp01(1f - alignment);

        // --- 1) Look ahead for smooth cornering ---
        // Reduce anticipation influence when deviating — the NavMesh's immediate
        // direction is more important than the look-ahead when avoiding obstacles
        float effectiveAnticipation = anticipationWeight * (1f - deviationFactor);

        Vector3 lookAheadPoint = GetLookAheadPoint(turnLookAheadDistance);
        Vector3 toLookAhead = lookAheadPoint - transform.position;
        toLookAhead.y = 0f;

        Vector3 lookAheadDir = toLookAhead.magnitude > 0.5f
            ? toLookAhead.normalized
            : immediateDir;

        Vector3 steerDir = Vector3.Lerp(immediateDir, lookAheadDir, effectiveAnticipation).normalized;

        // --- 2) Detect upcoming turn sharpness ---
        float turnAngle;
        float distanceToTurn;
        GetUpcomingTurnInfo(turnLookAheadDistance, out turnAngle, out distanceToTurn);

        float turnFactor = Mathf.Clamp01((turnAngle - brakeAngleThreshold) /
                                          (90f - brakeAngleThreshold));

        // --- 3) Apply main rolling force ---
        rb.AddForce(steerDir * rollForce, ForceMode.Force);

        // --- 4) Pre-turn braking ---
        if (turnAngle > brakeAngleThreshold && distanceToTurn > 0f)
        {
            float proximityFactor = Mathf.Clamp01(1f - (distanceToTurn / turnLookAheadDistance));
            float brakeStrength = brakingForce * turnFactor * proximityFactor;
            rb.AddForce(-currentVel.normalized * brakeStrength, ForceMode.Force);
        }

        // --- 5) Adaptive drift correction ---
        // Base grip: gentle on straights, relaxed in turns (for drift feel)
        float baseGrip = Mathf.Lerp(steeringSharpness, steeringSharpness * turnDriftRelax, turnFactor);

        // Obstacle grip: scales UP when deviating from NavMesh direction.
        // If the NavMesh says "go left" but the ball is going straight into a wall,
        // this multiplier forces it to actually turn.
        float adaptiveGrip = Mathf.Lerp(baseGrip, steeringSharpness * obstacleGripMultiplier, deviationFactor);

        Vector3 forwardComponent = Vector3.Project(currentVel, steerDir);
        Vector3 sidewaysDrift = currentVel - forwardComponent;

        rb.AddForce(-sidewaysDrift * adaptiveGrip, ForceMode.Force);
    }

    /// <summary>
    /// Walks along the NavMesh path corners to find a point that is
    /// 'lookDist' meters ahead. This is what the ball steers toward,
    /// so it begins turning before it reaches the actual corner.
    /// </summary>
    Vector3 GetLookAheadPoint(float lookDist)
    {
        if (!agent.hasPath) return transform.position + transform.forward * lookDist;

        Vector3[] corners = agent.path.corners;
        if (corners.Length < 2) return transform.position + transform.forward * lookDist;

        float accumulated = 0f;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 segStart = corners[i];
            Vector3 segEnd = corners[i + 1];
            float segLen = Vector3.Distance(segStart, segEnd);

            if (accumulated + segLen >= lookDist)
            {
                // The look-ahead point falls within this segment
                float remaining = lookDist - accumulated;
                return Vector3.Lerp(segStart, segEnd, remaining / segLen);
            }

            accumulated += segLen;
        }

        // Look-ahead extends past the end of the path — return the goal
        return corners[corners.Length - 1];
    }

    /// <summary>
    /// Scans the path corners within 'lookDist' meters and finds the
    /// sharpest upcoming turn angle and how far away it is.
    /// </summary>
    void GetUpcomingTurnInfo(float lookDist, out float sharpestAngle, out float distanceToSharpest)
    {
        sharpestAngle = 0f;
        distanceToSharpest = lookDist;

        if (!agent.hasPath) return;

        Vector3[] corners = agent.path.corners;
        if (corners.Length < 3) return;

        float accumulated = 0f;

        for (int i = 0; i < corners.Length - 2; i++)
        {
            float segLen = Vector3.Distance(corners[i], corners[i + 1]);
            accumulated += segLen;

            if (accumulated > lookDist) break;

            // Angle between this segment and the next
            Vector3 dirA = corners[i + 1] - corners[i];
            Vector3 dirB = corners[i + 2] - corners[i + 1];
            dirA.y = 0f;
            dirB.y = 0f;

            if (dirA.sqrMagnitude < 0.01f || dirB.sqrMagnitude < 0.01f) continue;

            float angle = Vector3.Angle(dirA.normalized, dirB.normalized);

            if (angle > sharpestAngle)
            {
                sharpestAngle = angle;
                distanceToSharpest = accumulated;
            }
        }
    }

    void ApplyWanderForce()
    {
        currentWanderTimer -= Time.fixedDeltaTime;

        if (currentWanderTimer <= 0f)
        {
            Vector3 desiredDir = agent.desiredVelocity.normalized;
            // Wander perpendicular to the travel direction for organic sway
            Vector3 perp = Vector3.Cross(desiredDir, Vector3.up).normalized;
            currentWanderDirection = perp * Random.Range(-1f, 1f);
            currentWanderTimer = wanderChangeInterval + Random.Range(-0.3f, 0.3f);
        }

        rb.AddForce(currentWanderDirection * wanderForce, ForceMode.Force);
    }

    /// <summary>
    /// Adds a gentle push along downhill slopes so the ball rolls
    /// convincingly on ramps and inclines.
    /// </summary>
    void ApplySlopeAssist()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
        {
            Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
            if (slopeDir.sqrMagnitude > 0.001f)
            {
                rb.AddForce(slopeDir * slopeAssist, ForceMode.Force);
            }
        }
    }

    /// <summary>
    /// Periodically checks whether the racer has made real progress toward
    /// the goal. A racer bouncing against a wall has velocity but makes no
    /// progress — this catches that case unlike simple speed checks.
    /// 
    /// Stage 1 (redirect):  Find a clear direction via raycasts and push that way
    /// Stage 2 (path follow): Kill velocity, push directly toward next NavMesh corner
    /// Stage 3 (respawn):   Teleport to last checkpoint
    /// </summary>
    void CheckProgressAndRecover()
    {
        progressCheckTimer += Time.fixedDeltaTime;
        if (progressCheckTimer < progressCheckInterval) return;
        progressCheckTimer = 0f;

        // Has the racer actually moved since the last check?
        float moved = Vector3.Distance(transform.position, lastProgressPosition);

        if (moved >= minimumProgressDistance)
        {
            // Making progress — reset
            failedProgressChecks = 0;
            lastProgressPosition = transform.position;
            return;
        }

        // Not making progress
        failedProgressChecks++;
        lastProgressPosition = transform.position;

        if (failedProgressChecks >= respawnAfterFailedChecks)
        {
            // Stage 3: respawn at last checkpoint
            failedProgressChecks = 0;
            Respawn();
        }
        else if (failedProgressChecks >= redirectAfterFailedChecks)
        {
            // Stage 1-2: smart redirect
            SmartRedirect();
        }
    }

    /// <summary>
    /// Attempts to find a clear path around whatever the racer is stuck on.
    /// First tries to follow the next NavMesh path corner directly.
    /// If that's blocked, casts rays in a circle to find the clearest direction
    /// and pushes that way.
    /// </summary>
    void SmartRedirect()
    {
        rb.linearVelocity *= 0.3f; // Bleed off momentum that's pushing into the wall

        Vector3 redirectDir = Vector3.zero;

        // Try 1: Push toward the next NavMesh path corner (not the final destination,
        // but the next CORNER — this is usually the direction around the obstacle)
        if (agent.hasPath && agent.path.corners.Length >= 2)
        {
            // corners[0] is current position, corners[1] is the next turn point
            Vector3 nextCorner = agent.path.corners[1];
            Vector3 toCorner = nextCorner - transform.position;
            toCorner.y = 0f;

            if (toCorner.magnitude > 0.5f)
            {
                redirectDir = toCorner.normalized;

                // Check if that direction is actually clear
                if (!Physics.Raycast(transform.position + Vector3.up * 0.3f,
                                     redirectDir, 2f, obstacleLayer))
                {
                    rb.AddForce(redirectDir * redirectForce, ForceMode.Impulse);
                    return;
                }
            }
        }

        // Try 2: Cast rays in a circle and find the clearest direction
        // weighted toward the goal
        Vector3 goalDir = Vector3.zero;
        if (goal != null)
        {
            goalDir = (goal.position - transform.position).normalized;
            goalDir.y = 0f;
        }

        float bestScore = -1f;
        Vector3 bestDir = Vector3.zero;

        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            Vector3 testDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            if (Physics.Raycast(transform.position + Vector3.up * 0.3f,
                                testDir, out RaycastHit hit, lookAheadDistance, obstacleLayer))
            {
                // Partially blocked — score by how far the ray got
                float clearance = hit.distance / lookAheadDistance;
                float goalAlignment = goalDir.sqrMagnitude > 0.01f
                    ? (Vector3.Dot(testDir, goalDir) + 1f) * 0.5f  // 0 to 1
                    : 0.5f;

                float score = clearance * 0.6f + goalAlignment * 0.4f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = testDir;
                }
            }
            else
            {
                // Fully clear — score it highly, bonus if it points toward goal
                float goalAlignment = goalDir.sqrMagnitude > 0.01f
                    ? (Vector3.Dot(testDir, goalDir) + 1f) * 0.5f
                    : 0.5f;

                float score = 1f * 0.6f + goalAlignment * 0.4f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = testDir;
                }
            }
        }

        if (bestDir.sqrMagnitude > 0.01f)
        {
            rb.AddForce(bestDir * redirectForce, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Casts a fan of rays ahead of the racer to detect obstacles early.
    /// Dodge force scales with proximity — closer obstacles get a harder push.
    /// Rays follow both the current velocity AND the NavMesh desired direction
    /// so the racer avoids obstacles on the path it's about to take, not just
    /// what's directly ahead of its current momentum.
    /// </summary>
    void ProactiveAvoidance()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.3f;
        Vector3 moveDir = rb.linearVelocity.normalized;

        // Also check along the NavMesh desired direction (where we're ABOUT to go)
        Vector3 navDir = agent.isOnNavMesh ? agent.desiredVelocity.normalized : moveDir;

        if (moveDir.sqrMagnitude < 0.01f && navDir.sqrMagnitude < 0.01f) return;

        // Use the NavMesh direction as primary, velocity as secondary
        Vector3 primaryDir = navDir.sqrMagnitude > 0.01f ? navDir : moveDir;
        primaryDir.y = 0f;
        primaryDir.Normalize();

        // Fan angles: center, slight left/right, wide left/right
        float[] fanAngles = { 0f, -20f, 20f, -45f, 45f };

        foreach (float angle in fanAngles)
        {
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * primaryDir;

            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, lookAheadDistance, obstacleLayer))
            {
                // Closer obstacles get a stronger dodge (1 = touching, 0 = at max distance)
                float closeness = 1f - (hit.distance / lookAheadDistance);
                float scaledForce = dodgeForce * closeness * closeness; // Quadratic falloff

                // Dodge perpendicular to the ray that hit
                Vector3 dodgeDirection = Vector3.Cross(Vector3.up, rayDir).normalized;

                // Push away from whichever side the obstacle is on
                Vector3 toObstacle = hit.point - transform.position;
                if (Vector3.Dot(Vector3.Cross(Vector3.up, primaryDir), toObstacle) > 0f)
                {
                    dodgeDirection = -dodgeDirection;
                }

                rb.AddForce(dodgeDirection * scaledForce, ForceMode.Force);
            }
        }
    }

    void ClampSpeed()
    {
        Vector3 horizontal = rb.linearVelocity;
        horizontal.y = 0f;

        if (horizontal.magnitude > maxSpeed)
        {
            horizontal = horizontal.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
        }
    }

    // --- RESPAWN LOGIC ---
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Boundary"))
        {
            Respawn();
        }
    }

    void Respawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reset stuck detection so the racer gets a fresh start
        failedProgressChecks = 0;
        progressCheckTimer = 0f;

        // Use checkpoint-based respawn if RacerProgress is attached,
        // otherwise fall back to the original spawn position
        RacerProgress progress = GetComponent<RacerProgress>();
        if (progress != null)
        {
            transform.position = progress.GetRespawnPosition();
            agent.Warp(transform.position);

            // Re-point the goal at the correct next checkpoint
            progress.OnRespawned();
        }
        else
        {
            transform.position = initialSpawnPosition;
            agent.Warp(initialSpawnPosition);
        }

        lastProgressPosition = transform.position;
    }
}