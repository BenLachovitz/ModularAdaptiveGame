using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ConfigurationTab : BaseTab
{
    private float terrainLength = 500f;
    private float terrainWidth = 500f;
    private bool immersiveValue = false;

    private Material planeMaterial;
    private List<Material> availableMaterials = new List<Material>();
    private string[] materialNames;
    private int selectedMaterialIndex = 0;

    public ConfigurationTab()
    {
        LoadMaterialsFromFolder();
    }

    public override void OnGUI()
    {
        LoadMaterialsFromFolder();
        DrawTerrainCreationTab();
    }

    private void DrawTerrainCreationTab()
    {
        GUILayout.Label("Terrain", EditorStyles.boldLabel);
        terrainLength = EditorGUILayout.FloatField("Terrain Length:", terrainLength);
        terrainWidth = EditorGUILayout.FloatField("Terrain Width:", terrainWidth);

        GUILayout.Label("\n\nPlane Settings", EditorStyles.boldLabel);
        Material tempMaterial = (Material)EditorGUILayout.ObjectField("Plane Material:", planeMaterial, typeof(Material), false);

        if (tempMaterial != null)
        {
            string path = AssetDatabase.GetAssetPath(tempMaterial);
            if (path.StartsWith("Assets/Materials/Materials"))
            {
                planeMaterial = tempMaterial;
            }
            else
            {
                EditorGUILayout.HelpBox("Please select a material from the Assets/Materials/Materials folder.", MessageType.Warning);
            }
        }

        GUILayout.Label("\n\nImmersive NPC Behavior", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Enable smart assets behavior", GUILayout.Width(EditorGUIUtility.labelWidth + 25));
        immersiveValue = EditorGUILayout.Toggle(immersiveValue);
        GUILayout.EndHorizontal();

        GUILayout.Label("\n");
        if (GUILayout.Button("Create Scene"))
        {
            CreateTerrainInScene();
        }
    }

    private void LoadMaterialsFromFolder()
    {
        availableMaterials.Clear();

        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" });

        List<string> namesList = new List<string>();
        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                availableMaterials.Add(mat);
                namesList.Add(mat.name);
            }
        }

        materialNames = namesList.ToArray();
    }

    private void CreateTerrainInScene()
    {
        Terrain existingTerrain = Object.FindFirstObjectByType<Terrain>();
        if (existingTerrain != null)
        {
            Debug.LogWarning("A terrain already exists in the scene!");
            Object.DestroyImmediate(existingTerrain.gameObject);
        }

        terrainGameObject = new GameObject("Terrain");
        Terrain terrain = terrainGameObject.AddComponent<Terrain>();
        TerrainCollider terrainCollider = terrainGameObject.AddComponent<TerrainCollider>();
        terrain.terrainData = new TerrainData
        {
            heightmapResolution = 513,
            size = new Vector3(terrainLength, 50f, terrainWidth)
        };
        terrainCollider.terrainData = terrain.terrainData;
        terrainGameObject.transform.position = Vector3.zero;

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Plane";

        // Unity's default Plane is 10x10 units, so scale it
        plane.transform.localScale = new Vector3(terrainLength / 10f, 1f, terrainWidth / 10f);
        plane.transform.position = new Vector3(terrainLength / 2f, 0.01f, terrainWidth / 2f);

        // Optional: parent the plane to the terrain
        plane.transform.SetParent(terrainGameObject.transform);

        if (planeMaterial != null)
        {
            plane.GetComponent<Renderer>().material = planeMaterial;
        }

        Debug.Log($"New Terrain (size: {terrainLength}X{terrainWidth}) created in the scene!");
    }
}