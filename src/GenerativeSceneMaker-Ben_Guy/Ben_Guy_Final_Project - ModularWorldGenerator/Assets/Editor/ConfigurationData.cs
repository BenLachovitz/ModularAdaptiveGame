using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConfigurationData
{
    public float terrainLength;
    public float terrainWidth;
    public float environmentDensity;
    public int numHorizontalRoads;
    public int numVerticalRoads;
    public string buildingAreaMaterial;
    public string roadMaterial;
    public string parkMaterial;
    public string crosswalkMaterial;
}