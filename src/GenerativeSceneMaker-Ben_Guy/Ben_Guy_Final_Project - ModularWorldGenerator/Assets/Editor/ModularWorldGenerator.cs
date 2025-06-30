using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.VisualScripting;

public class ModularWorldGenerator : EditorWindow
{
    private int selectedTab = 0; 
    [SerializeField] private ConfigurationTab configTab;
    [SerializeField] private NPCTab npcTab;
    [SerializeField] private BuildingsTab buildingsTab;
    [SerializeField] private VehiclesTab vehiclesTab;
    [SerializeField] private settingsTab settingsTab;
    private Vector2 scrollPos;

    [MenuItem("Tools/Modular World Generator")]
    public static void ShowWindow()
    {
        GetWindow<ModularWorldGenerator>("Modular World Generator");
    }

    private void OnEnable()
    {
        configTab = new ConfigurationTab();
        npcTab = new NPCTab();
        buildingsTab = new BuildingsTab();
        vehiclesTab = new VehiclesTab();
        settingsTab = new settingsTab();
    }

    private void OnGUI()
    {
        selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "Configuration", "Buildings", "NPC", "Vehicles", "Settings" });
        GUILayout.Space(10);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        switch (selectedTab)
        {
            case 0:
                configTab.OnGUI();
                break;
            case 1:
                buildingsTab.OnGUI();
                break;
            case 2:
                npcTab.OnGUI();
                break;
            case 3:
                vehiclesTab.OnGUI();
                break;
            case 4:
                settingsTab.OnGUI();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    public void SaveAllTabsData(string fileName)
    {
        string folderPath = "Assets/Saved Preferences";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Saved Preferences");
        }

        var preferencesData = new PreferencesData
        {
            configuration = configTab.GetTabData(),
            npc = npcTab.GetTabData(),
            buildings = buildingsTab.GetTabData(),
            vehicles = vehiclesTab.GetTabData(),
            timestamp = System.DateTime.Now.ToString(),
            savedByUser = System.Environment.UserName
        };

        string fullPath = System.IO.Path.Combine(folderPath, fileName + ".json");

        string jsonData = JsonUtility.ToJson(preferencesData, true);
        System.IO.File.WriteAllText(fullPath, jsonData);

        Debug.Log($"World generator data saved to: {fullPath}");
        AssetDatabase.Refresh();
    }

    public void LoadAllTabsDataFromJSON(string fileName)
    {
        string fullPath = System.IO.Path.Combine("Assets/Saved Preferences", fileName + ".json");

        if (!System.IO.File.Exists(fullPath))
        {
            Debug.LogError($"File not found: {fullPath}");
            return;
        }

        try
        {
            string jsonData = System.IO.File.ReadAllText(fullPath);
            PreferencesData preferencesData = JsonUtility.FromJson<PreferencesData>(jsonData);

            configTab.SetTabData(preferencesData.configuration);
            npcTab.SetTabData(preferencesData.npc);
            buildingsTab.SetTabData(preferencesData.buildings);
            vehiclesTab.SetTabData(preferencesData.vehicles);

            Debug.Log($"World generator data loaded from: {fullPath}");
            Debug.Log($"Data saved by: {preferencesData.savedByUser} at {preferencesData.timestamp}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading JSON file {fullPath}: {e.Message}");
        }
    }

    public void CreateRandomScene(int numOfBuildingsToCreate, int numOfNPCToCreate, int numOfVehcilesToCreate)
    {        
        buildingsTab.randomizeLists(numOfBuildingsToCreate);
        npcTab.randomizeLists(numOfNPCToCreate);
        vehiclesTab.randomizeLists(numOfVehcilesToCreate);
    }
}