using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Assigns a random material from a pool to all icosphere children on this racer.
/// Place this on the racer prefab. The material pool can be set per-prefab in
/// the Inspector or overridden at runtime by PlayerController.
/// 
/// Hierarchy expected: character/cart/Icosphere.001 through Icosphere.011
/// 
/// Also exposes the assigned material so other scripts (like RacerOutline)
/// can read it.
/// </summary>
public class RacerColorAssigner : MonoBehaviour
{
    [Header("Material Pool")]
    [Tooltip("Drag all racer color materials here. One will be picked at random.")]
    public List<Material> materialPool;

    [Header("Icosphere Settings")]
    [Tooltip("Path from this object to the parent of the icospheres")]
    public string icospherePath = "character/cart";
    [Tooltip("Base name of the icosphere objects (before the .XXX number)")]
    public string icosphereBaseName = "Icosphere";
    [Tooltip("First icosphere number")]
    public int firstIndex = 1;
    [Tooltip("Last icosphere number")]
    public int lastIndex = 11;

    [Header("Result (read-only)")]
    [Tooltip("The material that was assigned to this racer")]
    public Material assignedMaterial;

    // Cache of resolved icosphere renderers — avoids doing transform.Find()
    // across 11 slash-separated paths every time ApplyMaterial runs.
    private MeshRenderer[] cachedIcosphereRenderers;

    /// <summary>
    /// Call this to assign a random material from the pool.
    /// Called automatically in Start, or can be called externally
    /// (e.g. by PlayerController) with a specific material.
    /// </summary>
    public void AssignRandomMaterial()
    {
        if (materialPool == null || materialPool.Count == 0) return;

        Material selected = materialPool[Random.Range(0, materialPool.Count)];
        ApplyMaterial(selected);
    }

    /// <summary>
    /// Assigns a specific material to all icospheres.
    /// Used by PlayerController to assign from a unique pool.
    /// Caches renderer references on first call so re-coloring is O(n).
    /// </summary>
    public void ApplyMaterial(Material mat)
    {
        assignedMaterial = mat;

        if (cachedIcosphereRenderers == null)
        {
            ResolveIcosphereRenderers();
        }

        if (cachedIcosphereRenderers == null) return;

        for (int i = 0; i < cachedIcosphereRenderers.Length; i++)
        {
            MeshRenderer r = cachedIcosphereRenderers[i];
            if (r != null) r.material = mat;
        }
    }

    void ResolveIcosphereRenderers()
    {
        int count = lastIndex - firstIndex + 1;
        if (count <= 0)
        {
            cachedIcosphereRenderers = new MeshRenderer[0];
            return;
        }

        var list = new List<MeshRenderer>(count);
        for (int i = firstIndex; i <= lastIndex; i++)
        {
            string sphereName = icospherePath + "/" + icosphereBaseName + "." + i.ToString("D3");
            Transform sphere = transform.Find(sphereName);
            if (sphere != null)
            {
                MeshRenderer r = sphere.GetComponent<MeshRenderer>();
                if (r != null) list.Add(r);
            }
        }
        cachedIcosphereRenderers = list.ToArray();
    }

    /// <summary>
    /// Returns the color of the assigned material, useful for UI/results.
    /// </summary>
    public Color GetRacerColor()
    {
        if (assignedMaterial != null)
        {
            return assignedMaterial.color;
        }
        return Color.white;
    }
}