using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

public abstract class BaseTab 
{
    protected static GameObject terrainGameObject;
    protected static List<BuildingArea> buildingAreas = new List<BuildingArea>();
    protected static List<RoadArea> roadAreas = new List<RoadArea>();
    protected static bool isCityMode = false;

    public abstract void OnGUI();
    protected Terrain GetExistingTerrain()
    {
        return Object.FindFirstObjectByType<Terrain>();
    }

    //Abstract
    protected virtual void InstantiatePrefabOnTerrain(Transform terrainTransform, GameObject prefab, int count, List<Bounds> globalPlacedBounds)
    {
    }

    //Abstract
    protected virtual void DeletePrefabsFromScene()
    {
    }

    protected void GeneratePrefabs(List<GameObject> selectedPrefabs, List<int> counts)
    {
        Terrain existingTerrain = GetExistingTerrain();
        if (existingTerrain == null)
        {
            Debug.LogWarning("There is no terrain generated in the scene. Please generate one before adding objects.");
            return;
        }
        terrainGameObject = existingTerrain.gameObject;
        List<Bounds> globalPlacedBounds = new List<Bounds>(); 

        // Define cluster size - how many of each prefab to place at once
        int clusterSize = 3;

        // Define the threshold for when we consider the count is large enough for shuffling needs
        int countThreshold = 7; 

        // Checking if we need complex processing
        bool useSimpleGeneration = true;
        int itemsToProcess = Mathf.Min(selectedPrefabs.Count, counts.Count);

        if (selectedPrefabs.Count > 1)
        {
            for (int i = 0; i < itemsToProcess; i++)
            {
                if (selectedPrefabs[i] != null && counts[i] > 0)
                {
                    // If any prefab has a count above our threshold, we'll use complex generation
                    if (counts[i] > countThreshold)
                    {
                        useSimpleGeneration = false;
                        break;
                    }
                }
            }
        }

        if (useSimpleGeneration)
        {
            // Simple case: just place all prefabs directly, no need for shuffling
            for (int i = 0; i < itemsToProcess; i++)
            {
                if (selectedPrefabs[i] != null && counts[i] > 0)
                {
                    InstantiatePrefabOnTerrain(terrainGameObject.transform, selectedPrefabs[i], counts[i], globalPlacedBounds);
                }
            }

            return;
        }

        // Complex case: Create a list of "jobs" where each job represents a cluster of prefabs to place
        List<PrefabPlacementJob> placementJobs = new List<PrefabPlacementJob>();

        for (int i = 0; i < itemsToProcess; i++)
        {
            if (selectedPrefabs[i] != null && counts[i] > 0)
            {
                int remainingCount = counts[i];

                while (remainingCount > 0)
                {
                    int currentClusterSize = Mathf.Min(clusterSize, remainingCount);
                    placementJobs.Add(new PrefabPlacementJob
                    {
                        prefab = selectedPrefabs[i],
                        count = currentClusterSize
                    });
                    remainingCount -= currentClusterSize;
                }
            }
        }

        ShuffleList(placementJobs);

        foreach (var job in placementJobs)
        {
            InstantiatePrefabOnTerrain(terrainGameObject.transform, job.prefab, job.count, globalPlacedBounds);
        }
    }

    private class PrefabPlacementJob
    {
        public GameObject prefab;
        public int count;
    }

    //method to shuffle a list (Fisher-Yates algorithm)
    private void ShuffleList<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    protected bool IsProFeatureEnabled()
    {
        return LicenseValidator.IsProFeatureEnabledSmart("Pro Features");
    }
}