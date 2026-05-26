using System;
using UnityEngine;

// Haydut AI durumu — BanditNetSync int olarak ağ üzerinden taşır (animasyon/sync için).
public enum BanditState { Idle, Chase, Attack, Retreat }

// Haydut yapay zekası — basit state machine: Idle (ağaçtan çıkış) -> Chase -> Attack.
// Varsayılan hedef en yakın canlı kervandır (CaravanController). Oyuncu araya girip haydutu
// vurursa (BanditHealth.OnHealthChanged) haydut menzildeki en yakın oyuncuya döner.
// Hedefin IDamageable'ına attackInterval aralıklarla attackDamage uygular; değerler BanditData'dan gelir.
// Photon'a bağlı değildir; owner/non-owner gating network katmanına ait, durum State ile dışarı açılır.
public class BanditAI : MonoBehaviour
{
    [Header("Veri")]
    [SerializeField] private BanditData data;               // Raider/Brute — prefab'da atanır, spawner Configure ile de verebilir
    [SerializeField] private BanditHealth health;

    [Header("Davranış")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float playerAggroRange = 8f;   // vurulunca bu menzildeki en yakın oyuncuya döner
    [SerializeField] private float retargetInterval = 1f;   // hedef kaybolunca yeniden arama aralığı
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float retreatTimeout = 4f;     // kervan yok olunca ağaçlara çekilme süresi

    [Header("Ağaçtan Çıkış")]
    [SerializeField] private float emergeDuration = 0.6f;   // Idle bekleme (çıkış animasyonu süresi)
    [SerializeField] private GameObject emergeEffect;       // opsiyonel toz/yaprak efekti

    public BanditState State { get; private set; }

    private Transform target;
    private IDamageable targetDamageable;
    private float stateTimer;
    private float lastAttackTime;
    private float nextRetargetTime;
    private Vector3 spawnPosition;       // ağaçtan çıktığı nokta — geri çekilince buraya döner
    private bool chasingPlayer;          // hedef kervan mı (false) oyuncu mu (true)

    private float MoveSpeed => data != null ? data.moveSpeed : 3f;
    private float AttackDamage => data != null ? data.attackDamage : 5f;
    private float AttackInterval => (data != null && data.attackInterval > 0f) ? data.attackInterval : 1f;

    private void Awake()
    {
        if (health == null) health = GetComponent<BanditHealth>();
    }

    private void OnEnable()
    {
        spawnPosition = transform.position;
        EnterIdle();
        if (health != null) health.OnHealthChanged += HandleDamaged;
        EventBus.OnCaravanDestroyed += HandleCaravanDestroyed;
    }

    private void OnDisable()
    {
        if (health != null) health.OnHealthChanged -= HandleDamaged;
        EventBus.OnCaravanDestroyed -= HandleCaravanDestroyed;
    }

    // Spawner Raider/Brute verisini buradan da geçebilir (BanditHealth.Configure ile aynı veri).
    public void Configure(BanditData banditData) => data = banditData;

    private void Update()
    {
        switch (State)
        {
            case BanditState.Idle: TickIdle(); break;
            case BanditState.Chase: TickChase(); break;
            case BanditState.Attack: TickAttack(); break;
            case BanditState.Retreat: TickRetreat(); break;
        }
    }

    // ── Idle: ağaçtan yeni çıktı, kısa bekleme + efekt ──────────────────
    private void EnterIdle()
    {
        State = BanditState.Idle;
        stateTimer = 0f;
        target = null;
        targetDamageable = null;
        if (emergeEffect != null) emergeEffect.SetActive(true);
    }

    private void TickIdle()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer < emergeDuration) return;

        if (emergeEffect != null) emergeEffect.SetActive(false);
        AcquireDefaultTarget();
        State = BanditState.Chase;          // hedef boşsa Chase'te bekler ve aramaya devam eder
    }

    // ── Chase: hedefe yürü ──────────────────────────────────────────────
    private void TickChase()
    {
        if (!EnsureTarget()) return;

        Vector3 toTarget = Flat(target.position - transform.position);
        if (toTarget.sqrMagnitude <= attackRange * attackRange)
        {
            State = BanditState.Attack;
            return;
        }

        FaceDirection(toTarget);
        transform.position += toTarget.normalized * MoveSpeed * Time.deltaTime;
    }

    // ── Attack: menzilde, aralıklarla hasar ver ─────────────────────────
    private void TickAttack()
    {
        if (!EnsureTarget()) { State = BanditState.Chase; return; }

        Vector3 toTarget = Flat(target.position - transform.position);
        if (toTarget.sqrMagnitude > attackRange * attackRange)
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

    // ── Retreat: kervan yok oldu, ağaçlara geri çekil ───────────────────
    // Kervanın peşindeki haydutlar baskını bırakıp geri çekilir; oyuncuyla
    // çatışan haydut (chasingPlayer) yerinde kalıp dövüşmeye devam eder.
    private void HandleCaravanDestroyed()
    {
        if (State == BanditState.Retreat) return;
        if (chasingPlayer && TargetValid()) return;
        EnterRetreat();
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

        // Çıkış noktasına ulaşınca veya zaman aşımında sahneden kalk
        if (toSpawn.sqrMagnitude <= 0.25f || stateTimer >= retreatTimeout)
        {
            Destroy(gameObject);
            return;
        }

        FaceDirection(toSpawn);
        transform.position += toSpawn.normalized * MoveSpeed * Time.deltaTime;
    }

    // ── Hedefleme ───────────────────────────────────────────────────────
    private bool TargetValid() => target != null && targetDamageable != null && targetDamageable.IsAlive;

    // Hedef geçerliyse true; değilse gated olarak yeni kervan hedefi arar.
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

    // Varsayılan hedef: en yakın canlı kervan (TAG_CARAVAN)
    private void AcquireDefaultTarget()
    {
        GameObject[] caravans = GameObject.FindGameObjectsWithTag(GameConstants.TAG_CARAVAN);
        Transform nearest = null;
        IDamageable nearestDmg = null;
        float best = float.MaxValue;

        foreach (GameObject go in caravans)
        {
            if (go == null) continue;
            CaravanController c = go.GetComponent<CaravanController>();
            if (c == null || !c.IsAlive) continue;

            float d = (go.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = go.transform; nearestDmg = c; }
        }

        SetTarget(nearest, nearestDmg);
        chasingPlayer = false;
    }

    // Vurulunca: menzildeki en yakın canlı oyuncuya dön (oyuncu araya girdi)
    private void HandleDamaged(float current, float max)
    {
        if (current <= 0f) return;          // ölüm ayrı ele alınır

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

        if (nearest == null) return;        // menzilde oyuncu yok, kervanda kal

        SetTarget(nearest, nearestDmg);
        chasingPlayer = true;
        // Bekleyen ya da çekilen haydut, vurulunca oyuncuya döner
        if (State == BanditState.Idle || State == BanditState.Retreat) State = BanditState.Chase;
    }

    private void SetTarget(Transform t, IDamageable dmg)
    {
        target = t;
        targetDamageable = dmg;
    }

    // ── Hareket yardımcıları ────────────────────────────────────────────
    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
