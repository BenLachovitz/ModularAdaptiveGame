using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System.Collections.Generic;


public class RoadNavMeshSetup : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public int vehicleNavMeshArea = 10;
    public int npcNavMeshArea = 8;

    // Call this after generating your roads
    public void SetupRoadNavMesh(List<RoadArea> roadAreas)
    {
        Debug.Log("Setting up NavMesh for roads...");

        foreach (var road in roadAreas)
        {
            SetupRoadForNavMesh(road);
        }

        Debug.Log($"Roads prepared for NavMesh. Use Window > AI > Navigation to bake.");
        Debug.Log("Make sure to set Navigation Static on terrain and roads!");
    }

    private void SetupRoadForNavMesh(RoadArea road)
    {
        if (road.road == null) return;

        if (road.road.GetComponent<Collider>() == null)
        {
            MeshCollider meshCollider = road.road.AddComponent<MeshCollider>();
            meshCollider.convex = false;
        }

        GameObjectUtility.SetStaticEditorFlags(road.road, StaticEditorFlags.NavigationStatic);

        if (road.direction != -1)
        {
            CreateDirectionalConstraints(road);
        }
    }

    private void CreateDirectionalConstraints(RoadArea road)
    {
        // Create invisible NavMesh obstacles to guide direction
        GameObject constraintObj = new GameObject($"DirectionConstraint_{road.direction}");
        constraintObj.transform.SetParent(road.road.transform);
        constraintObj.transform.localPosition = Vector3.zero;

        // Add NavMesh obstacle that blocks wrong-way movement
        NavMeshObstacle obstacle = constraintObj.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.shape = NavMeshObstacleShape.Box;

        // Size and position obstacle based on direction
        Vector3 obstacleSize = Vector3.zero;
        Vector3 obstacleOffset = Vector3.zero;

        switch (road.direction)
        {
            case 0: // East-bound
                obstacleSize = new Vector3(0.5f, 1f, road.road.transform.localScale.z);
                obstacleOffset = new Vector3(-road.road.transform.localScale.x * 0.25f, 0, 0);
                break;
            case 1: // West-bound
                obstacleSize = new Vector3(0.5f, 1f, road.road.transform.localScale.z);
                obstacleOffset = new Vector3(road.road.transform.localScale.x * 0.25f, 0, 0);
                break;
            case 2: // North-bound
                obstacleSize = new Vector3(road.road.transform.localScale.x, 1f, 0.5f);
                obstacleOffset = new Vector3(0, 0, -road.road.transform.localScale.z * 0.25f);
                break;
            case 3: // South-bound
                obstacleSize = new Vector3(road.road.transform.localScale.x, 1f, 0.5f);
                obstacleOffset = new Vector3(0, 0, road.road.transform.localScale.z * 0.25f);
                break;
        }

        obstacle.size = obstacleSize;
        constraintObj.transform.localPosition = obstacleOffset;
    }

    // Helper method to create a simple vehicle agent for testing
    public void CreateTestVehicle(Vector3 position)
    {
        GameObject vehicle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        vehicle.name = "TestVehicle";
        vehicle.transform.position = position;
        vehicle.transform.localScale = new Vector3(1f, 0.5f, 2f); // Car-like proportions

        NavMeshAgent agent = vehicle.AddComponent<NavMeshAgent>();
        agent.speed = 5f;
        agent.acceleration = 8f;
        agent.angularSpeed = 120f;
        agent.stoppingDistance = 1f;

        vehicle.AddComponent<SimpleVehicleMovement>();
    }
}


// Simple test movement script
public class SimpleVehicleMovement : MonoBehaviour
{
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // Click to move vehicle
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                agent.SetDestination(hit.point);
            }
        }
    }
}