using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class SplineRailInstantiator : MonoBehaviour
{
    [SerializeField] GameObject forwardRail;
    [SerializeField] GameObject turnRail;

    [SerializeField] List<SplineContainer> splineList;

    bool drawingSplines;
    SplineContainer currentSpline;
    Vector3Int startDrawPos;

    void OnEnable()
    {
        SelectableGrid.OnDrawConveyorsStart += StartDrawSpline;
        SelectableGrid.OnDrawConveyorsEnd += StopDrawSpline;
    }

    void OnDisable()
    {
        SelectableGrid.OnDrawConveyorsStart -= StartDrawSpline;
        SelectableGrid.OnDrawConveyorsEnd -= StopDrawSpline;
    }

    void Update()
    {
        if (drawingSplines) ExecuteDrawSplines();
    }

    void StartDrawSpline()
    {
        startDrawPos = MouseScreenToWorld();
        if (startDrawPos == Vector3Int.one * int.MinValue)
        {
            drawingSplines = false;
            return;
        }

        float3 start = new(startDrawPos.x, startDrawPos.y, startDrawPos.z);

        // Looking if extending from an existing spline
        currentSpline = null;
        foreach (SplineContainer spline in splineList)
        {
            if (IsInsideSpline(start, spline.Spline))
            {
                currentSpline = spline;
                Debug.Log("extending existing spline");
            }
        }

        // If not starting draw over existing rails, create new spline
        if (currentSpline == null)
        {
            currentSpline = new();
            splineList.Add(currentSpline);
            Debug.Log("creating new spline");
        }

        drawingSplines = true;
    }

    void ExecuteDrawSplines()
    {
        // Get rails direction with dot product

        // Show tile highlights for all rails about to spawn
    }

    void StopDrawSpline()
    {
        if (!drawingSplines) return;

        // Hide tile highlights, draw rails

        drawingSplines = false;
    }

    Vector3Int MouseScreenToWorld()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity);
        if (!hit.collider) return Vector3Int.one * int.MinValue;
        return Vector3Int.RoundToInt(hit.point);
    }

    public static bool IsInsideSpline(float3 point, Spline spline)
    {
        SplineUtility.GetNearestPoint(spline, point, out var splinePoint, out var t);
        spline.Evaluate(t, out _, out var tangent, out _);
    
        var cross = math.cross(math.up(), math.normalize(tangent));
        return math.dot(splinePoint - point, cross) < 0;
    }
}