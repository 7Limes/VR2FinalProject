using UnityEngine;

public class GoalLine : MonoBehaviour
{
    // Prevent NPCs from triggering the line multiple times in one second
    // (Helps if they get stuck or jitter on the line)
    private float cooldown = 1.5f;

    void OnTriggerEnter(Collider other)
    {
        NPCMovement racer = other.GetComponent<NPCMovement>();

        if (racer != null && !racer.isFinished)
        {
            Color racerColor = Color.white;
            MeshRenderer renderer = other.GetComponentInChildren<MeshRenderer>();
            if (renderer != null) racerColor = renderer.material.color;

            // Tell the manager this racer crossed the line
            RaceManager.Instance.PassCheckPoint(racer, racerColor);
        }
    }
}