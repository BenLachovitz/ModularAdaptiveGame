using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCMovement : MonoBehaviour
{
    private List<Vector3> personalPoints = new List<Vector3>();
    private int currentTargetIndex = 0;
    private float speed;
    private Animator animator;
    private bool isMoving = true;
    private bool isTalking = false;
    private static int maxSimultaneousGatherers = 3;
    private static int currentGatherers = 0;
    private float nextGatherTime = 0f;
    private float stuckCheckCooldown = 2f;
    private float lastStuckCheckTime = 0f;
    private float idleTimeout = 10f;
    private float lastMoveTime = 0f;

    private NavMeshAgent agent;
    private NavMeshObstacle obstacle;

    public void Initialize(List<Vector3> availablePoints, float moveSpeed)
    {
        speed = moveSpeed;

        if (agent != null)
        {
            agent.acceleration = 0.9f;
            agent.angularSpeed = 180f;
            agent.speed = speed;
            agent.avoidancePriority = Random.Range(30, 60);
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.updateRotation = false;
        }

        if (availablePoints.Count >= 6)
        {
            HashSet<int> selectedIndices = new HashSet<int>();
            while (selectedIndices.Count < 6)
            {
                int randomIndex = Random.Range(0, availablePoints.Count);
                if (!selectedIndices.Contains(randomIndex))
                {
                    selectedIndices.Add(randomIndex);
                    personalPoints.Add(availablePoints[randomIndex]);
                }
            }
        }

        MoveToNextPoint();
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        obstacle = GetComponent<NavMeshObstacle>();
    }

    void Update()
    {
        if (personalPoints.Count == 0 || !isMoving || isTalking) return;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"{name} is off the NavMesh! Resetting...");

            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 1f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
                MoveToNextPoint();
            }
            return;
        }

        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        if (!agent.pathPending && agent.remainingDistance < 0.1f)
        {
            currentTargetIndex = (currentTargetIndex + 1) % personalPoints.Count;
            MoveToNextPoint();
        }

        if (isMoving && agent.velocity.magnitude < 0.05f && !agent.pathPending && !agent.isStopped)
        {
            if (Time.time - lastStuckCheckTime > stuckCheckCooldown)
            {
                lastStuckCheckTime = Time.time;

                Vector3 sidestep = transform.right * (Random.value > 0.5f ? 1 : -1);
                Vector3 newPosition = transform.position + sidestep * 0.5f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(newPosition, out hit, 0.5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }

                MoveToNextPoint(); // Failsafe to resume pathing
            }
        }

        if (Time.time - lastMoveTime > idleTimeout)
        {
            Debug.Log($"{name} was idle too long — reissuing destination");
            MoveToNextPoint();
        }

        if (Time.time >= nextGatherTime && currentGatherers < maxSimultaneousGatherers && !isTalking)
        {
            if (Random.value < 0.01f)
            {
                StartCoroutine(DoGathering());
            }
        }
    }

    private void MoveToNextPoint()
    {
        if (personalPoints.Count == 0 || agent == null) return;

        agent.SetDestination(personalPoints[currentTargetIndex]);
        animator.SetBool("Walking", true);
        lastMoveTime = Time.time;
    }

    private IEnumerator<System.Object> DoGathering()
    {
        agent.isStopped = true;
        agent.ResetPath();
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.velocity = Vector3.zero;

        currentGatherers++;
        animator.Play("Gathering");

        yield return new WaitForSeconds(4.85f);

        agent.updatePosition = true;
        agent.updateRotation = true;

        isMoving = true;
        agent.isStopped = false;
        animator.SetBool("Walking", true);

        MoveToNextPoint();

        currentGatherers--;
        nextGatherTime = Time.time + Random.Range(5f, 15f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isTalking || other == null || !other.CompareTag("NPC")) return;
        if (Random.value > 0.3f) return;

        NPCMovement otherNPC = other.GetComponent<NPCMovement>();
        if (otherNPC != null && !otherNPC.isTalking)
        {
            if (otherNPC.animator == null)
                otherNPC.animator = other.GetComponent<Animator>();

            isTalking = true;
            otherNPC.isTalking = true;

            isMoving = false;
            otherNPC.isMoving = false;

            if (agent != null) agent.isStopped = true;
            if (otherNPC.agent != null) otherNPC.agent.isStopped = true;

            animator.SetBool("Talking1", true);
            animator.SetBool("Walking", false);
            otherNPC.animator.SetBool("Talking1", true);
            otherNPC.animator.SetBool("Walking", false);

            StartCoroutine(EndTalking(otherNPC));
        }
    }

    private IEnumerator<System.Object> EndTalking(NPCMovement otherNPC)
    {
        float endTime = Time.time + 3.5f;

        while (Time.time < endTime)
        {
            if (this != null && otherNPC != null)
            {
                Vector3 dirToOther = (otherNPC.transform.position - transform.position).normalized;
                Vector3 dirToSelf = (transform.position - otherNPC.transform.position).normalized;

                if (dirToOther != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(dirToOther);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 180 * Time.deltaTime);
                }

                if (dirToSelf != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(dirToSelf);
                    otherNPC.transform.rotation = Quaternion.RotateTowards(otherNPC.transform.rotation, targetRotation, 180 * Time.deltaTime);
                }
            }

            yield return null;
        }

        animator.SetBool("Talking1", false);
        otherNPC.animator.SetBool("Talking1", false);

        isTalking = false;
        otherNPC.isTalking = false;

        isMoving = true;
        otherNPC.isMoving = true;

        animator.SetBool("Walking", true);
        otherNPC.animator.SetBool("Walking", true);

        if (agent != null) agent.isStopped = false;
        if (otherNPC.agent != null) otherNPC.agent.isStopped = false;

        MoveToNextPoint();
        otherNPC.MoveToNextPoint();
    }
}