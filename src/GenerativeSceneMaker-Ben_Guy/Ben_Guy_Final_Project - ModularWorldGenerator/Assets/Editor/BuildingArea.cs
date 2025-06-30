using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[System.Serializable]
public class BuildingAreaData
{
    public float areaLength;
    public float areaWidth;
    public Vector3 position;
    public float cellSize;
    public int GridWidth;
    public int GridLength;
    public bool[] gridOccupiedFlat;
    public string areaObjectName;
    public string accessRoadName;
    public string parkAreaName;
}


[System.Serializable]
public class BuildingArea
{
    public GameObject areaObject { get; set; }
    public GameObject accessRoad { get; set; }
    public GameObject parkArea { get; set; }
    public float areaLength { get; set; }
    public float areaWidth { get; set; }
    public Vector3 position { get; set; }
    public float cellSize { get; private set; }
    private bool[,] gridOccupied; 
    public bool[,] GridOccupied => gridOccupied;
    public int GridWidth;
    public int GridLength;

    public BuildingArea(GameObject area, float length, float width, Vector3 pos, float cellSize = 2f, GameObject accessRoad = null
        , GameObject parkArea = null)
    {
        areaObject = area;
        this.accessRoad = accessRoad;
        this.parkArea = parkArea;
        areaLength = length;
        areaWidth = width;
        position = pos;

        this.cellSize = cellSize;

        GridWidth = Mathf.FloorToInt(width / cellSize);
        GridLength = Mathf.FloorToInt(length / cellSize);
        gridOccupied = new bool[GridWidth, GridLength];
    }

    public bool CanPlaceAt(int gridX, int gridZ, int sizeX, int sizeZ)
    {
        if (gridX + sizeX > GridWidth || gridZ + sizeZ > GridLength)
            return false;

        for (int x = gridX; x < gridX + sizeX; x++)
        {
            for (int z = gridZ; z < gridZ + sizeZ; z++)
            {
                if (gridOccupied[x, z]) return false;
            }
        }
        return true;
    }

    public void MarkOccupied(int gridX, int gridZ, int sizeX, int sizeZ)
    {
        for (int x = gridX; x < gridX + sizeX; x++)
        {
            for (int z = gridZ; z < gridZ + sizeZ; z++)
            {
                if (x < GridWidth && x >= 0 && z < GridLength && z >= 0)
                    gridOccupied[x, z] = true;
            }
        }
    }

    public void MarkEntrancePathFromRoad(int pathWidth = 4, float depth = 4f)
    {
        if (accessRoad == null)
        {
            Debug.LogWarning("Access road not assigned for this BuildingArea.");
            return;
        }

        Vector3 roadCenter = accessRoad.GetComponent<Renderer>()?.bounds.center ?? accessRoad.transform.position;
        Vector3 areaCenter = position;

        Vector3 dir = (areaCenter - roadCenter).normalized;
        dir.y = 0;

        // Calculate entrance start point just inside the area
        Vector3 entranceStart = areaCenter - dir * (Mathf.Max(areaWidth, areaLength) / 2f); // at the edge
        Vector3 entranceEnd = entranceStart + dir * depth;

        // Convert world coords to grid
        int startX = Mathf.FloorToInt((entranceStart.x - (position.x - areaWidth / 2f)) / cellSize);
        int startZ = Mathf.FloorToInt((entranceStart.z - (position.z - areaLength / 2f)) / cellSize);

        int endX = Mathf.FloorToInt((entranceEnd.x - (position.x - areaWidth / 2f)) / cellSize);
        int endZ = Mathf.FloorToInt((entranceEnd.z - (position.z - areaLength / 2f)) / cellSize);

        // Clamp to grid
        startX = Mathf.Clamp(startX, 0, GridWidth - 1);
        startZ = Mathf.Clamp(startZ, 0, GridLength - 1);
        endX = Mathf.Clamp(endX, 0, GridWidth - 1);
        endZ = Mathf.Clamp(endZ, 0, GridLength - 1);

        // DDA to step only the limited distance
        int steps = Mathf.Max(Mathf.Abs(endX - startX), Mathf.Abs(endZ - startZ));
        float dx = (endX - startX) / (float)steps;
        float dz = (endZ - startZ) / (float)steps;

        float x = startX;
        float z = startZ;

        for (int i = 0; i <= steps; i++)
        {
            for (int offsetX = -pathWidth / 2; offsetX <= pathWidth / 2; offsetX++)
            {
                for (int offsetZ = -pathWidth / 2; offsetZ <= pathWidth / 2; offsetZ++)
                {
                    int gx = Mathf.RoundToInt(x) + offsetX;
                    int gz = Mathf.RoundToInt(z) + offsetZ;

                    if (gx >= 0 && gx < GridWidth && gz >= 0 && gz < GridLength)
                    {
                        gridOccupied[gx, gz] = true;
                    }
                }
            }

            x += dx;
            z += dz;
        }

        //Debug.DrawLine(entranceStart + Vector3.up * 0.5f, entranceEnd + Vector3.up * 0.5f, Color.yellow, 10f);
    }



    public Vector3 GridToWorld(int gridX, int gridZ)
    {
        float worldX = position.x - areaWidth / 2 + gridX * cellSize + cellSize / 2;
        float worldZ = position.z - areaLength / 2 + gridZ * cellSize + cellSize / 2;
        return new Vector3(worldX, position.y, worldZ);
    }

    public void ClearOccupiedCells()
    {
        if (gridOccupied == null) return;

        Array.Clear(gridOccupied, 0, gridOccupied.Length);
    }
    
    public void RestoreGridFromFlat(bool[] flatGrid, int width, int length)
    {
        if (flatGrid == null || flatGrid.Length != width * length)
        {
            return;
        }

        gridOccupied = new bool[width, length];
        GridWidth = width;
        GridLength = length;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                gridOccupied[x, z] = flatGrid[x * length + z];
            }
        }
    }

    public bool[] GetGridAsFlat()
    {
        bool[] flat = new bool[GridWidth * GridLength];
        for (int x = 0; x < GridWidth; x++)
        {
            for (int z = 0; z < GridLength; z++)
            {
                flat[x * GridLength + z] = gridOccupied[x, z];
            }
        }
        return flat;
    }

}


