using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class ConfigurationTab : BaseTab
{
    private float terrainLength = 500f;
    private float terrainWidth = 500f;
    private float chosenTerrainWidth = 500f;
    private float chosenTerrainLength = 500f;
    private bool immersiveValue = false;

    // Environment configuration
    private float environmentDensity = 0.5f; // 0.0 to 1.0 density slider
    private int numBuildingAreas = 5;
    private int minBuildingAreas = 10; 
    private int maxBuildingAreas = 100; 

    private float cityModeThreshold = 0.7f; 

    // Size ranges for building areas - will be dynamically adjusted based on density
    private float minBuildingAreaSize = 50f;
    private float maxBuildingAreaSize = 130f;

    // City mode building sizes (smaller buildings packed closer together)
    private float cityMinBuildingSize = 20f;
    private float cityMaxBuildingSize = 60f;

    // Road configuration
    private float roadWidth = 10f;
    private int numHorizontalRoads = 3; 
    private int numVerticalRoads = 3; 
    private int minMainRoads = 1;
    private int maxMainRoads = 4;

    // Park configuration for city mode
    private float parkRatio = 0.1f; // Base ratio of park area in city mode (adjusted by density)

    // Materials
    private Material planeMaterial;
    private Material roadMaterial;
    private Material parkMaterial;
    private List<Material> availableMaterials = new List<Material>();
    private string[] materialNames;
    int newBuildingMatIndex = -1;
    int newRoadMatIndex = -1;
    int newParkMatIndex = -1;
    int newcrossMatIndex = -1;

    // Trees Config
    private List<GameObject> treePrefabs = new List<GameObject>();
    private List<GameObject> placedTrees = new List<GameObject>();
    private float baseTreeSpacing = 10f;
    private float treeSpacing; // Will be adjusted based on environment density
    private float minTreeMargin = 3f; // Minimum margin to keep trees away from buildings/roads
    private float maxTreeMargin = 10f; // Maximum margin to keep trees away from buildings/roads
    private GameObject treesContainer;

    // Lists to store generated planes
    private List<GameObject> parkAreas = new List<GameObject>();
    private List<Rect> mainRoadRects = new List<Rect>();
    private GameObject baseSurface; // The base park that covers the entire terrain

    [Header("Crosswalk Settings")]
    private Material crosswalkMaterial;
    public float crosswalkWidth = 3f;
    private float crosswalkSpacing = 80f; // Distance between mid-block crosswalks
    private bool generateMidBlockCrosswalks = true; // Option to create crosswalks on straight roads

    private ConfigurationAttributeData attributeData = new ConfigurationAttributeData();

    // Control
    bool materialsError = false;

    LayerMovementManager existingManager;

    public ConfigurationTab()
    {
        LoadMaterialsFromFolder();
        EditorApplication.playModeStateChanged += this.OnPlayModeChangedInstance;
        EditorApplication.quitting += OnEditorQuitting;
        EditorApplication.focusChanged += OnEditorFocusChanged;
        loadAttributes();
    }

    ~ConfigurationTab()
    {
        CleanupEventSubscriptions();
    }

    private void OnEditorQuitting()
    {
        saveAttributes();
    }

    private void OnEditorFocusChanged(bool hasFocus)
    {
        if (!hasFocus)
            saveAttributes();
    }

    private void CleanupEventSubscriptions()
    {
        EditorApplication.playModeStateChanged -= this.OnPlayModeChangedInstance;
        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.focusChanged -= OnEditorFocusChanged;
    }

    public void Dispose()
    {
        saveAttributes();
        CleanupEventSubscriptions();
    }

    private void saveAttributes()
    {
        try
        {
            attributeData.terrainLength = this.chosenTerrainLength;
            attributeData.terrainWidth = this.chosenTerrainWidth;
            attributeData.environmentDensity = this.environmentDensity;
            attributeData.horizontalRoads = this.numHorizontalRoads;
            attributeData.verticalRoads = this.numVerticalRoads;
            attributeData.buildingAreaMaterial = this.newBuildingMatIndex;
            attributeData.roadMaterial = this.newRoadMatIndex;
            attributeData.parkMaterial = this.newParkMatIndex;
            attributeData.crosswalkMaterial = this.newcrossMatIndex;
            attributeData.isAllLayersExist = allLayersExist;

            string json = JsonUtility.ToJson(attributeData, true);
            string key = $"{GetType().Name}_AttributeData";
            EditorPrefs.SetString(key, json);

            Debug.Log($"Attributes saved: {json}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save attributes: {e.Message}");
        }
    }

    private void loadAttributes()
    {
        try
        {
            string key = $"{GetType().Name}_AttributeData";
            if (EditorPrefs.HasKey(key))
            {
                string json = EditorPrefs.GetString(key);
                if (!string.IsNullOrEmpty(json))
                {
                    attributeData = JsonUtility.FromJson<ConfigurationAttributeData>(json);
                    this.chosenTerrainLength = attributeData.terrainLength;
                    this.chosenTerrainWidth = attributeData.terrainWidth;
                    this.environmentDensity = attributeData.environmentDensity;
                    this.numHorizontalRoads = attributeData.horizontalRoads;
                    this.numVerticalRoads = attributeData.verticalRoads;
                    this.newBuildingMatIndex = attributeData.buildingAreaMaterial;
                    allLayersExist = attributeData.isAllLayersExist;
                    findTerrainAsGameObject();
                    if (newBuildingMatIndex > 0 && newBuildingMatIndex < availableMaterials.Count + 1) 
                        planeMaterial = availableMaterials[newBuildingMatIndex - 1];
                    this.newRoadMatIndex = attributeData.roadMaterial;
                    if (newRoadMatIndex > 0 && newRoadMatIndex < availableMaterials.Count + 1) 
                        roadMaterial = availableMaterials[newRoadMatIndex - 1];
                    this.newParkMatIndex = attributeData.parkMaterial;
                    if (newParkMatIndex > 0 && newParkMatIndex < availableMaterials.Count + 1) 
                        parkMaterial = availableMaterials[newParkMatIndex - 1];
                    this.newcrossMatIndex = attributeData.crosswalkMaterial;
                    if (newcrossMatIndex > 0 && newcrossMatIndex < availableMaterials.Count + 1) 
                        crosswalkMaterial = availableMaterials[newcrossMatIndex - 1];

                    Debug.Log($"Attributes loaded successfully from: {json}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load attributes: {e.Message}");
        }
    }

    private void OnPlayModeChangedInstance(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            saveAttributes();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            loadAttributes();
        }
    }

    public void SetTabData(ConfigurationData data)
    {
        LoadMaterialsFromFolder();
        this.chosenTerrainLength = data.terrainLength;
        this.chosenTerrainWidth = data.terrainWidth;
        this.environmentDensity = data.environmentDensity;
        this.numHorizontalRoads = data.numHorizontalRoads;
        this.numVerticalRoads = data.numVerticalRoads;
        newBuildingMatIndex = GetIndexOfMaterialByName(data.buildingAreaMaterial);
        if (newBuildingMatIndex > 0 && newBuildingMatIndex < availableMaterials.Count + 1)
        {
            planeMaterial = availableMaterials[newBuildingMatIndex - 1];
        }
        newRoadMatIndex = GetIndexOfMaterialByName(data.roadMaterial);
        if (newRoadMatIndex > 0 && newRoadMatIndex < availableMaterials.Count + 1)
        {
            roadMaterial = availableMaterials[newRoadMatIndex - 1];
        }
        newParkMatIndex = GetIndexOfMaterialByName(data.parkMaterial);
        if (newParkMatIndex > 0 && newParkMatIndex < availableMaterials.Count + 1)
        {
            parkMaterial = availableMaterials[newParkMatIndex - 1];
        }
        newcrossMatIndex = GetIndexOfMaterialByName(data.crosswalkMaterial);
        if (newcrossMatIndex > 0 && newcrossMatIndex < availableMaterials.Count + 1)
        {
            crosswalkMaterial = availableMaterials[newcrossMatIndex - 1];

        }

    }


    public ConfigurationData GetTabData()
    {
        string buildingArea = "";
        string road = "";
        string park = "";
        string crosswalk = "";

        if (newBuildingMatIndex > 0)
            buildingArea = this.availableMaterials[newBuildingMatIndex - 1].name;

        if (newRoadMatIndex > 0)
            road = this.availableMaterials[newRoadMatIndex - 1].name;

        if (newParkMatIndex > 0)
            park = this.availableMaterials[newParkMatIndex - 1].name;

        if (newcrossMatIndex > 0)
            crosswalk = this.availableMaterials[newcrossMatIndex - 1].name;
        return new ConfigurationData
        {
            terrainLength = this.chosenTerrainLength,
            terrainWidth = this.chosenTerrainWidth,
            environmentDensity = this.environmentDensity,
            numHorizontalRoads = this.numHorizontalRoads,
            numVerticalRoads = this.numVerticalRoads,
            buildingAreaMaterial = buildingArea,
            roadMaterial = road,
            parkMaterial = park,
            crosswalkMaterial = crosswalk
        };
    }

    public override void OnGUI()
    {
        LoadMaterialsFromFolder();
        DrawTerrainCreationTab();
    }

    private int GetIndexOfMaterialByName(string name)
    {
        if (string.IsNullOrEmpty(name) || availableMaterials.Count == 0)
            return 0;

        for (int i = 0; i < availableMaterials.Count; i++)
        {
            if (name.Equals(availableMaterials[i].name))
                return i + 1;
        }
        return 0;
    }

    private int GetIndexOfMaterial(Material mat)
    {
        if (mat == null || availableMaterials.Count == 0)
            return 0;

        for (int i = 0; i < availableMaterials.Count; i++)
        {
            if (availableMaterials[i] == mat)
                return i + 1;
        }

        return 0; // Default to first material if not found
    }

    private int getNumOfCellsOnTerrain()
    {
        return (numVerticalRoads + 1) * (numHorizontalRoads + 1);
    }

    private void DrawTerrainCreationTab()
    {
        GUILayout.Label("Terrain", EditorStyles.boldLabel);

        if (!IsProFeatureEnabled())
        {
            EditorGUILayout.HelpBox("PRO FEATURE: Terrain customization is only available with a Pro license. Go to Settings tab to activate your license.", MessageType.Warning);
            GUI.enabled = false;
            chosenTerrainLength = 500;
            chosenTerrainWidth = 500;
        }
        chosenTerrainLength = EditorGUILayout.Slider("Terrain Length:", chosenTerrainLength, 100f, 1000f);
        chosenTerrainWidth = EditorGUILayout.Slider("Terrain Width:", chosenTerrainWidth, 100f, 1000f);
        GUI.enabled = true;

        GUILayout.Label("\nEnvironment Settings", EditorStyles.boldLabel);

        // Environment density slider
        GUILayout.BeginHorizontal();
        GUILayout.Label("Environment Density:", GUILayout.Width(EditorGUIUtility.labelWidth));
        environmentDensity = GUILayout.HorizontalSlider(environmentDensity, 0f, 1f);
        GUILayout.Label(environmentDensity.ToString("F2"), GUILayout.Width(35));
        GUILayout.EndHorizontal();

        // Display city mode indicator
        if (environmentDensity >= cityModeThreshold)
        {
            EditorGUILayout.HelpBox("City Mode Enabled - Dense urban environment will be generated", MessageType.Info);
            minMainRoads = 3;
            maxMainRoads = 7;
        }
        else
        {
            minMainRoads = 1;
            maxMainRoads = 4;
        }

        // Building size range based on density
        float minSize, maxSize;
        if (environmentDensity >= cityModeThreshold)
        {
            // In city mode, as density increases, buildings get smaller
            float cityDensityFactor = (environmentDensity - cityModeThreshold) / (1 - cityModeThreshold);
            minSize = Mathf.Lerp(cityMinBuildingSize, cityMinBuildingSize * 0.8f, cityDensityFactor);
            maxSize = Mathf.Lerp(cityMaxBuildingSize, cityMaxBuildingSize * 0.8f, cityDensityFactor);
        }
        else
        {
            minSize = Mathf.Lerp(minBuildingAreaSize * 1.2f, minBuildingAreaSize * 0.9f, environmentDensity);
            maxSize = Mathf.Lerp(maxBuildingAreaSize, maxBuildingAreaSize * 0.8f, environmentDensity);
        }

        // Calculate area-based building placement
        int numOfCells = getNumOfCellsOnTerrain();
        float cellWidth = chosenTerrainWidth / (numVerticalRoads + 1);
        float cellLength = chosenTerrainLength / (numHorizontalRoads + 1);

        if (environmentDensity >= cityModeThreshold)
        {
            // In city mode, calculate park areas
            float invertedCityDensity = 1f - ((environmentDensity - cityModeThreshold) / (1f - cityModeThreshold));
            int numParkAreas = Mathf.RoundToInt(numOfCells * invertedCityDensity * 0.6f);
            EditorGUILayout.LabelField($"Park Areas: Approximately {numParkAreas}");
        }
        else
        {
            // More practical approach: base number on cell count, then adjust for cell size
            float remappedDensity = environmentDensity / cityModeThreshold;
            remappedDensity = Mathf.Clamp01(remappedDensity);
            float baseBuildingsPerCell = Mathf.Lerp(0.3f, 1.0f, remappedDensity);

            // Size factor: larger cells can fit a few more buildings, but not linearly
            float referenceCellSize = 50f; // Reference cell size (100x100)
            float currentCellSize = Mathf.Sqrt(cellWidth * cellLength);
            float sizeFactor = Mathf.Sqrt(currentCellSize / referenceCellSize); // Square root for more reasonable scaling
            sizeFactor = Mathf.Clamp(sizeFactor, 1f, 3.0f); // Cap the scaling effect

            numBuildingAreas = Mathf.RoundToInt(numOfCells * baseBuildingsPerCell * sizeFactor);
            EditorGUILayout.LabelField($"Building Areas: Approximately {numBuildingAreas}");
        }

        if (environmentDensity < cityModeThreshold)
        {
            EditorGUILayout.LabelField($"Building Size Range: {minSize.ToString("F1")} - {maxSize.ToString("F1")} units");
        }

        // Main road grid configuration
        GUILayout.Label("\nRoad Network", EditorStyles.boldLabel);
        if (!IsProFeatureEnabled())
        {
            EditorGUILayout.HelpBox("PRO FEATURE: Road Network customization is only available with a Pro license. Go to Settings tab to activate your license.", MessageType.Warning);
            GUI.enabled = false;
            numHorizontalRoads = 3;
            numVerticalRoads = 3;
        }
        numHorizontalRoads = EditorGUILayout.IntSlider("Horizontal Main Roads:", numHorizontalRoads, minMainRoads, maxMainRoads);
        numVerticalRoads = EditorGUILayout.IntSlider("Vertical Main Roads:", numVerticalRoads, minMainRoads, maxMainRoads);
        GUI.enabled = true;


        // Vegetation settings section
        GUILayout.Label("\nVegetation Settings", EditorStyles.boldLabel);

        // Tree density information based on environment density
        float calculatedTreeDensity = CalculateTreeDensity(environmentDensity);
        EditorGUILayout.LabelField($"Tree Density: {calculatedTreeDensity.ToString("F2")} (adjusted for environment)");

        // Park info for city mode
        if (environmentDensity >= cityModeThreshold)
        {
            float cityParkRatio = CalculateCityParkRatio(environmentDensity);
            EditorGUILayout.LabelField($"Park Area: {(cityParkRatio * 100).ToString("F1")}% of urban space");
        }

        GUILayout.Label("\nMaterials", EditorStyles.boldLabel);

        // Materials
        int buildingMatIndex = GetIndexOfMaterial(planeMaterial);
        newBuildingMatIndex = EditorGUILayout.Popup("Building Area Material:", buildingMatIndex, materialNames);
        if (newBuildingMatIndex != buildingMatIndex && newBuildingMatIndex > 0 && newBuildingMatIndex < availableMaterials.Count + 1)
        {
            planeMaterial = availableMaterials[newBuildingMatIndex - 1];
        }

        int roadMatIndex = GetIndexOfMaterial(roadMaterial);
        newRoadMatIndex = EditorGUILayout.Popup("Road Material:", roadMatIndex, materialNames);
        if (newRoadMatIndex != roadMatIndex && newRoadMatIndex > 0 && newRoadMatIndex < availableMaterials.Count + 1)
        {
            roadMaterial = availableMaterials[newRoadMatIndex - 1];
        }

        int parkMatIndex = GetIndexOfMaterial(parkMaterial);
        newParkMatIndex = EditorGUILayout.Popup("Park Material:", parkMatIndex, materialNames);
        if (newParkMatIndex != parkMatIndex && newParkMatIndex > 0 && newParkMatIndex < availableMaterials.Count + 1)
        {
            parkMaterial = availableMaterials[newParkMatIndex - 1];
        }

        int crossMatIndex = GetIndexOfMaterial(crosswalkMaterial);
        newcrossMatIndex = EditorGUILayout.Popup("Crosswalk Material:", crossMatIndex, materialNames);
        if (newcrossMatIndex != crossMatIndex && newcrossMatIndex > 0 && newcrossMatIndex < availableMaterials.Count + 1)
        {
            crosswalkMaterial = availableMaterials[newcrossMatIndex - 1];
        }

        if (materialsError)
            EditorGUILayout.HelpBox("Please select materials for all surfaces", MessageType.Error);

        GUILayout.Label("");
        if (!allLayersExist)
        {
            GUI.enabled = false;
            EditorGUILayout.HelpBox("Please create all necessary layers. For that go to settings tab.", MessageType.Error);
        }
        else
        {
            GUI.enabled = true;
        }
        if (GUILayout.Button("Create Scene"))
        {
            if (planeMaterial != null && roadMaterial != null && parkMaterial != null)
            {
                materialsError = false;
                CreateTerrainInScene();
            }
            else
                materialsError = true;
        }

        if (GUILayout.Button("Create Full Random Scene"))
        {
            if (planeMaterial != null && roadMaterial != null && parkMaterial != null)
            {
                materialsError = false;
                CreateFullRandomScene();
            }
            else
                materialsError = true;
        }
        GUI.enabled = true;
    }

    private void LoadMaterialsFromFolder()
    {
        availableMaterials.Clear();

        string folderPath = "Assets/ModularWorldTool/Materials/Materials";

        if (!System.IO.Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Materials folder not found at: {folderPath}");
            materialNames = new string[0];
            return;
        }

        List<string> namesList = new List<string>();

        namesList.Add("Select Material");
        string[] materialFiles = System.IO.Directory.GetFiles(folderPath, "*.mat");

        foreach (string filePath in materialFiles)
        {
            string assetPath = filePath.Replace("\\", "/");

            if (assetPath.EndsWith(".mat"))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat != null)
                {
                    availableMaterials.Add(mat);
                    namesList.Add(mat.name);
                }
            }
        }

        materialNames = namesList.ToArray();
    }

    private void CreateTerrainInScene()
    {

        LayerMovementManager manager = Object.FindObjectOfType<LayerMovementManager>();
        if (manager != null)
        {
            manager.PrepareForTerrainGeneration(); // This clears all NavMesh data
        }

        foreach (GameObject tree in placedTrees)
        {
            if (tree != null)
            {
                Object.DestroyImmediate(tree);
            }
        }
        placedTrees.Clear();

        LoadTreePrefabs();

        buildingAreas.Clear();
        roadAreas.Clear();
        parkAreas.Clear();
        mainRoadRects.Clear();
        baseSurface = null;

        Terrain existingTerrain = Object.FindFirstObjectByType<Terrain>();
        if (existingTerrain != null)
        {
            Transform terrainTransform = existingTerrain.transform;
            for (int i = terrainTransform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(terrainTransform.GetChild(i).gameObject);
            }
            Object.DestroyImmediate(existingTerrain.gameObject);
        }

#if UNITY_EDITOR
    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    System.GC.Collect(); // Force garbage collection
#endif

        terrainWidth = chosenTerrainWidth + roadWidth * 2;
        terrainLength = chosenTerrainLength + roadWidth * 2;

        terrainGameObject = new GameObject("Terrain");
        Terrain terrain = terrainGameObject.AddComponent<Terrain>();
        TerrainCollider terrainCollider = terrainGameObject.AddComponent<TerrainCollider>();
        terrain.terrainData = new TerrainData
        {
            heightmapResolution = 513,
            size = new Vector3(terrainWidth, 50f, terrainLength)
        };
        terrainCollider.terrainData = terrain.terrainData;
        terrainGameObject.transform.position = Vector3.zero;

        GameObject parksContainer = new GameObject("ParkAreas");
        GameObject buildingsContainer = new GameObject("BuildingAreas");
        GameObject roadsContainer = new GameObject("RoadAreas");
        GameObject mainRoadsContainer = new GameObject("MainRoads");
        GameObject perimeterRoadsContainer = new GameObject("PerimeterRoads");
        treesContainer = new GameObject("Trees");

        parksContainer.transform.SetParent(terrainGameObject.transform);
        buildingsContainer.transform.SetParent(terrainGameObject.transform);
        roadsContainer.transform.SetParent(terrainGameObject.transform);
        mainRoadsContainer.transform.SetParent(terrainGameObject.transform);
        perimeterRoadsContainer.transform.SetParent(terrainGameObject.transform);
        treesContainer.transform.SetParent(terrainGameObject.transform);

        DeletePreviousRoadData();
        SetupLayerMovementManager();
        CreateBaseSurface(terrain);
        GeneratePerimeterRoads(perimeterRoadsContainer, terrainWidth, terrainLength);
        GenerateMainRoadsWithCrosswalks(mainRoadsContainer);

        if (environmentDensity >= cityModeThreshold)
        {
            isCityMode = true;
            GenerateCityLayout(parksContainer, buildingsContainer, roadsContainer);
        }
        else
        {
            isCityMode = false;
            GenerateBuildingsAlongRoads(buildingsContainer, roadsContainer);
        }

        SaveRoadDataForRuntime();

        PlaceTreesInParkAreas();

        DebugDrawRoadDirections(roadAreas);

        manager = Object.FindObjectOfType<LayerMovementManager>();
        if (manager != null)
            manager.SetupLayers();
        AssignLayersToTerrainObjectsWithCrosswalks();

        manager.OnTerrainGenerationComplete();

    }

    private void GeneratePerimeterRoads(GameObject perimeterRoadsContainer, float actualTerrainWidth, float actualTerrainLength)
    {
        float usableWidth = chosenTerrainWidth;
        float usableLength = chosenTerrainLength;
        float horizontalSpacing = usableLength / (numHorizontalRoads + 1);
        float verticalSpacing = usableWidth / (numVerticalRoads + 1);

        CreateCornerIntersections(perimeterRoadsContainer, actualTerrainWidth, actualTerrainLength);

        GenerateTopPerimeterSegments(perimeterRoadsContainer, actualTerrainWidth, actualTerrainLength, verticalSpacing);
        GenerateBottomPerimeterSegments(perimeterRoadsContainer, actualTerrainWidth, verticalSpacing);
        GenerateLeftPerimeterSegments(perimeterRoadsContainer, actualTerrainLength, horizontalSpacing);
        GenerateRightPerimeterSegments(perimeterRoadsContainer, actualTerrainWidth, actualTerrainLength, horizontalSpacing);
    }

    private void CreateCornerIntersections(GameObject perimeterRoadsContainer, float actualTerrainWidth, float actualTerrainLength)
    {
        Vector3[] cornerPositions = {
        new Vector3(roadWidth / 2, 0.03f, roadWidth / 2), // Bottom-Left
        new Vector3(actualTerrainWidth - roadWidth / 2, 0.03f, roadWidth / 2), // Bottom-Right  
        new Vector3(actualTerrainWidth - roadWidth / 2, 0.03f, actualTerrainLength - roadWidth / 2), // Top-Right
        new Vector3(roadWidth / 2, 0.03f, actualTerrainLength - roadWidth / 2) // Top-Left
    };

        string[] cornerNames = { "Corner_BottomLeft", "Corner_BottomRight", "Corner_TopRight", "Corner_TopLeft" };

        // For corner intersections, each allows only LEFT turn relative to incoming direction

        int[] vDir = { 3, 2, 2, 3 };        // Bottom-Left: South, Bottom-Right: North, Top-Right: North, Top-Left: South
        int[] hDir = { 0, 0, 1, 1 };        // Bottom-Left: East, Bottom-Right: East, Top-Right: West, Top-Left: West
        int[] changeVDir = { 1, 1, 1, 1 };  // All corners allow left turn from vertical roads
        int[] changeHDir = { 1, 1, 1, 1 };  // All corners allow left turn from horizontal roads

        for (int i = 0; i < cornerPositions.Length; i++)
        {
            GameObject cornerIntersection = CreatePlane(cornerNames[i], roadWidth, roadWidth, cornerPositions[i]);
            cornerIntersection.transform.SetParent(perimeterRoadsContainer.transform);

            BoxCollider intersactionCollider = cornerIntersection.GetComponent<BoxCollider>();
            if (intersactionCollider == null)
            {
                intersactionCollider = cornerIntersection.AddComponent<BoxCollider>();
            }

            Vector3 scale = cornerIntersection.transform.localScale;

            intersactionCollider.size = new Vector3(
                roadWidth / scale.x,
                0.1f / scale.y,
                roadWidth / scale.z);                

            intersactionCollider.center = Vector3.zero; // Center relative to the GameObject's position
            intersactionCollider.isTrigger = false;
                

            // Create corner intersection - only left turns allowed, no straight through
            roadAreas.Add(new IntersectionArea(
                cornerIntersection,
                false, false, -1,
                vDir[i], hDir[i], changeVDir[i], changeHDir[i],
                false, false
            ));

            if (roadMaterial != null)
                SetTilingBasedOnScale(cornerIntersection, roadMaterial);

                mainRoadRects.Add(new Rect(
                    cornerPositions[i].x - roadWidth / 2,
                    cornerPositions[i].z - roadWidth / 2,
                    roadWidth, roadWidth
            ));
        }
    }

    private void GenerateTopPerimeterSegments(GameObject perimeterRoadsContainer, float actualTerrainWidth, float actualTerrainLength, float verticalSpacing)
    {
        float zPos = actualTerrainLength - roadWidth / 2;
        float startOffsetX = roadWidth;
        int direction = 1; // West-bound

        int changeDir = -1; 
        int vDir = 3; // South 
        int hDir = 1; // West 

        // Create segments between corner intersections and any intermediate intersections
        for (int i = 0; i <= numVerticalRoads; i++)
        {
            float segmentStartX = (i == 0) ? roadWidth : startOffsetX + i * verticalSpacing + roadWidth / 2;
            float segmentEndX = (i == numVerticalRoads) ? actualTerrainWidth - roadWidth : startOffsetX + (i + 1) * verticalSpacing - roadWidth / 2;

            float segmentWidth = segmentEndX - segmentStartX;
            if (segmentWidth <= 0) continue;

            GameObject roadSegment = CreateDirectionalRoadPlane(
                $"PerimeterRoad_Top_Segment_{i}",
                segmentWidth, roadWidth,
                new Vector3(segmentStartX + segmentWidth / 2, 0.07f, zPos),
                direction
            );

            roadSegment.transform.SetParent(perimeterRoadsContainer.transform);

            
            BoxCollider roadCollider = roadSegment.GetComponent<BoxCollider>();
            if (roadCollider == null)
            {
                roadCollider = roadSegment.AddComponent<BoxCollider>();
            }

            Vector3 scale = roadSegment.transform.localScale;

            roadCollider.size = new Vector3(
                segmentWidth / scale.x,
                0.1f / scale.y,
                roadWidth / scale.z);

            roadCollider.center = Vector3.zero; // Center relative to the GameObject's position
            roadCollider.isTrigger = false;

            roadAreas.Add(new RoadArea(roadSegment, true, false, direction));

            if (roadMaterial != null)
                SetTilingBasedOnScale(roadSegment, roadMaterial);

            mainRoadRects.Add(new Rect(segmentStartX, zPos - roadWidth / 2, segmentWidth, roadWidth));

            if (i < numVerticalRoads)
            {
                float xPos = startOffsetX + (i + 1) * verticalSpacing;
                Vector3 intersectionCenter = new Vector3(xPos, 0.03f, zPos);

                GameObject intersection = CreatePlane($"PerimeterIntersection_Top_{i + 1}", roadWidth, roadWidth, intersectionCenter);
                intersection.transform.SetParent(perimeterRoadsContainer.transform);

                BoxCollider intersactionCollider = intersection.GetComponent<BoxCollider>();
                if (intersactionCollider == null)
                {
                    intersactionCollider = intersection.AddComponent<BoxCollider>();
                }

                scale = intersection.transform.localScale;

                intersactionCollider.size = new Vector3(
                    roadWidth / scale.x,
                    0.1f / scale.y,
                    roadWidth / scale.z);

                intersactionCollider.center = Vector3.zero; // Center relative to the GameObject's position
                intersactionCollider.isTrigger = false;

                // Top perimeter: only every other intersection allows left turn into city

                roadAreas.Add(new IntersectionArea(
                    intersection, false, false, -1,
                    vDir, hDir, 1, changeDir,
                    false, true 
                ));

                if (roadMaterial != null)
                    SetTilingBasedOnScale(intersection, roadMaterial);

                mainRoadRects.Add(new Rect(xPos - roadWidth / 2, zPos - roadWidth / 2, roadWidth, roadWidth));

                changeDir *= -1;
            }
        }
    }

    private void GenerateBottomPerimeterSegments(GameObject perimeterRoadsContainer, float actualTerrainWidth, float verticalSpacing)
    {
        float zPos = roadWidth / 2; // Bottom edge
        float startOffsetX = roadWidth;
        int direction = 0; // East-bound 

        int changeDir = 1;
        int vDir = 2; // North
        int hDir = 0; // East

        for (int i = 0; i <= numVerticalRoads; i++)
        {
            float segmentStartX = (i == 0) ? roadWidth : startOffsetX + i * verticalSpacing + roadWidth / 2;
            float segmentEndX = (i == numVerticalRoads) ? actualTerrainWidth - roadWidth : startOffsetX + (i + 1) * verticalSpacing - roadWidth / 2;

            float segmentWidth = segmentEndX - segmentStartX;
            if (segmentWidth <= 0) continue;

            GameObject roadSegment = CreateDirectionalRoadPlane(
                $"PerimeterRoad_Bottom_Segment_{i}",
                segmentWidth, roadWidth,
                new Vector3(segmentStartX + segmentWidth / 2, 0.07f, zPos),
                direction
            );

            roadSegment.transform.SetParent(perimeterRoadsContainer.transform);

            BoxCollider roadCollider = roadSegment.GetComponent<BoxCollider>();
            if (roadCollider == null)
            {
                roadCollider = roadSegment.AddComponent<BoxCollider>();
            }

            Vector3 scale = roadSegment.transform.localScale;

            roadCollider.size = new Vector3(
                segmentWidth / scale.x,
                0.1f / scale.y,
                roadWidth / scale.z);

            roadCollider.center = Vector3.zero; // Center relative to the GameObject's position
            roadCollider.isTrigger = false;

            roadAreas.Add(new RoadArea(roadSegment, true, false, direction));

            if (roadMaterial != null)
                SetTilingBasedOnScale(roadSegment, roadMaterial);

            mainRoadRects.Add(new Rect(segmentStartX, zPos - roadWidth / 2, segmentWidth, roadWidth));

            if (i < numVerticalRoads)
            {
                float xPos = startOffsetX + (i + 1) * verticalSpacing;
                Vector3 intersectionCenter = new Vector3(xPos, 0.03f, zPos);

                GameObject intersection = CreatePlane($"PerimeterIntersection_Bottom_{i + 1}", roadWidth, roadWidth, intersectionCenter);
                intersection.transform.SetParent(perimeterRoadsContainer.transform);

                BoxCollider intersactionCollider = intersection.GetComponent<BoxCollider>();
                if (intersactionCollider == null)
                {
                    intersactionCollider = intersection.AddComponent<BoxCollider>();
                }

                scale = intersection.transform.localScale;

                intersactionCollider.size = new Vector3(
                    roadWidth / scale.x,
                    0.1f / scale.y,
                    roadWidth / scale.z);

                intersactionCollider.center = Vector3.zero; // Center relative to the GameObject's position
                intersactionCollider.isTrigger = false;

                // Bottom perimeter: only every other intersection allows left turn into city

                roadAreas.Add(new IntersectionArea(
                    intersection, false, false, -1,
                    vDir, hDir, 1, changeDir,
                    false, true
                ));

                if (roadMaterial != null)
                    SetTilingBasedOnScale(intersection, roadMaterial);

                mainRoadRects.Add(new Rect(xPos - roadWidth / 2, zPos - roadWidth / 2, roadWidth, roadWidth));

                changeDir *= -1;
            }
        }
    }

    private void GenerateLeftPerimeterSegments(GameObject perimeterRoadsContainer, float actualTerrainLength, float horizontalSpacing)
    {
        float xPos = roadWidth / 2; // Left edge
        float startOffsetZ = roadWidth;
        int direction = 3; // South-bound

        int changeDir = 1; 
        int vDir = 3; // South
        int hDir = 0; // East

        for (int i = 0; i <= numHorizontalRoads; i++)
        {
            float segmentStartZ = (i == 0) ? roadWidth : startOffsetZ + i * horizontalSpacing + roadWidth / 2;
            float segmentEndZ = (i == numHorizontalRoads) ? actualTerrainLength - roadWidth : startOffsetZ + (i + 1) * horizontalSpacing - roadWidth / 2;

            float segmentHeight = segmentEndZ - segmentStartZ;
            if (segmentHeight <= 0) continue;

            GameObject roadSegment = CreateDirectionalRoadPlane(
                $"PerimeterRoad_Left_Segment_{i}",
                roadWidth, segmentHeight,
                new Vector3(xPos, 0.07f, segmentStartZ + segmentHeight / 2),
                direction
            );

            roadSegment.transform.SetParent(perimeterRoadsContainer.transform);

            BoxCollider roadCollider = roadSegment.GetComponent<BoxCollider>();
            if (roadCollider == null)
            {
                roadCollider = roadSegment.AddComponent<BoxCollider>();
            }

            Vector3 scale = roadSegment.transform.localScale;

            roadCollider.size = new Vector3(
                roadWidth / scale.x,
                0.1f / scale.y,
                segmentHeight / scale.z);
                
            roadCollider.center = Vector3.zero; // Center relative to the GameObject's position
            roadCollider.isTrigger = false;

            roadAreas.Add(new RoadArea(roadSegment, false, true, direction));

            if (roadMaterial != null)
                SetTilingBasedOnScale(roadSegment, roadMaterial);

            mainRoadRects.Add(new Rect(xPos - roadWidth / 2, segmentStartZ, roadWidth, segmentHeight));

            if (i < numHorizontalRoads)
            {
                float zPos = startOffsetZ + (i + 1) * horizontalSpacing;
                Vector3 intersectionCenter = new Vector3(xPos, 0.03f, zPos);

                GameObject intersection = CreatePlane($"PerimeterIntersection_Left_{i + 1}", roadWidth, roadWidth, intersectionCenter);
                intersection.transform.SetParent(perimeterRoadsContainer.transform);

                BoxCollider intersactionCollider = intersection.GetComponent<BoxCollider>();
                if (intersactionCollider == null)
                {
                    intersactionCollider = intersection.AddComponent<BoxCollider>();
                }

                scale = intersection.transform.localScale;

                intersactionCollider.size = new Vector3(
                    roadWidth / scale.x,
                    0.1f / scale.y,
                    roadWidth / scale.z);

                intersactionCollider.center = Vector3.zero; // Center relative to the GameObject's position
                intersactionCollider.isTrigger = false;

                // Left perimeter: only every other intersection allows left turn into city

                roadAreas.Add(new IntersectionArea(
                    intersection, false, false, -1,
                    vDir, hDir, changeDir, 1,
                    true, false 
                ));

                if (roadMaterial != null)
                    SetTilingBasedOnScale(intersection, roadMaterial);

                mainRoadRects.Add(new Rect(xPos - roadWidth / 2, zPos - roadWidth / 2, roadWidth, roadWidth));

                changeDir *= -1;
            }
        }
    }

    private void GenerateRightPerimeterSegments(GameObject perimeterRoadsContainer, float actualTerrainWidth, float actualTerrainLength, float horizontalSpacing)
    {
        float xPos = actualTerrainWidth - roadWidth / 2; // Right edge
        float startOffsetZ = roadWidth;
        int direction = 2; // North-bound

        int changeDir = -1; 
        int vDir = 2; // North
        int hDir = 1; // West

        for (int i = 0; i <= numHorizontalRoads; i++)
        {
            float segmentStartZ = (i == 0) ? roadWidth : startOffsetZ + i * horizontalSpacing + roadWidth / 2;
            float segmentEndZ = (i == numHorizontalRoads) ? actualTerrainLength - roadWidth : startOffsetZ + (i + 1) * horizontalSpacing - roadWidth / 2;

            float segmentHeight = segmentEndZ - segmentStartZ;
            if (segmentHeight <= 0) continue;

            GameObject roadSegment = CreateDirectionalRoadPlane(
                $"PerimeterRoad_Right_Segment_{i}",
                roadWidth, segmentHeight,
                new Vector3(xPos, 0.07f, segmentStartZ + segmentHeight / 2),
                direction
            );

            roadSegment.transform.SetParent(perimeterRoadsContainer.transform);

            BoxCollider roadCollider = roadSegment.GetComponent<BoxCollider>();
            if (roadCollider == null)
            {
                roadCollider = roadSegment.AddComponent<BoxCollider>();
            }

            Vector3 scale = roadSegment.transform.localScale;

            roadCollider.size = new Vector3(
                roadWidth / scale.x,
                0.1f / scale.y,
                segmentHeight / scale.z);

            roadCollider.center = Vector3.zero; // Center relative to the GameObject's position
            roadCollider.isTrigger = false;

            roadAreas.Add(new RoadArea(roadSegment, false, true, direction));

            if (roadMaterial != null)
                SetTilingBasedOnScale(roadSegment, roadMaterial);

            mainRoadRects.Add(new Rect(xPos - roadWidth / 2, segmentStartZ, roadWidth, segmentHeight));

            if (i < numHorizontalRoads)
            {
                float zPos = startOffsetZ + (i + 1) * horizontalSpacing;
                Vector3 intersectionCenter = new Vector3(xPos, 0.03f, zPos);

                GameObject intersection = CreatePlane($"PerimeterIntersection_Right_{i + 1}", roadWidth, roadWidth, intersectionCenter);
                intersection.transform.SetParent(perimeterRoadsContainer.transform);

                BoxCollider intersactionCollider = intersection.GetComponent<BoxCollider>();
                if (intersactionCollider == null)
                {
                    intersactionCollider = intersection.AddComponent<BoxCollider>();
                }

                scale = intersection.transform.localScale;

                intersactionCollider.size = new Vector3(
                    roadWidth / scale.x,
                    0.1f / scale.y,
                    roadWidth / scale.z);

                intersactionCollider.center = Vector3.zero; // Center relative to the GameObject's position
                intersactionCollider.isTrigger = false;

                // Right perimeter: only every other intersection allows left turn into city

                roadAreas.Add(new IntersectionArea(
                    intersection, false, false, -1,
                    vDir, hDir, changeDir, 1,
                    true, false 
                ));

                if (roadMaterial != null)
                    SetTilingBasedOnScale(intersection, roadMaterial);

                mainRoadRects.Add(new Rect(xPos - roadWidth / 2, zPos - roadWidth / 2, roadWidth, roadWidth));

                changeDir *= -1;
            }
        }
    }

    private void SetTilingBasedOnScale(GameObject obj, Material sourceMaterial)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        Material materialInstance = new Material(sourceMaterial);
        Vector3 scale = obj.transform.localScale;
        Vector2 tiling = new Vector2(scale.x, scale.z);
        if (obj.name.Contains("Crosswalk"))
            tiling = new Vector2(scale.x, scale.z * 2f);
        materialInstance.mainTextureScale = tiling;
        renderer.material = materialInstance;
    }

    private float CalculateCityParkRatio(float envDensity)
    {
        float cityDensityFactor = (envDensity - cityModeThreshold) / (1 - cityModeThreshold);
        return Mathf.Lerp(parkRatio, parkRatio * 0.5f, cityDensityFactor);
    }

    private void GenerateCityLayout(GameObject parksContainer, GameObject buildingsContainer, GameObject roadsContainer)
    {
        List<Rect> cityCells = CalculateCityCells();

        // At moderate density (~0.72), have parks in 60% of cells but smaller
        // At max density (1.0), have parks in only 30-40% of cells
        float densityFactor = (environmentDensity - cityModeThreshold) / (1 - cityModeThreshold);

        // Start at 60% at city threshold, decrease to 35% at max density
        float parkCellPercentage = Mathf.Lerp(0.6f, 0.35f, densityFactor);

        int totalParkCells = Mathf.CeilToInt(cityCells.Count * parkCellPercentage);

        float maxParkSizeFactor = Mathf.Lerp(0.5f, 0.3f, densityFactor);
        float minParkSizeFactor = Mathf.Lerp(0.4f, 0.1f, densityFactor);

        List<int> assignedCells = new List<int>();

        List<int> cellIndices = new List<int>();
        for (int i = 0; i < cityCells.Count; i++)
        {
            cellIndices.Add(i);
        }

        ShuffleCellIndices(cellIndices);

        for (int i = 0; i < totalParkCells && i < cellIndices.Count; i++)
        {
            int cellIndex = cellIndices[i];

            assignedCells.Add(cellIndex);

            Rect cell = cityCells[cellIndex];

            float parkSizeFactor = Random.Range(minParkSizeFactor, maxParkSizeFactor);

            float parkWidth = cell.width * Mathf.Sqrt(parkSizeFactor);
            float parkLength = cell.height * Mathf.Sqrt(parkSizeFactor);

            float parkX = cell.x + (cell.width - parkWidth) * Random.Range(0.1f, 0.9f);
            float parkZ = cell.y + (cell.height - parkLength) * Random.Range(0.1f, 0.9f);

            GameObject park = CreatePlane($"ParkArea_{parkAreas.Count}", parkWidth, parkLength,
                               new Vector3(parkX + parkWidth / 2, 0.02f, parkZ + parkLength / 2));
            park.transform.SetParent(parksContainer.transform);

            if (parkMaterial != null)
            {
                SetTilingBasedOnScale(park, parkMaterial);
            }

            parkAreas.Add(park);

            FillCellWithBuildings(cell, new Rect(parkX, parkZ, parkWidth, parkLength), buildingsContainer, park);
        }

        for (int i = 0; i < cityCells.Count; i++)
        {
            if (!assignedCells.Contains(i))
            {
                FillCellWithBuildings(cityCells[i], new Rect(0, 0, 0, 0), buildingsContainer, null);
            }
        }
    }

    private void ShuffleCellIndices(List<int> indices)
    {
        int n = indices.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            int value = indices[k];
            indices[k] = indices[n];
            indices[n] = value;
        }
    }

    private void FillCellWithBuildings(Rect cell, Rect excludedArea, GameObject buildingsContainer, GameObject park)
    {
        // If there's no excluded area (park), creating one large building area for the entire cell
        if (excludedArea.width <= 0 || excludedArea.height <= 0)
        {
            Vector3 position = new Vector3(cell.x + cell.width / 2, 0.05f, cell.y + cell.height / 2);
            GameObject buildingArea = CreatePlane($"BuildingArea_{buildingAreas.Count}",
                                                 cell.width, cell.height, position);
            buildingArea.transform.SetParent(buildingsContainer.transform);
            buildingArea.layer = 9;
            if (planeMaterial != null)
            {
                SetTilingBasedOnScale(buildingArea, planeMaterial);
            }
            BuildingArea newBuildingArea = new BuildingArea(buildingArea, cell.height, cell.width, position);
            buildingAreas.Add(newBuildingArea);
            return;
        }

        // If there is a park, divide the remaining space into building areas
        List<Rect> buildingRects = new List<Rect>();

        // Area above the park
        if (excludedArea.y > cell.y)
        {
            Rect topRect = new Rect(
                cell.x,
                cell.y,
                cell.width,
                excludedArea.y - cell.y
            );
            if (topRect.width > 0 && topRect.height > 0)
            {
                buildingRects.Add(topRect);
            }
        }

        // Area below the park
        float parkBottom = excludedArea.y + excludedArea.height;
        if (parkBottom < cell.y + cell.height)
        {
            Rect bottomRect = new Rect(
                cell.x,
                parkBottom,
                cell.width,
                (cell.y + cell.height) - parkBottom
            );
            if (bottomRect.width > 0 && bottomRect.height > 0)
            {
                buildingRects.Add(bottomRect);
            }
        }

        // Area to the left of the park between the top and bottom rects
        if (excludedArea.x > cell.x)
        {
            Rect leftRect = new Rect(
                cell.x,
                excludedArea.y,
                excludedArea.x - cell.x,
                excludedArea.height
            );
            if (leftRect.width > 0 && leftRect.height > 0)
            {
                buildingRects.Add(leftRect);
            }
        }

        // Area to the right of the park between the top and bottom rects
        float parkRight = excludedArea.x + excludedArea.width;
        if (parkRight < cell.x + cell.width)
        {
            Rect rightRect = new Rect(
                parkRight,
                excludedArea.y,
                (cell.x + cell.width) - parkRight,
                excludedArea.height
            );
            if (rightRect.width > 0 && rightRect.height > 0)
            {
                buildingRects.Add(rightRect);
            }
        }

        // Create building areas for each of the calculated rectangles
        foreach (Rect buildingRect in buildingRects)
        {
            Vector3 position = new Vector3(buildingRect.x + buildingRect.width / 2, 0.05f, buildingRect.y + buildingRect.height / 2);
            GameObject buildingArea = CreatePlane($"BuildingArea_{buildingAreas.Count}",
                                                buildingRect.width, buildingRect.height, position);
            buildingArea.transform.SetParent(buildingsContainer.transform);
            buildingArea.layer = 9;

            if (planeMaterial != null)
            {
                SetTilingBasedOnScale(buildingArea, planeMaterial);
            }

            BuildingArea newBuildingArea = new BuildingArea(buildingArea, buildingRect.height, buildingRect.width, position);
            newBuildingArea.parkArea = park;
            buildingAreas.Add(newBuildingArea);
        }
    }

    private List<Rect> CalculateCityCells()
    {
        List<Rect> cells = new List<Rect>();

        List<float> horizontalRoadPositions = new List<float>();
        List<float> verticalRoadPositions = new List<float>();

        horizontalRoadPositions.Add(0);
        horizontalRoadPositions.Add(terrainLength);
        verticalRoadPositions.Add(0);
        verticalRoadPositions.Add(terrainWidth);

        foreach (Rect road in mainRoadRects)
        {
            if (road.width > road.height) // Horizontal road
            {
                float roadCenterY = road.y + road.height / 2;
                if (!horizontalRoadPositions.Contains(roadCenterY))
                {
                    horizontalRoadPositions.Add(roadCenterY);
                }
            }
            else // Vertical road
            {
                float roadCenterX = road.x + road.width / 2;
                if (!verticalRoadPositions.Contains(roadCenterX))
                {
                    verticalRoadPositions.Add(roadCenterX);
                }
            }
        }

        horizontalRoadPositions.Sort();
        verticalRoadPositions.Sort();

        for (int h = 0; h < horizontalRoadPositions.Count - 1; h++)
        {
            for (int v = 0; v < verticalRoadPositions.Count - 1; v++)
            {
                float x = verticalRoadPositions[v];
                float y = horizontalRoadPositions[h];
                float width = verticalRoadPositions[v + 1] - x;
                float height = horizontalRoadPositions[h + 1] - y;

                // Adjust for road width
                float xAdjusted = x + (x > 0 ? roadWidth / 2 : 0);
                float yAdjusted = y + (y > 0 ? roadWidth / 2 : 0);
                float widthAdjusted = width - (x > 0 ? roadWidth / 2 : 0) - (x + width < terrainWidth ? roadWidth / 2 : 0);
                float heightAdjusted = height - (y > 0 ? roadWidth / 2 : 0) - (y + height < terrainLength ? roadWidth / 2 : 0);

                if (widthAdjusted > 0 && heightAdjusted > 0)
                {
                    cells.Add(new Rect(xAdjusted, yAdjusted, widthAdjusted, heightAdjusted));
                }
            }
        }

        return cells;
    }

    private float CalculateTreeDensity(float envDensity)
    {
        float treeDensity = 0.9f - (envDensity * 0.3f);
        return Mathf.Clamp(treeDensity, 0.7f, 0.9f); // That way we ensure have at least 30% tree density
    }

    private void PlaceTreesInParkAreas()
    {
        if (treePrefabs.Count == 0)
        {
            Debug.LogWarning("No tree prefabs available for placement.");
            return;
        }

        float treeDensityFactor = CalculateTreeDensity(environmentDensity);
        treeSpacing = Mathf.Lerp(baseTreeSpacing * 2.5f, baseTreeSpacing * 0.8f, treeDensityFactor);

        List<Rect> occupiedAreas = new List<Rect>();

        foreach (Rect roadRect in mainRoadRects)
        {
            float randomMarginFactor = Mathf.Lerp(0.5f, 1.5f, Random.value);
            float baseMargin = Mathf.Lerp(minTreeMargin, maxTreeMargin, environmentDensity * 0.6f + 0.2f);
            float margin = baseMargin * randomMarginFactor;

            occupiedAreas.Add(new Rect(
                roadRect.x - margin,
                roadRect.y - margin,
                roadRect.width + (margin * 2),
                roadRect.height + (margin * 2)
            ));
        }

        foreach (BuildingArea buildingArea in buildingAreas)
        {
            Vector3 pos = buildingArea.position;
            float width = buildingArea.areaWidth;
            float length = buildingArea.areaLength;

            float leftX = pos.x - width / 2;
            float bottomZ = pos.z - length / 2;

            float sizeRatio = Mathf.Clamp01((width + length) / (maxBuildingAreaSize * 2));
            float randomFactor = Mathf.Lerp(0.7f, 1.3f, Random.value);
            float margin = Mathf.Lerp(minTreeMargin, maxTreeMargin, sizeRatio) * randomFactor;

            occupiedAreas.Add(new Rect(
                leftX - margin,
                bottomZ - margin,
                width + (margin * 2),
                length + (margin * 2)
            ));
        }

        foreach (RoadArea roadArea in roadAreas)
        {
            GameObject road = roadArea.road;

            Vector3 pos = road.transform.position;
            Vector3 scale = road.transform.localScale;

            float width = scale.x * 10;
            float length = scale.z * 10;

            float leftX = pos.x - width / 2;
            float bottomZ = pos.z - length / 2;

            float randomFactor = Mathf.Lerp(0.5f, 1.2f, Random.value);
            float margin = Mathf.Lerp(minTreeMargin * 0.7f, maxTreeMargin * 0.8f, Random.value) * randomFactor;

            occupiedAreas.Add(new Rect(
                leftX - margin,
                bottomZ - margin,
                width + (margin * 2),
                length + (margin * 2)
            ));
        }

        int maxTreeAttempts = Mathf.RoundToInt(terrainWidth * terrainLength / (treeSpacing * treeSpacing) * 1.5f);
        int treeAttempts = 0;
        int treesPlaced = 0;
        int desiredTreeCount = Mathf.RoundToInt((terrainWidth * terrainLength) / (treeSpacing * treeSpacing) * treeDensityFactor);

        while (treesPlaced < desiredTreeCount && treeAttempts < maxTreeAttempts)
        {
            treeAttempts++;

            float treeX = Random.Range(0, terrainWidth);
            float treeZ = Random.Range(0, terrainLength);

            float edgeMargin = Mathf.Lerp(4f, 10f, Random.value);
            if (treeX < edgeMargin || treeX > terrainWidth - edgeMargin ||
                treeZ < edgeMargin || treeZ > terrainLength - edgeMargin)
                continue;

            bool insideOccupiedArea = false;
            foreach (Rect area in occupiedAreas)
            {
                if (area.Contains(new Vector2(treeX, treeZ)))
                {
                    insideOccupiedArea = true;
                    break;
                }
            }

            if (!insideOccupiedArea)
            {
                // Adding clustering effect - trees tend to grow in groups in nature
                // In rural areas, more clustering. In urban areas, more evenly spaced
                float clusterChance = 0.4f; ;
                if (Random.value < clusterChance)
                {
                    int clusterSize = Random.Range(2, 5);
                    for (int i = 0; i < clusterSize; i++)
                    {
                        float clusterRadius = Random.Range(3f, 8f);
                        float angle = Random.Range(0, 360) * Mathf.Deg2Rad;
                        float distance = Random.Range(0, clusterRadius);

                        float clusterTreeX = treeX + Mathf.Cos(angle) * distance;
                        float clusterTreeZ = treeZ + Mathf.Sin(angle) * distance;

                        if (clusterTreeX > edgeMargin && clusterTreeX < terrainWidth - edgeMargin &&
                            clusterTreeZ > edgeMargin && clusterTreeZ < terrainLength - edgeMargin &&
                            !IsInOccupiedArea(clusterTreeX, clusterTreeZ, occupiedAreas))
                        {
                            PlaceTreeAt(clusterTreeX, clusterTreeZ);
                            treesPlaced++;
                        }
                    }
                }
                else
                {
                    PlaceTreeAt(treeX, treeZ);
                    treesPlaced++;
                }
            }
        }
    }

    private bool IsInOccupiedArea(float x, float z, List<Rect> areas)
    {
        foreach (Rect area in areas)
        {
            if (area.Contains(new Vector2(x, z)))
                return true;
        }
        return false;
    }

    private void PlaceTreeAt(float x, float z)
    {
        GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Count)];

        Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

        float randomScale = Random.Range(0.65f, 0.95f);

        GameObject tree = Object.Instantiate(treePrefab,
                                            new Vector3(x, 0, z),
                                            randomRotation);

        tree.transform.localScale *= randomScale;

        tree.name = $"Tree_{placedTrees.Count}";

        tree.transform.SetParent(treesContainer.transform);

        placedTrees.Add(tree);

        UnityEngine.AI.NavMeshObstacle obstacle = tree.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = tree.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        }
        obstacle.carving = true;
        obstacle.carvingMoveThreshold = 0.1f;
        obstacle.carvingTimeToStationary = 0.5f;
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = Vector3.zero;
        obstacle.radius = 1.5f;
    }

    private void CreateBaseSurface(Terrain t)
    {
        if (environmentDensity > 0.7)
        {
            baseSurface = CreatePlane("Visual_BaseSurface", terrainWidth, terrainLength,
                                  new Vector3(terrainWidth / 2, 0.01f, terrainLength / 2));

            if (planeMaterial != null)
            {
                SetTilingBasedOnScale(baseSurface, planeMaterial);
            }
        }
        else
        {
            baseSurface = CreatePlane("Visual_BaseSurface", terrainWidth, terrainLength,
                                  new Vector3(terrainWidth / 2, 0.01f, terrainLength / 2));

            if (parkMaterial != null)
            {
                SetTilingBasedOnScale(baseSurface, parkMaterial);
            }
        }
        baseSurface.transform.SetParent(t.transform);
        baseSurface.layer = 0;
    }

    private GameObject CreateDirectionalRoadPlane(string name, float width, float height, Vector3 position, int direction)
    {
        GameObject roadSegment = CreatePlane(name, width, height, position);
        return roadSegment;
    }

    private void GenerateMainRoadsWithCrosswalks(GameObject mainRoadsContainer)
    {
        GameObject crosswalksContainer = new GameObject("Crosswalks");
        crosswalksContainer.transform.SetParent(terrainGameObject.transform);

        float usableWidth = chosenTerrainWidth;
        float usableLength = chosenTerrainLength;
        float startOffsetX = roadWidth;
        float startOffsetZ = roadWidth;

        float horizontalSpacing = usableLength / (numHorizontalRoads + 1);
        float verticalSpacing = usableWidth / (numVerticalRoads + 1);

        List<Vector3> intersectionPositions = new List<Vector3>();

        int direction = 0;
        int changeDir = 1;

        for (int i = 1; i <= numHorizontalRoads; i++)
        {
            float zPos = startOffsetZ + horizontalSpacing * i;

            for (int j = 0; j <= numVerticalRoads; j++)
            {
                float segmentStartX = startOffsetX;

                if (j > 0)
                {
                    segmentStartX = startOffsetX + j * verticalSpacing + roadWidth / 2;
                }

                float segmentEndX = (j < numVerticalRoads)
                    ? startOffsetX + (j + 1) * verticalSpacing - roadWidth / 2
                    : startOffsetX + usableWidth;

                float segmentWidth = segmentEndX - segmentStartX;
                if (segmentWidth <= 0) continue;

                GameObject roadSegment = CreateDirectionalRoadPlane($"MainRoad_H_{i}_Segment_{j}",
                    segmentWidth, roadWidth,
                    new Vector3(segmentStartX + segmentWidth / 2, 0.03f, zPos),
                    direction);

                roadSegment.transform.SetParent(mainRoadsContainer.transform);

                BoxCollider roadCollider = roadSegment.GetComponent<BoxCollider>();
                if (roadCollider == null)
                {
                    roadCollider = roadSegment.AddComponent<BoxCollider>();
                }

                Vector3 scale = roadSegment.transform.localScale;

                roadCollider.size = new Vector3(
                    segmentWidth / scale.x,
                    0.1f / scale.y,
                    roadWidth / scale.z);

                roadCollider.center = Vector3.zero; // Center relative to the GameObject's position
                roadCollider.isTrigger = false;

                roadAreas.Add(new RoadArea(roadSegment, true, false, direction));

                if (roadMaterial != null)
                    SetTilingBasedOnScale(roadSegment, roadMaterial);

                mainRoadRects.Add(new Rect(segmentStartX, zPos - roadWidth / 2, segmentWidth, roadWidth));

                if (generateMidBlockCrosswalks && segmentWidth > crosswalkSpacing)
                {
                    CreateMidBlockCrosswalksOnSegment(
                        new Vector3(segmentStartX + segmentWidth / 2, 0.035f, zPos),
                        segmentWidth, true, crosswalksContainer
                    );
                }
            }

            if (changeDir == 1) direction++;
            else direction--;
            changeDir *= -1;
        }

        direction = 2;
        changeDir = 1;

        for (int i = 1; i <= numVerticalRoads; i++)
        {
            float xPos = startOffsetX + verticalSpacing * i;

            for (int j = 0; j <= numHorizontalRoads; j++)
            {
                float segmentStartZ = startOffsetZ;

                if (j > 0)
                {
                    segmentStartZ = startOffsetZ + j * horizontalSpacing + roadWidth / 2;
                }

                float segmentEndZ = (j < numHorizontalRoads)
                    ? startOffsetZ + (j + 1) * horizontalSpacing - roadWidth / 2
                    : startOffsetZ + usableLength;

                float segmentHeight = segmentEndZ - segmentStartZ;
                if (segmentHeight <= 0) continue;

                GameObject roadSegment = CreateDirectionalRoadPlane($"MainRoad_V_{i}_Segment_{j}",
                    roadWidth, segmentHeight,
                    new Vector3(xPos, 0.03f, segmentStartZ + segmentHeight / 2),
                    direction);

                roadSegment.transform.SetParent(mainRoadsContainer.transform);


                BoxCollider roadCollider = roadSegment.GetComponent<BoxCollider>();
                if (roadCollider == null)
                {
                    roadCollider = roadSegment.AddComponent<BoxCollider>();
                }

                Vector3 scale = roadSegment.transform.localScale;

                roadCollider.size = new Vector3(
                    roadWidth / scale.x,
                    0.1f / scale.y,
                    segmentHeight / scale.z);

                roadCollider.center = Vector3.zero; // Center relative to the GameObject's position
                roadCollider.isTrigger = false;

                roadAreas.Add(new RoadArea(roadSegment, false, true, direction));

                if (roadMaterial != null)
                    SetTilingBasedOnScale(roadSegment, roadMaterial);

                mainRoadRects.Add(new Rect(xPos - roadWidth / 2, segmentStartZ, roadWidth, segmentHeight));

                if (generateMidBlockCrosswalks && segmentHeight > crosswalkSpacing)
                {
                    CreateMidBlockCrosswalksOnSegment(
                        new Vector3(xPos, 0.035f, segmentStartZ + segmentHeight / 2),
                        segmentHeight, false, crosswalksContainer
                    );
                }
            }

            if (changeDir == 1) direction++;
            else direction--;
            changeDir *= -1;
        }

        int changeVDir = 0;
        int changeHDir = 1;
        int vDir = 2;
        int hDir = 0;

        for (int h = 1; h <= numHorizontalRoads; h++)
        {
            float zPos = startOffsetZ + horizontalSpacing * h;

            for (int v = 1; v <= numVerticalRoads; v++)
            {
                float xPos = startOffsetX + verticalSpacing * v;
                Vector3 intersectionCenter = new Vector3(xPos, 0.03f, zPos);

                GameObject intersection = CreatePlane($"Intersection_H{h}_V{v}",
                    roadWidth, roadWidth, intersectionCenter);

                intersection.transform.SetParent(mainRoadsContainer.transform);


                BoxCollider intersactionCollider = intersection.GetComponent<BoxCollider>();
                if (intersactionCollider == null)
                {
                    intersactionCollider = intersection.AddComponent<BoxCollider>();
                }

                Vector3 scale = intersection.transform.localScale;

                intersactionCollider.size = new Vector3(
                    roadWidth / scale.x,
                    0.1f / scale.y,
                    roadWidth / scale.z);

                intersactionCollider.center = Vector3.zero; // Center relative to the GameObject's position
                intersactionCollider.isTrigger = false;

                roadAreas.Add(new IntersectionArea(intersection, false, false, -1, vDir, hDir, changeVDir, changeHDir, true, true));
                changeVDir = (changeVDir + 1) % 2;
                changeHDir = (changeHDir + 1) % 2;

                if (roadMaterial != null)
                    SetTilingBasedOnScale(intersection, roadMaterial);

                mainRoadRects.Add(new Rect(xPos - roadWidth / 2, zPos - roadWidth / 2, roadWidth, roadWidth));

                CreateCrosswalksAtIntersection(
                    intersectionCenter, roadWidth, crosswalksContainer, crosswalkMaterial
                );

                intersectionPositions.Add(intersectionCenter);
                vDir = ((vDir + 1) % 2) + 2;

            }
            vDir = 2;
            hDir = (hDir + 1) % 2;
        }
    }

    public List<GameObject> CreateCrosswalksAtIntersection(Vector3 intersectionCenter, float roadWidth, GameObject parentContainer, Material crosswalkMaterial)
    {
        LayerMovementManager manager = Object.FindObjectOfType<LayerMovementManager>();
        if (manager == null)
        {
            Debug.LogError("LayerMovementManager not found! Cannot assign crosswalks.");
        }

        List<GameObject> crosswalks = new List<GameObject>();

        Vector3[] crosswalkPositions = {
            intersectionCenter + new Vector3(0, 0.005f, roadWidth * 0.75f),
            intersectionCenter + new Vector3(0, 0.005f, -roadWidth * 0.75f),
            intersectionCenter + new Vector3(roadWidth * 0.75f, 0.005f, 0),
            intersectionCenter + new Vector3(-roadWidth * 0.75f, 0.005f, 0)
        };

        string[] directions = { "North", "South", "East", "West" };
        bool[] isHorizontal = { true, true, false, false };

        for (int i = 0; i < crosswalkPositions.Length; i++)
        {
            GameObject crosswalk = CreatePlane(
                $"Crosswalk_{directions[i]}_{crosswalks.Count}",
                crosswalkWidth,
                roadWidth,
                crosswalkPositions[i]
            );

            if (crosswalkMaterial != null)
            {
                SetTilingBasedOnScale(crosswalk, crosswalkMaterial);
            }

            if (isHorizontal[i])
                crosswalk.transform.rotation = Quaternion.Euler(0, 90f, 0);
            crosswalk.transform.SetParent(parentContainer.transform);
            manager.AssignCrosswalkLayer(crosswalk);

            crosswalks.Add(crosswalk);
        }

        return crosswalks;
    }

    private void CreateMidBlockCrosswalksOnSegment(Vector3 segmentCenter, float segmentLength, bool isHorizontal, GameObject crosswalksContainer)
    {
        int numCrosswalks = Mathf.FloorToInt(segmentLength / crosswalkSpacing);

        if (numCrosswalks < 1) return;

        for (int i = 1; i <= numCrosswalks; i++)
        {
            Vector3 crosswalkPosition;

            if (isHorizontal)
            {
                float spacing = segmentLength / (numCrosswalks + 1);
                float xOffset = -segmentLength / 2 + spacing * i;
                crosswalkPosition = segmentCenter + new Vector3(xOffset, 0, 0);
            }
            else
            {
                float spacing = segmentLength / (numCrosswalks + 1);
                float zOffset = -segmentLength / 2 + spacing * i;
                crosswalkPosition = segmentCenter + new Vector3(0, 0, zOffset);
            }

            CreateMidBlockCrosswalk(
                crosswalkPosition, isHorizontal, roadWidth, crosswalksContainer, crosswalkMaterial
            );
        }
    }

    public GameObject CreateMidBlockCrosswalk(Vector3 roadCenter, bool isHorizontalRoad, float roadWidth, GameObject parentContainer, Material crosswalkMaterial)
    {
        LayerMovementManager manager = Object.FindObjectOfType<LayerMovementManager>();
        if (manager == null)
        {
            Debug.LogError("LayerMovementManager not found! Cannot assign crosswalks.");
        }

        GameObject crosswalk = CreatePlane(
            $"MidBlockCrosswalk_{manager.getTrackedCrosswalks().Count}",
            crosswalkWidth,
            roadWidth,
            roadCenter
        );

        if (crosswalkMaterial != null)
        {
            SetTilingBasedOnScale(crosswalk, crosswalkMaterial);
        }

        float rotation = isHorizontalRoad ? 0f : 90f;
        crosswalk.transform.rotation = Quaternion.Euler(0, rotation, 0);
        crosswalk.transform.SetParent(parentContainer.transform);
        manager.AssignCrosswalkLayer(crosswalk);

        return crosswalk;
    }

    private void AssignLayersToTerrainObjectsWithCrosswalks()
    {
        LayerMovementManager manager = Object.FindObjectOfType<LayerMovementManager>();
        if (manager == null)
        {
            Debug.LogWarning("LayerMovementManager not found!");
            return;
        }

        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("MainRoad_") || obj.name.Contains("PerimeterRoad_") || obj.name.Contains("Intersection_") 
                || obj.name.Contains("AccessRoad_") || obj.name.Contains("Corner_"))
            {
                manager.AssignRoadLayer(obj);
            }
            else if (obj.name.Contains("BuildingArea_"))
            {
                manager.AssignBuildingAreaLayer(obj);
            }
            else if (obj.name.Contains("ParkArea_"))
            {
                manager.AssignParkAreaLayer(obj);
            }
            else if (obj.name.Contains("Crosswalk_") || obj.name.Contains("MidBlockCrosswalk_"))
            {
                manager.AssignCrosswalkLayer(obj); 
            }
        }
    }

    private void GenerateBuildingsAlongRoads(GameObject buildingsContainer, GameObject roadsContainer)
    {
        List<Rect> availableCells = CalculateCityCells();

        GameObject parksContainer = GameObject.Find("ParkAreas");
        if (parksContainer == null)
        {
            parksContainer = new GameObject("ParkAreas");
            parksContainer.transform.SetParent(terrainGameObject.transform);
        }

        List<Rect> occupiedAreas = new List<Rect>(mainRoadRects);

        float densityAdjustedMinSize = Mathf.Lerp(minBuildingAreaSize * 1.2f, minBuildingAreaSize * 0.9f, environmentDensity);
        float densityAdjustedMaxSize = Mathf.Lerp(maxBuildingAreaSize, maxBuildingAreaSize * 0.8f, environmentDensity);

        foreach (Rect cell in availableCells)
        {
            CreateParkAreaForEntireCell(cell, parksContainer);
        }

        int attempts = 0;
        int maxAttempts = numBuildingAreas * 10;

        while (buildingAreas.Count < numBuildingAreas && attempts < maxAttempts)
        {
            attempts++;

            float areaWidth = Random.Range(densityAdjustedMinSize, densityAdjustedMaxSize);
            float areaLength = Random.Range(densityAdjustedMinSize, densityAdjustedMaxSize);

            Rect selectedRoad = mainRoadRects[Random.Range(0, mainRoadRects.Count)];

            bool isHorizontalRoad = selectedRoad.width > selectedRoad.height;

            float posX, posZ;
            if (isHorizontalRoad)
            {
                posX = Random.Range(roadWidth * 2, terrainWidth - areaWidth - roadWidth * 2);
                float roadCenterZ = selectedRoad.y + selectedRoad.height / 2;
                float buildingOffset = Random.Range(roadWidth / 2, roadWidth * 4);
                posZ = Random.value > 0.5f ?
                    roadCenterZ + roadWidth / 2 + buildingOffset :
                    roadCenterZ - roadWidth / 2 - areaLength - buildingOffset;
            }
            else
            {
                posZ = Random.Range(roadWidth * 2, terrainLength - areaLength - roadWidth * 2);
                float roadCenterX = selectedRoad.x + selectedRoad.width / 2;
                float buildingOffset = Random.Range(roadWidth / 2, roadWidth * 4);
                posX = Random.value > 0.5f ?
                    roadCenterX + roadWidth / 2 + buildingOffset :
                    roadCenterX - roadWidth / 2 - areaWidth - buildingOffset;
            }

            if (IsWithinTerrainBounds(posX, posZ, areaWidth, areaLength))
            {
                Rect newBuildingRect = new Rect(posX, posZ, areaWidth, areaLength);

                bool overlaps = false;
                foreach (Rect occupiedArea in occupiedAreas)
                {
                    if (newBuildingRect.Overlaps(occupiedArea))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    string buildingAreaName = $"BuildingArea_{buildingAreas.Count}";
                    Vector3 position = new Vector3(posX + (areaWidth / 2), 0.05f, posZ + (areaLength / 2));
                    GameObject buildingArea = CreatePlane(buildingAreaName, areaWidth, areaLength, position);
                    buildingArea.transform.SetParent(buildingsContainer.transform);
                    buildingArea.layer = 9;

                    if (planeMaterial != null)
                    {
                        SetTilingBasedOnScale(buildingArea, planeMaterial);
                    }

                    BuildingArea newBuildingArea = new BuildingArea(buildingArea, areaLength, areaWidth, position);
                    buildingAreas.Add(newBuildingArea);
                    occupiedAreas.Add(newBuildingRect);

                    CreateAccessRoad(roadsContainer, posX, posZ, areaWidth, areaLength, selectedRoad, isHorizontalRoad, newBuildingArea);

                    Rect containingCell = FindCellContainingBuilding(availableCells, newBuildingRect);
                    if (containingCell.width > 0)
                    {
                        FillCellWithParks(containingCell, newBuildingRect, parksContainer, buildingArea, newBuildingArea.accessRoad);
                    }
                }
            }
        }
    }

    private void CreateParkAreaForEntireCell(Rect cell, GameObject parksContainer)
    {
        Vector3 position = new Vector3(cell.x + cell.width / 2, 0.02f, cell.y + cell.height / 2);
        GameObject park = CreatePlane($"ParkArea_Cell_{parkAreas.Count}", cell.width, cell.height, position);
        park.transform.SetParent(parksContainer.transform);

        if (parkMaterial != null)
        {
            SetTilingBasedOnScale(park, parkMaterial);
        }

        parkAreas.Add(park);
    }

    private Rect FindCellContainingBuilding(List<Rect> cells, Rect buildingRect)
    {
        Vector2 buildingCenter = new Vector2(buildingRect.x + buildingRect.width / 2, buildingRect.y + buildingRect.height / 2);

        foreach (Rect cell in cells)
        {
            if (cell.Contains(buildingCenter))
            {
                return cell;
            }
        }

        return new Rect(0, 0, 0, 0);
    }

    private void FillCellWithParks(Rect cell, Rect buildingArea, GameObject parksContainer, GameObject buildingGameObject, GameObject accessRoad)
    {
        RemoveExistingParkInCell(cell);

        Rect accessRoadRect = new Rect(0, 0, 0, 0);
        if (accessRoad != null)
        {
            Vector3 roadPos = accessRoad.transform.position;
            Vector3 roadScale = accessRoad.transform.localScale;
            float roadWidth = roadScale.x * 10;
            float roadLength = roadScale.z * 10;
            accessRoadRect = new Rect(roadPos.x - roadWidth / 2, roadPos.z - roadLength / 2, roadWidth, roadLength);
        }

        List<Rect> parkRects = new List<Rect>();

        if (buildingArea.y > cell.y)
        {
            Rect topRect = new Rect(cell.x, cell.y, cell.width, buildingArea.y - cell.y);
            if (topRect.width > 0 && topRect.height > 0)
            {
                parkRects.Add(topRect);
            }
        }

        float buildingBottom = buildingArea.y + buildingArea.height;
        if (buildingBottom < cell.y + cell.height)
        {
            Rect bottomRect = new Rect(cell.x, buildingBottom, cell.width, (cell.y + cell.height) - buildingBottom);
            if (bottomRect.width > 0 && bottomRect.height > 0)
            {
                parkRects.Add(bottomRect);
            }
        }

        if (buildingArea.x > cell.x)
        {
            Rect leftRect = new Rect(cell.x, buildingArea.y, buildingArea.x - cell.x, buildingArea.height);
            if (leftRect.width > 0 && leftRect.height > 0)
            {
                parkRects.Add(leftRect);
            }
        }

        float buildingRight = buildingArea.x + buildingArea.width;
        if (buildingRight < cell.x + cell.width)
        {
            Rect rightRect = new Rect(buildingRight, buildingArea.y, (cell.x + cell.width) - buildingRight, buildingArea.height);
            if (rightRect.width > 0 && rightRect.height > 0)
            {
                parkRects.Add(rightRect);
            }
        }

        foreach (Rect parkRect in parkRects)
        {
            if (accessRoadRect.width > 0 && parkRect.Overlaps(accessRoadRect))
            {
                CreateParkAreasAroundObstacle(parkRect, accessRoadRect, parksContainer);
            }
            else
            {
                Vector3 position = new Vector3(parkRect.x + parkRect.width / 2, 0.02f, parkRect.y + parkRect.height / 2);
                GameObject park = CreatePlane($"ParkArea_{parkAreas.Count}", parkRect.width, parkRect.height, position);
                park.transform.SetParent(parksContainer.transform);

                if (parkMaterial != null)
                {
                    SetTilingBasedOnScale(park, parkMaterial);
                }

                parkAreas.Add(park);
            }
        }
    }

    private void RemoveExistingParkInCell(Rect cell)
    {
        for (int i = parkAreas.Count - 1; i >= 0; i--)
        {
            GameObject park = parkAreas[i];
            Vector3 parkPos = park.transform.position;
            Vector3 parkScale = park.transform.localScale;

            float parkWidth = parkScale.x * 10;
            float parkLength = parkScale.z * 10;

            if (Mathf.Approximately(parkWidth, cell.width) &&
                Mathf.Approximately(parkLength, cell.height) &&
                Mathf.Approximately(parkPos.x, cell.x + cell.width / 2) &&
                Mathf.Approximately(parkPos.z, cell.y + cell.height / 2))
            {
                Object.DestroyImmediate(park);
                parkAreas.RemoveAt(i);
                break;
            }
        }
    }

    private void CreateParkAreasAroundObstacle(Rect parkRect, Rect obstacle, GameObject parksContainer)
    {
        List<Rect> remainingParkAreas = new List<Rect>();

        // Area above the obstacle
        if (obstacle.y > parkRect.y)
        {
            Rect topArea = new Rect(parkRect.x, parkRect.y, parkRect.width, obstacle.y - parkRect.y);
            if (topArea.width > 0 && topArea.height > 0)
                remainingParkAreas.Add(topArea);
        }

        // Area below the obstacle
        float obstacleBottom = obstacle.y + obstacle.height;
        if (obstacleBottom < parkRect.y + parkRect.height)
        {
            Rect bottomArea = new Rect(parkRect.x, obstacleBottom, parkRect.width, (parkRect.y + parkRect.height) - obstacleBottom);
            if (bottomArea.width > 0 && bottomArea.height > 0)
                remainingParkAreas.Add(bottomArea);
        }

        // Area to the left of the obstacle
        if (obstacle.x > parkRect.x)
        {
            Rect leftArea = new Rect(parkRect.x, obstacle.y, obstacle.x - parkRect.x, obstacle.height);
            if (leftArea.width > 0 && leftArea.height > 0)
                remainingParkAreas.Add(leftArea);
        }

        // Area to the right of the obstacle
        float obstacleRight = obstacle.x + obstacle.width;
        if (obstacleRight < parkRect.x + parkRect.width)
        {
            Rect rightArea = new Rect(obstacleRight, obstacle.y, (parkRect.x + parkRect.width) - obstacleRight, obstacle.height);
            if (rightArea.width > 0 && rightArea.height > 0)
                remainingParkAreas.Add(rightArea);
        }

        foreach (Rect area in remainingParkAreas)
        {
            Vector3 position = new Vector3(area.x + area.width / 2, 0.02f, area.y + area.height / 2);
            GameObject park = CreatePlane($"ParkArea_{parkAreas.Count}", area.width, area.height, position);
            park.transform.SetParent(parksContainer.transform);

            if (parkMaterial != null)
            {
                SetTilingBasedOnScale(park, parkMaterial);
            }

            parkAreas.Add(park);
        }
    }

    private void CreateAccessRoad(GameObject roadsContainer,
                                  float buildingX, float buildingZ,
                                  float buildingWidth, float buildingLength,
                                  Rect mainRoad, bool isHorizontalRoad, BuildingArea buildingArea)
    {
        // Access road position and dimensions
        float roadX, roadZ, roadWidth, roadLength;

        if (isHorizontalRoad)
        {
            roadWidth = this.roadWidth;
            float mainRoadCenterZ = mainRoad.y + mainRoad.height / 2;
            float mainRoadTopEdge = mainRoad.y + mainRoad.height;
            float mainRoadBottomEdge = mainRoad.y;

            roadX = buildingX + buildingWidth / 2 - roadWidth / 2;

            if (buildingZ > mainRoadCenterZ)
            {
                // Building is above the road, connect from the bottom of the building to the top edge of the main road
                roadZ = mainRoadTopEdge;
                roadLength = buildingZ - mainRoadTopEdge;
            }
            else
            {
                // Building is below the road, connect from the top of the building to the bottom edge of the main road
                roadZ = buildingZ + buildingLength;
                roadLength = mainRoadBottomEdge - (buildingZ + buildingLength);
            }
        }
        else // For vertical main roads, create a horizontal access road

        {
            roadLength = this.roadWidth;
            float mainRoadCenterX = mainRoad.x + mainRoad.width / 2;
            float mainRoadRightEdge = mainRoad.x + mainRoad.width;
            float mainRoadLeftEdge = mainRoad.x;

            roadZ = buildingZ + buildingLength / 2 - roadLength / 2;

            if (buildingX > mainRoadCenterX)
            {
                // Building is to the right of the road, connect from the left of the building to the right edge of the main road
                roadX = mainRoadRightEdge;
                roadWidth = buildingX - mainRoadRightEdge;
            }
            else
            {
                // Building is to the left of the road, connect from the right of the building to the left edge of the main road
                roadX = buildingX + buildingWidth;
                roadWidth = mainRoadLeftEdge - (buildingX + buildingWidth);
            }
        }

        if (roadWidth > 0 && roadLength > 0)
        {
            string accessRoadName = $"AccessRoad_{roadAreas.Count}";
            GameObject accessRoad = CreatePlane(accessRoadName, roadWidth, roadLength,
                new Vector3(roadX + roadWidth / 2, 0.03f, roadZ + roadLength / 2));
            accessRoad.transform.SetParent(roadsContainer.transform);
            roadAreas.Add(new RoadArea(accessRoad, false, false, -2));
            buildingArea.accessRoad = accessRoad;
            buildingArea.MarkEntrancePathFromRoad(pathWidth: 4, depth: 4f);

            if (roadMaterial != null)
            {
                SetTilingBasedOnScale(accessRoad, roadMaterial);
            }
        }
    }

    private GameObject CreatePlane(string name, float width, float length, Vector3 position)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = name;

        // Unity's default Plane is 10x10 units, so scale it appropriately
        plane.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);
        plane.transform.position = position;

        return plane;
    }

    private void LoadTreePrefabs()
    {
        treePrefabs.Clear();

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ModularWorldTool/Prefabs/For Parks/Trees" });

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (treePrefab != null)
            {
                treePrefabs.Add(treePrefab);
            }
        }

        if (treePrefabs.Count == 0)
        {
            Debug.LogWarning("No tree prefabs found in Assets/Prefabs/For Parks/Trees");
        }
    }

    private bool IsWithinTerrainBounds(float posX, float posZ, float width, float length)
    {
        // Check if any part of the plane extends beyond terrain boundaries
        bool withinXBounds = (posX >= 0) && (posX + width <= terrainWidth);
        bool withinZBounds = (posZ >= 0) && (posZ + length <= terrainLength);

        return withinXBounds && withinZBounds;
    }

    private void DebugDrawRoadDirections(List<RoadArea> roadAreas, float duration = 50f)
    {
        float arrowLength = 6f;
        float arrowHeight = 0.6f;
        float headSize = 1.5f;

        foreach (var roadArea in roadAreas)
        {
            if (roadArea.road == null)
                continue;

            Vector3 position = roadArea.road.transform.position;
            position.y += arrowHeight;

            if (roadArea is IntersectionArea intersection)
            {
                DrawIntersectionDirections(intersection, position, arrowLength, headSize, duration);
            }
            else
            {
                DrawRegularRoadDirection(roadArea, position, arrowLength, headSize, duration);
            }
        }
    }

    private void DrawIntersectionDirections(IntersectionArea intersection, Vector3 position, float arrowLength, float headSize, float duration)
    {
        Vector3 verticalDirection = GetDirectionVector(intersection.connectedV);
        Color verticalColor = GetDirectionColor(intersection.connectedV);
        DrawDirectionArrow(position + Vector3.left * 2f, verticalDirection, verticalColor, arrowLength * 0.8f, headSize, duration, "V");

        Vector3 horizontalDirection = GetDirectionVector(intersection.connectedH);
        Color horizontalColor = GetDirectionColor(intersection.connectedH);
        DrawDirectionArrow(position + Vector3.right * 2f, horizontalDirection, horizontalColor, arrowLength * 0.8f, headSize, duration, "H");

        DrawDebugCross(position, 3f, Color.yellow, duration);
    }

    private void DrawRegularRoadDirection(RoadArea roadArea, Vector3 position, float arrowLength, float headSize, float duration)
    {
        if (roadArea.direction == -2)
            return;

        Vector3 direction = GetDirectionVector(roadArea.direction);
        Color arrowColor = GetDirectionColor(roadArea.direction);

        if (direction != Vector3.zero)
        {
            DrawDirectionArrow(position, direction, arrowColor, arrowLength, headSize, duration, "");
        }
    }

    private void DrawDirectionArrow(Vector3 startPos, Vector3 direction, Color color, float length, float headSize, float duration, string label)
    {
        Vector3 arrowEnd = startPos + direction * length;

        Debug.DrawLine(startPos, arrowEnd, color, duration);

        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * headSize;
        Vector3 back = arrowEnd - direction * headSize;

        Debug.DrawLine(arrowEnd, back + right, color, duration);
        Debug.DrawLine(arrowEnd, back - right, color, duration);
        Debug.DrawLine(back + right, back - right, color, duration);

        if (!string.IsNullOrEmpty(label))
        {
            Vector3 labelPos = startPos - direction * 1f + Vector3.up * 1f;
            Debug.DrawLine(labelPos, labelPos + Vector3.up * 0.5f, color, duration);
        }
    }

    private Vector3 GetDirectionVector(int direction)
    {
        switch (direction)
        {
            case 0: return Vector3.right;   // East/Right
            case 1: return Vector3.left;    // West/Left
            case 2: return Vector3.forward; // North/Up
            case 3: return Vector3.back;    // South/Down
            default: return Vector3.zero;
        }
    }

    private Color GetDirectionColor(int direction)
    {
        switch (direction)
        {
            case 0: return Color.red;       // East/Right - Red
            case 1: return new Color(1f, 0.5f, 0.5f); // West/Left - Light Red
            case 2: return Color.blue;      // North/Up - Blue
            case 3: return new Color(0.5f, 0.5f, 1f); // South/Down - Light Blue
            default: return Color.white;
        }
    }

    private void DrawDebugCross(Vector3 center, float size, Color color, float duration)
    {
        Debug.DrawLine(center - Vector3.right * size, center + Vector3.right * size, color, duration);
        Debug.DrawLine(center - Vector3.forward * size, center + Vector3.forward * size, color, duration);
    }

    private void SetupLayerMovementManager()
    {
        existingManager = Object.FindObjectOfType<LayerMovementManager>();
        if (existingManager == null)
        {
            GameObject managerObj = new GameObject("LayerMovementManager");
            managerObj.AddComponent<LayerMovementManager>();
            managerObj.transform.SetParent(terrainGameObject.transform);
        }
    }

    private void DeletePreviousRoadData()
    {
        string assetPath = "Assets/Resources/RoadData.asset";
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<RoadDataContainer>(assetPath) != null)
        {
            UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            UnityEditor.AssetDatabase.Refresh();
        }
        roadAreas.Clear();
        mainRoadRects.Clear();
    }

    private void SaveRoadDataForRuntime()
    {
        Debug.Log("Starting to save road data for runtime...");
        RoadDataContainer roadDataAsset = Resources.Load<RoadDataContainer>("RoadData");
        if (roadDataAsset == null)
        {
            roadDataAsset = ScriptableObject.CreateInstance<RoadDataContainer>();
            if (!System.IO.Directory.Exists("Assets/Resources"))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");
            }
            UnityEditor.AssetDatabase.CreateAsset(roadDataAsset, "Assets/Resources/RoadData.asset");
        }
        roadDataAsset.ClearData();

        roadDataAsset.terrainWidth = terrainWidth;
        roadDataAsset.terrainLength = terrainLength;
        roadDataAsset.roadWidth = roadWidth;
        Debug.Log($"Saving {roadAreas.Count} road areas to ScriptableObject...");

        foreach (RoadArea roadArea in roadAreas)
        {
            if (roadArea.road != null)
            {
                if (roadArea is IntersectionArea intersection)
                {
                    roadDataAsset.AddIntersectionSegment(
                        intersection.road,
                        intersection.fromVertical,
                        intersection.fromHorizontal,
                        intersection.canGoStraightV,
                        intersection.canGoStraightH
                    );
                }
                else
                {
                    roadDataAsset.AddRoadSegment(
                        roadArea.road,
                        roadArea.horizontal,
                        roadArea.vertical,
                        roadArea.direction
                    );
                }
            }
        }

        UnityEditor.EditorUtility.SetDirty(roadDataAsset);
        UnityEditor.AssetDatabase.SaveAssets();
        var directionalRoads = roadDataAsset.GetRoadSegmentsWithDirection();
        var intersections = roadDataAsset.GetIntersections();
    }
    
    private void CreateFullRandomScene()
    {
        chosenTerrainLength = Random.Range(250, 1000);
        chosenTerrainWidth = Random.Range(250, 1000);
        environmentDensity = Random.Range(0, 100) / 100f;
        Debug.LogWarning("denstity random: " + environmentDensity);
        if (environmentDensity >= 0.7)
        {
            numHorizontalRoads = Random.Range(3, 7);
            numVerticalRoads = Random.Range(3, 7);
        }
        else
        {
            numHorizontalRoads = Random.Range(1, 4);
            numVerticalRoads = Random.Range(1, 4);
        }
        int numOfBuildingsToCreate = CalculateTotalBuildings();
        int numOfNPCToCreate = CalculateTotalNPCs();
        int numOfVehcilesToCreate = calculateTotalVehicles();

        if (environmentDensity > 0.8)
        {
        int tempNum = (numHorizontalRoads + 1) * (numVerticalRoads + 1);
        numOfBuildingsToCreate = numOfBuildingsToCreate + tempNum * 3;
        numOfNPCToCreate = numOfNPCToCreate + tempNum*10;
        tempNum = numHorizontalRoads * numVerticalRoads;
        numOfVehcilesToCreate = numOfVehcilesToCreate + tempNum*3;
        }
        CreateTerrainInScene();

        ModularWorldGenerator mainWindow = EditorWindow.GetWindow<ModularWorldGenerator>();

        mainWindow.CreateRandomScene(numOfBuildingsToCreate,numOfNPCToCreate,numOfVehcilesToCreate);
    }
    
    private int CalculateTotalBuildings()
    {
        int plotsHorizontal = numHorizontalRoads + 1;
        int plotsVertical = numVerticalRoads + 1;
        int totalPlots = plotsHorizontal * plotsVertical;

        float avgPlotLength = (float)chosenTerrainLength / plotsHorizontal;
        float avgPlotWidth = (float)chosenTerrainWidth / plotsVertical;
        float plotArea = avgPlotLength * avgPlotWidth;

        int buildingsPerPlot = CalculateBuildingsPerPlot(plotArea);

        return totalPlots * buildingsPerPlot;
    }

    private int CalculateBuildingsPerPlot(float plotArea)
    {
        if (plotArea < 2000) return Random.Range(10, 15);     
        else if (plotArea < 5000) return Random.Range(12, 17);  
        else if (plotArea < 10000) return Random.Range(15,23);
        else return Random.Range(23, 30);
    }

    private int CalculateTotalNPCs()
    {
        int plotsHorizontal = numHorizontalRoads + 1;
        int plotsVertical = numVerticalRoads + 1;
        int totalPlots = plotsHorizontal * plotsVertical;

        float avgPlotLength = (float)chosenTerrainLength / plotsHorizontal;
        float avgPlotWidth = (float)chosenTerrainWidth / plotsVertical;
        float plotArea = avgPlotLength * avgPlotWidth;

        int NPCsPerPlot = CalculateNPCsPerPlot(plotArea);

        return totalPlots * NPCsPerPlot;
    }

    private int CalculateNPCsPerPlot(float plotArea)
    {
        if (plotArea < 2000) return Random.Range(10, 15);      
        else if (plotArea < 5000) return Random.Range(15, 20);   
        else if (plotArea < 10000) return Random.Range(20, 25); 
        else return Random.Range(25, 30);  
    }

    private int calculateTotalVehicles()
    {
        int min = (numVerticalRoads * 2) + (numHorizontalRoads * 2);
        int max = (numVerticalRoads * 4) + (numHorizontalRoads * 4);
        return Random.Range(min,max);
    }
}