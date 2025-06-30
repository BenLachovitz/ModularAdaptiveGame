using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VehiclesData
{
    public List<VehicleDataInstance> list;
}

[System.Serializable]
public class VehicleDataInstance
{
    public string vehicleName;
    public int vehicleCount;
}