using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

[Serializable]
public class PrefabAttributeData
{
    public List<GameObject> selectedPrefabs;
    public List<int> prefabCounts;
    public List<int> selectedIndexes;
    public List<BuildingAreaData> buildingAreasData;
    public List<RoadAreaData> roadAreasData;
    public bool isCityMode;
}