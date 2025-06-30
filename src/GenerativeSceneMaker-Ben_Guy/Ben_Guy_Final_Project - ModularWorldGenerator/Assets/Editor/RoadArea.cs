using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class RoadAreaData
{
    public string roadName; 
    public bool horizontal;
    public bool vertical;
    public int direction;
}


[System.Serializable]
public class RoadArea
{
    public GameObject road { get; set; }
    public bool horizontal { get; set; }
    public bool vertical { get; set; }

    // For horizontal = true, 0 - Right, 1 - Left
    // For verticle = true, 2 - down, 3 - up
    // For both = false, -1 = Intersection, -2 = access road.
    public int direction { get; set; }

    public RoadArea (GameObject road, bool horizontal, bool vertical, int direction)
    {
        this.road = road;
        this.horizontal = horizontal;
        this.vertical = vertical;
        this.direction = direction;
    }
}