using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NPCData
{
    public List<NPCDataInstance> list;
}

[System.Serializable]
public class NPCDataInstance
{
    public string npcName;
    public int npcCount;
}