using UnityEngine;
using NaughtyAttributes;
using System.Collections.Generic;
using System;

public class SelectableGrid : MonoBehaviour, ISelectableAdvanced
{
    [SerializeField, OnValueChanged("RedrawBoard")] int boardResolution;
    [SerializeField] Transform squareSelectionTransform;

    Vector3[,] cells = new Vector3[10, 10];

    public static Action OnDrawConveyorsStart;
    public static Action OnDrawConveyorsEnd;

    void Update()
    {
        ShowHighlighter();
    }

    void ShowHighlighter()
    {
        Vector3Int tilePos = MouseScreenToWorld();
        if (tilePos == Vector3Int.one * int.MinValue) return;
        
        tilePos.y = 0;
        if (Mathf.Abs(tilePos.x) <= boardResolution / 2 && Mathf.Abs(tilePos.z) <= boardResolution / 2)
            squareSelectionTransform.position = tilePos + Vector3.up * .1f;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void RedrawBoard()
    {
        Debug.Log("redrawing board");
        cells = new Vector3[boardResolution, boardResolution];
        for (int i = 0; i < boardResolution; i++)
        {
            for (int j = 0; j < boardResolution; j++)
            {
                cells[i, j] = new Vector2(j - boardResolution / 2, i - boardResolution / 2);
            }
        }
    }

    Vector3Int MouseScreenToWorld()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity);
        if (!hit.collider) return Vector3Int.one * int.MinValue;
        return Vector3Int.RoundToInt(hit.point);
    }

    public void OnSelectPerformed()
    {
        OnDrawConveyorsStart();
    }

    public void OnSelectCanceled()
    {
        OnDrawConveyorsEnd();
    }

    public void OnRightClickPerformed() {}

    public void OnRightClickCanceled() {}
}

/*
class Cell : ISelectableAdvanced
{
    Transform squareSelectionTransform;

    public Cell(Transform squareSelectionTransform)
    {
        this.squareSelectionTransform = squareSelectionTransform;
    }

    public void OnSelectPerformed()
    {
        
    }

    public void OnSelectCanceled()
    {
        
    }

    public void OnHovered()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity);
        squareSelectionTransform.position = hit.collider.transform.position;
    }

    public void OnRightClickPerformed() {}

    public void OnRightClickCanceled() {}
}
*/