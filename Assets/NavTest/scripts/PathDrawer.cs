using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent), typeof(LineRenderer))]
public class PathDrawer : MonoBehaviour
{
    private NavMeshAgent agent;
    private LineRenderer lineRenderer;

    [Header("Line Settings")]
    public int smoothingIterations = 3;
    public float heightOffset = 0.2f;

    [Header("Distance & Fading")]
    [Tooltip("How many meters ahead the line should be drawn.")]
    public float maxDrawDistance = 5f;

    [Header("Update Throttling")]
    [Tooltip("How often (seconds) to rebuild the drawn path. Lower = smoother, higher = cheaper. 0 = every frame.")]
    public float updateInterval = 0.05f;

    // Reusable buffers — allocated once, reused every frame to avoid GC spikes in VR
    private List<Vector3> bufferA;
    private List<Vector3> bufferB;
    private List<Vector3> truncatedPoints;
    private Vector3[] positionScratch;
    private float updateTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;

        bufferA = new List<Vector3>(64);
        bufferB = new List<Vector3>(64);
        truncatedPoints = new List<Vector3>(64);
        positionScratch = new Vector3[0];

        SetupFadeGradient();
    }

    void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer > 0f) return;
        updateTimer = updateInterval;

        if (agent.hasPath && agent.path.corners.Length > 1)
        {
            lineRenderer.enabled = true;
            DrawCurvedPath();
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    void DrawCurvedPath()
    {
        Vector3[] corners = agent.path.corners;

        // Seed bufferA with the raw path corners (no per-frame List allocation)
        bufferA.Clear();
        for (int i = 0; i < corners.Length; i++) bufferA.Add(corners[i]);

        // Ping-pong smoothing between two pre-allocated buffers
        List<Vector3> src = bufferA;
        List<Vector3> dst = bufferB;
        for (int i = 0; i < smoothingIterations; i++)
        {
            SmoothCurveInto(src, dst);
            List<Vector3> swap = src; src = dst; dst = swap;
        }

        // Truncate to maxDrawDistance + apply height offset
        truncatedPoints.Clear();
        if (src.Count > 0)
        {
            truncatedPoints.Add(src[0] + Vector3.up * heightOffset);
        }

        float currentDistance = 0f;
        for (int i = 1; i < src.Count; i++)
        {
            Vector3 previousPoint = src[i - 1];
            Vector3 currentPoint = src[i];

            float segmentDistance = Vector3.Distance(previousPoint, currentPoint);

            if (currentDistance + segmentDistance > maxDrawDistance)
            {
                float remainingDistance = maxDrawDistance - currentDistance;
                Vector3 finalPoint = previousPoint + (currentPoint - previousPoint).normalized * remainingDistance;
                truncatedPoints.Add(finalPoint + Vector3.up * heightOffset);
                break;
            }

            currentDistance += segmentDistance;
            truncatedPoints.Add(currentPoint + Vector3.up * heightOffset);
        }

        // Reuse the positions array if it's the right size (avoids ToArray() allocation)
        int count = truncatedPoints.Count;
        if (positionScratch.Length != count)
        {
            positionScratch = new Vector3[count];
        }
        for (int i = 0; i < count; i++) positionScratch[i] = truncatedPoints[i];

        lineRenderer.positionCount = count;
        lineRenderer.SetPositions(positionScratch);
    }

    void SmoothCurveInto(List<Vector3> points, List<Vector3> output)
    {
        output.Clear();
        if (points.Count == 0) return;

        output.Add(points[0]);
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            output.Add(Vector3.Lerp(p0, p1, 0.25f));
            output.Add(Vector3.Lerp(p0, p1, 0.75f));
        }
        output.Add(points[points.Count - 1]);
    }

    void SetupFadeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        lineRenderer.colorGradient = gradient;
    }
}
