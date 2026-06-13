using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using UnityEditor.Splines;
using System.Linq;
using Unity.VisualScripting;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine.Rendering;
using System;

public class SplineConveyorInstantiator : MonoBehaviour
{
    [Header("Conveyors")]
    [SerializeField] GameObject forwardConveyor;
    [SerializeField] GameObject turnConveyor;
    [SerializeField] Transform conveyorsParent;
    public static Dictionary<Spline, List<GameObject>> conveyors = new();

    [Header("Highlighters")]
    [SerializeField] GameObject conveyorCreateHighlighter;
    [SerializeField] Transform highlightersParent;

    List<GameObject> conveyorHighlights = new();

    [Header("Splines")]
    [SerializeField] GameObject conveyorSplinePrefab;
    [SerializeField] List<SplineContainer> splineList;

    bool drawingSplines;
    SplineContainer currentSpline;

    Vector3 startDrawPos;
    Vector3 endDrawPos;

    const float yOffset = -.15f;
    //const Vector3 nullVector = Vector3.one * float.MinValue;

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

    #region StateMachine

    void Update()
    {
        if (drawingSplines) ExecuteDrawSplines();
    }

    void StartDrawSpline()
    {
        startDrawPos = MouseScreenToWorld();
        startDrawPos.y = yOffset;
        if (startDrawPos == Vector3Int.one * int.MinValue)
        {
            drawingSplines = false;
            return;
        }

        // Looking if extending from an existing spline
        currentSpline = null;
        for (int i = 0; i < splineList.Count; i++)
        {
            SplineContainer spline = splineList[i];
            if (!spline) continue;
            if (IsInsideLinearSpline(startDrawPos, spline))
            {
                DrawOverExistingSpline(startDrawPos, spline);
                break;
            }
        }

        // If not starting draw over existing rails, create new spline
        if (currentSpline == null)
        {
            GameObject newSpline = Instantiate(conveyorSplinePrefab);
            currentSpline = newSpline.GetComponent<SplineContainer>();
            splineList.Add(currentSpline);
            Debug.Log("creating new spline");
        }

        drawingSplines = true;
    }

    void DrawOverExistingSpline(Vector3 start, SplineContainer spline)
    {
        currentSpline = spline;

        int knotStartIndex = GetNextKnotIndex(start, spline);
        //Debug.Log("next knot index: " + knotStartIndex);

        if (knotStartIndex != -1)
        {
            Vector3Int nextKnotPos = Vector3Int.RoundToInt(((List<BezierKnot>)spline.Spline.Knots)[knotStartIndex].Position);

            // Getting overhead knots based on click
            List<BezierKnot> overheadKnots = GetKnotsUntilEnd(nextKnotPos, spline.Spline);

            List<float3> knotsToRemove = new();
            overheadKnots.ForEach(knot => knotsToRemove.Add(knot.Position));

            // foreach (Vector3 knot in knotsToRemove)
            //     Debug.Log("knot to remove pos: " + knot);


            // Broken spline is all the knots after the break
            GameObject newBrokenSpline = Instantiate(conveyorSplinePrefab);
            SplineContainer brokenSpline = newBrokenSpline.GetComponent<SplineContainer>();
            splineList.Add(brokenSpline);


            brokenSpline.Spline.AddRange(knotsToRemove);
            foreach (BezierKnot knot in overheadKnots)
            {
                currentSpline.Spline.Remove(knot);
            }

        }
    }

    void ExecuteDrawSplines()
    {
        // Get rails direction with dot product
        Vector3Int mousePos = MouseScreenToWorld();
        Vector2Int planarDist = new Vector2Int(mousePos.x, mousePos.z) - new Vector2Int(Mathf.RoundToInt(startDrawPos.x), Mathf.RoundToInt(startDrawPos.z));
        Vector3Int dist = new(planarDist.x, 0, planarDist.y);
        float angle = Vector3.SignedAngle(Vector3Int.right, dist, Vector3.up);

        Vector3Int drawDir = angle >= -45 && angle <= 45 ? Vector3Int.right : angle <= -45 && angle >= -135 ? Vector3Int.forward : 
        angle >= 135 || angle <= -135 ? Vector3Int.left : Vector3Int.back;

        // Show tile highlights for all rails about to spawn
        int highlightsAmount = drawDir == Vector3Int.right || drawDir == Vector3Int.left ? Mathf.Abs(dist.x) + 1 : Mathf.Abs(dist.z) + 1;
        if (highlightsAmount != conveyorHighlights.Count())
        {
            foreach (GameObject go in conveyorHighlights) Destroy(go);
            conveyorHighlights.Clear();
            for (int i = 0; i < highlightsAmount; i++)
            {
                GameObject newHighlight = Instantiate(conveyorCreateHighlighter, highlightersParent);
                conveyorHighlights.Add(newHighlight);
            }
        }

        for (int i = 0; i < highlightsAmount; i++)
        {
            conveyorHighlights[i].transform.position = startDrawPos + drawDir * i + Vector3.up * .1f;
        }
        endDrawPos = startDrawPos + drawDir * (highlightsAmount - 1);
    }

    void StopDrawSpline()
    {
        if (!drawingSplines) return;

        // Add end knot (and begin knot if making a new spline)
        if (currentSpline.Spline.Knots.Count() == 0)
            currentSpline.Spline.Add(new Vector3(startDrawPos.x, yOffset, startDrawPos.z), TangentMode.Linear);            
        currentSpline.Spline.Add(new Vector3(endDrawPos.x, yOffset, endDrawPos.z), TangentMode.Linear);

        Vector3Int dir = Vector3Int.RoundToInt((endDrawPos - startDrawPos).normalized);

        if (!conveyors.ContainsKey(currentSpline.Spline))
            conveyors.Add(currentSpline.Spline, new List<GameObject>());
        else
        {
            // If current spline already has conveyors, destroy duplicate at draw point
            GameObject duplicateConveyor = GetConveyor(startDrawPos, currentSpline.Spline);
            conveyors[currentSpline.Spline].Remove(duplicateConveyor);
            Destroy(duplicateConveyor);
        }

        // Draw rails on highlights
        for (int i = 0; i < conveyorHighlights.Count; i++)
        {
            Vector3 pos = conveyorHighlights[i].transform.position;
            pos.y = yOffset;
            BezierKnot posKnot = GetKnot(pos, currentSpline.Spline);

            bool isAtExtremity = (Vector3)posKnot.Position == (Vector3)((List<BezierKnot>)currentSpline.Spline.Knots)[0].Position
                             || (Vector3)posKnot.Position == (Vector3)((List<BezierKnot>)currentSpline.Spline.Knots)[^1].Position;

            float rotation;
            GameObject conveyorToSpawn;

            // Spawn turn rail
            if ((Vector3)posKnot.Position != Vector3.one * float.MinValue && !isAtExtremity)
            {
                conveyorToSpawn = turnConveyor;

                Vector3 previousEval = GetPreviousEval(pos, currentSpline);
                Vector3 nextEval = GetNextEval(pos, currentSpline);

                Vector3 previousDir = (previousEval - pos).normalized;;
                Vector3 nextDir = (nextEval - pos).normalized;

                bool adjacentConveyors = previousEval != Vector3.one * float.MinValue && nextEval != Vector3.one * float.MinValue;
                Debug.Assert(adjacentConveyors, "failed to get previous and next dir of turning conveyor");

                rotation = nextDir == Vector3.back && previousDir == Vector3.left || nextDir == Vector3.left && previousDir == Vector3.back ? 90 : 
                    nextDir == Vector3.left && previousDir == Vector3.forward || nextDir == Vector3.forward && previousDir == Vector3.left ? 180 :
                    nextDir == Vector3.forward && previousDir == Vector3.right || nextDir == Vector3.right && previousDir == Vector3.forward ? -90 : 0;
            }
            else
            {
                conveyorToSpawn = forwardConveyor;
                rotation = dir == Vector3.right ? -90 : dir == Vector3.left ? 90 : dir == Vector3.back ? 180 : 0;
            }

            GameObject conveyor = Instantiate(conveyorToSpawn, conveyorsParent);
            conveyor.transform.position = pos;
            conveyor.transform.Rotate(new Vector3(0, rotation, 0));

            conveyors[currentSpline.Spline].Add(conveyor);
        }

        // Destroy highlights
        foreach (GameObject go in conveyorHighlights) Destroy(go);
        conveyorHighlights.Clear();

        drawingSplines = false;
    }

    #endregion StateMachine

    #region Utils

    GameObject GetConveyor(Vector3 point, Spline spline)
    {
        foreach (GameObject go in conveyors[spline])
        {
            if (go.transform.position == point) return go;
        }
        return null;
    }

    List<BezierKnot> GetKnotsUntilEnd(Vector3 point, Spline spline)
    {
        List<BezierKnot> knots = new(spline.Knots);
        for (int i = 0; i < knots.Count; i++)
        {
            if ((Vector3)knots[i].Position == point)
            {
                knots.RemoveRange(0, i);
                List<BezierKnot> result = new();
                knots.ForEach(knot => result.Add(knot));

                return result;
            }
        }
        return null;
    }

    bool HasKnot(Vector3 point, Spline spline)
    {
        foreach (BezierKnot knot in spline.Knots)
        {
            if ((Vector3)knot.Position == point) return true;
        }
        return false;
    }

    BezierKnot GetKnot(Vector3 point, Spline spline)
    {
        foreach (BezierKnot knot in spline.Knots)
        {
            if ((Vector3)knot.Position == point) return knot;
        }
        return new BezierKnot(Vector3.one * float.MinValue);
    }

    Vector3 GetNextEval(Vector3 point, SplineContainer spline)
    {
        float length = (int)spline.CalculateLength();
        for (int i = 0; i < length; i++)
        {
            Vector3 eval = spline.EvaluatePosition(i / length);
            Vector3 posCheck = new(Mathf.Round(eval.x), yOffset, Mathf.Round(eval.z));
            if (point == posCheck) return spline.EvaluatePosition((i + 1) / length);
        }
        return Vector3.one * float.MinValue;
    }

    Vector3 GetPreviousEval(Vector3 point, SplineContainer spline)
    {
        float length = (int)spline.CalculateLength();
        for (int i = 1; i <= length; i++)
        {
            Vector3 eval = spline.EvaluatePosition(i / length);
            Vector3 posCheck = new(Mathf.Round(eval.x), yOffset, Mathf.Round(eval.z));
            if (point == posCheck) return spline.EvaluatePosition((i - 1) / length);
        }
        return Vector3.one * float.MinValue;
    }

    List<Vector3> GetKnotEvaluations(Spline spline)
    {
        List<Vector3> knotEvaluations = new();
        foreach (BezierKnot knot in spline.Knots)
        {
            knotEvaluations.Add(knot.Position);
        }
        return knotEvaluations;
    }

    int GetNextKnotIndex(Vector3 point, SplineContainer spline)
    {
        int pointEvalIndex = GetSplineEvaluationIndex(point, spline);
        List<Vector3> knotEvaluations = GetKnotEvaluations(spline.Spline);

        float length = (int)spline.CalculateLength();
        for (int i = pointEvalIndex + 1; i < length + 1; i++)
        {
            int nextKnotIndex = knotEvaluations.IndexOf(Vector3Int.RoundToInt(spline.EvaluatePosition(i / length)));
            if (nextKnotIndex != -1) return nextKnotIndex;
        }
        return -1;
    }

    Vector3Int MouseScreenToWorld()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity);
        if (!hit.collider) return Vector3Int.one * int.MinValue;
        return Vector3Int.RoundToInt(hit.point);
    }

    public bool IsInsideLinearSpline(Vector3 point, SplineContainer spline)
    {
        float length = (int)spline.CalculateLength();
        for (int i = 0; i <= length; i++)
        {
            //Debug.Log($"point: {point}, spline evaluation: {Vector3Int.RoundToInt(spline.EvaluatePosition(i / length))}");
            Vector3 eval = spline.EvaluatePosition(i / length);
            Vector3 posCheck = new(Mathf.Round(eval.x), yOffset, Mathf.Round(eval.z));
            if (point == posCheck) return true;
        }
        return false;
    }

    public static float GetSplineEvaluation(Vector3 point, SplineContainer spline)
    {
        float length = (int)spline.CalculateLength();
        for (int i = 0; i <= length; i++)
        {
            if (point == Vector3Int.RoundToInt(spline.EvaluatePosition(i / length))) return i / length;
        }
        return -1;
    }

    public static int GetSplineEvaluationIndex(Vector3 point, SplineContainer spline)
    {
        float length = (int)spline.CalculateLength();
        for (int i = 0; i <= length; i++)
        {
            if (point == Vector3Int.RoundToInt(spline.EvaluatePosition(i / length))) return i;
        }
        return -1;
    }

    #endregion Utils
}