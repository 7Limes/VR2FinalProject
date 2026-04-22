using UnityEngine;

/// <summary>
/// Creates an outline effect for a specific mesh (e.g. the racer's cylinder body)
/// by duplicating it, scaling it up slightly, and flipping its normals so only
/// the outer shell is visible — producing a solid colored border.
/// 
/// Reads the material from RacerColorAssigner if present, or uses a
/// manually assigned outline material.
/// 
/// SETUP:
/// 1. Add this script to the racer prefab.
/// 2. Set outlineTargetPath to the child mesh to outline (e.g. "character/Cylinder").
/// 3. Either add RacerColorAssigner (outline will use the same material
///    automatically) or drag a material into outlineMaterial manually.
/// 4. Adjust outlineThickness to taste (0.03-0.08 works well for most models).
/// 
/// Call GenerateOutline() to create/rebuild the outline at any time.
/// </summary>
public class RacerOutline : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Path to the child mesh that should get the outline " +
             "(e.g. 'character/Cylinder'). Uses transform.Find().")]
    public string outlineTargetPath = "character/Cylinder";

    [Header("Outline Settings")]
    [Tooltip("How much larger the outline meshes are than the original (in local scale units)")]
    public float outlineThickness = 0.05f;

    [Tooltip("Manual outline material. If empty, uses RacerColorAssigner's material.")]
    public Material outlineMaterial;

    [Tooltip("Rendering layer for outline objects (leave at 0 for default)")]
    public int outlineRenderQueue = 2999;

    // Track individual outline objects for cleanup
    private System.Collections.Generic.List<GameObject> outlineObjects = new System.Collections.Generic.List<GameObject>();

    void Start()
    {
        // Wait one frame so RacerColorAssigner has time to apply its material
        Invoke(nameof(GenerateOutline), 0f);
    }

    /// <summary>
    /// Creates the outline by duplicating the target mesh with flipped
    /// normals and expanding it slightly. Can be called again to rebuild.
    /// </summary>
    public void GenerateOutline()
    {
        // Clean up any existing outline objects
        foreach (var obj in outlineObjects)
        {
            if (obj != null) Destroy(obj);
        }
        outlineObjects.Clear();

        // --- Find the target mesh first ---
        Transform target = transform.Find(outlineTargetPath);

        // If direct path fails, search recursively by name
        // (handles cases where the script is on a child instead of root)
        if (target == null)
        {
            string targetName = outlineTargetPath;
            int lastSlash = outlineTargetPath.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                targetName = outlineTargetPath.Substring(lastSlash + 1);
            }
            target = FindChildRecursive(transform, targetName);
        }

        if (target == null)
        {
            Debug.LogWarning($"[RacerOutline] {gameObject.name}: Could not find " +
                             $"'{outlineTargetPath}'. Check the path in the Inspector.");
            return;
        }

        MeshFilter mf = target.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning($"[RacerOutline] {gameObject.name}: '{outlineTargetPath}' " +
                             "has no MeshFilter or mesh.");
            return;
        }

        // --- Resolve the material ---
        Material mat = outlineMaterial;

        // Check this object and all parents for RacerColorAssigner
        if (mat == null)
        {
            RacerColorAssigner colorAssigner = GetComponent<RacerColorAssigner>();
            if (colorAssigner == null)
            {
                colorAssigner = GetComponentInParent<RacerColorAssigner>();
            }

            if (colorAssigner != null && colorAssigner.assignedMaterial != null)
            {
                mat = colorAssigner.assignedMaterial;
            }
        }

        // Last resort: read the material already on the target mesh
        if (mat == null)
        {
            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                mat = mr.sharedMaterial;
            }
        }

        if (mat == null)
        {
            Debug.LogWarning($"[RacerOutline] {gameObject.name}: No outline material found. " +
                             "Assign one manually or add RacerColorAssigner.");
            return;
        }

        CreateOutlineMesh(mf, mat);
    }

    /// <summary>
    /// Creates a single outline mesh: duplicates the original mesh with
    /// flipped normals and flipped triangle winding so the inside faces
    /// become visible from outside. The mesh is slightly larger than the
    /// original, creating the outline border.
    /// </summary>
    void CreateOutlineMesh(MeshFilter sourceMF, Material mat)
    {
        Mesh sourceMesh = sourceMF.sharedMesh;

        // Duplicate and flip the mesh
        Mesh outlineMesh = new Mesh();
        outlineMesh.name = sourceMesh.name + "_outline";
        outlineMesh.vertices = sourceMesh.vertices;
        outlineMesh.uv = sourceMesh.uv;

        // Flip normals
        Vector3[] normals = sourceMesh.normals;
        Vector3[] flippedNormals = new Vector3[normals.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            flippedNormals[i] = -normals[i];
        }
        outlineMesh.normals = flippedNormals;

        // Reverse triangle winding order so back faces become front faces
        for (int sub = 0; sub < sourceMesh.subMeshCount; sub++)
        {
            int[] triangles = sourceMesh.GetTriangles(sub);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i];
                triangles[i] = triangles[i + 1];
                triangles[i + 1] = temp;
            }
            outlineMesh.SetTriangles(triangles, sub);
        }

        outlineMesh.RecalculateBounds();

        // Expand vertices along their normals to make the mesh slightly larger
        Vector3[] expandedVerts = sourceMesh.vertices;
        Vector3[] sourceNormals = sourceMesh.normals;
        for (int i = 0; i < expandedVerts.Length; i++)
        {
            expandedVerts[i] += sourceNormals[i] * outlineThickness;
        }
        outlineMesh.vertices = expandedVerts;

        // Create the outline as a child of the source mesh so it
        // moves, rotates, and scales with the character automatically
        GameObject outlineObj = new GameObject("outline_" + sourceMF.gameObject.name);
        outlineObj.transform.SetParent(sourceMF.transform, false);
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localRotation = Quaternion.identity;
        outlineObj.transform.localScale = Vector3.one;

        // Add mesh components
        MeshFilter outlineMF = outlineObj.AddComponent<MeshFilter>();
        outlineMF.mesh = outlineMesh;

        MeshRenderer outlineMR = outlineObj.AddComponent<MeshRenderer>();

        // Create a material instance for the outline
        Material outlineMat = new Material(mat);
        outlineMat.renderQueue = outlineRenderQueue;
        outlineMR.material = outlineMat;

        // Disable shadows on the outline
        outlineMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineMR.receiveShadows = false;

        // Track for cleanup
        outlineObjects.Add(outlineObj);
    }

    /// <summary>
    /// Updates the outline material color to match a new material.
    /// Call this if the racer's color changes at runtime.
    /// </summary>
    public void UpdateOutlineColor(Material newMat)
    {
        foreach (var obj in outlineObjects)
        {
            if (obj == null) continue;
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material.color = newMat.color;
            }
        }
    }

    /// <summary>
    /// Searches all children recursively for a child with the given name.
    /// </summary>
    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}