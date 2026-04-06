using UnityEngine;

public class GoalLine : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // check if the object crossing the line has an NPCMovement script
        if (other.GetComponent<NPCMovement>() != null)
        {
            // send the name of the gameobject to the leaderboard
            RaceManager.Instance.RecordFinisher(other.gameObject.name);

            // optional: you could add logic here to make the npc cheer or stop moving
        }
    }
}