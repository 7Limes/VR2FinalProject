using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives the visual rotation of the "character/cart" child so the cart:
///   1. Faces the NavMesh pathfinding direction (smoothed yaw).
///   2. Tilts up to maxTiltAngle° in the direction of motion (forward, back,
///      diagonal) — UFO-style "lean into motion" with smooth transitions.
///
/// The racer's root Rigidbody rolls freely, which would normally spin the
/// child visuals. This script writes cart.rotation in WORLD space every
/// LateUpdate, which cleanly overrides any rotation inherited from the parent.
///
/// Auto-added by PlayerController at spawn time (same pattern as RacerProgress
/// and RacerColorAssigner). Can also be placed on the racer prefab manually.
/// </summary>
public class CartVisualRotation : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Name of the visual-root child to rotate. Tries this name first, then searches " +
             "recursively. 'character' rotates the whole visual subtree (cart + cylinder + outlines). " +
             "'cart' only rotates the icospheres.")]
    public string cartPath = "character";

    [Tooltip("Extra yaw rotation added on top of the facing direction. " +
             "Set to 180 if the model's front faces -Z (most common). " +
             "Override per-prefab if the Champion needs a different value.")]
    public float yawOffset = 180f;

    [Header("Tilt")]
    [Tooltip("Maximum pitch/roll angle the cart can tilt (degrees).")]
    public float maxTiltAngle = 15f;

    [Tooltip("Speed at which tilt reaches its maximum. Below this, tilt scales linearly. " +
             "Leave at 0 to auto-read from NPCMovement.maxSpeed.")]
    public float referenceSpeed = 0f;

    [Header("Smoothing")]
    [Tooltip("Approx. time (seconds) the yaw takes to converge on a new facing direction.")]
    [Range(0.01f, 1f)]
    public float yawSmoothTime = 0.12f;

    [Tooltip("Approx. time (seconds) the tilt takes to ease toward its new target.")]
    [Range(0.01f, 1f)]
    public float tiltSmoothTime = 0.2f;

    [Header("Idle Behavior")]
    [Tooltip("Below this speed (world m/s) the cart holds its last facing and levels out.")]
    public float minMoveSpeed = 0.3f;

    [Header("Debug")]
    [Tooltip("Log the current yaw target and tilt values every second so you can confirm the script is running.")]
    public bool debugLogging = false;
    private float debugTimer;

    // Cached references — resolved once in Start
    private Transform cart;
    private NPCMovement npcMovement;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private RaceManager raceManager;

    // Smoothed state
    private Quaternion smoothedYaw = Quaternion.identity;
    private float currentPitch;
    private float currentRoll;
    private bool yawInitialized;

    void Start()
    {
        // 1. Try the configured path directly (e.g. "character")
        cart = transform.Find(cartPath);

        // 2. Recursive search by leaf name (handles different nesting depths)
        if (cart == null)
        {
            string leafName = cartPath.Contains("/")
                ? cartPath.Substring(cartPath.LastIndexOf('/') + 1)
                : cartPath;
            cart = FindChildRecursive(transform, leafName);
        }

        // 3. If the configured path is "character" but the prefab only has "cart",
        //    fall back to "cart" so the script still works on simpler prefabs.
        if (cart == null && cartPath != "cart")
        {
            cart = transform.Find("cart");
            if (cart == null) cart = FindChildRecursive(transform, "cart");
        }

        if (cart == null)
        {
            Debug.LogWarning($"[CartVisualRotation] {gameObject.name}: could not find " +
                             $"'{cartPath}' (or 'cart') anywhere in the hierarchy. " +
                             $"Direct children: {GetChildNames(transform)}. Visual rotation disabled.");
            enabled = false;
            return;
        }

        Debug.Log($"[CartVisualRotation] {gameObject.name}: rotating '{GetPath(cart)}'. Ready.");

        npcMovement = GetComponent<NPCMovement>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // Seed the smoothed yaw with whatever the cart is already at so the
        // cart doesn't snap on the first frame.
        smoothedYaw = cart.rotation;
        yawInitialized = true;
    }

    void LateUpdate()
    {
        if (cart == null) return;

        // Pre-race: hold level and facing its spawn orientation.
        if (raceManager == null) raceManager = RaceManager.Instance;
        if (raceManager == null || !raceManager.raceHasStarted)
        {
            // Ease any residual tilt back to zero so the starting grid looks tidy.
            DampTiltTowardZero();
            ApplyRotation();
            return;
        }

        // ---- YAW: face the NavMesh's desired path direction ----
        Vector3 pathDir = Vector3.zero;
        if (agent != null && agent.isOnNavMesh)
        {
            pathDir = agent.desiredVelocity;
        }

        // Fall back to physical velocity if the NavMesh hasn't produced a direction
        Vector3 physVel = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 facingSource = pathDir.sqrMagnitude > 0.01f ? pathDir : physVel;
        facingSource.y = 0f;

        // motionYaw = "the direction the racer is actually travelling" in world space.
        // Used for tilt math regardless of yawOffset so forward velocity always means
        // forward lean even when the mesh is rotated 180°.
        Quaternion motionYaw = smoothedYaw;

        if (facingSource.sqrMagnitude > minMoveSpeed * minMoveSpeed)
        {
            // Pure motion direction (no offset) — used only for tilt computation below
            motionYaw = Quaternion.LookRotation(facingSource.normalized, Vector3.up);

            // Display yaw = motion direction + per-model offset.
            // Common case: yawOffset = 180 when the mesh's front faces -Z.
            Quaternion targetDisplayYaw = motionYaw * Quaternion.Euler(0f, yawOffset, 0f);

            float yawT = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, yawSmoothTime));
            smoothedYaw = yawInitialized
                ? Quaternion.Slerp(smoothedYaw, targetDisplayYaw, yawT)
                : targetDisplayYaw;
            yawInitialized = true;
        }
        // else: hold the last yaw (prevents spinning when stationary)

        // ---- TILT: lean into direction of motion in the cart's local frame ----
        float refSpeed = referenceSpeed > 0.01f
            ? referenceSpeed
            : (npcMovement != null && npcMovement.maxSpeed > 0.01f ? npcMovement.maxSpeed : 10f);

        float targetPitch = 0f;
        float targetRoll  = 0f;

        if (physVel.sqrMagnitude > minMoveSpeed * minMoveSpeed)
        {
            // Project velocity into the MOTION yaw frame (not the display yaw) so the
            // tilt direction is always correct regardless of yawOffset.
            Vector3 localVel = Quaternion.Inverse(motionYaw) * new Vector3(physVel.x, 0f, physVel.z);

            float forwardRatio = Mathf.Clamp(localVel.z / refSpeed, -1f, 1f);
            float sideRatio    = Mathf.Clamp(localVel.x / refSpeed, -1f, 1f);

            // +X Euler = nose tilts forward (down) in Unity.
            // +Z Euler = left side dips (right banking) — negate for "lean into turn".
            targetPitch = forwardRatio * maxTiltAngle;   // forward motion → nose dips forward
            targetRoll  = -sideRatio   * maxTiltAngle;   // rightward motion → left side dips (bank right)
        }

        float tiltT = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, tiltSmoothTime));
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, tiltT);
        currentRoll  = Mathf.Lerp(currentRoll,  targetRoll,  tiltT);

        if (debugLogging)
        {
            debugTimer -= Time.deltaTime;
            if (debugTimer <= 0f)
            {
                debugTimer = 1f;
                Vector3 euler = smoothedYaw.eulerAngles;
                Debug.Log($"[CartVisualRotation] {gameObject.name} | " +
                          $"yaw={euler.y:F1}° pitch={currentPitch:F1}° roll={currentRoll:F1}° | " +
                          $"speed={physVel.magnitude:F2} pathDir={pathDir.magnitude:F2}");
            }
        }

        ApplyRotation();
    }

    void DampTiltTowardZero()
    {
        float tiltT = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, tiltSmoothTime));
        currentPitch = Mathf.Lerp(currentPitch, 0f, tiltT);
        currentRoll  = Mathf.Lerp(currentRoll,  0f, tiltT);
    }

    void ApplyRotation()
    {
        // yaw * tilt — tilt is applied in the cart's own local frame after facing.
        cart.rotation = smoothedYaw * Quaternion.Euler(currentPitch, 0f, currentRoll);
    }

    // --- Hierarchy helpers ---

    static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    static string GetChildNames(Transform t)
    {
        var names = new System.Text.StringBuilder();
        foreach (Transform child in t)
        {
            names.Append(child.name);
            names.Append(' ');
        }
        return names.Length > 0 ? names.ToString() : "(none)";
    }
}
