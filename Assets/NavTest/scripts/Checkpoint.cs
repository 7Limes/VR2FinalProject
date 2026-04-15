using UnityEngine;

/// <summary>
/// Place this on each checkpoint and the Start/Finish zone.
/// 
/// SETUP:
/// 1. Create an empty GameObject where you want the checkpoint.
/// 2. Add a BoxCollider, set Is Trigger = true, size it to cover the track width.
/// 3. Add this Checkpoint script.
/// 4. (Optional) Create a child empty GameObject as a respawn point offset
///    from the checkpoint (e.g. slightly before it on the track) and drag it
///    into the "respawnPoint" slot. If left empty, racers respawn at the
///    checkpoint's own position.
/// 5. Drag this into CheckpointManager's checkpoints list (in order)
///    or into the startFinishZone slot.
/// 
/// The checkpointIndex and isStartFinish fields are set automatically
/// by CheckpointManager at runtime — don't set them manually.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Set by CheckpointManager at runtime — do not edit")]
    [HideInInspector] public int checkpointIndex;
    [HideInInspector] public bool isStartFinish;

    [Header("Respawn Settings")]
    [Tooltip("Where racers respawn when they fall off after this checkpoint. " +
             "If empty, uses this checkpoint's position.")]
    public Transform respawnPoint;

    [Tooltip("Which direction the racer should face after respawning (Y rotation). " +
             "If a respawnPoint is set, uses its forward direction instead.")]
    public float respawnYRotation = 0f;

    void Start()
    {
        // Make sure the collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"Checkpoint '{gameObject.name}' collider was not a trigger — fixed automatically.");
        }
    }

    /// <summary>
    /// Fires every physics frame while a racer is inside this trigger.
    /// This ensures the checkpoint registers even if the racer was already
    /// inside the box when this became their next target (OnTriggerEnter
    /// would miss that case since the racer never "entered" — they were
    /// already there). RacerProgress guards against double-processing,
    /// so only the first valid frame actually advances progress.
    /// </summary>
    void OnTriggerStay(Collider other)
    {
        RacerProgress racer = other.GetComponent<RacerProgress>();
        if (racer == null)
        {
            racer = other.GetComponentInParent<RacerProgress>();
        }

        if (racer != null)
        {
            if (isStartFinish)
            {
                racer.OnCrossedStartFinish();
            }
            else
            {
                racer.OnHitCheckpoint(checkpointIndex);
            }
        }
    }

    /// <summary>
    /// Returns the position to respawn a racer at.
    /// </summary>
    public Vector3 GetRespawnPosition()
    {
        if (respawnPoint != null)
        {
            return respawnPoint.position;
        }
        return transform.position;
    }

    /// <summary>
    /// Returns the rotation to respawn a racer with.
    /// </summary>
    public Quaternion GetRespawnRotation()
    {
        if (respawnPoint != null)
        {
            return respawnPoint.rotation;
        }
        return Quaternion.Euler(0f, respawnYRotation, 0f);
    }

    void OnDrawGizmos()
    {
        // Draw checkpoint markers in the editor for easy visualization
        if (isStartFinish)
        {
            Gizmos.color = Color.green;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }

        Gizmos.DrawWireSphere(transform.position, 1f);

        if (respawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(respawnPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, respawnPoint.position);
        }
    }
}