using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;



public class settingsTab : BaseTab
{
    private string sceneNameToSave = "";
    private string PreferencesNameToSave = "";

    private int selectedIndex;
    private int tempIndex = 0;

    private string licenseKeyInput = "";
    private string licenseStatusMessage = "";
    private bool isValidatingLicense = false;

    public override void OnGUI()
    {
        DrawMultiSelectionTab();
    }

    protected void DrawMultiSelectionTab()
    {
        layerFunctionality();
        GUILayout.Space(20);
        licenseFunctionality();
        GUILayout.Space(20);
        savePreferencesFunctionality();
        GUILayout.Space(20);
        loadPreferencesFunctionality();
    }

    private void licenseFunctionality()
    {
        GUILayout.Label("License Management", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (LicenseValidator.IsProActive)
        {
            EditorGUILayout.HelpBox("Pro License Active - All features unlocked!", MessageType.Info);

            if (GUILayout.Button("Deactivate License", GUILayout.Height(25)))
            {
                LicenseValidator.DeactivateLicense();
                licenseStatusMessage = "";
            }
        }
        else
        {
            if (LicenseValidator.getIsRevoked())
                licenseStatusMessage = "";

            EditorGUILayout.HelpBox("Pro features are locked. Enter your license key to unlock.", MessageType.Warning);

            GUILayout.Label("License Key:");
            GUI.SetNextControlName("LicenseKeyField");
            licenseKeyInput = EditorGUILayout.TextField(licenseKeyInput, GUILayout.Height(20));
            GUILayout.Space(5);

            if (!string.IsNullOrEmpty(licenseStatusMessage))
            {
                MessageType messageType = licenseStatusMessage.Contains("successfully") ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox(licenseStatusMessage, messageType);
            }

            GUI.enabled = !string.IsNullOrEmpty(licenseKeyInput) && !isValidatingLicense;

            string buttonText = isValidatingLicense ? "Validating..." : "Activate License";
            if (GUILayout.Button(buttonText, GUILayout.Height(30)))
            {
                ActivateLicense();
            }
            GUI.enabled = true;
        }
    }

    private void ActivateLicense()
    {
        isValidatingLicense = true;
        licenseStatusMessage = "Validating license...";

        LicenseValidator.ValidateLicense(licenseKeyInput, (success, message) => {
            isValidatingLicense = false;
            if (success)
            {
                licenseStatusMessage = "License activated successfully!";
                licenseKeyInput = "";
                GUI.FocusControl("");
            }
            else
            {
                licenseStatusMessage = $"Activation failed: {message}";
            }
        });

    }

    private void loadPreferencesFunctionality()
    {
        GUILayout.Label("Load Selected Preferences", EditorStyles.boldLabel);
        GUILayout.Space(10);
        string[] jsonFiles;
        if (Directory.Exists("Assets/Saved Preferences"))
            jsonFiles = Directory.GetFiles("Assets/Saved Preferences", "*.json");
        else
            jsonFiles = new string[0];
        string[] jsonNames = new string[jsonFiles.Length + 1];
        int preferenceIndex = tempIndex;

        jsonNames[0] = "Select Preference";
        for (int i = 0; i < jsonFiles.Length; i++)
        {
            jsonNames[i + 1] = Path.GetFileNameWithoutExtension(jsonFiles[i]);
        }

        selectedIndex = EditorGUILayout.Popup("Select Preference:", preferenceIndex, jsonNames);
        if (selectedIndex != preferenceIndex && selectedIndex > 0 && selectedIndex < jsonNames.Length + 1)
            tempIndex = selectedIndex;

        GUILayout.Space(5);
        if (selectedIndex == 0)
            GUI.enabled = false;

        if (GUILayout.Button("Load preferences", GUILayout.Height(30)))
        {
            loadPreferences(jsonNames[selectedIndex]);
        }
        GUI.enabled = true;
    }

    private void loadPreferences(string fileName)
    {
        ModularWorldGenerator mainWindow = EditorWindow.GetWindow<ModularWorldGenerator>();
        mainWindow.LoadAllTabsDataFromJSON(fileName);
        selectedIndex = 0;
        tempIndex = 0;
    }

    private void savePreferencesFunctionality()
    {
        GUILayout.Label("Save Preferences", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("Preferences file Name:");
        PreferencesNameToSave = EditorGUILayout.TextField(PreferencesNameToSave, GUILayout.Height(20));
        GUILayout.Space(5);

        if (string.IsNullOrEmpty(PreferencesNameToSave))
        {
            GUI.enabled = false;
        }
        if (GUILayout.Button("Save preferences", GUILayout.Height(30)))
        {
            savePreferences();
        }
        GUI.enabled = true;
    }

    private void savePreferences()
    {
        if (string.IsNullOrEmpty(PreferencesNameToSave.Trim()))
        {
            EditorUtility.DisplayDialog("Save Preferences", "Please enter a name for the preferences file.", "OK");
            return;
        }
        ModularWorldGenerator mainWindow = EditorWindow.GetWindow<ModularWorldGenerator>();
        mainWindow.SaveAllTabsData(PreferencesNameToSave.Trim());
        EditorUtility.DisplayDialog("Save Preferences", $"All tab data saved successfully!", "OK");
        PreferencesNameToSave = "";
    }

    private void layerFunctionality()
    {
        GUILayout.Label("Movement Layer Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox("This will create the required layers for NPC and vehicle movement.", MessageType.Info);

        allLayersExist = AllRequiredLayersExist();

        GUI.enabled = !allLayersExist;

        if (GUILayout.Button("Create Movement Layers", GUILayout.Height(30)))
        {
            CreateMovementLayers();
        }

        GUI.enabled = true;

        if (allLayersExist)
        {
            EditorGUILayout.HelpBox("All required layers already exist. No action needed.", MessageType.None);
        }
    }

    private bool AllRequiredLayersExist()
    {
        string[] requiredLayers = { "Road", "BuildingArea", "ParkArea", "Building", "Crosswalk" };

        foreach (string layer in requiredLayers)
        {
            if (!LayerExists(layer))
                return false;
        }
        return true;
    }

    void CreateMovementLayers()
    {
        string[] requiredLayers = { "Road", "BuildingArea", "ParkArea", "Building", "Crosswalk" };

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProp = tagManager.FindProperty("layers");

        foreach (string layerName in requiredLayers)
        {
            if (!LayerExists(layerName))
            {
                CreateLayer(layersProp, layerName);
            }
        }
        tagManager.ApplyModifiedProperties();
        Debug.Log("Movement layers setup complete!");
    }

    bool LayerExists(string layerName)
    {
        for (int i = 0; i < 32; i++)
        {
            if (LayerMask.LayerToName(i) == layerName)
                return true;
        }
        return false;
    }

    void CreateLayer(SerializedProperty layersProp, string layerName)
    {
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProp.stringValue))
            {
                layerProp.stringValue = layerName;
                Debug.Log($"Created layer: {layerName}");
                return;
            }
        }
        Debug.LogWarning($"No available layer slots for: {layerName}");
    }
}