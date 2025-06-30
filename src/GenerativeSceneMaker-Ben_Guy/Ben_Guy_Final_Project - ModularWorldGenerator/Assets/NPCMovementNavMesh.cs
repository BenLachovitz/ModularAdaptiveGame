using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPCMovementNavMesh : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 4f;
    public float arrivalThreshold = 1f;
    public float maxDestinationDistance = 100f;
    public int maxPathfindingAttempts = 10;
    public float stuckThreshold = 0.01f;
    public float stuckTimer = 3f;
    public float destinationReachCheckInterval = 0.5f;

    [Header("Collision Avoidance")]
    [SerializeField] private float avoidanceRadius = 2f; 
    [SerializeField] private float avoidanceForce = 5f; 
    [SerializeField] private float separationDistance = 1.5f; 
    [SerializeField] private LayerMask npcLayerMask = -1; 
    [SerializeField] private bool enableAvoidance = true;
    [SerializeField] private float avoidanceUpdateInterval = 0.1f; 

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool showNavMeshPath = true;
    public bool showAvoidanceGizmos = true;

    private Vector3 destination;
    private LayerMovementManager layerManager;
    private NavMeshAgent navAgent;
    private bool isMoving = true;

    // Stuck detection
    private Vector3 lastPosition;
    private float timeSinceMovement;
    private float lastDestinationCheck;

    // Collision avoidance
    private float lastAvoidanceUpdate;
    private List<Transform> nearbyNPCs = new List<Transform>();
    private Collider npcCollider;

    void Start()
    {
        layerManager = LayerMovementManager.Instance;
        if (layerManager == null)
        {
            Debug.LogError($"LayerMovementManager not found! NPC {name} movement disabled.");
            enabled = false;
            return;
        }

        SetupNPCCollider();
        SetupNavMeshAgent();
        lastPosition = transform.position;
        ChooseNewDestination();
    }

    void SetupNPCCollider()
    {
        npcCollider = GetComponent<Collider>();
        if (npcCollider == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0, 0.9f, 0);

            capsule.isTrigger = true;
            npcCollider = capsule;
        }
        else
        {
            npcCollider.isTrigger = true;
        }

        if (gameObject.layer == 0) // Default layer
        {
            gameObject.layer = LayerMask.NameToLayer("NPC") != -1 ?
                LayerMask.NameToLayer("NPC") : gameObject.layer;
        }
    }

    void SetupNavMeshAgent()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        navAgent.speed = speed;
        navAgent.stoppingDistance = arrivalThreshold;
        navAgent.autoBraking = true;
        navAgent.autoRepath = true;
        navAgent.angularSpeed = 120f;
        navAgent.acceleration = 8f; 
        navAgent.radius = 0.5f;
        navAgent.height = 2.0f;

        // Configure built-in NavMesh avoidance
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        navAgent.avoidancePriority = Random.Range(10, 90); // Random priority to avoid deadlocks

        if (!navAgent.isOnNavMesh)
        {
            Vector3 nearestNavMeshPos = layerManager.GetNearestNavMeshPosition(transform.position, 5f);
            if (nearestNavMeshPos != transform.position)
            {
                transform.position = nearestNavMeshPos;
            }
            else
            {
                Debug.LogWarning($"NPC {name} could not be placed on NavMesh!");
            }
        }
    }

    void Update()
    {
        if (!isMoving || navAgent == null || !navAgent.isOnNavMesh) return;

        if (enableAvoidance && Time.time - lastAvoidanceUpdate >= avoidanceUpdateInterval)
        {
            UpdateCollisionAvoidance();
            lastAvoidanceUpdate = Time.time;
        }

        if (Time.time - lastDestinationCheck >= destinationReachCheckInterval)
        {
            lastDestinationCheck = Time.time;
            CheckDestinationStatus();
        }

        DetectIfStuck();

        if (navAgent.speed != speed)
        {
            navAgent.speed = speed;
        }
    }

    void UpdateCollisionAvoidance()
    {
        if (!enableAvoidance) return;

        Collider[] nearbyColliders = Physics.OverlapSphere(
            transform.position,
            avoidanceRadius,
            npcLayerMask
        );

        nearbyNPCs.Clear();

        foreach (Collider col in nearbyColliders)
        {
            if (col.gameObject != gameObject && col.GetComponent<NPCMovementNavMesh>() != null)
            {
                nearbyNPCs.Add(col.transform);
            }
        }

        Vector3 separationForce = CalculateSeparationForce();

        if (separationForce.magnitude > 0.1f)
        {
            ApplyAvoidanceToNavAgent(separationForce);
        }
    }

    Vector3 CalculateSeparationForce()
    {
        Vector3 separationForce = Vector3.zero;
        int neighborCount = 0;

        foreach (Transform neighbor in nearbyNPCs)
        {
            float distance = Vector3.Distance(transform.position, neighbor.position);

            if (distance < separationDistance && distance > 0.1f)
            {
                Vector3 direction = (transform.position - neighbor.position).normalized;

                // Stronger force when closer
                float force = (separationDistance - distance) / separationDistance;
                separationForce += direction * force;
                neighborCount++;
            }
        }

        if (neighborCount > 0)
        {
            separationForce = separationForce.normalized * avoidanceForce;
        }

        return separationForce;
    }

    void ApplyAvoidanceToNavAgent(Vector3 avoidanceForce)
    {
        Vector3 currentDestination = navAgent.destination;
        Vector3 currentPosition = transform.position;

        Vector3 avoidanceDestination = currentPosition + avoidanceForce.normalized * 2f;

        Vector3 validAvoidancePos = layerManager.GetNearestNavMeshPosition(avoidanceDestination, 2f);

        if (layerManager.IsValidNPCPosition(validAvoidancePos, 0.1f))
        {
            navAgent.SetDestination(validAvoidancePos);

            CancelInvoke(nameof(ReturnToOriginalDestination));
            Invoke(nameof(ReturnToOriginalDestination), 1f);
        }
    }

    void ReturnToOriginalDestination()
    {
        if (destination != Vector3.zero && navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(destination);
        }
    }

    // Trigger-based collision detection (complementary to sphere detection)
    void OnTriggerEnter(Collider other)
    {
        // Check if it's another NPC (has NPCMovementNavMesh component) and avoidance is enabled
        if (other.GetComponent<NPCMovementNavMesh>() != null && enableAvoidance)
        {
            // Handle immediate collision response
            Vector3 pushDirection = (transform.position - other.transform.position).normalized;
            Vector3 pushPosition = transform.position + pushDirection * 0.5f;

            Vector3 validPushPos = layerManager.GetNearestNavMeshPosition(pushPosition, 1f);
            if (layerManager.IsValidNPCPosition(validPushPos, 0.1f))
            {
                // Small immediate adjustment
                transform.position = Vector3.Lerp(transform.position, validPushPos, 0.1f);
            }
        }
    }

    void CheckDestinationStatus()
    {
        if (!navAgent.hasPath && !navAgent.pathPending)
        {
            ChooseNewDestination();
        }
        else if (navAgent.hasPath && navAgent.remainingDistance < arrivalThreshold)
        {
            ChooseNewDestination();
        }
    }

    void DetectIfStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        if (distanceMoved < stuckThreshold)
        {
            timeSinceMovement += Time.deltaTime;

            if (timeSinceMovement >= stuckTimer)
            {
                ChooseNewDestination();
                timeSinceMovement = 0f;
            }
        }
        else
        {
            timeSinceMovement = 0f;
        }

        lastPosition = transform.position;
    }

    void ChooseNewDestination()
    {
        if (!navAgent.isOnNavMesh)
            return;
        

        Vector3 currentPosition = transform.position;
        bool foundValidDestination = false;

        for (int attempt = 0; attempt < maxPathfindingAttempts; attempt++)
        {
            Vector3 randomPoint = currentPosition + Random.insideUnitSphere * maxDestinationDistance;
            randomPoint.y = currentPosition.y; 

            RaycastHit hit;
            if (Physics.Raycast(randomPoint + Vector3.up * 5f, Vector3.down, out hit, 10f, layerManager.npcMovementLayers))
            {
                randomPoint.y = hit.point.y;
            }
            else
            {
                continue; // No valid ground found
            }

            if (!layerManager.IsValidNPCPosition(randomPoint, 0.1f))
            {
                continue;
            }

            Vector3 navMeshPoint = layerManager.GetNearestNavMeshPosition(randomPoint, 5f);
            if (Vector3.Distance(navMeshPoint, randomPoint) > 5f)
            {
                continue;
            }

            NavMeshPath testPath = new NavMeshPath();
            if (navAgent.CalculatePath(navMeshPoint, testPath))
            {
                if (testPath.status == NavMeshPathStatus.PathComplete)
                {
                    if (ValidateNavMeshPathWithCrosswalks(testPath))
                    {
                        destination = navMeshPoint;
                        navAgent.SetDestination(destination);
                        foundValidDestination = true;
                        break;
                    }
                }
            }
        }

        if (!foundValidDestination)
        {
            Debug.LogWarning($"NPC {name}: Could not find valid NavMesh path after {maxPathfindingAttempts} attempts. Pausing movement for {stuckTimer} seconds.");
            isMoving = false;
            navAgent.ResetPath();

            Invoke(nameof(ResumeMovementSearch), stuckTimer);
        }
        else
        {
            isMoving = true;
            timeSinceMovement = 0f; 
        }
    }

    void ResumeMovementSearch()
    {
        isMoving = true;
        ChooseNewDestination();
    }

    bool ValidateNavMeshPathWithCrosswalks(NavMeshPath path)
    {
        if (path.corners.Length < 2) return true; 

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 segmentStart = path.corners[i];
            Vector3 segmentEnd = path.corners[i + 1];

            int segmentChecks = Mathf.CeilToInt(Vector3.Distance(segmentStart, segmentEnd) / 5f); 
            segmentChecks = Mathf.Clamp(segmentChecks, 1, 5); // min 1 check, max 5 per segment

            for (int j = 0; j <= segmentChecks; j++)
            {
                float t = (float)j / segmentChecks;
                Vector3 checkPoint = Vector3.Lerp(segmentStart, segmentEnd, t);

                if (!IsValidNPCPositionWithCrosswalks(checkPoint))
                {
                    return false;
                }
            }
        }

        return true;
    }

    bool IsValidNPCPositionWithCrosswalks(Vector3 position)
    {
        RaycastHit hit;
        Vector3 rayOrigin = position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 2f))
        {
            GameObject surfaceObject = hit.collider.gameObject;
            int surfaceLayer = surfaceObject.layer;

            bool isOnValidLayer = (layerManager.npcMovementLayers & (1 << surfaceLayer)) != 0;

            return isOnValidLayer;
        }

        return false;
    }

    public void SetMoving(bool moving)
    {
        isMoving = moving;

        if (navAgent != null)
        {
            if (moving)
            {
                // Resume movement if we have a destination
                if (destination != Vector3.zero && navAgent.isOnNavMesh)
                {
                    navAgent.SetDestination(destination);
                }
                else
                {
                    ChooseNewDestination();
                }
            }
            else
            {
                // Stop NavMesh agent
                navAgent.ResetPath();
            }
        }
    }

    // Method to manually set a destination (useful for scripted movement)
    public bool SetDestination(Vector3 newDestination)
    {
        if (navAgent == null || !navAgent.isOnNavMesh) return false;

        // Validate the destination first
        if (!layerManager.IsValidNPCPosition(newDestination, 0.1f))
        {
            Debug.LogWarning($"Cannot set destination for {name}: position {newDestination} is not valid for NPCs");
            return false;
        }

        // Find nearest NavMesh point
        Vector3 navMeshPoint = layerManager.GetNearestNavMeshPosition(newDestination, 5f);

        // Test if path is possible
        NavMeshPath testPath = new NavMeshPath();
        if (navAgent.CalculatePath(navMeshPoint, testPath) && testPath.status == NavMeshPathStatus.PathComplete)
        {
            destination = navMeshPoint;
            navAgent.SetDestination(destination);
            isMoving = true;
            return true;
        }
        else
        {
            Debug.LogWarning($"Cannot reach destination {newDestination} from {transform.position}");
            return false;
        }
    }

    // Public methods to control avoidance behavior
    public void SetAvoidanceEnabled(bool enabled)
    {
        enableAvoidance = enabled;
    }

    public void SetAvoidanceParameters(float radius, float force, float separation)
    {
        avoidanceRadius = radius;
        avoidanceForce = force;
        separationDistance = separation;
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Draw current destination
        if (isMoving && destination != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(destination, 0.5f);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, destination);
        }

        // Draw NavMesh path
        if (showNavMeshPath && navAgent != null && navAgent.hasPath)
        {
            Vector3[] pathCorners = navAgent.path.corners;

            for (int i = 0; i < pathCorners.Length - 1; i++)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pathCorners[i], pathCorners[i + 1]);

                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(pathCorners[i], 0.2f);
            }

            if (pathCorners.Length > 0)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(pathCorners[pathCorners.Length - 1], 0.2f);
            }
        }

        // Draw collision avoidance gizmos
        if (showAvoidanceGizmos && enableAvoidance)
        {
            // Draw avoidance radius
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, avoidanceRadius);

            // Draw separation distance
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, separationDistance);

            // Draw connections to nearby NPCs
            foreach (Transform nearbyNPC in nearbyNPCs)
            {
                if (nearbyNPC != null)
                {
                    float distance = Vector3.Distance(transform.position, nearbyNPC.position);

                    if (distance < separationDistance)
                    {
                        Gizmos.color = Color.red;
                    }
                    else
                    {
                        Gizmos.color = Color.yellow;
                    }

                    Gizmos.DrawLine(transform.position, nearbyNPC.position);
                }
            }
        }

        // Draw agent radius
        if (navAgent != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, navAgent.radius);
        }
    }
}