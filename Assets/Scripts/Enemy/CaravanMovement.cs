using System.Collections;
using System;
using UnityEngine;
using UnityEngine.AI;

// Ticari kervan hareketi — dogu/bati ticaret yolundan kaleye dogru ilerler.
// CaravanData.moveSpeed kullanir. Kaleye varinca OnReachedCastle, teslim sonrasi
// geri donup yol cikisina varinca OnDeparted tetiklenir.
[RequireComponent(typeof(NavMeshAgent))]
public class CaravanMovement : MonoBehaviour
{
    [SerializeField] private CaravanData data;
    [SerializeField] private Transform castleTarget;        // Kaledeki teslim noktasi
    [SerializeField] private Transform exitTarget;          // Geri donulen yol cikisi
    [SerializeField] private float arrivalDistance = 2.0f;
    [SerializeField] private float turnSpeed = 4f;

    private NavMeshAgent agent;
    private Transform currentTarget;
    private bool moving;
    private bool returning;

    public bool HasArrived { get; private set; }
    public event Action OnReachedCastle;
    public event Action OnDeparted;

    private float MoveSpeed => data != null ? data.moveSpeed : 3f;

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    public void Configure(CaravanData caravanData)
    {
        data = caravanData;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) 
        {
            agent.speed = MoveSpeed;
            agent.angularSpeed = turnSpeed * 50f; 
        }
    }

    public void SetCastleTarget(Transform t) => castleTarget = t;
    public void SetExitTarget(Transform t) => exitTarget = t;

    // Kaleye dogru yolculugu baslatir.
    public void BeginApproach()
    {
        currentTarget = castleTarget;
        if (currentTarget == null) return;

        StopAllCoroutines();
        StartCoroutine(StartNavigationRoutine(currentTarget, false));
    }

    // Teslim sonrasi yol cikisina geri donus.
    public void BeginDepart()
    {
        currentTarget = exitTarget;
        if (currentTarget == null) return;

        StopAllCoroutines();
        StartCoroutine(StartNavigationRoutine(currentTarget, true));
    }

    private IEnumerator StartNavigationRoutine(Transform target, bool isReturning)
    {
        moving = false;
        returning = isReturning;
        HasArrived = false;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        
        // Ensure agent is reset
        if (agent != null)
        {
            agent.enabled = false;
            yield return null;
            agent.enabled = true;
            yield return null;

            // Try to find a valid position on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 20.0f, NavMesh.AllAreas))
            {
                bool warped = agent.Warp(hit.position);
                Debug.Log($"[CaravanMovement] Warp to {hit.position} result: {warped}");
            }
            else
            {
                Debug.LogWarning($"[CaravanMovement] SamplePosition failed for {gameObject.name} at {transform.position}");
            }

            yield return null;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                bool destSet = agent.SetDestination(target.position);
                agent.stoppingDistance = arrivalDistance;
                moving = true;
                Debug.Log($"[CaravanMovement] Destination {target.name} set result: {destSet}");
            }
            else
            {
                Debug.LogWarning($"[CaravanMovement] Agent is STILL not on NavMesh after warp for {gameObject.name}");
            }
        }
    }

    public void StopMoving()
    {
        moving = false;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
    }

    private void Update()
    {
        if (!moving || currentTarget == null || agent == null) return;

        // Fallback: If we should be moving but somehow lost NavMesh
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            return; 
        }

        // Don't check for arrival if the path is still being calculated 
        if (agent.pathPending) return;

        if (agent.hasPath && agent.remainingDistance <= agent.stoppingDistance)
        {
            moving = false;
            if (returning) OnDeparted?.Invoke();
            else { HasArrived = true; OnReachedCastle?.Invoke(); }
        }
    }
}
