using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BuildingsData
{
    public List<BuildingsDataInstance> list;
}

[System.Serializable]
public class BuildingsDataInstance
{
    public string buildingName;
    public int buildingCount;
}