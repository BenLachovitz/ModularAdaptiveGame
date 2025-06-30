using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;
using Unity.VisualScripting;
using System.Dynamic;

public class NPCTab : PrefabTab
{
    public NPCTab() : base("NPC", "Assets/Prefabs/NPC")
    {
    }

    public void SetTabData(NPCData data)
    {
        selectedIndexes.Clear();
        selectedPrefabs.Clear();
        prefabCounts.Clear();
        
        List<string> combinedGuids = new List<string>();

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolderPath });
        combinedGuids.AddRange(prefabGuids);

        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { prefabFolderPath });
        combinedGuids.AddRange(modelGuids);

        string[] prefabPaths = combinedGuids.ToArray();
        string[] prefabNames = new string[prefabPaths.Length];

        for (int i = 0; i < prefabPaths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabPaths[i]);
            prefabNames[i] = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        for (int i = 0; i < data.list.Count(); i++)
        {
            int index = -1;
            for (int j = 0; j < prefabNames.Length; j++)
            {
                if (prefabNames[j].Equals(data.list[i].npcName))
                    index = j;
            }
            string selectedPrefabPath = AssetDatabase.GUIDToAssetPath(prefabPaths[index]);
            selectedPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(selectedPrefabPath));
            selectedIndexes.Add(index);
            prefabCounts.Add(data.list[i].npcCount);
        }
    }

    public NPCData GetTabData()
    {
        List<NPCDataInstance> temp = new List<NPCDataInstance>();

        for (int i = 0; i < base.selectedIndexes.Count; i++)
        {
            string currentName = base.selectedPrefabs[i].name;
            int currentCount = base.prefabCounts[i];

            NPCDataInstance existingInstance = temp.Find(x => x.npcName == currentName);

            if (existingInstance != null)
            {
                existingInstance.npcCount += currentCount;
            }
            else
            {
                temp.Add(new NPCDataInstance
                {
                    npcName = currentName,
                    npcCount = currentCount
                });
            }
        }

        return new NPCData
        {
            list = temp
        };
    }

    public override void OnGUI()
    {
        GUILayout.Label("NPC Placement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Place NPCs on terrain avoiding roads and existing objects.", MessageType.Info);
        DrawMultiSelectionTab();
    }

    protected override void InstantiatePrefabOnTerrain(Transform terrainTransform, GameObject prefab, int count, List<Bounds> globalPlacedBounds)
    {
        if (prefab == null)
        {
            Debug.LogWarning("NPC prefab is null. Skipping generation.");
            return;
        }

        List<BoxCollider> roadColliders = new List<BoxCollider>();

        foreach (RoadArea area in roadAreas)
        {
            GameObject roadAreaTemp = area.road;
            BoxCollider[] colliders = roadAreaTemp.GetComponentsInChildren<BoxCollider>();
            roadColliders.AddRange(colliders);
        }

        GameObject temp = GameObject.Instantiate(prefab);
        Bounds npcBounds = GetCombinedBounds(temp);
        GameObject.DestroyImmediate(temp);

        float buffer = Mathf.Max(npcBounds.size.x, npcBounds.size.z) * 0.5f;

        GameObject parent = GameObject.FindObjectsOfType<GameObject>()
         .FirstOrDefault(go => go.name.Contains("NPCs " + prefab.name + " Instances"));
        if (parent == null)
            parent = new GameObject("NPCs " + prefab.name + " Instances");

        parent.transform.SetParent(terrainTransform);

        int placed = 0;
        int attempts = 0;
        int maxAttempts = count * 100;

        Terrain terrain = terrainGameObject?.GetComponent<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        Vector3 terrainSize = terrain.terrainData.size;
        Vector3 terrainPos = terrain.transform.position;

        // Get all existing scene colliders to avoid overlap with buildings, trees, etc.
        Collider[] allSceneColliders = GameObject.FindObjectsOfType<Collider>()
            .Where(c =>
                !(c is TerrainCollider) &&
                c.gameObject.layer != 0 &&
                c.gameObject.name.IndexOf("BuildingArea") == -1 &&
                c.gameObject.name.IndexOf("ParkArea") == -1 &&
                c.gameObject.name.IndexOf("RoadArea") == -1 &&
                c.gameObject.name.IndexOf("Terrain") == -1)
            .ToArray();

        Collider[] buildingColliders = GameObject.FindObjectsOfType<Collider>()
            .Where(c => c.transform.parent?.name.Contains("Building") == true && 
                    c.transform.parent?.name.Contains("Instance") == true)
            .Where(c => !(c is TerrainCollider))
            .ToArray();

        while (placed < count && attempts < maxAttempts)
        {
            attempts++;
            float x = Random.Range(terrainPos.x, terrainPos.x + terrainSize.x);
            float z = Random.Range(terrainPos.z, terrainPos.z + terrainSize.z);
            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrainPos.y;

            Vector3 spawnPos = new Vector3(x, y, z);
            Bounds proposedBounds = new Bounds(spawnPos + npcBounds.center, npcBounds.size + Vector3.one * buffer);

            // Check if intersects with any road
            bool insideRoad = roadColliders.Any(c => c.bounds.Intersects(proposedBounds));
            if (insideRoad)
                continue;

            // Check collision with other scene objects (trees, props, etc.)
            bool collidesWithSceneObjects = allSceneColliders.Any(c => c.bounds.Intersects(proposedBounds));
            if (collidesWithSceneObjects)
                continue;

            // Check collision specifically with building colliders
            bool collidesWithBuildings = buildingColliders.Any(c => c.bounds.Intersects(proposedBounds));

            if (collidesWithBuildings)
                continue;


            // Check against previously placed bounds from this generation
            bool collidesWithPlacedObjects = globalPlacedBounds.Any(bounds => bounds.Intersects(proposedBounds));
            if (collidesWithPlacedObjects)
                continue;

            // Instantiate and place the NPC
            GameObject npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            npcInstance.transform.position = spawnPos;

            SetupNPCAnimator(npcInstance);


            float randomY = Random.Range(0f, 360f);
            npcInstance.transform.rotation = Quaternion.Euler(0f, randomY, 0f);

            npcInstance.transform.SetParent(parent.transform);

            AddColliderToNPC(npcInstance, npcBounds);

            AddMovementToNPC(npcInstance);

            globalPlacedBounds.Add(proposedBounds);
            placed++;
        }
    }

    private void AddColliderToNPC(GameObject npcInstance, Bounds npcBounds)
    {
        if (npcInstance.GetComponent<Collider>() == null)
        {
            CapsuleCollider collider = npcInstance.AddComponent<CapsuleCollider>();

            collider.height = npcBounds.size.y;
            collider.radius = Mathf.Max(npcBounds.size.x, npcBounds.size.z) * 0.5f;
            collider.center = new Vector3(0, npcBounds.size.y * 0.5f, 0);

            collider.isTrigger = true;
        }
    }

    private void SetupNPCAnimator(GameObject npcInstance)
    {
        Animator animator = npcInstance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = npcInstance.AddComponent<Animator>();
        }

        string animatorControllerPath = "Assets/Animation/WalkController.controller";
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorControllerPath);

        if (controller != null)
            animator.runtimeAnimatorController = controller;
        else
            Debug.LogWarning($"Animator Controller not found at: {animatorControllerPath}");

        // Try to automatically detect and assign Avatar
        Avatar avatar = GetOrCreateAvatar(npcInstance);
        if (avatar != null)
        {
            animator.avatar = avatar;
        }

        // Set up basic animator settings
        animator.applyRootMotion = true; 
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    private Avatar GetOrCreateAvatar(GameObject npcInstance)
    {
        // Try to find existing Avatar in the prefab
        Animator existingAnimator = npcInstance.GetComponent<Animator>();
        if (existingAnimator != null && existingAnimator.avatar != null)
        {
            return existingAnimator.avatar;
        }

        // Look for a existing NPC avatar
        string avatarPath = $"Assets/Animation/{npcInstance.name}_Avatar.asset";
        Avatar existingAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        if (existingAvatar != null)
        {
            return existingAvatar;
        }

        // Try to create Avatar from the model (for Mixamo characters)
        return CreateAvatarFromModel(npcInstance);
    }

    private Avatar CreateAvatarFromModel(GameObject npcInstance)
    {
        // For Mixamo characters, try to auto-generate Avatar
        SkinnedMeshRenderer skinnedMesh = npcInstance.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMesh != null)
        {
            // Get the root bone transform
            Transform rootBone = skinnedMesh.rootBone;
            if (rootBone != null)
            {
                // Create a generic humanoid avatar
                Avatar avatar = AvatarBuilder.BuildGenericAvatar(npcInstance, rootBone.name);

                // Save the avatar as an asset for reuse
                string avatarSavePath = $"Assets/Animation/{npcInstance.name}_Avatar.asset";

                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(avatarSavePath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(avatar, avatarSavePath);
                AssetDatabase.SaveAssets();

                return avatar;
            }
        }

        Debug.LogWarning($"Could not create Avatar for {npcInstance.name}");
        return null;
    }

    private Bounds GetCombinedBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    protected override void DeletePrefabsFromScene()
    {
        if (terrainGameObject == null)
        {
            Debug.LogWarning("Terrain GameObject is null. Cannot delete NPCs instances.");
            return;
        }

        List<GameObject> objectsToDelete = new List<GameObject>();

        foreach (Transform child in terrainGameObject.transform)
        {
            if (child.name.StartsWith("NPCs ") && child.name.EndsWith(" Instances"))
            {
                objectsToDelete.Add(child.gameObject);
            }
        }

        int deletedCount = objectsToDelete.Count;
        foreach (GameObject obj in objectsToDelete)
        {
            Object.DestroyImmediate(obj);
        }
    }

    private void AddMovementToNPC(GameObject npcInstance)
    {
        if (npcInstance.GetComponent<NPCMovementNavMesh>() == null)
            npcInstance.AddComponent<NPCMovementNavMesh>();
        
    }
    
     public void randomizeLists(int numToCreate)
    {
        selectedIndexes.Clear();
        selectedPrefabs.Clear();
        prefabCounts.Clear();

        List<string> combinedGuids = new List<string>();

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolderPath });
        combinedGuids.AddRange(prefabGuids);

        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { prefabFolderPath });
        combinedGuids.AddRange(modelGuids);

        string[] prefabPaths = combinedGuids.ToArray();
        string[] prefabNames = new string[prefabPaths.Length];

        for (int i = 0; i < prefabPaths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabPaths[i]);
            prefabNames[i] = System.IO.Path.GetFileNameWithoutExtension(path);
            selectedIndexes.Add(i);
            string selectedPrefabPath = AssetDatabase.GUIDToAssetPath(prefabPaths[i]);
            selectedPrefabs.Add(null);
            selectedPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(selectedPrefabPath);
            prefabCounts.Add(0);
        }

        for (int i = 0; i < numToCreate; i++)
        {
            int tempIndex = Random.Range(0, selectedPrefabs.Count);
            prefabCounts[tempIndex] += 1;
        }

        for (int i = 0; i < selectedPrefabs.Count; i++)
        {
            if (prefabCounts[i] == 0)
            {
                selectedIndexes.RemoveAt(i);
                selectedPrefabs.RemoveAt(i);
                prefabCounts.RemoveAt(i);
            }
        }

        GeneratePrefabs(selectedPrefabs, prefabCounts);
    }
}