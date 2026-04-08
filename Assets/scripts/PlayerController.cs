using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    [Header("NPC Spawning")]
    public GameObject npcPrefab;
    public GameObject championPrefab;
    public int numberOfNPCs = 12;
    public Transform npcSpawnCenter;
    public Transform goalPoint;

    [Header("Appearance Settings")]
    [Tooltip("Drag your different colored materials here. Make sure you have at least as many materials as regular NPCs!")]
    public List<Material> racerMaterials;

    // This is our "deck of cards" we will draw from and discard
    private List<Material> availableMaterialsPool;

    [Header("Obstacle Spawning")]
    public GameObject obstaclePrefab;
    public float dropHeight = 15f;

    void Start()
    {
        // Copy the materials into our temporary pool so we don't delete the originals from the Inspector permanently
        availableMaterialsPool = new List<Material>(racerMaterials);

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
            bool isChampion = false;

            // Check if it's the Champion
            if (i == 0 && championPrefab != null)
            {
                prefabToSpawn = championPrefab;
                isChampion = true;
            }
            else
            {
                prefabToSpawn = npcPrefab;
            }

            GameObject newNPC = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            // Set clean names for the Leaderboard
            newNPC.name = isChampion ? "Champion" : "Racer " + i;

            NPCMovement movementScript = newNPC.GetComponent<NPCMovement>();
            if (movementScript != null)
            {
                movementScript.goal = goalPoint;
            }

            // --- NEW: Assign Unique Random Material ---
            // We only randomize regular racers so the Champion keeps its special color
            if (!isChampion)
            {
                AssignUniqueMaterial(newNPC);
            }
        }
    }

    void AssignUniqueMaterial(GameObject npc)
    {
        // Make sure we still have colors left in the pool
        if (availableMaterialsPool.Count > 0)
        {
            // Pick a random index from whatever is left
            int randomIndex = Random.Range(0, availableMaterialsPool.Count);

            // Grab the material at that index
            Material selectedMaterial = availableMaterialsPool[randomIndex];

            // Apply it to the capsule's MeshRenderer
            MeshRenderer renderer = npc.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = selectedMaterial;
            }

            // Discard it from the pool so no one else can draw this color
            availableMaterialsPool.RemoveAt(randomIndex);
        }
        else
        {
            // If you spawn 12 NPCs but only put 5 materials in the list, it will warn you and keep the default color
            Debug.LogWarning("Not enough materials in the pool for all NPCs! " + npc.name + " will use the default color.");
        }
    }

    void DropObstacleAtMouse()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

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

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Ground", "Track")))
        {
            goalPoint.position = hit.point;
        }
    }
}