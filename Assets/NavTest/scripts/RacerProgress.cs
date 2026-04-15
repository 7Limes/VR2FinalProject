using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tracks an individual racer's progress through checkpoints and laps.
/// Updates NPCMovement.goal to point at the next checkpoint so the
/// NavMesh pathfinding always leads the racer to the right place.
/// Also provides the correct respawn position based on the last
/// checkpoint the racer successfully passed.
/// 
/// SETUP: Added automatically by PlayerController at spawn time.
/// Can also be added manually to any racer prefab.
/// </summary>
[RequireComponent(typeof(NPCMovement))]
public class RacerProgress : MonoBehaviour
{
    [Header("Progress (read-only at runtime)")]
    [Tooltip("Index of the next checkpoint this racer needs to hit")]
    public int nextCheckpointIndex = 0;

    [Tooltip("Index of the last checkpoint this racer successfully passed (-1 = none yet)")]
    public int lastPassedCheckpoint = -1;

    [Tooltip("Current lap (0 = hasn't crossed start yet, 1 = first lap, etc.)")]
    public int currentLap = 0;

    [Tooltip("True once the racer has completed all laps")]
    public bool isFinished = false;

    [Tooltip("True once the racer has hit all checkpoints in the current lap " +
             "and is heading for the Start/Finish")]
    public bool headingToFinishLine = false;

    [Header("Debug")]
    [Tooltip("Enable to see checkpoint progress logs in the Console")]
    public bool debugLogging = true;

    [Header("Checkpoint Detection")]
    [Tooltip("Distance to the nearest surface of a checkpoint's collider " +
             "at which it counts as passed")]
    public float checkpointReachDistance = 3f;

    [Tooltip("Distance at which the destination starts extending past the " +
             "current checkpoint toward the next one. Farther away = aims " +
             "directly at the checkpoint. Closer = aims past it.")]
    public float blendStartDistance = 20f;

    [Tooltip("How far past the current checkpoint to aim when the blend is " +
             "fully active. Higher = smoother flow, lower = tighter to checkpoint.")]
    public float overshootDistance = 15f;

    private NPCMovement npcMovement;
    private CheckpointManager cpManager;

    void Start()
    {
        npcMovement = GetComponent<NPCMovement>();
        cpManager = CheckpointManager.Instance;

        if (cpManager == null)
        {
            Debug.LogError($"[RacerProgress] {gameObject.name}: No CheckpointManager found! " +
                           "Make sure a GameObject with CheckpointManager exists in the scene.");
            return;
        }

        if (cpManager.checkpoints.Count == 0)
        {
            Debug.LogWarning($"[RacerProgress] {gameObject.name}: CheckpointManager has 0 checkpoints. " +
                             "Drag your checkpoint objects into the CheckpointManager's list.");
        }

        cpManager.RegisterRacer(this);

        // Point the racer at the first checkpoint
        SetGoalToNextCheckpoint();

        if (debugLogging)
        {
            Debug.Log($"[RacerProgress] {gameObject.name} initialized. " +
                      $"Goal: {(npcMovement.goal != null ? npcMovement.goal.name : "NULL")} " +
                      $"| Checkpoints in course: {cpManager.TotalCheckpoints}");
        }
    }

    /// <summary>
    /// Every physics frame, calculates a destination that always passes
    /// THROUGH the current checkpoint:
    /// 
    /// Far away: destination = current checkpoint (head straight to it)
    /// Approaching: destination extends PAST the checkpoint toward the next one
    /// At reach distance: passage registered, cycle resets for next checkpoint
    /// 
    /// Unlike Lerp blending (which places the destination between checkpoints
    /// and lets the NavMesh route around them), this pushes the destination
    /// to the far side of the current checkpoint. The racer MUST pass through
    /// it to reach the destination — no cutting corners.
    /// </summary>
    void FixedUpdate()
    {
        if (isFinished) return;
        if (cpManager == null) return;
        if (npcMovement == null) return;
        if (!RaceManager.Instance.raceHasStarted) return;

        // --- Get the checkpoint we need to pass through ---
        Transform currentCP = headingToFinishLine
            ? cpManager.GetStartFinishTransform()
            : cpManager.GetCheckpointTransform(nextCheckpointIndex);

        if (currentCP == null) return;

        // Measure distance to nearest surface of the checkpoint's collider
        float dist = GetDistanceToCollider(currentCP);

        // --- Register passage when close enough ---
        if (dist <= checkpointReachDistance)
        {
            if (headingToFinishLine)
            {
                OnCrossedStartFinish();
            }
            else
            {
                OnHitCheckpoint(nextCheckpointIndex);
            }
            return;
        }

        // --- Calculate the through-point destination ---
        Transform nextCP = cpManager.GetLookAheadTransform(
            nextCheckpointIndex, headingToFinishLine);

        if (nextCP == null)
        {
            // No next checkpoint — just head to current
            npcMovement.SetDestinationDirect(currentCP.position);
            return;
        }

        // Direction from current checkpoint toward the next one
        Vector3 throughDir = (nextCP.position - currentCP.position).normalized;

        // Blend factor: 0 when far, 1 when close
        float blendRange = blendStartDistance - checkpointReachDistance;
        float rawT = 1f - Mathf.Clamp01((dist - checkpointReachDistance) / blendRange);
        float t = Mathf.SmoothStep(0f, 1f, rawT);

        // Destination starts AT the current checkpoint and extends PAST it
        // toward the next one as the racer approaches.
        // t=0 (far):   destination = currentCP.position (aim right at it)
        // t=1 (close):  destination = currentCP.position + overshootDistance toward nextCP
        //
        // The racer must pass through/near the current checkpoint to reach
        // this point — the NavMesh cannot route around it.
        Vector3 blendedDestination = currentCP.position + throughDir * (t * overshootDistance);

        npcMovement.SetDestinationDirect(blendedDestination);
    }

    /// <summary>
    /// Returns the distance from this racer to the nearest surface of a
    /// checkpoint's collider. Uses Collider.ClosestPoint so the entire
    /// box volume counts, not just the center point.
    /// </summary>
    float GetDistanceToCollider(Transform checkpoint)
    {
        Collider col = checkpoint.GetComponent<Collider>();
        if (col != null)
        {
            Vector3 closestPoint = col.ClosestPoint(transform.position);
            return Vector3.Distance(transform.position, closestPoint);
        }
        return Vector3.Distance(transform.position, checkpoint.position);
    }

    /// <summary>
    /// Called by Checkpoint.OnTriggerEnter when the racer enters a checkpoint trigger.
    /// Only advances if this is the correct next checkpoint (prevents skipping).
    /// </summary>
    public void OnHitCheckpoint(int checkpointIndex)
    {
        if (isFinished) return;
        if (cpManager == null) return;

        // Must be the checkpoint we're looking for
        if (checkpointIndex != nextCheckpointIndex) return;

        // Don't accept checkpoints if we should be heading to the finish line
        if (headingToFinishLine) return;

        lastPassedCheckpoint = checkpointIndex;
        nextCheckpointIndex++;

        // Have we hit all checkpoints in this lap?
        if (nextCheckpointIndex >= cpManager.TotalCheckpoints)
        {
            // Now head for the Start/Finish to complete the lap
            headingToFinishLine = true;
            SetGoalToStartFinish();

            if (debugLogging)
            {
                Debug.Log($"[RacerProgress] {gameObject.name} hit final checkpoint {checkpointIndex}. " +
                          $"Heading to Start/Finish to complete lap {currentLap}.");
            }
        }
        else
        {
            SetGoalToNextCheckpoint();

            if (debugLogging)
            {
                Debug.Log($"[RacerProgress] {gameObject.name} passed checkpoint {checkpointIndex}. " +
                          $"Now heading to checkpoint {nextCheckpointIndex}.");
            }
        }

        SyncToNPCMovement();
    }

    /// <summary>
    /// Called by Checkpoint.OnTriggerEnter when the racer crosses the Start/Finish zone.
    /// </summary>
    public void OnCrossedStartFinish()
    {
        if (isFinished) return;
        if (cpManager == null) return;

        // First crossing at race start — begin lap 1
        if (currentLap == 0)
        {
            currentLap = 1;
            nextCheckpointIndex = 0;
            headingToFinishLine = false;
            lastPassedCheckpoint = -1;
            SetGoalToNextCheckpoint();
            SyncToNPCMovement();

            if (debugLogging)
            {
                Debug.Log($"[RacerProgress] {gameObject.name} crossed start line. Lap 1 begins.");
            }
            return;
        }

        // Must have hit all checkpoints before crossing counts as a lap
        if (!headingToFinishLine) return;

        // Complete the lap
        currentLap++;
        headingToFinishLine = false;
        nextCheckpointIndex = 0;
        lastPassedCheckpoint = -1;

        if (debugLogging)
        {
            Debug.Log($"[RacerProgress] {gameObject.name} completed a lap! Now on lap {currentLap}/{cpManager.totalLaps}.");
        }

        // Check if we've finished all laps
        if (currentLap > cpManager.totalLaps)
        {
            isFinished = true;
            npcMovement.isFinished = true;
            cpManager.RecordFinish(this);

            // Stop the racer
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (debugLogging)
            {
                Debug.Log($"[RacerProgress] {gameObject.name} FINISHED THE RACE!");
            }
        }
        else
        {
            // Start the next lap — head for checkpoint 0
            SetGoalToNextCheckpoint();
        }

        SyncToNPCMovement();
    }

    /// <summary>
    /// Returns the position where this racer should respawn based on
    /// the last checkpoint they passed. If they haven't passed any,
    /// respawns at the Start/Finish zone.
    /// </summary>
    public Vector3 GetRespawnPosition()
    {
        if (cpManager == null) return transform.position;
        return cpManager.GetRespawnPosition(lastPassedCheckpoint);
    }

    /// <summary>
    /// After respawning, re-point the goal at the correct next target.
    /// Called by NPCMovement after a respawn.
    /// </summary>
    public void OnRespawned()
    {
        if (headingToFinishLine)
        {
            SetGoalToStartFinish();
        }
        else
        {
            SetGoalToNextCheckpoint();
        }
    }

    // --- Internal helpers ---

    void SetGoalToNextCheckpoint()
    {
        if (cpManager == null || npcMovement == null) return;

        Transform target = cpManager.GetCheckpointTransform(nextCheckpointIndex);
        if (target != null)
        {
            npcMovement.goal = target;
            npcMovement.ForcePathRefresh();
        }
        else if (debugLogging)
        {
            Debug.LogWarning($"[RacerProgress] {gameObject.name}: Checkpoint {nextCheckpointIndex} " +
                             "not found in CheckpointManager!");
        }
    }

    void SetGoalToStartFinish()
    {
        if (cpManager == null || npcMovement == null) return;

        Transform target = cpManager.GetStartFinishTransform();
        if (target != null)
        {
            npcMovement.goal = target;
            npcMovement.ForcePathRefresh();
        }
        else if (debugLogging)
        {
            Debug.LogWarning($"[RacerProgress] {gameObject.name}: StartFinishZone not set " +
                             "in CheckpointManager!");
        }
    }

    /// <summary>
    /// Keeps NPCMovement's race progress fields in sync so other systems
    /// (like a RaceManager UI or leaderboard) can read them from one place.
    /// </summary>
    void SyncToNPCMovement()
    {
        if (npcMovement == null) return;
        npcMovement.currentLap = currentLap;
        npcMovement.isFinished = isFinished;
    }

    /// <summary>
    /// Overall race progress as a single number for ranking.
    /// Higher = further in the race. Accounts for laps, checkpoints passed,
    /// and distance to the next checkpoint for real-time ordering.
    /// </summary>
    public float GetRaceProgress()
    {
        if (cpManager == null) return 0f;

        int totalCPs = cpManager.TotalCheckpoints;
        // Each lap is worth (totalCPs + 1) points (checkpoints + finish line crossing)
        float lapProgress = (currentLap - 1) * (totalCPs + 1);
        float checkpointProgress = nextCheckpointIndex;

        // Add fractional progress toward the next checkpoint
        float fractionToNext = 0f;
        if (npcMovement != null && npcMovement.goal != null)
        {
            float dist = Vector3.Distance(transform.position, npcMovement.goal.position);
            // Approximate: closer = higher progress (capped at 1)
            fractionToNext = Mathf.Clamp01(1f - (dist / 50f));
        }

        return lapProgress + checkpointProgress + fractionToNext;
    }

    /// <summary>
    /// Draws a line from the racer to their current goal in the Scene view,
    /// plus the checkpoint index — makes it easy to visually verify
    /// that each racer is heading to the right place.
    /// </summary>
    void OnDrawGizmos()
    {
        if (npcMovement == null || npcMovement.goal == null) return;

        // Green line = heading to checkpoint, Yellow = heading to finish
        Gizmos.color = headingToFinishLine ? Color.yellow : Color.green;
        Gizmos.DrawLine(transform.position, npcMovement.goal.position);

#if UNITY_EDITOR
        // Show checkpoint index above the racer
        string label = headingToFinishLine
            ? $"→ FINISH (Lap {currentLap})"
            : $"→ CP {nextCheckpointIndex} (Lap {currentLap})";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, label);
#endif
    }
}