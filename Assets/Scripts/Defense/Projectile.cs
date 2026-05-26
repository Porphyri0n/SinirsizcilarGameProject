using System;
using UnityEngine;

// Kule mermisi — Rigidbody ile uçar, çarpınca IDamageable'a hasar verir.
// Cannon mermisi: yerçekimi açık (parabolik). Archer oku: yerçekimi kapalı (düz, hızlı).
// splashRadius > 0 ise vuruş noktası çevresine Physics.OverlapSphere ile alan hasarı uygular.
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Ayarlar")]
    [SerializeField] private float lifetime = 6f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool destroyOnHit = true;

    private Rigidbody rb;
    private float damage;
    private float splashRadius;
    private bool launched;
    private float spawnTime;
    private GameObject owner;       // Kuleyi kullanan oyuncuya / kuleye geri vurmamak için
    private Vector3 lastPosition;   // tunneling kontrolü — bir önceki frame konumu
    private bool hasHit;            // tek isabet: sweep + OnTriggerEnter çift saymasın

    public bool Launched => launched;
    public float Damage => damage;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Kule (CannonTower / ArcherTower) bunu spawn'dan sonra çağırır.
    // useGravity true → top güllesi parabolik, false → düz ok.
    public void Launch(Vector3 direction, float speed, float damage,
                       float splashRadius, bool useGravity, GameObject owner = null)
    {
        this.damage = damage;
        this.splashRadius = splashRadius;
        this.owner = owner;
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
        if (Time.time - spawnTime >= lifetime)
            Destroy(gameObject);

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

    // Sweep ve trigger yolları buradan geçer; hasHit çift isabeti engeller.
    private void ProcessHit(Collider hit, Vector3 point)
    {
        hasHit = true;

        if (splashRadius > 0f)
            ApplySplashDamage(point);
        else
            ApplySingleDamage(hit, point);

        if (destroyOnHit) Destroy(gameObject);
    }

    private bool IsOwnerCollider(Collider col)
        => owner != null && col.transform.IsChildOf(owner.transform);

    private void ApplySingleDamage(Collider hit, Vector3 point)
    {
        IDamageable target = hit.GetComponentInParent<IDamageable>();
        if (target != null && target.IsAlive)
            target.TakeDamage(damage, point);
    }

    private void ApplySplashDamage(Vector3 center)
    {
        Collider[] hits = Physics.OverlapSphere(center, splashRadius, hitMask);
        foreach (Collider col in hits)
        {
            if (owner != null && col.transform.IsChildOf(owner.transform)) continue;
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) continue;
            target.TakeDamage(damage, center);
        }
    }
}
