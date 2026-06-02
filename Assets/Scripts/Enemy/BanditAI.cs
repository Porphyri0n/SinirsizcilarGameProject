using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

// Haydut AI durumu — BanditNetSync int olarak ağ üzerinden taşır (animasyon/sync için).
public enum BanditState { Idle, Chase, Attack, Retreat }

// Haydut yapay zekası — basit state machine: Idle (ağaçtan çıkış) -> Chase -> Attack.
// NavMeshAgent kullanarak engellerin etrafından dolanır.
public class BanditAI : MonoBehaviour
{
    [Header("Veri")]
    [SerializeField] private BanditData data;               // Raider/Brute
    [SerializeField] private BanditHealth health;
    [SerializeField] private NavMeshAgent agent;

    [Header("Davranış")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float playerAggroRange = 8f;
    [SerializeField] private float retargetInterval = 1f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float retreatTimeout = 4f;
    [SerializeField] [Tooltip("BanditData.attackDamage ile çarpılır — 2 = iki kat hasar.")]
    private float damageMultiplier = 2f;

    [Header("Ağaçtan Çıkış")]
    [SerializeField] private float emergeDuration = 0.6f;
    [SerializeField] private GameObject emergeEffect;

    public BanditState State { get; private set; }

    [SerializeField] private Animator animator;
    private BanditState lastState = (BanditState)(-1);
    private bool wasDead = false;

    private Transform target;
    private IDamageable targetDamageable;
    private float stateTimer;
    private float lastAttackTime;
    private float nextRetargetTime;
    private Vector3 spawnPosition;
    private bool chasingPlayer;

    private float MoveSpeed => data != null ? data.moveSpeed : 3f;
    private float AttackDamage => (data != null ? data.attackDamage : 5f) * damageMultiplier;
    private float AttackInterval => (data != null && data.attackInterval > 0f) ? data.attackInterval : 1f;

    private BanditNetSync netSync;
    private Rigidbody rb;
    private bool IsServer => netSync != null ? netSync.IsServer : true;

    private void Awake()
    {
        if (health == null) health = GetComponent<BanditHealth>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        netSync = GetComponent<BanditNetSync>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Freeze rotation so physics can never tip the character over.
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.isKinematic  = true;   // NavMeshAgent owns movement; physics is passenger.
        }
    }

    private void OnEnable()
    {
        spawnPosition = transform.position;
        EnterIdle();
        if (health != null) health.OnHealthChanged += HandleDamaged;
        EventBus.OnCaravanDestroyed += HandleCaravanDestroyed;
        EventBus.OnCaravanLooted += HandleCaravanLooted;

        lastState = (BanditState)(-1);
        wasDead = false;

        if (agent != null)
        {
            agent.speed = MoveSpeed;
            agent.stoppingDistance = attackRange * 0.8f;
        }
        
        // Clientlarda agent'ı kapat (Server-authoritative position sync için)
        if (agent != null && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            agent.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (health != null) health.OnHealthChanged -= HandleDamaged;
        EventBus.OnCaravanDestroyed -= HandleCaravanDestroyed;
        EventBus.OnCaravanLooted -= HandleCaravanLooted;
    }

    public void Configure(BanditData banditData)
    {
        data = banditData;
        if (agent != null) agent.speed = MoveSpeed;
    }

    private void Update()
    {
        if (IsServer)
        {
            UpdateServerLogic();
            if (netSync != null) netSync.AiState = (int)State;
        }
        else
        {
            UpdateClientState();
        }

        UpdateAnimations();
    }

    // Safety net: clamp X and Z rotation to 0 every frame so a physics nudge
    // or a bad NavMesh normal can never tilt the character.
    private void LateUpdate()
    {
        Vector3 e = transform.eulerAngles;
        if (Mathf.Abs(e.x) > 0.1f || Mathf.Abs(e.z) > 0.1f)
        {
            e.x = 0f;
            e.z = 0f;
            transform.eulerAngles = e;
        }

        // Drain any angular velocity that slipped through.
        if (rb != null && !rb.isKinematic)
            rb.angularVelocity = Vector3.zero;
    }

    private void UpdateServerLogic()
    {
        switch (State)
        {
            case BanditState.Idle: TickIdle(); break;
            case BanditState.Chase: TickChase(); break;
            case BanditState.Attack: TickAttack(); break;
            case BanditState.Retreat: TickRetreat(); break;
        }
    }

    private void UpdateClientState()
    {
        if (netSync != null)
        {
            State = (BanditState)netSync.AiState;
        }
    }

    private void EnterIdle()
    {
        State = BanditState.Idle;
        stateTimer = 0f;
        target = null;
        targetDamageable = null;
        if (emergeEffect != null) emergeEffect.SetActive(true);
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
    }

    private void TickIdle()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer < emergeDuration) return;

        if (emergeEffect != null) emergeEffect.SetActive(false);
        AcquireDefaultTarget();
        State = BanditState.Chase;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
    }

    private void TickChase()
    {
        if (!EnsureTarget()) return;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(target.position);
            
            // Large targets check: if we are close to the collider bounds, we can attack
            bool withinRange = false;
            float distToTarget = Vector3.Distance(transform.position, target.position);
            
            if (distToTarget <= attackRange) 
            {
                withinRange = true;
            }
            else
            {
                // Check if target has a collider and we are close to its surface
                var col = target.GetComponentInChildren<Collider>();
                if (col != null)
                {
                    float distToSurface = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
                    if (distToSurface <= attackRange) withinRange = true;
                }
            }

            if (withinRange || (!agent.pathPending && agent.remainingDistance <= attackRange))
            {
                State = BanditState.Attack;
            }
        }
        else
        {
            // Fallback to simple movement if NavMesh fails
            Vector3 toTarget = Flat(target.position - transform.position);
            float distSqr = toTarget.sqrMagnitude;
            
            if (distSqr <= attackRange * attackRange)
            {
                State = BanditState.Attack;
                return;
            }
            FaceDirection(toTarget);
            transform.position += toTarget.normalized * MoveSpeed * Time.deltaTime;
        }
    }

    private void TickAttack()
    {
        if (!EnsureTarget()) { State = BanditState.Chase; return; }

        Vector3 toTarget = Flat(target.position - transform.position);
        if (toTarget.sqrMagnitude > attackRange * attackRange * 1.2f)
        {
            State = BanditState.Chase;
            return;
        }

        FaceDirection(toTarget);
        if (Time.time >= lastAttackTime + AttackInterval)
        {
            lastAttackTime = Time.time;
            targetDamageable.TakeDamage(AttackDamage, transform.position);
        }
    }

    private void HandleCaravanDestroyed()
    {
        if (State == BanditState.Retreat) return;
        
        AcquireDefaultTarget();
        if (TargetValid())
        {
            State = BanditState.Chase;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        }
        else
        {
            EnterRetreat();
        }
    }

    private void HandleCaravanLooted()
    {
        if (State == BanditState.Retreat) return;
        
        AcquireDefaultTarget();
        if (TargetValid())
        {
            State = BanditState.Chase;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        }
        else
        {
            EnterRetreat();
        }
    }

    private void EnterRetreat()
    {
        State = BanditState.Retreat;
        stateTimer = 0f;
        target = null;
        targetDamageable = null;
    }

    private void TickRetreat()
    {
        stateTimer += Time.deltaTime;
        Vector3 toSpawn = Flat(spawnPosition - transform.position);

        if (toSpawn.sqrMagnitude <= 0.25f || stateTimer >= retreatTimeout)
        {
            Destroy(gameObject);
            return;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(spawnPosition);
        }
        else
        {
            FaceDirection(toSpawn);
            transform.position += toSpawn.normalized * MoveSpeed * Time.deltaTime;
        }
    }

    private bool TargetValid()
    {
        if (target == null || targetDamageable == null || !targetDamageable.IsAlive) return false;

        CaravanController caravan = target.GetComponent<CaravanController>();
        if (caravan != null && caravan.IsLooted) return false;

        return true;
    }

    private bool EnsureTarget()
    {
        if (TargetValid()) return true;

        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + retargetInterval;
            AcquireDefaultTarget();
        }
        return TargetValid();
    }

    private void AcquireDefaultTarget()
    {
        // 1. Check for Caravans (Original priority)
        GameObject[] caravans = GameObject.FindGameObjectsWithTag(GameConstants.TAG_CARAVAN);
        Transform nearest = null;
        IDamageable nearestDmg = null;
        float best = float.MaxValue;

        foreach (GameObject go in caravans)
        {
            if (go == null) continue;
            CaravanController c = go.GetComponent<CaravanController>();
            if (c == null || !c.IsAlive || c.IsLooted) continue;

            float d = (go.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = go.transform; nearestDmg = c; }
        }

        if (nearest != null)
        {
            SetTarget(nearest, nearestDmg);
            chasingPlayer = false;
            return;
        }

        // 2. Check for Players
        GameObject[] players = GameObject.FindGameObjectsWithTag(GameConstants.TAG_PLAYER);
        best = float.MaxValue;
        foreach (GameObject p in players)
        {
            if (p == null) continue;
            IDamageable dmg = p.GetComponent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) continue;

            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = p.transform; nearestDmg = dmg; }
        }

        if (nearest != null)
        {
            SetTarget(nearest, nearestDmg);
            chasingPlayer = true;
            return;
        }

        // 3. Fallback to Castle
        if (CastleHealth.Instance != null && CastleHealth.Instance.IsAlive)
        {
            SetTarget(CastleHealth.Instance.transform, CastleHealth.Instance);
            chasingPlayer = false;
        }
    }

    // Sahnede saldırılabilir (yaşayan) bir kervan var mı?
    private bool HasLivingCaravan()
    {
        GameObject[] caravans = GameObject.FindGameObjectsWithTag(GameConstants.TAG_CARAVAN);
        foreach (GameObject go in caravans)
        {
            if (go == null) continue;
            CaravanController c = go.GetComponent<CaravanController>();
            if (c != null && c.IsAlive && !c.IsLooted) return true;
        }
        return false;
    }

    private void HandleDamaged(float current, float max)
    {
        if (current <= 0f) return;

        // Kervan önceliği: yaşayan bir kervan varken, hasar alsak bile hedefi bırakıp
        // oyuncuya dönme — haydutlar kervana saldırmayı sürdürür.
        if (HasLivingCaravan()) return;

        GameObject[] players = GameObject.FindGameObjectsWithTag(GameConstants.TAG_PLAYER);
        Transform nearest = null;
        IDamageable nearestDmg = null;
        float best = playerAggroRange * playerAggroRange;

        foreach (GameObject p in players)
        {
            if (p == null) continue;
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d > best) continue;

            IDamageable dmg = p.GetComponent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) continue;

            best = d;
            nearest = p.transform;
            nearestDmg = dmg;
        }

        if (nearest == null) return;

        SetTarget(nearest, nearestDmg);
        chasingPlayer = true;
        if (State == BanditState.Idle || State == BanditState.Retreat) State = BanditState.Chase;
    }

    private void SetTarget(Transform t, IDamageable dmg)
    {
        target = t;
        targetDamageable = dmg;
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        bool isDead = (health != null && !health.IsAlive);
        if (isDead)
        {
            if (!wasDead)
            {
                wasDead = true;
                animator.CrossFade("Die", 0.15f);
            }
            return;
        }

        wasDead = false;

        if (State != lastState)
        {
            lastState = State;
            switch (State)
            {
                case BanditState.Idle:
                    animator.CrossFade("Idle", 0.15f);
                    break;
                case BanditState.Chase:
                case BanditState.Retreat:
                    animator.CrossFade("WalkRun", 0.15f);
                    break;
                case BanditState.Attack:
                    animator.CrossFade("Attack", 0.15f);
                    break;
            }
        }
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
