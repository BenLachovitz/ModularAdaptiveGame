using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LayerMovementManager : MonoBehaviour
{
    [Header("Layer References")]
    public int roadLayer;
    public int buildingAreaLayer;
    public int parkAreaLayer;
    public int buildingLayer;
    public int crosswalkLayer;

    [Header("Movement LayerMasks")]
    public LayerMask npcMovementLayers;
    public LayerMask vehicleMovementLayers;
    public LayerMask obstacleLayers;

    [Header("Movement Settings")]
    public float npcSpeed = 2f;
    public float vehicleSpeed = 8f;
    public float directionChangeInterval = 3f;

    [Header("NavMesh Settings")]
    [SerializeField] private bool autoSetupNavMesh = true;
    [SerializeField] private bool setupVehicleNavMesh = true;
    [SerializeField] private float navMeshAgentRadius = 0.5f;
    [SerializeField] private float navMeshAgentHeight = 2.0f;
    [SerializeField] private float navMeshMaxSlope = 45f;
    [SerializeField] private float navMeshStepHeight = 0.4f;

    [Header("NavMesh Vehicle Settings")]
    [SerializeField] private float vehicleAgentRadius = 1f;
    [SerializeField] private float vehicleAgentHeight = 4f;

    private static LayerMovementManager instance;
    public static LayerMovementManager Instance => instance;

    // Track objects for NavMesh setup
    private List<GameObject> trackedRoads = new List<GameObject>();
    private List<GameObject> trackedBuildingAreas = new List<GameObject>();
    private List<GameObject> trackedParkAreas = new List<GameObject>();
    private List<GameObject> trackedObstacles = new List<GameObject>();
    private List<GameObject> trackedCrosswalks = new List<GameObject>();

    // Track NavMesh surfaces for proper cleanup
    private List<NavMeshSurface> activeSurfaces = new List<NavMeshSurface>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            SetupLayers();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Complete cleanup method called BEFORE terrain generation
    public void PrepareForNewTerrain()
    {
        Debug.Log("Preparing for new terrain - cleaning up previous NavMesh data...");

        ClearAllNavMeshData();
        ClearTrackedObjects();

        Debug.Log("NavMesh cleanup complete - ready for new terrain");
    }

    // Comprehensive NavMesh cleanup
    private void ClearAllNavMeshData()
    {
#if UNITY_EDITOR
        try
        {
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();

            foreach (var surface in activeSurfaces)
            {
                if (surface != null)
                {
                    DestroyImmediate(surface.gameObject);
                }
            }
            activeSurfaces.Clear();

            NavMeshSurface[] allSurfaces = FindObjectsOfType<NavMeshSurface>();
            foreach (var surface in allSurfaces)
            {
                DestroyImmediate(surface.gameObject);
            }

            ResetAllNavigationStaticFlags();

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during NavMesh cleanup: {e.Message}");
        }
#endif
    }

    private void ResetAllNavigationStaticFlags()
    {
#if UNITY_EDITOR
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int resetCount = 0;
        
        foreach (GameObject obj in allObjects)
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(obj);
            if ((flags & StaticEditorFlags.NavigationStatic) != 0)
            {
                StaticEditorFlags newFlags = flags & ~StaticEditorFlags.NavigationStatic;
                GameObjectUtility.SetStaticEditorFlags(obj, newFlags);
                resetCount++;
            }
        }
#endif
    }

    private void ClearTrackedObjects()
    {
        trackedRoads.Clear();
        trackedBuildingAreas.Clear();
        trackedParkAreas.Clear();
        trackedObstacles.Clear();
        trackedCrosswalks.Clear();
    }

    public void SetupLayers()
    {
        roadLayer = LayerMask.NameToLayer("Road");
        buildingAreaLayer = LayerMask.NameToLayer("BuildingArea");
        parkAreaLayer = LayerMask.NameToLayer("ParkArea");
        buildingLayer = LayerMask.NameToLayer("Building");
        crosswalkLayer = LayerMask.NameToLayer("Crosswalk");

        if (roadLayer == -1) Debug.LogWarning("Road layer not found! Create it in Project Settings.");
        if (buildingAreaLayer == -1) Debug.LogWarning("BuildingArea layer not found!");
        if (parkAreaLayer == -1) Debug.LogWarning("ParkArea layer not found!");
        if (buildingLayer == -1) Debug.LogWarning("Building layer not found!");
        if (crosswalkLayer == -1) Debug.LogWarning("Crosswalk layer not found! Create it in Project Settings.");

        npcMovementLayers = (1 << buildingAreaLayer) | (1 << parkAreaLayer) | (1 << crosswalkLayer);
        vehicleMovementLayers = (1 << roadLayer) | (1 << crosswalkLayer);
        obstacleLayers = (1 << buildingLayer);
    }

    public void AssignCrosswalkLayer(GameObject obj)
    {
        obj.layer = crosswalkLayer;
        SetupNavMeshForCrosswalk(obj);
        if (!trackedCrosswalks.Contains(obj))
            trackedCrosswalks.Add(obj);
    }

    public List<GameObject> getTrackedCrosswalks() { return trackedCrosswalks; }

    private void SetupNavMeshForCrosswalk(GameObject crosswalkObj)
    {
        if (!autoSetupNavMesh) return;

#if UNITY_EDITOR
        // Mark as Navigation Static for NPC NavMesh
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(crosswalkObj);
        GameObjectUtility.SetStaticEditorFlags(crosswalkObj, flags | StaticEditorFlags.NavigationStatic);
#endif

        if (crosswalkObj.GetComponent<Collider>() == null)
        {
            crosswalkObj.AddComponent<MeshCollider>();
        }
    }

    public void AssignRoadLayer(GameObject obj)
    {
        obj.layer = roadLayer;
        SetupNavMeshForRoad(obj);
        if (!trackedRoads.Contains(obj))
            trackedRoads.Add(obj);
    }

    public void AssignBuildingAreaLayer(GameObject obj)
    {
        obj.layer = buildingAreaLayer;
        SetupNavMeshForWalkableArea(obj);
        if (!trackedBuildingAreas.Contains(obj))
            trackedBuildingAreas.Add(obj);
    }

    public void AssignParkAreaLayer(GameObject obj)
    {
        obj.layer = parkAreaLayer;
        SetupNavMeshForWalkableArea(obj);
        if (!trackedParkAreas.Contains(obj))
            trackedParkAreas.Add(obj);
    }

    public void AssignBuildingLayer(GameObject obj)
    {
        obj.layer = buildingLayer;
        SetupNavMeshForObstacle(obj);
        if (!trackedObstacles.Contains(obj))
            trackedObstacles.Add(obj);
    }

    private void SetupNavMeshForRoad(GameObject roadObj)
    {
        // For NPC NavMesh: Roads are NOT marked as Navigation Static at all
        if (roadObj.GetComponent<Collider>() == null)
        {
            roadObj.AddComponent<MeshCollider>();
        }

#if UNITY_EDITOR
        // Making sure roads are NEVER marked as Navigation Static
        StaticEditorFlags currentFlags = GameObjectUtility.GetStaticEditorFlags(roadObj);
        StaticEditorFlags newFlags = currentFlags & ~StaticEditorFlags.NavigationStatic;
        GameObjectUtility.SetStaticEditorFlags(roadObj, newFlags);
#endif
    }

    private void SetupNavMeshForWalkableArea(GameObject areaObj)
    {
        if (!autoSetupNavMesh) return;

#if UNITY_EDITOR
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(areaObj);
        GameObjectUtility.SetStaticEditorFlags(areaObj, flags | StaticEditorFlags.NavigationStatic);
#endif

        if (areaObj.GetComponent<Collider>() == null)
        {
            areaObj.AddComponent<MeshCollider>();
        }
    }

    private void SetupNavMeshForObstacle(GameObject obstacleObj)
    {
        if (!autoSetupNavMesh) return;

#if UNITY_EDITOR
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(obstacleObj);
        GameObjectUtility.SetStaticEditorFlags(obstacleObj, flags | StaticEditorFlags.NavigationStatic);
#endif

        if (obstacleObj.GetComponent<Collider>() == null)
        {
            obstacleObj.AddComponent<BoxCollider>();
        }
    }

    public void BakeNavMeshForScene()
    {
        if (!autoSetupNavMesh)
        {
            return;
        }

#if UNITY_EDITOR
        try
        {
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            
            foreach (var surface in activeSurfaces)
            {
                if (surface != null)
                {
                    DestroyImmediate(surface.gameObject);
                }
            }
            activeSurfaces.Clear();
            
            CreateNPCNavMeshSurface();

        bool hasVehicleAgentType = CreateAgentTypeViaNavMeshAPI();
            
        if (setupVehicleNavMesh && hasVehicleAgentType)
        {
            CreateVehicleNavMeshSurface();
        }
        else if (setupVehicleNavMesh && !hasVehicleAgentType)
        {
            Debug.LogWarning("⚠️ Vehicle NavMesh disabled - Agent Type 1 not available");
        }  
            ReportFinalNavMeshState();
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"NavMesh setup failed: {e.Message}");
        }
#endif
    }

    private bool CreateAgentTypeViaNavMeshAPI()
    {
#if UNITY_EDITOR

    var settingsCount = NavMesh.GetSettingsCount();
    if (settingsCount >= 2)
    {
        return true;
    }

    try
    {
        NavMeshBuildSettings newSettings = NavMesh.CreateSettings();
        newSettings.agentRadius = 100f;
        newSettings.agentHeight = vehicleAgentHeight;
        newSettings.agentSlope = navMeshMaxSlope;
        newSettings.agentClimb = navMeshStepHeight;
        
        return true;
    }
    catch (System.Exception e)
    {
        Debug.LogWarning($"NavMesh API method failed: {e.Message}");
        return false;
    }
#else
        return false;
#endif
    }

    private void CreateNPCNavMeshSurface()
    {
        GameObject npcNavMeshObj = new GameObject("NPC_NavMeshSurface");
        npcNavMeshObj.transform.SetParent(transform);

        var npcSurface = npcNavMeshObj.AddComponent<NavMeshSurface>();
        npcSurface.agentTypeID = 0;
        npcSurface.collectObjects = CollectObjects.All;
        npcSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        // Including only NPC-valid layers
        npcSurface.layerMask = (1 << buildingAreaLayer) | (1 << parkAreaLayer) | (1 << crosswalkLayer);

        npcSurface.BuildNavMesh();

        activeSurfaces.Add(npcSurface);
    }

    private void CreateVehicleNavMeshSurface()
    {
        GameObject vehicleNavMeshObj = new GameObject("Vehicle_NavMeshSurface");
        vehicleNavMeshObj.transform.SetParent(transform);

        var vehicleSurface = vehicleNavMeshObj.AddComponent<NavMeshSurface>();
        vehicleSurface.collectObjects = CollectObjects.All;
        vehicleSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        vehicleSurface.layerMask = (1 << roadLayer) | (1 << crosswalkLayer);

        bool surfaceCreated = false;
        try
        {
            vehicleSurface.agentTypeID = NavMesh.GetSettingsByIndex(1).agentTypeID;
            vehicleSurface.BuildNavMesh();
            surfaceCreated = true;
        }
        catch (System.Exception e)
        {
            Debug.Log($"Agent Type 1 failed: {e.Message}");
        }

        if (!surfaceCreated)
        {
            try
            {
                vehicleSurface.agentTypeID = 0;
                vehicleSurface.BuildNavMesh();
                surfaceCreated = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Vehicle NavMesh creation completely failed: {e.Message}");
                DestroyImmediate(vehicleNavMeshObj);
                return;
            }
        }

        if (surfaceCreated)
        {
            activeSurfaces.Add(vehicleSurface);
        }
    }

    private void ReportFinalNavMeshState()
    {
        var npcSurface = GameObject.Find("NPC_NavMeshSurface");
        var vehicleSurface = GameObject.Find("Vehicle_NavMeshSurface");

        if (npcSurface != null)
        {
            Debug.Log("NPC NavMesh Surface: Active (Agent Type 0)");
        }

        if (vehicleSurface != null)
        {
            Debug.Log("Vehicle NavMesh Surface: Active");
        }
    }

    // Public method for ConfigurationTab to call BEFORE terrain generation
    public void PrepareForTerrainGeneration()
    {
        PrepareForNewTerrain();
    }

    // Public method for ConfigurationTab to call AFTER terrain generation
    public void OnTerrainGenerationComplete()
    {
        // Small delay to ensure all objects are properly set up
        Invoke(nameof(BakeNavMeshForScene), 0.1f);
    }

    [ContextMenu("Manual Bake NavMesh")]
    public void ManualBakeNavMesh()
    {
#if UNITY_EDITOR
        try
        {
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            BakeNavMeshForScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"NavMesh baking failed: {e.Message}");
        }
#else
        Debug.LogWarning("NavMesh baking only available in Editor");
#endif
    }

    public bool IsValidNPCPosition(Vector3 position, float radius = 0.5f)
    {
        RaycastHit hit;
        Vector3 rayOrigin = position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1f))
        {
            GameObject surfaceObject = hit.collider.gameObject;
            int surfaceLayer = surfaceObject.layer;

            bool isOnValidLayer = (npcMovementLayers & (1 << surfaceLayer)) != 0;

            return isOnValidLayer;
        }

        return false;
    }

    public bool IsValidVehiclePosition(Vector3 position, float radius = 1f)
    {
        RaycastHit hit;
        Vector3 rayOrigin = position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1f))
        {
            GameObject surfaceObject = hit.collider.gameObject;
            int surfaceLayer = surfaceObject.layer;

            bool isOnValidLayer = (vehicleMovementLayers & (1 << surfaceLayer)) != 0;

            if (isOnValidLayer)
            {
                if (surfaceLayer == roadLayer)
                {
                    RoadDataManager roadManager = RoadDataManager.Instance;
                    if (roadManager != null)
                    {
                        RoadSegmentData roadData = roadManager.FindRoadAtPosition(position);
                        // Can add road-specific validation logic here if needed
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    public bool IsPositionOnNavMesh(Vector3 position, float maxDistance = 1f)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas);
    }

    public Vector3 GetNearestNavMeshPosition(Vector3 position, float maxDistance = 5f)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return position;
    }

    public void SetupNPCForNavMesh(GameObject npcObj)
    {
        NavMeshAgent agent = npcObj.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = npcObj.AddComponent<NavMeshAgent>();
        }

        agent.agentTypeID = 0;
        agent.speed = npcSpeed;
        agent.radius = navMeshAgentRadius;
        agent.height = navMeshAgentHeight;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.stoppingDistance = 1f;
    }

    public void SetupVehicleForNavMesh(GameObject vehicleObj)
    {
        NavMeshAgent agent = vehicleObj.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = vehicleObj.AddComponent<NavMeshAgent>();
        }

        try
        {
            agent.agentTypeID = NavMesh.GetSettingsByIndex(1).agentTypeID;
            agent.speed = vehicleSpeed;
            agent.radius = vehicleAgentRadius;
            agent.height = vehicleAgentHeight;
        }
        catch
        {
            agent.agentTypeID = 0;
            agent.speed = vehicleSpeed;
            agent.radius = navMeshAgentRadius;
            agent.height = navMeshAgentHeight;
        }

        agent.baseOffset = 0f;

        // Other vehicle-specific settings
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.stoppingDistance = 4f;
        agent.acceleration = 4f;
        agent.angularSpeed = 120f; 
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    public void ConfigureVehicleForNavigation(GameObject vehicleObj)
    {
        SetupVehicleForNavMesh(vehicleObj);

        if (vehicleObj.GetComponent<VehicleMovement>() == null)
        {
            vehicleObj.AddComponent<VehicleMovement>();
        }
    }

    public RoadSegmentData GetVehicleCurrentRoad(Vector3 vehiclePosition)
    {
        RoadDataManager roadManager = RoadDataManager.Instance;
        if (roadManager == null)
        {
            return null;
        }

        return roadManager.FindRoadAtPosition(vehiclePosition);
    }
}