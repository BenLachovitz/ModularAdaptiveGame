using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class VehiclesTab : PrefabTab
{
    private float vehiclePadding = 2.0f;
    public VehiclesTab() : base("Vehicles", "Assets/Prefabs/Vehicles")
    {

    }

    public void SetTabData(VehiclesData data)
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
                if (prefabNames[j].Equals(data.list[i].vehicleName))
                    index = j;
            }
            string selectedPrefabPath = AssetDatabase.GUIDToAssetPath(prefabPaths[index]);
            selectedPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(selectedPrefabPath));
            selectedIndexes.Add(index);
            prefabCounts.Add(data.list[i].vehicleCount);
        }
    }

    public VehiclesData GetTabData()
    {
        List<VehicleDataInstance> temp = new List<VehicleDataInstance>();

        for (int i = 0; i < base.selectedIndexes.Count; i++)
        {
            string currentName = base.selectedPrefabs[i].name;
            int currentCount = base.prefabCounts[i];

            VehicleDataInstance existingInstance = temp.Find(x => x.vehicleName == currentName);

            if (existingInstance != null)
            {
                existingInstance.vehicleCount += currentCount;
            }
            else
            {
                temp.Add(new VehicleDataInstance
                {
                    vehicleName = currentName,
                    vehicleCount = currentCount
                });
            }
        }

        return new VehiclesData
        {
            list = temp
        };
    }

    public override void OnGUI()
    {
        GUILayout.Label("Vehicle Placement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Place vehicles throughout your scene. Each vehicle will be positioned on the terrain surface.", MessageType.Info);

        DrawMultiSelectionTab();
    }

    protected override void InstantiatePrefabOnTerrain(Transform terrainTransform, GameObject prefab, int count, List<Bounds> globalPlacedBounds)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Vehicle prefab is null. Skipping generation.");
            return;
        }

        if (roadAreas.Count == 0)
        {
            Debug.LogWarning("No road areas defined. Skipping vehicle generation.");
            return;
        }

        List<RoadArea> validRoads = roadAreas.Where(road =>
            (road.horizontal ^ road.vertical) && // Not an intersection
            road.direction >= 0 && road.direction <= 3 // Valid direction
        ).ToList();

        if (validRoads.Count == 0)
        {
            Debug.LogWarning("No valid directional roads found. Skipping vehicle generation.");
            return;
        }

        Vector3 prefabSize = GetPrefabDimensions(prefab);
        float prefabHeight = prefabSize.y;
        float bottomOffset = GetPrefabBottomOffset(prefab);

        GameObject prefabParent = GetOrCreateVehicleParent(prefab.name);

        int successfulPlacements = 0;
        int maxAttempts = count * 100; 
        int attempts = 0;

        while (successfulPlacements < count && attempts < maxAttempts)
        {
            attempts++;
            RoadArea selectedRoad = validRoads[Random.Range(0, validRoads.Count)];
            if (TryPlaceVehicleOnRoad(selectedRoad, prefab, prefabSize, prefabHeight, bottomOffset, prefabParent, globalPlacedBounds))
            {
                successfulPlacements++;
            }
        }

        Debug.Log($"Vehicle placement complete: {successfulPlacements}/{count} vehicles placed in {attempts} attempts");
    }

    private float GetPrefabBottomOffset(GameObject prefab)
    {
        GameObject tempPrefab = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        tempPrefab.transform.position = new Vector3(10000, 10000, 10000);

        Bounds bounds = GetVehicleBounds(tempPrefab);
        float bottomOffset = bounds.center.y - tempPrefab.transform.position.y - (bounds.size.y / 2);

        Object.DestroyImmediate(tempPrefab);
        return bottomOffset;
    }

    private Bounds GetObjectBounds(GameObject obj)
    {
        BoxCollider collider = obj.GetComponent<BoxCollider>();
        if (collider != null)
        {
            return collider.bounds;
        }

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds;
        }

        return new Bounds(obj.transform.position, Vector3.one);
    }

    public List<RoadArea> GetRoadAreas()
    {
        return roadAreas;
    }

    public void ValidateVehicleNavMesh()
    {
        VehicleMovement[] allVehicles = Object.FindObjectsOfType<VehicleMovement>();

        foreach (VehicleMovement vehicle in allVehicles)
        {
            UnityEngine.AI.NavMeshAgent agent = vehicle.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                Vector3 vehiclePos = vehicle.transform.position;
                bool onNavMesh = agent.isOnNavMesh;

                if (!onNavMesh)
                {
                    Debug.LogWarning($"Vehicle {vehicle.name} is NOT on NavMesh at position {vehiclePos}");

                    UnityEngine.AI.NavMeshHit hit;
                    if (UnityEngine.AI.NavMesh.SamplePosition(vehiclePos, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        vehicle.transform.position = hit.position;
                    }
                }
            }
        }
    }

    private Vector3 GetPrefabDimensions(GameObject prefab)
    {
        GameObject tempPrefab = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        tempPrefab.transform.position = new Vector3(10000, 10000, 10000);

        Bounds combinedBounds = GetVehicleBounds(tempPrefab);
        Vector3 size = combinedBounds.size;

        Object.DestroyImmediate(tempPrefab);
        return size;
    }

    private bool TryPlaceVehicleOnRoad(RoadArea roadArea, GameObject prefab, Vector3 prefabSize, float prefabHeight, float bottomOffset, GameObject parent, List<Bounds> globalPlacedBounds)
    {
        GameObject roadObj = roadArea.road;

        Bounds roadBounds = GetObjectBounds(roadObj);

        Vector3 lanePosition = CalculateLanePosition(roadArea, roadBounds, prefabSize);

        if (lanePosition == Vector3.zero)
        {
            return false;
        }

        if (!IsPositionFree(lanePosition, prefabSize, bottomOffset, globalPlacedBounds))
        {
            return false;
        }

        GameObject vehicleInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        vehicleInstance.transform.position = lanePosition;
        vehicleInstance.transform.SetParent(parent.transform);

        SetVehicleRotation(vehicleInstance, roadArea);
        EnsureVehicleCollider(vehicleInstance, prefabSize, prefabHeight, bottomOffset);
        AddMovementToVehicle(vehicleInstance);

        globalPlacedBounds.Add(new Bounds(
            vehicleInstance.transform.position + new Vector3(0, prefabHeight / 2 + bottomOffset, 0),
            prefabSize
        ));
        return true;
    }

    private void EnsureVehicleCollider(GameObject vehicle, Vector3 size, float height, float bottomOffset)
    {
        if (vehicle.GetComponent<BoxCollider>() == null)
        {
            BoxCollider boxCollider = vehicle.AddComponent<BoxCollider>();
            boxCollider.size = size * 1.5f;
            boxCollider.center = new Vector3(0, height / 2 + bottomOffset, 0);
        }
    }

    private GameObject GetOrCreateVehicleParent(string prefabName)
    {
        string parentName = "Vehicles " + prefabName + " Instances";
        GameObject prefabParent = GameObject.Find(parentName);

        if (prefabParent == null)
        {
            prefabParent = new GameObject(parentName);
            prefabParent.transform.SetParent(terrainGameObject.transform);
        }

        return prefabParent;
    }

    private bool IsPositionFree(Vector3 position, Vector3 vehicleSize, float heightOffset, List<Bounds> placedBounds)
    {
        Vector3 boundsCenter = position + new Vector3(0, vehicleSize.y / 2 + heightOffset, 0);
        Vector3 paddedSize = vehicleSize + new Vector3(vehiclePadding, 0, vehiclePadding);

        Bounds newBounds = new Bounds(boundsCenter, paddedSize);

        foreach (Bounds existingBounds in placedBounds)
        {
            if (newBounds.Intersects(existingBounds))
            {
                return false;
            }
        }

        Collider[] colliders = Physics.OverlapBox(
            boundsCenter,
            paddedSize / 2,
            Quaternion.identity
        );

        foreach (Collider collider in colliders)
        {
            if (!collider.gameObject.name.Contains("Terrain") &&
                !collider.gameObject.name.Contains("Road") &&
                !collider.gameObject.name.Contains("Crosswalk") &&
                !collider.gameObject.name.Contains("Visual_BaseSurface"))
            {
                return false;
            }
        }
        return true;
    }

    private void SetVehicleRotation(GameObject vehicle, RoadArea roadArea)
    {
        float rotationAngle = 0f;

        switch (roadArea.direction)
        {
            case 0: // East-bound
                rotationAngle = 90f;
                break;
            case 1: // West-bound
                rotationAngle = 270f;
                break;
            case 2: // North-bound
                rotationAngle = 0f;
                break;
            case 3: // South-bound
                rotationAngle = 180f;
                break;
        }
        vehicle.transform.rotation = Quaternion.Euler(0, rotationAngle, 0);
    }

    private Vector3 CalculateLanePosition(RoadArea roadArea, Bounds roadBounds, Vector3 vehicleSize)
    {
        Vector3 roadCenter = roadBounds.center;
        float laneOffset = 0.5f; 

        Vector3 position = roadCenter;

        if (roadArea.horizontal)
        {
            // Position vehicle in appropriate lane
            float roadWidth = roadBounds.size.z;

            if (vehicleSize.z > roadWidth * 0.8f)
            {
                return Vector3.zero;
            }

            if (roadArea.direction == 0) // East-bound
            {
                position.z += laneOffset; // Right lane
            }
            else // West-bound (direction == 1)
            {
                position.z -= laneOffset; // Left lane
            }

            float minX = roadBounds.min.x + vehicleSize.x / 2;
            float maxX = roadBounds.max.x - vehicleSize.x / 2;
            if (maxX > minX)
            {
                position.x = Random.Range(minX, maxX);
            }
        }
        else
        {
            // Position vehicle in appropriate lane
            float roadWidth = roadBounds.size.x;

            if (vehicleSize.x > roadWidth * 0.8f)
            {
                Debug.LogWarning($"Vehicle too wide for road segment {roadArea.road.name}");
                return Vector3.zero;
            }

            if (roadArea.direction == 2) // North-bound
            {
                position.x += laneOffset; // Right lane
            }
            else // South-bound (direction == 3)
            {
                position.x -= laneOffset; // Left lane
            }

            float minZ = roadBounds.min.z + vehicleSize.z / 2;
            float maxZ = roadBounds.max.z - vehicleSize.z / 2;
            if (maxZ > minZ)
            {
                position.z = Random.Range(minZ, maxZ);
            }
        }

        position.y = roadBounds.min.y + 0.05f; // Slightly above road

        return position;
    }

    protected override void DeletePrefabsFromScene()
    {
        if (terrainGameObject == null)
        {
            Debug.LogWarning("Terrain GameObject is null. Cannot delete vehicle instances.");
            return;
        }

        List<GameObject> objectsToDelete = new List<GameObject>();

        foreach (Transform child in terrainGameObject.transform)
        {
            if (child.name.StartsWith("Vehicles ") && child.name.EndsWith(" Instances"))
            {
                objectsToDelete.Add(child.gameObject);
            }
        }

        int deletedCount = objectsToDelete.Count;
        foreach (GameObject obj in objectsToDelete)
        {
            Object.DestroyImmediate(obj);
        }

        Debug.Log(deletedCount > 0
            ? $"Deleted {deletedCount} vehicles instance group(s) from the scene."
            : "No vehicles instances found to delete.");
    }

    private void AddMovementToVehicle(GameObject vehicleInstance)
    {
        LayerMovementManager manager = Object.FindObjectOfType<LayerMovementManager>();
        if (manager != null)
        {
            // Use the new simplified vehicle configuration
            manager.ConfigureVehicleForNavigation(vehicleInstance);
        }
        else
        {
            Debug.LogWarning("LayerMovementManager not found! Vehicle may not navigate properly.");

            // Add basic VehicleMovement component
            if (vehicleInstance.GetComponent<VehicleMovement>() == null)
            {
                vehicleInstance.AddComponent<VehicleMovement>();
            }
        }
    }

    private Bounds GetVehicleBounds(GameObject vehicle)
    {
        Bounds combinedBounds = new Bounds();
        bool boundsInitialized = false;

        Renderer[] allRenderers = vehicle.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in allRenderers)
        {
            if (!boundsInitialized)
            {
                combinedBounds = renderer.bounds;
                boundsInitialized = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return boundsInitialized ? combinedBounds : new Bounds(vehicle.transform.position, Vector3.one);
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

        for (int i = selectedPrefabs.Count - 1; i > 0; i--)
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