**There is a link for video of our demo**

[Video for demo](https://drive.google.com/file/d/14EpRXUzhxVh5byu_-xDLFqzwP1azPLyU/view?usp=sharing)

# Modular World Generator

Modular World Generator is a Unity editor tool designed to generate and manage world elements through modular tabs. This tool facilitates the creation and manipulation of various types of in-game assets, including terrain, buildings, NPCs, and vehicles.

## Overview

The `ModularWorldGenerator` script serves as the main entry point for the tool. It initializes and manages different tabs, allowing users to interact with specific components of the world. Depending on the selected tab, the appropriate script methods are utilized.

## Script Structure

### ModularWorldGenerator (Main Script)
This script is responsible for creating the tool's interface and managing the various tabs. It dynamically switches between modules based on the selected tab.

#### Key Features:
- Opens the Unity editor tool via `MenuItem`.
- Initializes tab instances upon activation.
- Displays an interface for selecting and working with specific tabs.

### Tab Hierarchy
The system is designed with an inheritance structure to maximize flexibility and reusability.

- **BaseTab**  
  The root parent of all scripts. It contains general functions that every tab needs to use or override.

- **ConfigurationTab** _(Inherits from BaseTab)_  
  Handles the logic and algorithms for terrain generation.

- **PrefabTab** _(Inherits from BaseTab)_  
  Acts as the parent for all prefab-type scripts, containing general functions that different prefab tabs will use or override.

  - **BuildingsTab** _(Inherits from PrefabTab)_  
    Contains logic and algorithms specifically for building prefabs.

  - **NPCTab** _(Inherits from PrefabTab)_  
    Handles logic and algorithms for NPC prefabs.

  - **VehiclesTab** _(Inherits from PrefabTab)_  
    Manages logic and algorithms for vehicle prefabs.

## Usage

1. Open the tool through `Tools > Modular World Generator` in the Unity editor.
2. Select the desired tab (`Configuration`, `NPC`, `Buildings`, `Vehicles`).
3. Use the appropriate functions within each tab to manipulate world elements.

## Future Enhancements
Potential improvements include additional prefab types, extended customization options, and automated world generation features.

---

This structured approach ensures maintainability and scalability while keeping the functionality clear. Let me know if you’d like any refinements!
