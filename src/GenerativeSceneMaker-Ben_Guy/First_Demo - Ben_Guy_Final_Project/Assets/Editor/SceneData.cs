using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    [System.Serializable]
    public class SceneData
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 size;
        public List<ChildObjectData> children;
        public string timestamp;
        public int childCount;
    }

    [System.Serializable]
    public class ChildObjectData
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public List<ChildObjectData> children;
        public Vector3 localScale;
        public bool isActive;
    }