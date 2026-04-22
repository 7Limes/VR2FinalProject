using UnityEngine;

public class SpeedModifierTile : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("1.5 = 50% faster, 0.5 = 50% slower")]
    public float speedMultiplier = 1.5f;

    // We store the original speed so we don't permanently break the NPC
    private System.Collections.Generic.Dictionary<NPCMovement, float> originalSpeeds = new System.Collections.Generic.Dictionary<NPCMovement, float>();

    void OnTriggerEnter(Collider other)
    {
        NPCMovement npc = other.GetComponent<NPCMovement>();

        if (npc != null && !originalSpeeds.ContainsKey(npc))
        {
            // Store the speed they had before entering the tile
            originalSpeeds[npc] = npc.maxSpeed;

            // Apply the multiplier
            npc.maxSpeed *= speedMultiplier;
        }
    }

    void OnTriggerExit(Collider other)
    {
        NPCMovement npc = other.GetComponent<NPCMovement>();

        if (npc != null && originalSpeeds.ContainsKey(npc))
        {
            // Reset to the speed they had before entering
            npc.maxSpeed = originalSpeeds[npc];

            // Clean up the dictionary
            originalSpeeds.Remove(npc);
        }
    }
}