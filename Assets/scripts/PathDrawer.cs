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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;

        SetupFadeGradient();
    }

    void Update()
    {
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
        List<Vector3> pointList = new List<Vector3>(corners);

        // 1. Smooth the path
        for (int i = 0; i < smoothingIterations; i++)
        {
            pointList = SmoothCurve(pointList);
        }

        // 2. Limit the distance and apply height offset
        List<Vector3> truncatedPoints = new List<Vector3>();
        if (pointList.Count > 0)
        {
            // Start with the very first point and add the height offset
            truncatedPoints.Add(pointList[0] + Vector3.up * heightOffset);
        }

        float currentDistance = 0f;

        for (int i = 1; i < pointList.Count; i++)
        {
            Vector3 previousPoint = pointList[i - 1];
            Vector3 currentPoint = pointList[i];

            float segmentDistance = Vector3.Distance(previousPoint, currentPoint);

            if (currentDistance + segmentDistance > maxDrawDistance)
            {
                // If this segment pushes us over the max distance, calculate exactly where to cut it off
                float remainingDistance = maxDrawDistance - currentDistance;
                Vector3 finalPoint = previousPoint + (currentPoint - previousPoint).normalized * remainingDistance;

                truncatedPoints.Add(finalPoint + Vector3.up * heightOffset);
                break; // Stop adding points entirely
            }
            else
            {
                // We are still under the limit, so add the point
                currentDistance += segmentDistance;
                truncatedPoints.Add(currentPoint + Vector3.up * heightOffset);
            }
        }

        // 3. Apply the final points to the Line Renderer
        lineRenderer.positionCount = truncatedPoints.Count;
        lineRenderer.SetPositions(truncatedPoints.ToArray());
    }

    List<Vector3> SmoothCurve(List<Vector3> points)
    {
        List<Vector3> smoothedPoints = new List<Vector3>();
        smoothedPoints.Add(points[0]);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];

            Vector3 q = Vector3.Lerp(p0, p1, 0.25f);
            Vector3 r = Vector3.Lerp(p0, p1, 0.75f);

            smoothedPoints.Add(q);
            smoothedPoints.Add(r);
        }

        smoothedPoints.Add(points[points.Count - 1]);
        return smoothedPoints;
    }

    void SetupFadeGradient()
    {
        // Creates a gradient that keeps the material's original color, but fades alpha from 1 to 0
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        lineRenderer.colorGradient = gradient;
    }
}