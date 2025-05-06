using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public abstract class BaseTab
{
    // Common properties and methods for all tabs
    protected GameObject terrainGameObject;

    // Abstract method that each tab must implement
    public abstract void OnGUI();

    // Common method to check if terrain exists
    protected Terrain GetExistingTerrain()
    {
        return Object.FindFirstObjectByType<Terrain>();
    }

    // Common method to instantiate prefabs with collision detection
    protected void InstantiatePrefabOnTerrain(Transform terrainTransform, GameObject prefab, int count, List<Bounds> globalPlacedBounds)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Prefab is null. Skipping generation.");
            return;
        }

        Terrain terrain = terrainTransform.GetComponent<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("Terrain component not found. Skipping generation.");
            return;
        }

        // Get terrain dimensions
        float terrainLength = terrain.terrainData.size.z;
        float terrainWidth = terrain.terrainData.size.x;
        Vector3 terrainPosition = terrain.transform.position;

        float prefabHeight = 0;
        Vector3 prefabSize = Vector3.one; // Default size if no renderer found
        float bottomOffset = 0; // Distance from pivot to bottom of the prefab

        // Instantiate a temporary prefab to get accurate bounds
        GameObject tempPrefab = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        tempPrefab.transform.position = new Vector3(10000, 10000, 10000); // Place far away from scene

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

        // Clean up the temporary prefab
        Object.DestroyImmediate(tempPrefab);

        // Use a generous buffer
        float maxDimension = Mathf.Max(prefabSize.x, prefabSize.z);
        float collisionCheckBuffer = maxDimension * 0.75f;

        // Create the parent object for all instances
        GameObject prefabParent = new GameObject(prefab.name + " Instances");
        prefabParent.transform.SetParent(terrainGameObject.transform);

        int successfulPlacements = 0;
        int maxAttempts = count * 50; // Increase attempts significantly
        int attempts = 0;

        // Calculate boundary margins based on prefab size to ensure it stays within terrain
        float marginX = prefabSize.x / 2;
        float marginZ = prefabSize.z / 2;

        while (successfulPlacements < count && attempts < maxAttempts)
        {
            attempts++;

            // Generate random position within terrain bounds minus margins
            float x = Random.Range(marginX, terrainWidth - marginX);
            float z = Random.Range(marginZ, terrainLength - marginZ);
            float y = 0 - bottomOffset; // Position at ground level

            Vector3 worldPosition = new Vector3(x, y, z) + terrainPosition;

            // Calculate the center position for the bounds check
            Vector3 boundsCenter = worldPosition + new Vector3(0, prefabHeight / 2 + bottomOffset, 0);

            // Create a bounds object at the potential position
            Bounds newObjectBounds = new Bounds(
                boundsCenter,
                new Vector3(
                    prefabSize.x + collisionCheckBuffer,
                    prefabSize.y,
                    prefabSize.z + collisionCheckBuffer
                )
            );

            // Check if the bounds are entirely within the terrain
            bool withinTerrainBounds = IsWithinTerrainBounds(newObjectBounds, terrainPosition, terrainWidth, terrainLength);
            if (!withinTerrainBounds)
            {
                continue; // Skip this position if outside terrain bounds
            }

            // First check: see if this intersects with any placed bounds
            bool positionIsFree = true;
            foreach (Bounds existingBounds in globalPlacedBounds)
            {
                if (newObjectBounds.Intersects(existingBounds))
                {
                    positionIsFree = false;
                    break;
                }
            }

            // Second check: use Physics.OverlapBox to check for scene objects
            if (positionIsFree)
            {
                // Only check for static objects not accounted for in our placedBounds list
                Collider[] colliders = Physics.OverlapBox(
                    boundsCenter,
                    new Vector3(prefabSize.x, prefabSize.y, prefabSize.z) / 2, // Just the actual size, no buffer
                    Quaternion.identity
                );

                foreach (Collider collider in colliders)
                {
                    // Skip terrain and plane
                    if (!collider.gameObject.name.Contains("Terrain") &&
                        !collider.gameObject.name.Contains("Plane"))
                    {
                        positionIsFree = false;
                        break;
                    }
                }
            }

            // If position is free, place the object
            if (positionIsFree)
            {
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                prefabInstance.transform.position = worldPosition;
                prefabInstance.transform.SetParent(prefabParent.transform);

                // Add a collider if needed
                if (prefabInstance.GetComponent<Collider>() == null)
                {
                    BoxCollider boxCollider = prefabInstance.AddComponent<BoxCollider>();
                    boxCollider.size = prefabSize;
                    boxCollider.center = new Vector3(0, prefabHeight / 2 + bottomOffset, 0);
                }

                // Add the actual bounds with no buffer for accuracy
                globalPlacedBounds.Add(new Bounds(
                    worldPosition + new Vector3(0, prefabHeight / 2 + bottomOffset, 0),
                    prefabSize
                ));

                successfulPlacements++;

                // Visual debug - draw the bounds in scene view
                Debug.DrawLine(newObjectBounds.min, newObjectBounds.max, Color.green, 10f);
            }
        }

        if (successfulPlacements < count)
        {
            Debug.LogWarning($"Could only place {successfulPlacements} out of {count} instances of {prefab.name} due to space constraints.");
        }
        else
        {
            Debug.Log($"Successfully placed {count} instances of {prefab.name}");
        }
    }

    // Helper method to check if object bounds are entirely within terrain bounds
    private bool IsWithinTerrainBounds(Bounds objectBounds, Vector3 terrainPosition, float terrainWidth, float terrainLength)
    {
        // Calculate terrain corners in world space
        float minX = terrainPosition.x;
        float maxX = terrainPosition.x + terrainWidth;
        float minZ = terrainPosition.z;
        float maxZ = terrainPosition.z + terrainLength;

        // Check if object bounds are entirely within terrain bounds
        return (objectBounds.min.x >= minX && objectBounds.max.x <= maxX &&
                objectBounds.min.z >= minZ && objectBounds.max.z <= maxZ);
    }

    // Common method for prefab generation
    protected void GeneratePrefabs(List<GameObject> selectedPrefabs, List<int> counts)
    {
        Terrain existingTerrain = GetExistingTerrain();
        if (existingTerrain == null)
        {
            Debug.LogWarning("There is no terrain generated in the scene. Please generate one before adding objects.");
            return;
        }
        terrainGameObject = existingTerrain.gameObject;
        List<Bounds> globalPlacedBounds = new List<Bounds>(); // Shared bounds list

        int itemsToProcess = Mathf.Min(selectedPrefabs.Count, counts.Count);

        for (int i = 0; i < itemsToProcess; i++)
        {
            if (selectedPrefabs[i] != null)
            {
                InstantiatePrefabOnTerrain(terrainGameObject.transform, selectedPrefabs[i], counts[i], globalPlacedBounds);
            }
        }
    }
}