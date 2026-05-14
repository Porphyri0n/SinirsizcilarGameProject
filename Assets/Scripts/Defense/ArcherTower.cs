using System;
using UnityEngine;

// Okçu kulesi — orta menzil, düşük hasar, hızlı ateş. Tek hedef (splashRadius = 0).
// Değerleri DefenseData'dan okur, sol tık ile ateş eder.
public class ArcherTower : TowerController
{
    [Header("Okçu Ayarları")]
    [SerializeField] private Transform muzzle;              // Ok çıkış noktası
    [SerializeField] private LayerMask hitMask = ~0;        // Raycast katmanları

    private float lastFireTime = -999f;

    private float Damage => data != null ? data.damage : GameConstants.SWORD_BASE_DAMAGE;
    private float Range => data != null ? data.range : 20f;
    private float FireInterval => (data != null && data.fireRate > 0f) ? 1f / data.fireRate : 0.3f;

    protected override void Update()
    {
        base.Update();      // Çıkış tuşu
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
        {
            hitPoint = hit.point;
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
                target.TakeDamage(Damage, hitPoint);
        }

        EventBus.FireTowerFired(DefenseType, hitPoint);
    }
}
