using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PreferencesData
{
    public ConfigurationData configuration;
    public NPCData npc;
    public BuildingsData buildings;
    public VehiclesData vehicles;
    public string timestamp;
    public string savedByUser;
}