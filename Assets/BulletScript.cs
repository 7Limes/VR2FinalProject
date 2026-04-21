using UnityEngine;

public class LaserBullet : MonoBehaviour
{
    [Header("Settings")]
    public float slowFactor = 0.6f; // 40% of original speed
    public float slowDuration = 1.0f;

    void OnTriggerEnter(Collider other)
    {
        // Check if we hit the NPC
        NPCMovement npc = other.GetComponent<NPCMovement>();

        //if (npc != null)
        //{
            // Start the slowdown routine on the NPC
        //    npc.StartCoroutine(npc.ApplySlowdown(slowFactor, slowDuration));

            // Optional: Add impact visual/sound here
        //    Destroy(gameObject);
        //}
        //else if (!other.CompareTag("Player"))
        //{
            // Destroy if it hits a wall/floor
        //    Destroy(gameObject);
        //}
    }
}