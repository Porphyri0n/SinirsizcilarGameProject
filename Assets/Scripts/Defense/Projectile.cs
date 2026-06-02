using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Merminin hangi tarafa ait olduğu — dost ateşi (friendly fire) filtresi için.
public enum ProjectileTeam { Player, Enemy }

// Kule mermisi — Rigidbody ile uçar, çarpınca IDamageable'a hasar verir.
// Cannon mermisi: yerçekimi açık (parabolik). Archer oku: yerçekimi kapalı (düz, hızlı).
// splashRadius > 0 ise vuruş noktası çevresine Physics.OverlapSphere ile alan hasarı uygular.
[RequireComponent(typeof(Rigidbody))]
public class Projectile : NetworkBehaviour
{
    [Header("Ayarlar")]
    [SerializeField] private float lifetime = 6f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool destroyOnHit = true;

    public struct ProjectileSyncData : INetworkSerializable
    {
        public Vector3 direction;
        public float speed;
        public float damage;
        public float splashRadius;
        public bool useGravity;
        public ProjectileTeam team;
        public NetworkObjectReference owner;
        public NetworkObjectReference operatorToIgnore;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref direction);
            serializer.SerializeValue(ref speed);
            serializer.SerializeValue(ref damage);
            serializer.SerializeValue(ref splashRadius);
            serializer.SerializeValue(ref useGravity);
            serializer.SerializeValue(ref team);
            serializer.SerializeValue(ref owner);
            serializer.SerializeValue(ref operatorToIgnore);
        }
    }

    private Rigidbody rb;
    private float damage;
    private float splashRadius;
    private bool launched;
    private float spawnTime;
    private GameObject owner;       // Atan kule/gemi — kendine geri vurmamak için
    private GameObject operatorToIgnore;    // Kuleyi kullanan oyuncu — kendi atışıyla kendini vurmasın
    private ProjectileTeam team;            // Player atışı dost yapılara, Enemy atışı diğer düşmanlara vurmaz
    private Vector3 lastPosition;   // tunneling kontrolü — bir önceki frame konumu
    private bool hasHit;            // tek isabet: sweep + OnTriggerEnter çift saymasın

    private NetworkVariable<ProjectileSyncData> syncData = new NetworkVariable<ProjectileSyncData>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool Launched => launched;
    public float Damage => damage;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            ProjectileSyncData data = syncData.Value;
            GameObject ownerObj = null;
            if (data.owner.TryGet(out NetworkObject ownerNet)) ownerObj = ownerNet.gameObject;
            
            GameObject operatorObj = null;
            if (data.operatorToIgnore.TryGet(out NetworkObject operatorNet)) operatorObj = operatorNet.gameObject;

            Launch(data.direction, data.speed, data.damage, data.splashRadius, data.useGravity, data.team, ownerObj, operatorObj);
        }
    }

    public void SetSyncData(ProjectileSyncData data)
    {
        syncData.Value = data;
    }

    // Kule (CannonTower / ArcherTower) bunu spawn'dan sonra çağırır.
    // useGravity true → top güllesi parabolik, false → düz ok.
    public void Launch(Vector3 direction, float speed, float damage,
                       float splashRadius, bool useGravity, ProjectileTeam team,
                       GameObject owner = null, GameObject operatorToIgnore = null)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[Projectile] Rigidbody component is missing on " + gameObject.name, gameObject);
            return;
        }

        this.damage = damage;
        this.splashRadius = splashRadius;
        this.owner = owner;
        this.operatorToIgnore = operatorToIgnore;
        this.team = team;
        spawnTime = Time.time;
        launched = true;
        hasHit = false;
        lastPosition = transform.position;

        rb.useGravity = useGravity;
        rb.linearVelocity = direction.normalized * speed;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void Update()
    {
        if (!launched) return;
        
        if (IsServer && Time.time - spawnTime >= lifetime)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn();
            else
                Destroy(gameObject);
            return;
        }

        // Hareket yönüne döndür (parabolik mermide görsel düzgün dursun)
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
    }

    // Hızlı mermiler (özellikle ok) bir karede hedefi atlayıp içinden geçebiliyordu.
    // Önceki konumdan şimdikine ışın atıp aradaki çarpışmayı yakalarız (tunneling fix).
    private void FixedUpdate()
    {
        if (!launched || hasHit) return;

        Vector3 current = transform.position;
        Vector3 step = current - lastPosition;
        float dist = step.magnitude;

        if (dist > 0.0001f &&
            Physics.Raycast(lastPosition, step / dist, out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Collide))
        {
            if (!IsOwnerCollider(hit.collider))
                ProcessHit(hit.collider, hit.point);
        }

        lastPosition = current;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!launched || hasHit) return;
        if (IsOwnerCollider(other)) return;
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        ProcessHit(other, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!launched || hasHit) return;
        if (IsOwnerCollider(collision.collider)) return;
        if (((1 << collision.gameObject.layer) & hitMask) == 0) return;

        ProcessHit(collision.collider, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
    }

    // Sweep ve trigger yolları buradan geçer; hasHit çift isabeti engeller.
    private void ProcessHit(Collider hit, Vector3 point)
    {
        // Ölü/batan gemi hâlâ collider taşır; mermi ona takılmadan içinden geçip
        // arkadaki canlı hedefe gitsin (yoksa atışlar ölü gemilerde boşa gidiyordu).
        IDamageable target = hit.GetComponentInParent<IDamageable>();
        
        // DEBUG LOG: Hangi objeye çarptık ve IDamageable bulduk mu?
        Debug.Log($"[Projectile] Hit: {hit.name}, TargetFound: {target != null}, Layer: {LayerMask.LayerToName(hit.gameObject.layer)}");

        if (target != null && !target.IsAlive) return;

        hasHit = true;

        if (IsServer)
        {
            if (splashRadius > 0f)
                ApplySplashDamage(point);
            else if (target != null && !IsFriendlyTarget(target))
                target.TakeDamage(damage, point);

            if (destroyOnHit)
            {
                if (NetworkObject != null && NetworkObject.IsSpawned)
                    NetworkObject.Despawn();
                else
                    Destroy(gameObject);
            }
        }
    }

    private bool IsOwnerCollider(Collider col)
    {
        // Kendimize, sahibimize (kule/gemi) veya kuleyi kullanan operatöre çarpmayalım
        if (col.transform.IsChildOf(transform)) return true;
        if (owner != null && col.transform.IsChildOf(owner.transform)) return true;
        return operatorToIgnore != null && col.transform.IsChildOf(operatorToIgnore.transform);
    }

    private void ApplySplashDamage(Vector3 center)
    {
        Collider[] hits = Physics.OverlapSphere(center, splashRadius, hitMask);
        // Aynı hedef birden çok collider taşıyabilir (sur: hasar aşamaları + WallTop + taban).
        // Hedef başına TEK kez hasar uygula; yoksa tek gülle N×damage verip instakill yapar.
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        foreach (Collider col in hits)
        {
            if (owner != null && col.transform.IsChildOf(owner.transform)) continue;
            if (operatorToIgnore != null && col.transform.IsChildOf(operatorToIgnore.transform)) continue;
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) continue;
            if (IsFriendlyTarget(target)) continue;
            if (!damaged.Add(target)) continue;
            target.TakeDamage(damage, center);
        }
    }

    // Dost ateşi filtresi. Player atışı kendi yapılarına (sur/kule/kale) hasar vermez;
    // Enemy atışı diğer düşmanlara (gemi/haydut) vurmaz. Oyuncular kasıtlı hedef olabilir
    // (friendly fire açık) — operatörün kendisi ayrıca IsOwnerCollider ile muaf tutulur.
    private bool IsFriendlyTarget(IDamageable target)
    {
        Component comp = target as Component;
        if (comp == null) return false;
        Transform t = comp.transform;

        // Recursive check up the hierarchy for tags
        while (t != null)
        {
            if (team == ProjectileTeam.Player)
            {
                if (t.CompareTag(GameConstants.TAG_DEFENSE) || t.CompareTag(GameConstants.TAG_CASTLE))
                    return true;
            }
            else
            {
                if (t.CompareTag(GameConstants.TAG_ENEMY) || t.CompareTag(GameConstants.TAG_BANDIT))
                    return true;
            }
            t = t.parent;
        }

        return false;
    }
}

