using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RoadData", menuName = "City Generator/Road Data")]
public class RoadDataContainer : ScriptableObject
{
    [Header("Road Network Data")]
    public List<RoadSegmentData> roadSegments = new List<RoadSegmentData>();

    [Header("Generation Info")]
    public float terrainWidth;
    public float terrainLength;
    public float roadWidth;

    public void ClearData()
    {
        roadSegments.Clear();
    }

    public void AddRoadSegment(GameObject roadObject, bool horizontal, bool vertical, int direction)
    {
        if (roadObject == null) return;

        RoadSegmentData data = new RoadSegmentData
        {
            roadName = roadObject.name,
            position = roadObject.transform.position,
            scale = roadObject.transform.localScale,
            rotation = roadObject.transform.rotation,
            horizontal = horizontal,
            vertical = vertical,
            direction = direction
        };

        roadSegments.Add(data);
    }

    public void AddIntersectionSegment(GameObject intersectionObject, int fromVertical, int fromHorizontal, bool canGoStraightV, bool canGoStraightH)
    {
        if (intersectionObject == null) return;
        RoadSegmentData data = new RoadSegmentData
        {
            roadName = intersectionObject.name,
            position = intersectionObject.transform.position,
            scale = intersectionObject.transform.localScale,
            rotation = intersectionObject.transform.rotation,
            horizontal = false,
            vertical = false,
            direction = -1, // Intersection
            fromVertical = fromVertical,
            fromHorizontal = fromHorizontal,
            canGoStraightV = canGoStraightV,
            canGoStraightH = canGoStraightH
        };
        roadSegments.Add(data);
    }

    public List<RoadSegmentData> GetRoadSegmentsWithDirection()
    {
        return roadSegments.FindAll(r => r.direction >= 0 && r.direction <= 3);
    }

    public List<RoadSegmentData> GetIntersections()
    {
        return roadSegments.FindAll(r => r.IsIntersection());
    }
}

[System.Serializable]
public class RoadSegmentData
{
    public string roadName;
    public Vector3 position;
    public Vector3 scale;
    public Quaternion rotation;
    public bool horizontal;
    public bool vertical;
    public int direction; // 0=East, 1=West, 2=North, 3=South, -1=Intersection, -2=Access Road

    [Header("Intersection Specific Data")]
    // All next 4 variables only used when direction == -1 (intersection)
    public int fromVertical = -1;   
    public int fromHorizontal = -1;   
    public bool canGoStraightV = true; 
    public bool canGoStraightH = true; 

    public Vector3 GetDirectionVector()
    {
        switch (direction)
        {
            case 0: return Vector3.right;   // East
            case 1: return Vector3.left;    // West
            case 2: return Vector3.forward; // North
            case 3: return Vector3.back;    // South
            default: return Vector3.zero;
        }
    }

    public bool IsIntersection()
    {
        return direction == -1;
    }

    public Bounds GetBounds()
    {
        // Unity plane default size is 10x10, so multiply by scale
        Vector3 size = new Vector3(scale.x * 10f, 1f, scale.z * 10f);
        return new Bounds(position, size);
    }
}