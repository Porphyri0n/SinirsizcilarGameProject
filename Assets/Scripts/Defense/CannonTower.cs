using System;
using UnityEngine;

// Top kulesi — uzun menzil, ağır hasar, yavaş ateş. TowerController'dan türer.
// Tüm değerler DefenseData'dan okunur. splashRadius > 0 ise vuruş noktası çevresine alan hasarı (Physics.OverlapSphere).
public class CannonTower : TowerController
{
    [Header("Top Ayarları")]
    [SerializeField] private Transform muzzle;              // Mermi/atış çıkış noktası
    [SerializeField] private LayerMask hitMask = ~0;        // Nişan raycast'inin çarpacağı katmanlar

    private float lastFireTime = -999f;

    private float Damage => data != null ? data.damage : GameConstants.SWORD_BASE_DAMAGE;
    private float Range => data != null ? data.range : 30f;
    private float FireInterval => (data != null && data.fireRate > 0f) ? 1f / data.fireRate : 1f;
    private float SplashRadius => data != null ? data.splashRadius : 0f;

    protected override void Update()
    {
        base.Update();      // Kuleden çıkış tuşu kontrolü
        if (!IsOccupied) return;

        if (Input.GetMouseButtonDown(0) && Time.time >= lastFireTime + FireInterval)
            Fire();
    }

    private void Fire()
    {
        lastFireTime = Time.time;

        Transform origin = muzzle != null ? muzzle : transform;
        Vector3 hitPoint = origin.position + origin.forward * Range;
        if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, Range, hitMask))
            hitPoint = hit.point;

        ApplyDamage(hitPoint);
        EventBus.FireTowerFired(DefenseType, hitPoint);
    }

    // splashRadius > 0 → alan hasarı; değilse en yakın tek hedefe
    private void ApplyDamage(Vector3 point)
    {
        float radius = SplashRadius > 0f ? SplashRadius : 0.5f;
        Collider[] hits = Physics.OverlapSphere(point, radius);

        foreach (Collider col in hits)
        {
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) continue;

            target.TakeDamage(Damage, point);
            if (SplashRadius <= 0f) break;      // Tek hedef modunda ilk vuruşta dur
        }
    }
}
