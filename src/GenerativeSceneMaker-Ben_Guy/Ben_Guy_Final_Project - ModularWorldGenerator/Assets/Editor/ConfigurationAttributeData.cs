using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

[Serializable]
public class ConfigurationAttributeData
{
    public float terrainLength;
    public float terrainWidth;
    public float environmentDensity;
    public int horizontalRoads;
    public int verticalRoads;
    public int buildingAreaMaterial;
    public int roadMaterial;
    public int parkMaterial;
    public int crosswalkMaterial;
    public bool isAllLayersExist;
}