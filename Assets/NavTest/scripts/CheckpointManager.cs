using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines the race course layout. Place this on a single GameObject in the scene.
/// 
/// SETUP:
/// 1. Create empty GameObjects for each checkpoint around the track.
///    Add a BoxCollider (Is Trigger = true) and a Checkpoint script to each.
/// 2. Create one more for the Start/Finish zone (also BoxCollider trigger + Checkpoint).
/// 3. Drag them into this manager's "checkpoints" list IN ORDER of the race route.
/// 4. Drag the Start/Finish into the "startFinishZone" slot.
/// 5. Set totalLaps.
/// 
/// The race flow for each racer:
///   Start → Checkpoint[0] → Checkpoint[1] → ... → Checkpoint[N-1] → StartFinish (lap++)
///   Repeat for totalLaps. After final lap crossing StartFinish → racer is finished.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    [Header("Course Layout")]
    [Tooltip("Ordered list of checkpoints around the track (NOT including Start/Finish)")]
    public List<Checkpoint> checkpoints = new List<Checkpoint>();

    [Tooltip("The Start/Finish zone — racers cross this to complete a lap")]
    public Checkpoint startFinishZone;

    [Header("Race Settings")]
    public int totalLaps = 3;

    [Header("Race Results (read-only at runtime)")]
    public List<RacerProgress> allRacers = new List<RacerProgress>();
    public List<RacerProgress> finishOrder = new List<RacerProgress>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // --- Validate setup ---
        if (checkpoints.Count == 0)
        {
            Debug.LogError("[CheckpointManager] No checkpoints assigned! " +
                           "Drag your Checkpoint objects into the 'checkpoints' list in the Inspector.");
        }

        if (startFinishZone == null)
        {
            Debug.LogError("[CheckpointManager] No Start/Finish zone assigned! " +
                           "Create a trigger zone and drag it into 'startFinishZone' in the Inspector.");
        }

        // Check for null entries in the list
        for (int i = checkpoints.Count - 1; i >= 0; i--)
        {
            if (checkpoints[i] == null)
            {
                Debug.LogError($"[CheckpointManager] Checkpoint slot {i} is empty (null)! " +
                               "Remove it or assign a Checkpoint.");
                checkpoints.RemoveAt(i);
            }
        }

        // Assign indices to each checkpoint so they know their order
        for (int i = 0; i < checkpoints.Count; i++)
        {
            checkpoints[i].checkpointIndex = i;
            checkpoints[i].isStartFinish = false;

            // Verify the checkpoint has a trigger collider
            Collider col = checkpoints[i].GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogError($"[CheckpointManager] Checkpoint '{checkpoints[i].gameObject.name}' " +
                               "has no Collider! Add a BoxCollider and set Is Trigger = true.");
            }
            else if (!col.isTrigger)
            {
                Debug.LogWarning($"[CheckpointManager] Checkpoint '{checkpoints[i].gameObject.name}' " +
                                 "Collider is not a trigger — fixing automatically.");
                col.isTrigger = true;
            }
        }

        if (startFinishZone != null)
        {
            startFinishZone.checkpointIndex = -1;
            startFinishZone.isStartFinish = true;
        }

        Debug.Log($"[CheckpointManager] Setup complete: {checkpoints.Count} checkpoints, " +
                  $"{totalLaps} laps, Start/Finish: {(startFinishZone != null ? startFinishZone.gameObject.name : "MISSING")}");
    }

    /// <summary>
    /// Called by RacerProgress when it spawns. Keeps a master list of all racers.
    /// </summary>
    public void RegisterRacer(RacerProgress racer)
    {
        if (!allRacers.Contains(racer))
        {
            allRacers.Add(racer);
        }
    }

    /// <summary>
    /// Called by RacerProgress when a racer completes all laps.
    /// </summary>
    public void RecordFinish(RacerProgress racer)
    {
        if (!finishOrder.Contains(racer))
        {
            finishOrder.Add(racer);
            int place = finishOrder.Count;
            Debug.Log($"{racer.gameObject.name} finished in place #{place}!");
        }
    }

    /// <summary>
    /// Returns the total number of checkpoints (not counting Start/Finish).
    /// </summary>
    public int TotalCheckpoints => checkpoints.Count;

    /// <summary>
    /// Returns the Transform of a checkpoint by index.
    /// </summary>
    public Transform GetCheckpointTransform(int index)
    {
        if (index >= 0 && index < checkpoints.Count)
        {
            return checkpoints[index].transform;
        }
        return null;
    }

    /// <summary>
    /// Returns the Collider of a checkpoint by index (for ClosestPoint checks).
    /// </summary>
    public Collider GetCheckpointCollider(int index)
    {
        if (index >= 0 && index < checkpoints.Count)
        {
            return checkpoints[index].GetComponent<Collider>();
        }
        return null;
    }

    /// <summary>
    /// Returns the Transform of the checkpoint AFTER the given index.
    /// Used for look-ahead pathfinding so racers flow through checkpoints
    /// instead of stopping at each one.
    /// </summary>
    public Transform GetLookAheadTransform(int currentCheckpointIndex, bool isHeadingToFinish)
    {
        if (isHeadingToFinish)
        {
            // Currently heading to start/finish — look ahead to CP0 of next lap
            return GetCheckpointTransform(0);
        }

        int nextIndex = currentCheckpointIndex + 1;
        if (nextIndex >= checkpoints.Count)
        {
            // At the last checkpoint — look ahead to start/finish
            return GetStartFinishTransform();
        }

        return GetCheckpointTransform(nextIndex);
    }

    /// <summary>
    /// Returns the nearest respawn point for a given checkpoint index.
    /// Uses the checkpoint's custom respawn point if set, otherwise the checkpoint itself.
    /// </summary>
    public Vector3 GetRespawnPosition(int checkpointIndex)
    {
        if (checkpointIndex < 0 || checkpointIndex >= checkpoints.Count)
        {
            // Haven't passed any checkpoint yet — respawn at start/finish
            if (startFinishZone != null)
            {
                return startFinishZone.GetRespawnPosition();
            }
            return Vector3.zero;
        }

        return checkpoints[checkpointIndex].GetRespawnPosition();
    }

    /// <summary>
    /// Returns the Start/Finish zone's transform (used as the goal
    /// after the last checkpoint in each lap).
    /// </summary>
    public Transform GetStartFinishTransform()
    {
        return startFinishZone != null ? startFinishZone.transform : null;
    }
}