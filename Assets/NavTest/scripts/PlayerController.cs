using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("NPC Spawning")]
    public GameObject npcPrefab;
    public GameObject championPrefab;
    public int numberOfNPCs = 12;
    public Transform npcSpawnCenter;
    public Transform goalPoint;
    [Tooltip("Space between each racer in the starting grid")]
    public float spawnSpacing = 2f;
    [Tooltip("How many racers per row in the starting grid")]
    public int spawnColumns = 4;

    [Header("Appearance Settings")]
    public List<Material> racerMaterials;
    private List<Material> availableMaterialsPool;

    [Header("Obstacle Spawning")]
    public GameObject obstaclePrefab;
    public float dropHeight = 15f;

    [Header("Rolling Physics (applied to all spawned racers)")]
    [Tooltip("Base rolling force for normal racers")]
    public float baseRollForce = 14f;
    [Tooltip("Max velocity magnitude for racers")]
    public float baseMaxSpeed = 10f;
    [Tooltip("Champion gets this multiplier on force and max speed")]
    public float championSpeedMultiplier = 1.25f;
    [Tooltip("Rigidbody mass for each racer ball")]
    public float racerMass = 1f;
    [Tooltip("Rigidbody angular drag (higher = balls stop spinning sooner)")]
    public float racerAngularDrag = 2f;
    [Tooltip("Rigidbody linear drag")]
    public float racerLinearDrag = 0.5f;
    [Tooltip("Physics material dynamic friction")]
    public float ballFriction = 0.15f;
    [Tooltip("Physics material bounciness")]
    public float ballBounciness = 0.3f;

    // Shared physics material — every racer reuses the same instance instead
    // of allocating a new one. Cheaper memory and consistent behavior.
    private PhysicsMaterial sharedBallMaterial;

    void Start()
    {
        availableMaterialsPool = new List<Material>(racerMaterials);

        sharedBallMaterial = new PhysicsMaterial("BallPhysics_Shared")
        {
            dynamicFriction = ballFriction,
            staticFriction = ballFriction * 1.2f,
            bounciness = ballBounciness,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Average
        };

        SpawnNPCs();
    }

    void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            DropObstacleAtMouse();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            MoveGoalToMouse();
        }
    }

    void SpawnNPCs()
    {
        // Calculate grid offset so the formation is centered on the spawn point
        int rows = Mathf.CeilToInt((float)numberOfNPCs / spawnColumns);
        float gridWidth = (spawnColumns - 1) * spawnSpacing;
        float gridDepth = (rows - 1) * spawnSpacing;

        for (int i = 0; i < numberOfNPCs; i++)
        {
            int col = i % spawnColumns;
            int row = i / spawnColumns;

            // Center the grid on the spawn point
            Vector3 gridOffset = new Vector3(
                col * spawnSpacing - gridWidth * 0.5f,
                0.5f,
                row * spawnSpacing - gridDepth * 0.5f
            );
            Vector3 spawnPos = npcSpawnCenter.position + gridOffset;

            GameObject prefabToSpawn;
            bool isChampion = false;

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
            newNPC.name = isChampion ? "Champion" : "Racer " + i;

            // --- Set up rolling ball physics on the spawned racer ---
            SetupBallPhysics(newNPC, isChampion);

            // --- Wire up the movement script ---
            NPCMovement movementScript = newNPC.GetComponent<NPCMovement>();
            if (movementScript == null)
            {
                movementScript = newNPC.AddComponent<NPCMovement>();
            }

            // Set the initial goal to the first checkpoint so the racer
            // pathfinds to the right place from frame 1.
            // RacerProgress will take over goal management from here.
            Transform initialGoal = goalPoint;
            if (CheckpointManager.Instance != null
                && CheckpointManager.Instance.checkpoints.Count > 0)
            {
                initialGoal = CheckpointManager.Instance.GetCheckpointTransform(0);
            }
            movementScript.goal = initialGoal;
            movementScript.rollForce = isChampion ? baseRollForce * championSpeedMultiplier : baseRollForce;
            movementScript.maxSpeed = isChampion ? baseMaxSpeed * championSpeedMultiplier : baseMaxSpeed;

            // --- Wire up checkpoint/lap tracking ---
            RacerProgress progress = newNPC.GetComponent<RacerProgress>();
            if (progress == null)
            {
                progress = newNPC.AddComponent<RacerProgress>();
            }

            // --- Wire up UFO-style cart facing + tilt ---
            CartVisualRotation visualRot = newNPC.GetComponent<CartVisualRotation>();
            if (visualRot == null)
            {
                visualRot = newNPC.AddComponent<CartVisualRotation>();
            }

            if (!isChampion)
            {
                AssignUniqueMaterial(newNPC);
            }
            else
            {
                // Champion keeps its own material but still needs a
                // RacerColorAssigner so RacerOutline can read the color
                RacerColorAssigner colorAssigner = newNPC.GetComponent<RacerColorAssigner>();
                if (colorAssigner == null)
                {
                    colorAssigner = newNPC.AddComponent<RacerColorAssigner>();
                }

                // Read the champion's existing material from its mesh
                MeshRenderer championRenderer = newNPC.GetComponentInChildren<MeshRenderer>();
                if (championRenderer != null && championRenderer.sharedMaterial != null)
                {
                    colorAssigner.assignedMaterial = championRenderer.sharedMaterial;
                }

                // Trigger outline generation
                RacerOutline outline = newNPC.GetComponent<RacerOutline>();
                if (outline != null)
                {
                    outline.GenerateOutline();
                }
            }
        }
    }

    /// <summary>
    /// Ensures the racer has a Rigidbody + SphereCollider configured for
    /// natural rolling ball movement (Monkey Ball style).
    /// </summary>
    void SetupBallPhysics(GameObject racer, bool isChampion)
    {
        // --- Rigidbody ---
        Rigidbody rb = racer.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = racer.AddComponent<Rigidbody>();
        }

        rb.mass = racerMass;
        rb.linearDamping = racerLinearDrag;
        rb.angularDamping = racerAngularDrag;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Freeze no axes � we want full rolling freedom
        rb.constraints = RigidbodyConstraints.None;

        // Reuse the shared physics material — avoids allocating one per racer
        Collider col = racer.GetComponent<Collider>();
        if (col != null)
        {
            col.sharedMaterial = sharedBallMaterial;
        }
    }

    void AssignUniqueMaterial(GameObject npc)
    {
        if (availableMaterialsPool.Count > 0)
        {
            int randomIndex = Random.Range(0, availableMaterialsPool.Count);
            Material selectedMaterial = availableMaterialsPool[randomIndex];

            // Use RacerColorAssigner if present, otherwise apply directly
            RacerColorAssigner colorAssigner = npc.GetComponent<RacerColorAssigner>();
            if (colorAssigner == null)
            {
                colorAssigner = npc.AddComponent<RacerColorAssigner>();
            }
            colorAssigner.ApplyMaterial(selectedMaterial);

            // Trigger outline generation if RacerOutline is on the prefab
            RacerOutline outline = npc.GetComponent<RacerOutline>();
            if (outline != null)
            {
                outline.GenerateOutline();
            }

            availableMaterialsPool.RemoveAt(randomIndex);
        }
    }

    void DropObstacleAtMouse()
    {
        if (Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Ground", "Track")))
        {
            Vector3 spawnPosition = hit.point + (Vector3.up * dropHeight);
            Instantiate(obstaclePrefab, spawnPosition, Random.rotation);
        }
    }

    void MoveGoalToMouse()
    {
        if (Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Ground", "Track")))
        {
            goalPoint.position = hit.point;
        }
    }
}