using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public abstract class PrefabTab : BaseTab
{
    protected List<GameObject> selectedPrefabs = new List<GameObject>();
    protected List<int> prefabCounts = new List<int>();
    protected List<int> selectedIndexes = new List<int>();
    protected string prefabFolderPath;
    protected string tabName;

    protected PrefabTab(string tabName, string prefabFolderPath)
    {
        this.tabName = tabName;
        this.prefabFolderPath = prefabFolderPath;
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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox); // Creates a boxed section

            prefabCounts[i] = EditorGUILayout.IntField("Count:", prefabCounts[i]);

            List<string> combinedGuids = new List<string>();

            // Find prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolderPath });
            combinedGuids.AddRange(prefabGuids);

            // Find FBX models (t:Model)
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { prefabFolderPath });
            combinedGuids.AddRange(modelGuids);

            string[] prefabPaths = combinedGuids.ToArray();
            string[] prefabNames = new string[prefabPaths.Length];

            for (int j = 0; j < prefabPaths.Length; j++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabPaths[j]);
                prefabNames[j] = System.IO.Path.GetFileNameWithoutExtension(path);
            }

            if (selectedIndexes.Count <= i)
            {
                selectedIndexes.Add(0); // Ensure the list has enough elements
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

            // Create a flexible layout where the button moves down if needed
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(EditorGUIUtility.labelWidth - 75))) // Small width button
            {
                prefabCounts.RemoveAt(i);
                selectedPrefabs.RemoveAt(i);
                selectedIndexes.RemoveAt(i);
                i--; // Adjust the index as we've removed an item
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue; // Skip the rest of this iteration
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical(); // End boxed section
        }

        if (GUILayout.Button("+"))
        {
            prefabCounts.Add(1);
            selectedPrefabs.Add(null);
            selectedIndexes.Add(0);
        }

        if (GUILayout.Button("Generate " + tabName))
        {
            GeneratePrefabs(selectedPrefabs, prefabCounts);
        }
    }
}