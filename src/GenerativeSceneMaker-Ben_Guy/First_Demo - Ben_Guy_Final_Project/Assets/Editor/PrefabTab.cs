using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Unity.VisualScripting;



public abstract class PrefabTab : BaseTab
{
    protected List<GameObject> selectedPrefabs = new List<GameObject>();
    public List<int> prefabCounts = new List<int>();
    protected List<int> selectedIndexes = new List<int>();
    protected string prefabFolderPath;
    protected string tabName;

    private PrefabAttributeData attributeData = new PrefabAttributeData();


    protected PrefabTab(string tabName, string prefabFolderPath)
    {
        this.tabName = tabName;
        this.prefabFolderPath = prefabFolderPath;
        EditorApplication.playModeStateChanged += this.OnPlayModeChangedInstance;
        EditorApplication.quitting += OnEditorQuitting;
        EditorApplication.focusChanged += OnEditorFocusChanged;
        loadAttributes();

    }

    ~PrefabTab()
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
        EditorApplication.playModeStateChanged -= OnPlayModeChangedInstance;
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
            attributeData.selectedPrefabs = this.selectedPrefabs;
            attributeData.prefabCounts = this.prefabCounts;
            attributeData.selectedIndexes = this.selectedIndexes;
            attributeData.isCityMode = isCityMode;

            attributeData.buildingAreasData = new List<BuildingAreaData>();

            foreach (var buildingArea in buildingAreas)
            {
                BuildingAreaData data = new BuildingAreaData
                {
                    areaLength = buildingArea.areaLength,
                    areaWidth = buildingArea.areaWidth,
                    position = buildingArea.position,
                    cellSize = buildingArea.cellSize,
                    GridWidth = buildingArea.GridWidth,
                    GridLength = buildingArea.GridLength,

                    gridOccupiedFlat = ConvertGridToFlat(buildingArea.GridOccupied, buildingArea.GridWidth, buildingArea.GridLength),

                    areaObjectName = buildingArea.areaObject?.name,
                    accessRoadName = buildingArea.accessRoad?.name,
                    parkAreaName = buildingArea.parkArea?.name
                };

                attributeData.buildingAreasData.Add(data);
            }

            attributeData.roadAreasData = new List<RoadAreaData>();

            foreach (var roadArea in roadAreas)
            {
                RoadAreaData roadData = new RoadAreaData
                {
                    roadName = roadArea.road?.name,
                    horizontal = roadArea.horizontal,
                    vertical = roadArea.vertical,
                    direction = roadArea.direction
                };

                attributeData.roadAreasData.Add(roadData);
            }

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
                    attributeData = JsonUtility.FromJson<PrefabAttributeData>(json);

                    this.selectedIndexes = attributeData.selectedIndexes;
                    this.prefabCounts = attributeData.prefabCounts;
                    this.selectedPrefabs = attributeData.selectedPrefabs;
                    isCityMode = attributeData.isCityMode;

                    buildingAreas = new List<BuildingArea>();
                    
                    if (attributeData.buildingAreasData != null)
                    {
                        foreach (var data in attributeData.buildingAreasData)
                        {
                            GameObject areaObj = FindGameObjectByName(data.areaObjectName);
                            GameObject accessRoadObj = FindGameObjectByName(data.accessRoadName);
                            GameObject parkAreaObj = FindGameObjectByName(data.parkAreaName);
                            
                            BuildingArea buildingArea = new BuildingArea(
                                areaObj, 
                                data.areaLength, 
                                data.areaWidth, 
                                data.position, 
                                data.cellSize,
                                accessRoadObj,
                                parkAreaObj
                            );
                            
                            RestoreGridOccupancy(buildingArea, data.gridOccupiedFlat, data.GridWidth, data.GridLength);
                            
                            buildingAreas.Add(buildingArea);
                        }
                    }

                    roadAreas = new List<RoadArea>();
                    
                    if (attributeData.roadAreasData != null)
                    {
                        foreach (var roadData in attributeData.roadAreasData)
                        {
                            GameObject roadObj = FindGameObjectByName(roadData.roadName);
                            
                            RoadArea roadArea = new RoadArea(
                                roadObj,
                                roadData.horizontal,
                                roadData.vertical,
                                roadData.direction
                            );
                            
                            roadAreas.Add(roadArea);
                        }
                    }

                    Debug.Log($"Attributes loaded successfully from: {json}");
                }
            }
            else
            {
                Debug.Log("No saved attributes found, using defaults");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load attributes: {e.Message}");
        }
    }

    private bool[] ConvertGridToFlat(bool[,] grid, int width, int length)
    {
        if (grid == null) return new bool[0];

        bool[] flat = new bool[width * length];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                flat[x * length + z] = grid[x, z];
            }
        }
        return flat;
    }

    private void RestoreGridOccupancy(BuildingArea buildingArea, bool[] flatGrid, int width, int length)
    {
        if (flatGrid == null || flatGrid.Length == 0) return;

        buildingArea.RestoreGridFromFlat(flatGrid, width, length);
    }

    private GameObject FindGameObjectByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        
        return GameObject.Find(name);
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

    public override void OnGUI()
    {
        DrawMultiSelectionTab();
    }

    protected void DrawMultiSelectionTab()
    {
        GUILayout.Label(tabName, EditorStyles.boldLabel);

        for (int i = 0; i < prefabCounts.Count; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox); 

            prefabCounts[i] = EditorGUILayout.IntField("Count:", prefabCounts[i]);
            if (prefabCounts[i] <= 0)
                prefabCounts[i] = 1;

            List<string> combinedGuids = new List<string>();

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolderPath });
            combinedGuids.AddRange(prefabGuids);

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { prefabFolderPath });
            combinedGuids.AddRange(modelGuids);

            string[] prefabPaths = combinedGuids.ToArray();
            string[] prefabNames = new string[prefabPaths.Length];

            for (int j = 0; j < prefabPaths.Length; j++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabPaths[j]);
                prefabNames[j] = System.IO.Path.GetFileNameWithoutExtension(path);
            }

            // Ensure the list has enough elements
            if (selectedIndexes.Count <= i)
            {
                selectedIndexes.Add(0);
            }

            selectedIndexes[i] = EditorGUILayout.Popup("Select Prefab:", selectedIndexes[i], prefabNames);

            // Ensure selectedPrefabs has enough elements
            while (selectedPrefabs.Count <= i)
            {
                selectedPrefabs.Add(null);
            }

            if (prefabPaths.Length > 0 && selectedIndexes[i] >= 0 && selectedIndexes[i] < prefabNames.Length)
            {
                string selectedPrefabPath = AssetDatabase.GUIDToAssetPath(prefabPaths[selectedIndexes[i]]);
                selectedPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(selectedPrefabPath);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(EditorGUIUtility.labelWidth - 75)))
            {
                prefabCounts.RemoveAt(i);
                selectedPrefabs.RemoveAt(i);
                selectedIndexes.RemoveAt(i);
                i--;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        if (!IsProFeatureEnabled())
        {
            EditorGUILayout.HelpBox($"PRO FEATURE: {tabName} addition is only available with a Pro license. Go to Settings tab to activate your license.", MessageType.Warning);
            GUI.enabled = false;
            EnforceFreeVersionLimitation();
        }
            if (GUILayout.Button("+"))
        {
            prefabCounts.Add(1);
            selectedPrefabs.Add(null);
            selectedIndexes.Add(0);
        }
        GUI.enabled = true;


        if (GUILayout.Button("Generate " + tabName))
        {
            if (tabName == "NPC")
            {
                int count = 0;
                terrainGameObject = GetExistingTerrain().gameObject;
                if (terrainGameObject != null)
                {
                    foreach (Transform child in terrainGameObject.transform)
                    {

                        if (child.name.StartsWith("Buildings ") && child.name.EndsWith(" Instances"))
                        {
                            count++;
                        }
                    }
                }
                if (count > 0)
                    GeneratePrefabs(selectedPrefabs, prefabCounts);
                else
                    Debug.LogWarning("Must create buildings first");
            }
            else
                GeneratePrefabs(selectedPrefabs, prefabCounts);
        }

        if (GUILayout.Button("Remove " + tabName))
        {
            DeletePrefabsFromScene();
        }
    }

    private void EnforceFreeVersionLimitation()
    {
        if (prefabCounts.Count > 1)
        {
            if (prefabCounts.Count > 0)
            {
                int firstCount = prefabCounts[0];
                GameObject firstPrefab = selectedPrefabs.Count > 0 ? selectedPrefabs[0] : null;
                int firstIndex = selectedIndexes.Count > 0 ? selectedIndexes[0] : 0;

                prefabCounts.Clear();
                selectedPrefabs.Clear();
                selectedIndexes.Clear();

                prefabCounts.Add(firstCount);
                selectedPrefabs.Add(firstPrefab);
                selectedIndexes.Add(firstIndex);

                Debug.LogWarning($"Free version limitation: Only one {tabName} type can be selected. Keeping first selection only.");
            }
            else
            {
                EnsureMinimumSelection();
            }
        }
        else if (prefabCounts.Count == 0)
        {
            EnsureMinimumSelection();
        }
    }

    private void EnsureMinimumSelection()
    {
        if (prefabCounts.Count == 0)
        {
            prefabCounts.Add(1);
            selectedPrefabs.Add(null);
            selectedIndexes.Add(0);
        }
    }
}