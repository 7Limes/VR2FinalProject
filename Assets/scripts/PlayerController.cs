using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("NPC Spawning")]
    public GameObject npcPrefab;
    public GameObject championPrefab; // <-- Add this slot for your new Champion Prefab!
    public int numberOfNPCs = 12;
    public Transform npcSpawnCenter;
    public Transform goalPoint;

    [Header("Obstacle Spawning")]
    public GameObject obstaclePrefab;
    public float dropHeight = 15f;

    void Start()
    {
        SpawnNPCs();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            DropObstacleAtMouse();
        }

        if (Input.GetMouseButtonDown(1))
        {
            MoveGoalToMouse();
        }
    }

    void SpawnNPCs()
    {
        for (int i = 0; i < numberOfNPCs; i++)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
            Vector3 spawnPos = npcSpawnCenter.position + randomOffset;

            GameObject prefabToSpawn;

            // If it's the very first NPC we are spawning, make it the Champion
            if (i == 0 && championPrefab != null)
            {
                prefabToSpawn = championPrefab;
            }
            else
            {
                prefabToSpawn = npcPrefab;
            }

            GameObject newNPC = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            if (i == 0 && championPrefab != null)
            {
                newNPC.name = "Champion";
            }
            else
            {
                newNPC.name = "Racer " + i;
            }

            NPCMovement movementScript = newNPC.GetComponent<NPCMovement>();
            if (movementScript != null)
            {
                movementScript.goal = goalPoint;
            }
        }
    }

    void DropObstacleAtMouse()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Checking for "Ground" or "Track" based on your previous setup
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Ground", "Track")))
        {
            Vector3 spawnPosition = hit.point + (Vector3.up * dropHeight);
            Instantiate(obstaclePrefab, spawnPosition, Random.rotation);
        }
    }

    void MoveGoalToMouse()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Move the goal to where the player right-clicks
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Ground", "Track")))
        {
            goalPoint.position = hit.point;
        }
    }
}