using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ModularWorldGenerator : EditorWindow
{
    private int selectedTab = 0; // Variable to track the selected tab

    // Tab module instances
    private ConfigurationTab configTab;
    private NPCTab npcTab;
    private BuildingsTab buildingsTab;
    private VehiclesTab vehiclesTab;

    // Add a menu item to open the tool
    [MenuItem("Tools/Modular World Generator")]
    public static void ShowWindow()
    {
        GetWindow<ModularWorldGenerator>("Modular World Generator");
    }

    private void OnEnable()
    {
        // Initialize all tab modules
        configTab = new ConfigurationTab();
        npcTab = new NPCTab();
        buildingsTab = new BuildingsTab();
        vehiclesTab = new VehiclesTab();
    }

    private void OnGUI()
    {
        selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "Configuration", "NPC", "Buildings", "Vehicles" });
        GUILayout.Space(10);

        switch (selectedTab)
        {
            case 0:
                configTab.OnGUI();
                break;
            case 1:
                npcTab.OnGUI();
                break;
            case 2:
                buildingsTab.OnGUI();
                break;
            case 3:
                vehiclesTab.OnGUI();
                break;
        }
    }
}