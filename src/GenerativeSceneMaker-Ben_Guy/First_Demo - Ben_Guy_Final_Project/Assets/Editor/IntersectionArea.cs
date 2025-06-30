using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IntersectionArea : RoadArea
{
    public int fromVertical;
    public int fromHorizontal;
    public int connectedV;
    public int connectedH;
    public bool canGoStraightV;
    public bool canGoStraightH;

    // For horizontal = true, 0 - Right, 1 - Left
    // For verticle = true, 2 - down, 3 - up
    // For both = false, -1 = Intersection, -2 = access road.

    public IntersectionArea(GameObject road, bool horizontal, bool vertical, int direction, int cVert, int cHorz, int fromVert, int fromHorz, bool straightV,
        bool straightH)
    : base(road, horizontal, vertical, direction)
    {
        this.connectedV = cVert;
        this.connectedH = cHorz;
        this.fromVertical = fromVert;
        this.fromHorizontal = fromHorz;
        this.canGoStraightV = straightV;
        this.canGoStraightH = straightH;
    }
}