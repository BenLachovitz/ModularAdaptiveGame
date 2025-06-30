using UnityEngine;
using System.Collections.Generic;

public class RoadDataManager : MonoBehaviour
{
    [Header("Road Data Reference")]
    public RoadDataContainer roadData;

    private static RoadDataManager instance;

    public static RoadDataManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<RoadDataManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("RoadDataManager");
                    instance = go.AddComponent<RoadDataManager>();
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadRoadData();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void LoadRoadData()
    {
        if (roadData == null)
        {
            roadData = Resources.Load<RoadDataContainer>("RoadData");
        }
    }

    public void SetRoadData(RoadDataContainer newRoadData)
    {
        roadData = newRoadData;
    }

    public RoadDataContainer GetRoadData()
    {
        return roadData;
    }

    // Convenience methods for vehicle scripts
    public RoadSegmentData FindRoadAtPosition(Vector3 position)
    {
        if (roadData == null) return null;

        foreach (var road in roadData.roadSegments)
        {
            if (road.GetBounds().Contains(position))
            {
                return road;
            }
        }
        return null;
    }


    public RoadSegmentData FindIntersectionAtPosition(Vector3 position)
    {
        if (roadData == null) return null;

        foreach (var road in roadData.roadSegments)
        {
            if (road.IsIntersection() && road.GetBounds().Contains(position))
            {
                return road;
            }
        }
        return null;
    }
    
    public bool CanGoStraightVAtIntersection(Vector3 intersectionPos)
    {
        var intersection = FindIntersectionAtPosition(intersectionPos);
        return intersection?.canGoStraightV ?? true;
    }
    
    public bool CanGoStraightHAtIntersection(Vector3 intersectionPos)
    {
        var intersection = FindIntersectionAtPosition(intersectionPos);
        return intersection?.canGoStraightH ?? true; 
    }

    public int GetIntersectionFromVertical(Vector3 intersectionPos)
    {
        var intersection = FindIntersectionAtPosition(intersectionPos);
        return intersection?.fromVertical ?? -1;
    }

    public int GetIntersectionFromHorizontal(Vector3 intersectionPos)
    {
        var intersection = FindIntersectionAtPosition(intersectionPos);
        return intersection?.fromHorizontal ?? -1;
    }

    public List<RoadSegmentData> GetConnectedRoads(Vector3 intersectionPos, float searchRadius = 10f)
    {
        var connectedRoads = new List<RoadSegmentData>();

        if (roadData == null || roadData.roadSegments == null)
        {
            return connectedRoads;
        }

        foreach (var road in roadData.roadSegments)
        {
            if (road == null) continue;

            float distance = Vector3.Distance(intersectionPos, road.position);

            if (distance <= searchRadius && road.direction >= 0) // Valid directional roads only
            {
                connectedRoads.Add(road);
            }
        }

        Debug.LogWarning($"Number Of Connected: {connectedRoads.Count}");
        return connectedRoads;
    }
}