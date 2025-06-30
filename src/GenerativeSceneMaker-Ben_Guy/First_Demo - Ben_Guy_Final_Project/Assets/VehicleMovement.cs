using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

public class VehicleMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float normalSpeed = 14f;
    public float slowSpeed = 6f;
    public float stopDistance = 2f;
    public float npcDetectionRange = 7f;
    public float intersectionApproachDistance = 4f;

    [Header("Navigation")]
    public float waypointReachDistance = 3f;
    public float pathUpdateInterval = 0.3f;

    private NavMeshAgent agent;
    private LayerMovementManager movementManager;
    private List<Vector3> currentPath = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private bool isStoppedForNPC = false;
    private bool isAtIntersection = false;
    private RoadSegmentData currentRoadSegment;
    private float lastPathUpdate = 0f;

    private bool isApproachingIntersection = false;
    private float intersectionSlowDistance = 10f; // Distance to start slowing down
    private float intersectionSpeed = 6f; // Speed when near intersection

    // Road direction vectors for each direction type
    private static readonly Vector3[] DirectionVectors = {
        Vector3.right,     // 0: East-bound
        Vector3.left,      // 1: West-bound  
        Vector3.forward,   // 2: North-bound
        Vector3.back       // 3: South-bound
    };

    void Start()
    {
        InitializeVehicle();
        StartMovement();
    }

    void Update()
    {
        if (agent == null || movementManager == null) return;

        CheckForNPCsOnCrosswalks();
        UpdateMovement();

        if (Time.time - lastPathUpdate > pathUpdateInterval)
        {
            UpdatePathIfNeeded();
            lastPathUpdate = Time.time;
        }
    }

    private void InitializeVehicle()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

        movementManager = FindObjectOfType<LayerMovementManager>();
        if (movementManager == null)
        {
            return;
        }

        agent.acceleration = 8f; // Slower acceleration for more realistic movement
        agent.angularSpeed = 60f; // Degrees per second for turning
        agent.radius = 3.5f;
        agent.stoppingDistance = stopDistance;
        agent.autoBraking = true;
        agent.autoRepath = true;

        try
        {
            agent.agentTypeID = NavMesh.GetSettingsByIndex(1).agentTypeID; // Vehicle NavMesh
        }
        catch
        {
            agent.agentTypeID = 0; // Fallback to default
        }
    }

    private void StartMovement()
    {
        currentRoadSegment = GetCurrentRoadSegment();
        if (currentRoadSegment != null)
        {
            GenerateDirectionalPath();
        }
    }

    private RoadSegmentData GetCurrentRoadSegment()
    {
        RoadDataManager manager = RoadDataManager.Instance;
        if (manager == null) return null;

        return manager.FindRoadAtPosition(transform.position);
    }

    private void GenerateDirectionalPath()
    {
        if (currentRoadSegment == null) return;

        Vector3 currentPos = transform.position;

        List<Vector3> waypoints = new List<Vector3>();

        Bounds roadBounds = currentRoadSegment.GetBounds();

        if (currentRoadSegment.horizontal)
        {
            float direction = currentRoadSegment.direction == 0 ? 1f : -1f; // East vs West
            float targetX = direction > 0 ? roadBounds.max.x : roadBounds.min.x;

            waypoints.Add(new Vector3(targetX, currentPos.y, currentPos.z));
        }
        else
        {
            float direction = currentRoadSegment.direction == 2 ? 1f : -1f; // North vs South
            float targetZ = direction > 0 ? roadBounds.max.z : roadBounds.min.z;

            waypoints.Add(new Vector3(currentPos.x, currentPos.y, targetZ));
        }

        Vector3 intersectionPoint = FindNearestIntersection(waypoints[waypoints.Count - 1]);
        if (intersectionPoint != Vector3.zero)
        {
            waypoints.Add(intersectionPoint);

            RoadSegmentData nextRoad = ChooseNextRoadAtIntersection(intersectionPoint);
            if (nextRoad != null)
            {
                ExtendPathToNextRoad(waypoints, nextRoad);
            }
        }

        currentPath = waypoints;
        currentWaypointIndex = 0;

        if (currentPath.Count > 0)
        {
            MoveToNextWaypoint();
        }
    }

    private Vector3 FindNearestIntersection(Vector3 fromPosition)
    {
        RoadDataManager manager = RoadDataManager.Instance;
        if (manager != null)
        {
            var roadData = manager.GetRoadData();
            if (roadData != null)
            {
                var intersections = roadData.GetIntersections();

                foreach (var intersection in intersections)
                {
                    float distance = Vector3.Distance(fromPosition, intersection.position);
                    if (distance < intersectionApproachDistance * 2f)
                    {
                        return intersection.position;
                    }
                }
            }
        }

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Intersection"))
            {
                float distance = Vector3.Distance(fromPosition, obj.transform.position);

                if (distance < intersectionApproachDistance * 2f)
                {
                    return obj.transform.position;
                }
            }
        }
        return Vector3.zero;
    }

    private RoadSegmentData ChooseNextRoadAtIntersection(Vector3 intersectionPos)
    {
        RoadDataManager manager = RoadDataManager.Instance;
        if (manager == null) return null;

        RoadSegmentData intersectionData = manager.FindIntersectionAtPosition(intersectionPos);
        if (intersectionData == null || !intersectionData.IsIntersection())
        {
            return ChooseRoadRandomly(intersectionPos);
        }

        List<int> allowedDirections = GetAllowedDirections(intersectionData);

        if (allowedDirections.Count == 0)
        {
            return ChooseRoadRandomly(intersectionPos);
        }

        List<RoadSegmentData> validRoads = GetRoadsAtIntersectionByDirection(intersectionPos, allowedDirections);

        if (validRoads.Count == 0)
        {
            return ChooseRoadRandomly(intersectionPos);
        }

        int randomIndex = Random.Range(0, validRoads.Count);
        var chosenRoad = validRoads[randomIndex];

        return chosenRoad;
    }

    private List<RoadSegmentData> GetRoadsAtIntersectionByDirection(Vector3 intersectionPos, List<int> allowedDirections)
    {
        List<RoadSegmentData> validRoads = new List<RoadSegmentData>();
        RoadDataManager manager = RoadDataManager.Instance;

        if (manager?.GetRoadData()?.roadSegments == null)
            return validRoads;

        foreach (int direction in allowedDirections)
        {
            RoadSegmentData roadInDirection = FindRoadInDirectionFromIntersection(intersectionPos, direction);

            if (roadInDirection != null && roadInDirection.roadName != currentRoadSegment?.roadName)
            {
                validRoads.Add(roadInDirection);
            }
        }

        return validRoads;
    }

    private RoadSegmentData FindRoadInDirectionFromIntersection(Vector3 intersectionPos, int targetDirection)
    {
        RoadDataManager manager = RoadDataManager.Instance;
        var allRoads = manager.GetRoadData().roadSegments;

        float tolerance = 5f; 
        float closestDistance = float.MaxValue;
        RoadSegmentData closestRoad = null;

        foreach (var road in allRoads)
        {
            if (road.IsIntersection() || road.direction != targetDirection)
                continue;

            bool isCorrectPosition = false;

            switch (targetDirection)
            {
                case 0: // East - road should be to the right and roughly same Z
                    isCorrectPosition = road.position.x > intersectionPos.x &&
                                      Mathf.Abs(road.position.z - intersectionPos.z) < tolerance;
                    break;

                case 1: // West - road should be to the left and roughly same Z
                    isCorrectPosition = road.position.x < intersectionPos.x &&
                                      Mathf.Abs(road.position.z - intersectionPos.z) < tolerance;
                    break;

                case 2: // North - road should be forward and roughly same X
                    isCorrectPosition = road.position.z > intersectionPos.z &&
                                      Mathf.Abs(road.position.x - intersectionPos.x) < tolerance;
                    break;

                case 3: // South - road should be backward and roughly same X
                    isCorrectPosition = road.position.z < intersectionPos.z &&
                                      Mathf.Abs(road.position.x - intersectionPos.x) < tolerance;
                    break;
            }

            if (isCorrectPosition)
            {
                float distance = Vector3.Distance(intersectionPos, road.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoad = road;
                }
            }
        }
        return closestRoad;
    }

    private List<int> GetAllowedDirections(RoadSegmentData intersectionData)
    {
        List<int> allowedDirections = new List<int>();

        if (currentRoadSegment == null)
        {
            return allowedDirections;
        }

        if (currentRoadSegment.horizontal)
        {
            int allowedTurn = intersectionData.fromHorizontal;

            if (allowedTurn == 0) // Right turn allowed
            {
                if (currentRoadSegment.direction == 0) // Coming from East-bound road
                {
                    allowedDirections.Add(3); // Turn right = South
                }
                else if (currentRoadSegment.direction == 1) // Coming from West-bound road  
                {
                    allowedDirections.Add(2); // Turn right = North
                }
            }
            else if (allowedTurn == 1) // Left turn allowed
            {
                if (currentRoadSegment.direction == 0) // Coming from East-bound road
                {
                    allowedDirections.Add(2); // Turn left = North
                }
                else if (currentRoadSegment.direction == 1) // Coming from West-bound road
                {
                    allowedDirections.Add(3); // Turn left = South
                }
            }

            if (intersectionData.canGoStraightH)
            {
                allowedDirections.Add(currentRoadSegment.direction); // Continue same direction
            }
        }
        else if (currentRoadSegment.vertical)
        {
            int allowedTurn = intersectionData.fromVertical;

            if (allowedTurn == 0) // Right turn allowed
            {
                if (currentRoadSegment.direction == 2) // Coming from North-bound road
                {
                    allowedDirections.Add(0); // Turn right = East
                }
                else if (currentRoadSegment.direction == 3) // Coming from South-bound road
                {
                    allowedDirections.Add(1); // Turn right = West
                }
            }
            else if (allowedTurn == 1) // Left turn allowed
            {
                if (currentRoadSegment.direction == 2) // Coming from North-bound road
                {
                    allowedDirections.Add(1); // Turn left = West
                }
                else if (currentRoadSegment.direction == 3) // Coming from South-bound road
                {
                    allowedDirections.Add(0); // Turn left = East
                }
            }

            if (intersectionData.canGoStraightV)
            {
                allowedDirections.Add(currentRoadSegment.direction); // Continue same direction
            }
        }

        return allowedDirections;
    }

    private RoadSegmentData ChooseRoadRandomly(Vector3 intersectionPos)
    {
        RoadDataManager manager = RoadDataManager.Instance;
        var connectedRoads = manager.GetConnectedRoads(intersectionPos, 65f);
        var validRoads = connectedRoads.Where(road =>
            road.roadName != currentRoadSegment?.roadName &&
            road.direction >= 0 && road.direction <= 3
        ).ToList();

        if (validRoads.Count == 0) return null;

        for (int attempt = 0; attempt < 25; attempt++)
        {
            int randomIndex = Random.Range(0, validRoads.Count);
            var candidateRoad = validRoads[randomIndex];

            if (IsRoadChoiceReasonable(candidateRoad, intersectionPos))
            {
                return candidateRoad;
            }
        }

        return validRoads[0];
    }

    private bool IsRoadChoiceReasonable(RoadSegmentData candidateRoad, Vector3 intersectionPos)
    {
        if (currentRoadSegment == null) return true;

        Vector3 vehiclePos = transform.position;
        Vector3 vehicleDirection = transform.forward;

        Vector3 directionToRoad = (candidateRoad.position - intersectionPos).normalized;

        float backwardDot = Vector3.Dot(vehicleDirection, directionToRoad);
        if (backwardDot < -0.7f)
        {
            return false;
        }

        bool directionConsistent = IsRoadDirectionConsistent(candidateRoad, intersectionPos);
        if (!directionConsistent)
        {
            return false;
        }

        if (currentRoadSegment.direction == candidateRoad.direction)
        {
            float distance = Vector3.Distance(vehiclePos, candidateRoad.position);
            if (distance < 30f)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsRoadDirectionConsistent(RoadSegmentData road, Vector3 intersectionPos)
    {
        Vector3 directionToRoad = (road.position - intersectionPos).normalized;

        switch (road.direction)
        {
            case 0: // East-bound - should be generally eastward
                return directionToRoad.x > -0.8f;

            case 1: // West-bound - should be generally westward
                return directionToRoad.x < 0.8f; 

            case 2: // North-bound - should be generally northward  
                return directionToRoad.z > -0.8f; 

            case 3: // South-bound - should be generally southward
                return directionToRoad.z < 0.8f; 

            default:
                return true;
        }
    }

    private void ExtendPathToNextRoad(List<Vector3> waypoints, RoadSegmentData nextRoad)
    {
        Vector3 intersectionCenter = waypoints[waypoints.Count - 1];
        waypoints.RemoveAt(waypoints.Count - 1);
        Vector3 vehicleCurrentPos = transform.position;
        Vector3 newRoadTarget = CalculateNewRoadEntryPoint(nextRoad, intersectionCenter);

        List<Vector3> curveWaypoints = CreateEdgyCurveViaIntersection(
            vehicleCurrentPos,
            intersectionCenter,
            newRoadTarget
        );

        waypoints.Clear();
        waypoints.AddRange(curveWaypoints);

        Vector3 newRoadDestination = CalculateRoadDestination(nextRoad);
        waypoints.Add(newRoadDestination);

        currentRoadSegment = nextRoad;

        currentWaypointIndex = 0;
    }

    private Vector3 CalculateNewRoadEntryPoint(RoadSegmentData road, Vector3 intersectionCenter)
    {
        Bounds roadBounds = road.GetBounds();
        Vector3 entryPoint = roadBounds.center;

        if (road.horizontal)
        {
            if (road.direction == 0) // East-bound
            {
                entryPoint.x = roadBounds.min.x + roadBounds.size.x * 0.3f; 
            }
            else // West-bound
            {
                entryPoint.x = roadBounds.max.x - roadBounds.size.x * 0.3f; 
            }
            entryPoint.z = roadBounds.center.z;
        }
        else 
        {
            if (road.direction == 2) // North-bound
            {
                entryPoint.z = roadBounds.min.z + roadBounds.size.z * 0.3f; 
            }
            else // South-bound
            {
                entryPoint.z = roadBounds.max.z - roadBounds.size.z * 0.3f; 
            }
            entryPoint.x = roadBounds.center.x;
        }

        entryPoint.y = intersectionCenter.y; 
        return entryPoint;
    }

    private List<Vector3> CreateEdgyCurveViaIntersection(Vector3 start, Vector3 intersectionCenter, Vector3 end)
    {
        List<Vector3> curvePoints = new List<Vector3>();
        int numberOfPoints = 6; 

        for (int i = 1; i <= numberOfPoints; i++)
        {
            float t = (float)i / (numberOfPoints + 1);
            float edgeFactor = CalculateEdgeFactor(t);
            Vector3 curvePoint = CalculateEdgyBezierPoint(t, start, intersectionCenter, end, edgeFactor);
            curvePoints.Add(curvePoint);
        }
        return curvePoints;
    }

    private float CalculateEdgeFactor(float t)
    {

        float distanceFromMiddle = Mathf.Abs(t - 0.5f);
        float edgeFactor = 1.0f + (1.0f - distanceFromMiddle * 1.0f) * 2.0f;

        return edgeFactor;
    }

    private Vector3 CalculateEdgyBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, float edgeFactor)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;

        // Standard Bezier
        Vector3 point = uu * p0 + 2 * u * t * p1 + tt * p2;

        // Pull middle points CLOSER to intersection (more "edgy")
        if (t > 0.2f && t < 0.8f) // Only affect middle points
        {
            Vector3 toIntersection = (p1 - point).normalized;
            float pullDistance = Vector3.Distance(point, p1) * (edgeFactor - 1.0f) * 0.4f; 
            point += toIntersection * pullDistance; 
        }

        return point;
    }

    private Vector3 CalculateRoadDestination(RoadSegmentData road)
    {
        Bounds roadBounds = road.GetBounds();
        Vector3 destination = roadBounds.center;

        if (road.horizontal)
        {
            if (road.direction == 0) // East-bound
            {
                destination.x = roadBounds.center.x + roadBounds.size.x * 0.3f; 
            }
            else // West-bound
            {
                destination.x = roadBounds.center.x - roadBounds.size.x * 0.3f; 
            }
        }
        else 
        {
            if (road.direction == 2) // North-bound
            {
                destination.z = roadBounds.center.z + roadBounds.size.z * 0.3f; 
            }
            else // South-bound
            {
                destination.z = roadBounds.center.z - roadBounds.size.z * 0.3f; 
            }
        }
        return destination;
    }

    private void CheckForNPCsOnCrosswalks()
    {
        Vector3 checkPosition = transform.position + transform.forward * npcDetectionRange;
        Collider[] colliders = Physics.OverlapSphere(checkPosition, npcDetectionRange / 2f);

        bool npcOnCrosswalk = false;

        foreach (Collider collider in colliders)
        {
            if (collider.name.Contains("NPC"))
            {
                RaycastHit hit;
                if (Physics.Raycast(collider.transform.position, Vector3.down, out hit, 2f))
                {
                    if (hit.collider.gameObject.layer == movementManager.crosswalkLayer)
                    {
                        npcOnCrosswalk = true;
                        break;
                    }
                }
            }
        }
        isStoppedForNPC = npcOnCrosswalk;
    }

    private void UpdateMovement()
    {
        if (currentPath.Count == 0) return;

        CheckIntersectionApproach();

        if (isStoppedForNPC)
        {
            agent.speed = 0f;
            agent.isStopped = true;
            return;
        }
        else
        {
            agent.isStopped = false;
            agent.speed = isApproachingIntersection ? intersectionSpeed : normalSpeed;
        }

        if (currentWaypointIndex < currentPath.Count)
        {
            Vector3 currentTarget = currentPath[currentWaypointIndex];
            float distanceToWaypoint = Vector3.Distance(transform.position, currentTarget);

            // Check for U-turn situation (waypoint is behind us)
            Vector3 directionToWaypoint = (currentTarget - transform.position).normalized;
            float forwardDot = Vector3.Dot(transform.forward, directionToWaypoint);

            // If waypoint is significantly behind us, skip it
            if (forwardDot < -0.3f && distanceToWaypoint > 3f)
            {
                Debug.LogWarning($"Waypoint {currentWaypointIndex} is behind vehicle, skipping!");
                currentWaypointIndex++;
                if (currentWaypointIndex < currentPath.Count)
                {
                    MoveToNextWaypoint();
                }
                return;
            }

            if (distanceToWaypoint < waypointReachDistance)
            {
                currentWaypointIndex++;

                if (currentWaypointIndex < currentPath.Count)
                {
                    MoveToNextWaypoint();
                }
                else
                {
                    GenerateDirectionalPath();
                }
            }
            else if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning("Invalid path, regenerating...");
                GenerateDirectionalPath();
            }
        }
    }

    private void CheckIntersectionApproach()
    {
        if (currentPath.Count == 0) return;

        isApproachingIntersection = false;

        RoadDataManager manager = RoadDataManager.Instance;
        if (manager != null)
        {
            var roadData = manager.GetRoadData();
            if (roadData != null)
            {
                var intersections = roadData.GetIntersections();
                foreach (var intersection in intersections)
                {
                    float distanceToIntersection = Vector3.Distance(transform.position, intersection.position);
                    if (distanceToIntersection < intersectionSlowDistance)
                    {
                        isApproachingIntersection = true;
                        return;
                    }
                }
            }
        }

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Intersection"))
            {
                float distanceToIntersection = Vector3.Distance(transform.position, obj.transform.position);
                if (distanceToIntersection < intersectionSlowDistance)
                {
                    isApproachingIntersection = true;
                    return;
                }
            }
        }
    }

    private void MoveToNextWaypoint()
    {
        if (currentWaypointIndex >= currentPath.Count) return;

        Vector3 targetWaypoint = currentPath[currentWaypointIndex];

        // Ensure waypoint is ahead of vehicle (not behind)
        Vector3 directionToTarget = (targetWaypoint - transform.position).normalized;
        float forwardAlignment = Vector3.Dot(transform.forward, directionToTarget);

        if (forwardAlignment < -0.5f) // Waypoint is significantly behind
        {
            Debug.LogWarning($"Skipping waypoint {currentWaypointIndex} - it's behind the vehicle");
            currentWaypointIndex++;
            if (currentWaypointIndex < currentPath.Count)
            {
                MoveToNextWaypoint();
            }
            return;
        }

        // Make sure the waypoint is on NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetWaypoint, out hit, 3f, NavMesh.AllAreas))
        {
            targetWaypoint = hit.position;
        }

        bool destinationSet = agent.SetDestination(targetWaypoint);

        if (!destinationSet)
        {
            Debug.LogWarning($"Failed to set destination, skipping waypoint {currentWaypointIndex}");
            currentWaypointIndex++;
            if (currentWaypointIndex < currentPath.Count)
            {
                MoveToNextWaypoint();
            }
        }
    }

    private void UpdatePathIfNeeded()
    {
        if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
        {
            GenerateDirectionalPath();
        }
    }
}