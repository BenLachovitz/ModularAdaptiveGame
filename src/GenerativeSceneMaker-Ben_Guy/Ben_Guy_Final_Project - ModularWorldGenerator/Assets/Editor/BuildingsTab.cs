using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class BuildingsTab : PrefabTab
{
    public BuildingsTab() : base("Buildings", "Assets/Prefabs/Buildings")
    {
    }

    public void SetTabData(BuildingsData data)
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
                if (prefabNames[j].Equals(data.list[i].buildingName))
                    index = j;
            }
            string selectedPrefabPath = AssetDatabase.GUIDToAssetPath(prefabPaths[index]);
            selectedPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(selectedPrefabPath));
            selectedIndexes.Add(index);
            prefabCounts.Add(data.list[i].buildingCount);
        }
    }

    public BuildingsData GetTabData()
    {
        List<BuildingsDataInstance> temp = new List<BuildingsDataInstance>();

        for (int i = 0; i < base.selectedIndexes.Count; i++)
        {
            string currentName = base.selectedPrefabs[i].name;
            int currentCount = base.prefabCounts[i];

            BuildingsDataInstance existingInstance = temp.Find(x => x.buildingName == currentName);

            if (existingInstance != null)
            {
                existingInstance.buildingCount += currentCount;
            }
            else
            {
                temp.Add(new BuildingsDataInstance
                {
                    buildingName = currentName,
                    buildingCount = currentCount
                });
            }
        }

        return new BuildingsData
        {
            list = temp
        };
    }

    public override void OnGUI()
    {
        GUILayout.Label("Building Placement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Place buildings throughout your scene. Each building will be positioned within designated building areas and oriented toward the nearest road.", MessageType.Info);

        DrawMultiSelectionTab();
    }

    protected new void GeneratePrefabs(List<GameObject> selectedPrefabs, List<int> counts)
    {
        if (buildingAreas.Count == 0)
        {
            Debug.LogWarning("No building areas defined. Please create building areas with 'BuildingArea_' in their name before generating buildings.");
            return;
        }

        if (roadAreas.Count == 0)
        {
            Debug.LogWarning("No road areas defined. Building rotation will be random. Create road areas with 'RoadArea' or 'street' in their name for oriented buildings.");
        }

        base.GeneratePrefabs(selectedPrefabs, counts);
    }

    protected override void DeletePrefabsFromScene()
    {
        if (terrainGameObject == null)
            return;
        

        List<GameObject> objectsToDelete = new List<GameObject>();

        foreach (Transform child in terrainGameObject.transform)
        {
            if (child.name.StartsWith("Buildings ") && child.name.EndsWith(" Instances"))
            {
                objectsToDelete.Add(child.gameObject);
            }
        }

        int deletedCount = objectsToDelete.Count;
        foreach (GameObject obj in objectsToDelete)
        {
            Object.DestroyImmediate(obj);
        }

        foreach (BuildingArea buildingArea in buildingAreas)
        {
            buildingArea.ClearOccupiedCells();
        }
    }

    protected override void InstantiatePrefabOnTerrain(Transform terrainTransform, GameObject prefab, int count, List<Bounds> globalPlacedBounds)
    {
        if (prefab == null)
            return;
        

        if (buildingAreas.Count == 0)
        {
            Debug.LogWarning("No building areas defined. Skipping generation. Please create objects with 'BuildingArea' in their name.");
            return;
        }

        // Prefab measurements
        float prefabHeight = 0;
        Vector3 prefabSize = Vector3.one; 
        float bottomOffset = 0; 

        // Instantiate a temporary prefab to get accurate bounds
        GameObject tempPrefab = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        tempPrefab.transform.position = new Vector3(10000, 10000, 10000); // Placing far away from scene

        // Get the combined bounds of all renderers
        Bounds combinedBounds = new Bounds();
        bool boundsInitialized = false;

        Renderer[] allRenderers = tempPrefab.GetComponentsInChildren<Renderer>();
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

        if (boundsInitialized)
        {
            prefabSize = combinedBounds.size;
            prefabHeight = prefabSize.y;
            bottomOffset = combinedBounds.center.y - tempPrefab.transform.position.y - (prefabSize.y / 2);
        }
        else
        {
            // Fallback for prefabs without renderers
            prefabHeight = 1f;
            prefabSize = Vector3.one;
            bottomOffset = 0;
        }

        Object.DestroyImmediate(tempPrefab);

        GameObject prefabParent = GameObject.FindObjectsOfType<GameObject>()
                .FirstOrDefault(go => go.name.Contains("Buildings " + prefab.name + " Instances"));
        if (prefabParent == null)
            prefabParent = new GameObject("Buildings " + prefab.name + " Instances");

        prefabParent.transform.SetParent(terrainGameObject.transform);

        int successfulPlacements = 0;

        if (!isCityMode)
            successfulPlacements = handlesNonCityModeBuildings(prefab, count, globalPlacedBounds, prefabParent, prefabSize,
                prefabHeight, bottomOffset);
        else
            successfulPlacements = handlesCityModeBuildings(prefab, count, globalPlacedBounds, prefabParent, prefabSize,
                prefabHeight, bottomOffset);
    }

    private void ClearNPCsInBuildingArea(Vector3 worldPosition, Vector3 prefabSize, float prefabHeight, float bottomOffset)
    {
        // Calculate the area to check 
        Vector3 checkCenter = worldPosition + new Vector3(0, prefabHeight / 2 + bottomOffset, 0);
        Vector3 checkSize = prefabSize + Vector3.one * 1f; 

        Collider[] collidersInArea = Physics.OverlapBox(checkCenter, checkSize / 2f);

        foreach (Collider col in collidersInArea)
        {
            GameObject obj = col.gameObject;
            if (obj.transform.parent != null && obj.transform.parent.name.ToLower().Contains("npc"))
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private int handlesNonCityModeBuildings(GameObject prefab, int count, List<Bounds> globalPlacedBounds, GameObject prefabParent
        , Vector3 prefabSize, float prefabHeight, float bottomOffset)
    {
        int successfulPlacements = 0;
        // Edge-based placement (frame-like structure)
        buildingAreas = buildingAreas.OrderBy(x => Random.value).ToList();

        foreach (BuildingArea buildingArea in buildingAreas)
        {
            float cellSize = 2f;

            int originalSizeX = Mathf.CeilToInt(prefabSize.x / cellSize);
            int originalSizeZ = Mathf.CeilToInt(prefabSize.z / cellSize);

            int spacing = 2;

            List<EdgePosition> edgePositions = new List<EdgePosition>();

            // Top edge (z = 0)
            for (int x = 0; x < buildingArea.GridWidth; x++)
            {
                edgePositions.Add(new EdgePosition(
                    x, 0,
                    Quaternion.Euler(0, 0, 0), // Face inward (south)
                    originalSizeX, originalSizeZ
                ));
            }

            // Bottom edge (z = gridLength - 1)
            for (int x = 0; x < buildingArea.GridWidth; x++)
            {
                edgePositions.Add(new EdgePosition(
                    x, buildingArea.GridLength - originalSizeZ,
                    Quaternion.Euler(0, 180, 0), // Face inward (north)
                    originalSizeX, originalSizeZ
                ));
            }

            // Left edge (x = 0)
            for (int z = 0; z < buildingArea.GridLength; z++)
            {
                edgePositions.Add(new EdgePosition(
                    0, z,
                    Quaternion.Euler(0, 90, 0), // Face inward (east)
                    originalSizeZ, originalSizeX // Swap due to rotation
                ));
            }

            // Right edge (x = gridWidth - 1)
            for (int z = 0; z < buildingArea.GridLength; z++)
            {
                edgePositions.Add(new EdgePosition(
                    buildingArea.GridWidth - originalSizeZ, z,
                    Quaternion.Euler(0, 270, 0), // Face inward (west)
                    originalSizeZ, originalSizeX // Swap due to rotation
                ));
            }

            // Shuffle the positions to get a more natural distribution
            edgePositions = edgePositions.OrderBy(x => Random.value).ToList();

            foreach (var position in edgePositions)
            {
                if (position.x + position.footprintX > buildingArea.GridWidth ||
                    position.z + position.footprintZ > buildingArea.GridLength)
                    continue;

                if (!buildingArea.CanPlaceAt(position.x, position.z, position.footprintX, position.footprintZ))
                    continue;

                float angleY = position.rotation.eulerAngles.y;
                bool swapAxes = Mathf.Abs(Mathf.Round(angleY) % 180) == 90;

                Vector3 cellOrigin = buildingArea.GridToWorld(position.x, position.z);
                Vector3 worldPosition;

                if (swapAxes)
                {
                    // When rotated 90 or 270 degrees, we need to adjust the positioning logic
                    worldPosition = cellOrigin + new Vector3(prefabSize.z / 2f, 0, prefabSize.x / 2f);
                }
                else
                {
                    worldPosition = cellOrigin + new Vector3(prefabSize.x / 2f, 0, prefabSize.z / 2f);
                }

                worldPosition.y = buildingArea.position.y - bottomOffset;

                Vector3 boundsCenter = worldPosition + new Vector3(0, prefabHeight / 2 + bottomOffset, 0);
                Bounds newObjectBounds;

                if (swapAxes)
                {
                    newObjectBounds = new Bounds(boundsCenter, new Vector3(prefabSize.z, prefabHeight, prefabSize.x));
                }
                else
                {
                    newObjectBounds = new Bounds(boundsCenter, new Vector3(prefabSize.x, prefabHeight, prefabSize.z));
                }

                if (globalPlacedBounds.Any(existing => existing.Intersects(newObjectBounds)))
                    continue;

                ClearNPCsInBuildingArea(worldPosition, prefabSize, prefabHeight, bottomOffset);


                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.transform.position = worldPosition;
                instance.transform.rotation = position.rotation;
                instance.transform.SetParent(prefabParent.transform);

                BoxCollider buildingColliderSurface = instance.GetComponent<BoxCollider>();
                if (buildingColliderSurface == null)
                {
                    buildingColliderSurface = instance.AddComponent<BoxCollider>();
                }

                Vector3 scaleSurface = instance.transform.localScale;

                buildingColliderSurface.size = new Vector3(prefabSize.x / scaleSurface.x, 4f, prefabSize.z / scaleSurface.z);

                buildingColliderSurface.center = new Vector3(0, 0, 0);
                buildingColliderSurface.isTrigger = false;

                SetupBuildingAsNavMeshObstacle(instance);

                int startX = Mathf.Max(0, position.x - spacing);
                int startZ = Mathf.Max(0, position.z - spacing);
                int endX = Mathf.Min(buildingArea.GridWidth, position.x + position.footprintX + spacing);
                int endZ = Mathf.Min(buildingArea.GridLength, position.z + position.footprintZ + spacing);

                buildingArea.MarkOccupied(startX, startZ, endX - startX, endZ - startZ);
                globalPlacedBounds.Add(newObjectBounds);

                successfulPlacements++;
                if (successfulPlacements >= count)
                    return successfulPlacements;
            }
        }
        return successfulPlacements;
    }

    private struct EdgePosition
    {
        public int x;
        public int z;
        public Quaternion rotation;
        public int footprintX;
        public int footprintZ;

        public EdgePosition(int x, int z, Quaternion rotation, int footprintX, int footprintZ)
        {
            this.x = x;
            this.z = z;
            this.rotation = rotation;
            this.footprintX = footprintX;
            this.footprintZ = footprintZ;
        }
    }

    private int handlesCityModeBuildings(GameObject prefab, int count, List<Bounds> globalPlacedBounds, GameObject prefabParent
        , Vector3 prefabSize, float prefabHeight, float bottomOffset)
    {
        int successfulPlacements = 0;

        buildingAreas = buildingAreas.OrderBy(x => Random.value).ToList();

        foreach (BuildingArea buildingArea in buildingAreas)
        {
            float cellSize = 2f;
            int originalSizeX = Mathf.CeilToInt(prefabSize.x / cellSize);
            int originalSizeZ = Mathf.CeilToInt(prefabSize.z / cellSize);

            int spacing = 2; 

            Vector3 referenceCenter;

            int stepX = 1; 
            int stepZ = 1; 

            if (buildingArea.parkArea != null)
            {
                // Use park position as reference if available
                referenceCenter = buildingArea.parkArea.GetComponent<Renderer>()?.bounds.center ??
                                  buildingArea.parkArea.transform.position;
            }
            else
            {
                // Use building area center as reference if no park is available
                referenceCenter = buildingArea.position;
                stepX = 2; stepZ = 2;
                spacing = 3;
            }

            List<GridPosition> gridPositions = new List<GridPosition>();

            for (int x = 0; x < buildingArea.GridWidth; x += stepX)
            {
                for (int z = 0; z < buildingArea.GridLength; z += stepZ)
                {

                    int paddingCells = 1;

                    if (x < paddingCells || z < paddingCells ||
                        x >= buildingArea.GridWidth - paddingCells ||
                        z >= buildingArea.GridLength - paddingCells)
                        continue;

                    if (!buildingArea.CanPlaceAt(x, z, 1, 1))
                        continue;

                    Vector3 worldPos = buildingArea.GridToWorld(x, z);

                    Vector3 direction = (worldPos - referenceCenter).normalized;
                    direction.y = 0;

                    if (direction.magnitude < 0.01f)
                        continue;

                    // Calculate rotation to face away from reference center (park or area center)
                    float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    float snappedAngle = Mathf.Round(angle / 90f) * 90f;
                    Quaternion rotation = Quaternion.Euler(0, snappedAngle, 0);

                    // Calculate the correct footprint based on rotation
                    bool swapAxes = Mathf.Abs(Mathf.Round(rotation.eulerAngles.y) % 180) == 90;
                    int footprintX = swapAxes ? originalSizeZ : originalSizeX;
                    int footprintZ = swapAxes ? originalSizeX : originalSizeZ;

                    // Make sure we don't go out of bounds with the rotated footprint
                    if (x + footprintX > buildingArea.GridWidth || z + footprintZ > buildingArea.GridLength)
                        continue;

                    // Check if we can place with the full footprint
                    if (!buildingArea.CanPlaceAt(x, z, footprintX, footprintZ))
                        continue;

                    float distance = Vector3.Distance(worldPos, referenceCenter);

                    if (x + footprintX >= buildingArea.GridWidth - paddingCells ||
                          z + footprintZ >= buildingArea.GridLength - paddingCells)
                        continue;

                    gridPositions.Add(new GridPosition(
                        x, z,
                        rotation,
                        footprintX, footprintZ,
                        distance
                    ));
                }
            }

            var randomizedPositions = gridPositions.OrderBy(x => Random.value).ToList();

            foreach (var position in randomizedPositions)
            {
                if (!buildingArea.CanPlaceAt(position.x, position.z, position.footprintX, position.footprintZ))
                    continue;

                float angleY = position.rotation.eulerAngles.y;
                bool swapAxes = Mathf.Abs(Mathf.Round(angleY) % 180) == 90;

                Vector3 cellOrigin = buildingArea.GridToWorld(position.x, position.z);
                Vector3 worldPosition;

                if (swapAxes)
                {
                    // When rotated 90 or 270 degrees, we need to adjust the positioning logic
                    worldPosition = cellOrigin + new Vector3(prefabSize.z / 2f, 0, prefabSize.x / 2f);
                }
                else
                {
                    worldPosition = cellOrigin + new Vector3(prefabSize.x / 2f, 0, prefabSize.z / 2f);
                }

                worldPosition.y = buildingArea.position.y - bottomOffset;

                Vector3 boundsCenter = worldPosition + new Vector3(0, prefabHeight / 2 + bottomOffset, 0);
                Bounds newObjectBounds;

                if (swapAxes)
                {
                    newObjectBounds = new Bounds(boundsCenter, new Vector3(prefabSize.z, prefabHeight, prefabSize.x));
                }
                else
                {
                    newObjectBounds = new Bounds(boundsCenter, new Vector3(prefabSize.x, prefabHeight, prefabSize.z));
                }

                if (globalPlacedBounds.Any(existing => existing.Intersects(newObjectBounds)))
                    continue;

                ClearNPCsInBuildingArea(worldPosition, prefabSize, prefabHeight, bottomOffset);

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.transform.position = worldPosition;
                instance.transform.rotation = position.rotation;
                instance.transform.SetParent(prefabParent.transform);

                BoxCollider buildingColliderSurface = instance.GetComponent<BoxCollider>();
                if (buildingColliderSurface == null)
                {
                    buildingColliderSurface = instance.AddComponent<BoxCollider>();
                }

                Vector3 scaleSurface = instance.transform.localScale;

                buildingColliderSurface.size = new Vector3(prefabSize.x / scaleSurface.x, 4f, prefabSize.z / scaleSurface.z);

                buildingColliderSurface.center = new Vector3(0, 0, 0);
                buildingColliderSurface.isTrigger = false;

                SetupBuildingAsNavMeshObstacle(instance);

                int startX = Mathf.Max(0, position.x - spacing);
                int startZ = Mathf.Max(0, position.z - spacing);
                int endX = Mathf.Min(buildingArea.GridWidth, position.x + position.footprintX + spacing);
                int endZ = Mathf.Min(buildingArea.GridLength, position.z + position.footprintZ + spacing);

                buildingArea.MarkOccupied(startX, startZ, endX - startX, endZ - startZ);

                globalPlacedBounds.Add(newObjectBounds);

                successfulPlacements++;

                if (successfulPlacements >= count)
                    return successfulPlacements;
            }
        }

        return successfulPlacements;
    }

    private struct GridPosition
    {
        public int x;
        public int z;
        public Quaternion rotation;
        public int footprintX;
        public int footprintZ;
        public float distanceFromReference;

        public GridPosition(int x, int z, Quaternion rotation, int footprintX, int footprintZ, float distanceFromReference)
        {
            this.x = x;
            this.z = z;
            this.rotation = rotation;
            this.footprintX = footprintX;
            this.footprintZ = footprintZ;
            this.distanceFromReference = distanceFromReference;
        }
    }

    // Method to add NavMesh Obstacle to buildings
    private void SetupBuildingAsNavMeshObstacle(GameObject buildingInstance)
    {
        UnityEngine.AI.NavMeshObstacle obstacle = buildingInstance.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = buildingInstance.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        }

        obstacle.carving = true;
        obstacle.carvingMoveThreshold = 0.1f;
        obstacle.carvingTimeToStationary = 0.5f;

        Vector3 obstacleSize = GetRotatedObstacleSize(buildingInstance);

        if (obstacleSize != Vector3.zero)
        {
            obstacle.center = Vector3.zero;
            obstacle.size = obstacleSize;
        }
        else
        {
            obstacle.size = new Vector3(8f, 12f, 8f); 
        }
    }

    // Enhanced method to get obstacle size accounting for rotation
    private Vector3 GetRotatedObstacleSize(GameObject buildingInstance)
    {
        // 1. Try to get bounds from all renderers
        Renderer[] allRenderers = buildingInstance.GetComponentsInChildren<Renderer>();

        if (allRenderers.Length > 0)
        {
            return CalculateLocalObstacleSizeFromRenderers(buildingInstance, allRenderers);
        }

        // 2. Try collider bounds
        Collider buildingCollider = buildingInstance.GetComponent<Collider>();
        if (buildingCollider != null)
        {
            return CalculateLocalObstacleSizeFromCollider(buildingInstance, buildingCollider);
        }

        // 3. Try children colliders
        Collider[] childColliders = buildingInstance.GetComponentsInChildren<Collider>();
        if (childColliders.Length > 0)
        {
            return CalculateLocalObstacleSizeFromChildColliders(buildingInstance, childColliders);
        }

        return Vector3.zero; // No size could be determined
    }

    private Vector3 CalculateLocalObstacleSizeFromRenderers(GameObject buildingInstance, Renderer[] renderers)
    {
        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localSize = TransformWorldBoundsToLocalObstacleSize(buildingInstance, worldBounds);

        return localSize;
    }

    private Vector3 CalculateLocalObstacleSizeFromCollider(GameObject buildingInstance, Collider collider)
    {
        Bounds worldBounds = collider.bounds;
        Vector3 localSize = TransformWorldBoundsToLocalObstacleSize(buildingInstance, worldBounds);

        return localSize;
    }

    private Vector3 CalculateLocalObstacleSizeFromChildColliders(GameObject buildingInstance, Collider[] colliders)
    {
        Bounds worldBounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            worldBounds.Encapsulate(colliders[i].bounds);
        }

        Vector3 localSize = TransformWorldBoundsToLocalObstacleSize(buildingInstance, worldBounds);

        return localSize;
    }

    private Vector3 TransformWorldBoundsToLocalObstacleSize(GameObject buildingInstance, Bounds worldBounds)
    {
        // NavMeshObstacle size should be in LOCAL space of the building
        // So we need to "un-rotate" the world bounds to get the correct local size

        Transform buildingTransform = buildingInstance.transform;

        // Get the 8 corners of the world bounds and transfoming them to local space
        Vector3[] worldCorners = new Vector3[8];
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        worldCorners[0] = new Vector3(min.x, min.y, min.z);
        worldCorners[1] = new Vector3(max.x, min.y, min.z);
        worldCorners[2] = new Vector3(min.x, max.y, min.z);
        worldCorners[3] = new Vector3(max.x, max.y, min.z);
        worldCorners[4] = new Vector3(min.x, min.y, max.z);
        worldCorners[5] = new Vector3(max.x, min.y, max.z);
        worldCorners[6] = new Vector3(min.x, max.y, max.z);
        worldCorners[7] = new Vector3(max.x, max.y, max.z);

        Vector3[] localCorners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            localCorners[i] = buildingTransform.InverseTransformPoint(worldCorners[i]);
        }

        Vector3 localMin = localCorners[0];
        Vector3 localMax = localCorners[0];

        for (int i = 1; i < 8; i++)
        {
            localMin = Vector3.Min(localMin, localCorners[i]);
            localMax = Vector3.Max(localMax, localCorners[i]);
        }

        Vector3 localSize = localMax - localMin;

        localSize.x = Mathf.Max(localSize.x, 1f);
        localSize.y = Mathf.Max(localSize.y, 1f);
        localSize.z = Mathf.Max(localSize.z, 1f);

        return localSize;
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