using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Sol tık: kılıçla saldırı. Sağ tık basılı: kalkanla blok.
// Silah yokken hiçbir şey yapamaz. Saldırı ATTACK_COOLDOWN ile sınırlı.
[RequireComponent(typeof(WeaponManager))]
public class PlayerCombat : NetworkBehaviour
{
    [Header("Saldırı")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private float attackRange = 3.5f;
    [SerializeField] private float attackRadius = 0.9f;
    [SerializeField] private LayerMask hitMask = ~0;

    [SerializeField] private int playerID = -1;

    [Header("Geri Bildirim (Juice)")]
    [SerializeField] private float attackShakeMagnitude = 0.05f;
    [SerializeField] private float attackShakeDuration = 0.1f;
    [SerializeField] private float hitShakeMagnitude = 0.2f;
    [SerializeField] private float hitShakeDuration = 0.15f;
    [SerializeField] private float hitStopDuration = 0.05f;
    [SerializeField] private float attackDashForce = 2.5f;   // Saldırı anında hafif ileri atılma
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioSource sfxSource;

    private WeaponManager weapons;
    private PotionSystem potions;       // opsiyonel — Strength iksiri hasarı çarpar
    private PlayerController controller; // İleri atılma için
    private float nextAttackTime;
    private bool isBlocking;

    public bool IsBlocking => isBlocking;
    public bool IsOnAttackCooldown => Time.time < nextAttackTime;

    // Strength iksiri aktifse canlı çarpan, değilse 1. Buff bitince PotionSystem 1'e döndüğü için otomatik reset.
    private float StrengthMultiplier => potions != null ? potions.StrengthMultiplier : 1f;

    private void Awake()
    {
        weapons = GetComponent<WeaponManager>();
        potions = GetComponent<PotionSystem>();
        controller = GetComponent<PlayerController>();
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (attackOrigin == null) attackOrigin = transform;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleBlockInput();
        HandleAttackInput();
    }

    private void HandleAttackInput()
    {
        // Blok sırasında saldırı yapma — daha doğal hissedir
        if (isBlocking) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (!weapons.HasSword) return;
        if (IsOnAttackCooldown) return;

        DoAttack();
    }

    private struct CombatTargetCandidate
    {
        public GameObject gameObject;
        public Vector3 point;
        public float distance;
        public IDamageable damageable;
    }

    private void DoAttack()
    {
        WeaponData sword = weapons.Sword;
        float damage = sword.damage * StrengthMultiplier;
        float cooldown = sword.attackSpeed > 0f ? 1f / sword.attackSpeed : GameConstants.ATTACK_COOLDOWN;
        nextAttackTime = Time.time + Mathf.Max(GameConstants.ATTACK_COOLDOWN, cooldown);

        Vector3 origin = attackOrigin.position;
        Vector3 dir = attackOrigin.forward;

        // Hafif bir saldırı sarsıntısı ve ileri atılma
        if (CombatJuice.Instance != null)
            CombatJuice.Instance.Shake(attackShakeDuration, attackShakeMagnitude);
        
        if (controller != null && attackDashForce > 0f)
            controller.ApplyImpulse(dir * attackDashForce);

        // Sweeping sphere cast to find all hit candidates in attack range
        RaycastHit[] hits = Physics.SphereCastAll(origin, attackRadius, dir, attackRange, hitMask, QueryTriggerInteraction.Collide);
        
        // Overlap sphere at origin to capture extremely close or overlapping targets
        Collider[] overlapColliders = Physics.OverlapSphere(origin, attackRadius + 0.5f, hitMask, QueryTriggerInteraction.Collide);
        
        List<CombatTargetCandidate> candidates = new List<CombatTargetCandidate>();

        // Add SphereCastAll hits
        foreach (var h in hits)
        {
            if (h.collider != null)
            {
                GameObject targetGO = h.collider.gameObject;
                // Skip self or children of self
                if (targetGO == gameObject || targetGO.transform.IsChildOf(transform))
                    continue;

                IDamageable target = targetGO.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive && !IsFriendlyTarget(targetGO))
                {
                    candidates.Add(new CombatTargetCandidate
                    {
                        gameObject = targetGO,
                        point = h.point == Vector3.zero ? targetGO.transform.position : h.point,
                        distance = h.distance,
                        damageable = target
                    });
                }
            }
        }

        // Add OverlapSphere hits that are not already captured
        foreach (var col in overlapColliders)
        {
            if (col != null)
            {
                GameObject targetGO = col.gameObject;
                // Skip self or children of self
                if (targetGO == gameObject || targetGO.transform.IsChildOf(transform))
                    continue;

                IDamageable target = targetGO.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive && !IsFriendlyTarget(targetGO))
                {
                    if (!candidates.Exists(c => c.gameObject == targetGO))
                    {
                        candidates.Add(new CombatTargetCandidate
                        {
                            gameObject = targetGO,
                            point = col.ClosestPoint(origin),
                            distance = Vector3.Distance(origin, targetGO.transform.position),
                            damageable = target
                        });
                    }
                }
            }
        }

        // Filter candidates by dot product and distance
        List<CombatTargetCandidate> validCandidates = new List<CombatTargetCandidate>();
        foreach (var c in candidates)
        {
            Vector3 toTarget = (c.gameObject.transform.position - origin).normalized;
            float dot = Vector3.Dot(dir, toTarget);
            float dist = Vector3.Distance(origin, c.gameObject.transform.position);

            // Accept target if in front (dot > 0.0f) or extremely close
            if (dot > 0.0f || dist < attackRadius + 0.5f)
            {
                validCandidates.Add(c);
            }
        }

        bool hitSomething = false;
        Vector3 firePos = origin + dir * attackRange;

        if (validCandidates.Count > 0)
        {
            // Sort to find the closest target
            validCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            var chosen = validCandidates[0];
            // Normalize to the root that owns IDamageable so component searches are reliable.
            GameObject targetGO = (chosen.damageable as MonoBehaviour)?.gameObject ?? chosen.gameObject;
            IDamageable target = chosen.damageable;
            firePos = chosen.point;

            // Network Sync: route to the correct networked RPC based on unit type.
            // Always walk up the hierarchy so child colliders on large prefabs resolve correctly.
            BanditNetSync banditNet = targetGO.GetComponent<BanditNetSync>()
                                  ?? targetGO.GetComponentInParent<BanditNetSync>();
            ShipHealth shipHealth   = targetGO.GetComponent<ShipHealth>()
                                  ?? targetGO.GetComponentInParent<ShipHealth>();
            CaravanController caravan = targetGO.GetComponent<CaravanController>()
                                     ?? targetGO.GetComponentInParent<CaravanController>();
            CombatNetSync playerNet = targetGO.GetComponent<CombatNetSync>()
                                   ?? targetGO.GetComponentInParent<CombatNetSync>();

            if (banditNet != null)
            {
                banditNet.RequestTakeDamageRpc(damage, firePos);
            }
            else if (shipHealth != null)
            {
                // Covers both normal ships and BossShip (which has ShipHealth on its root).
                shipHealth.RequestTakeDamageRpc(damage, firePos);
            }
            else if (caravan != null)
            {
                caravan.RequestTakeDamageRpc(damage, firePos);
            }
            else if (playerNet != null)
            {
                playerNet.RequestTakeDamage(damage, firePos);
            }
            else
            {
                // Non-networked or local fallback.
                target.TakeDamage(damage, firePos);
            }

            hitSomething = true;
            ApplyHitJuice(firePos);
        }

        EventBus.FirePlayerAttacked(playerID, damage, firePos);
    }

    private bool IsFriendlyTarget(GameObject targetGO)
    {
        Transform t = targetGO.transform;
        while (t != null)
        {
            if (t.CompareTag(GameConstants.TAG_DEFENSE) || t.CompareTag(GameConstants.TAG_CASTLE))
                return true;
            t = t.parent;
        }
        return false;
    }

    private void ApplyHitJuice(Vector3 pos)
    {
        if (CombatJuice.Instance != null)
        {
            CombatJuice.Instance.Shake(hitShakeDuration, hitShakeMagnitude);
            CombatJuice.Instance.HitStop(hitStopDuration);
        }

        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, pos, Quaternion.identity);
        }

        if (sfxSource != null && hitSound != null)
        {
            sfxSource.PlayOneShot(hitSound);
        }
    }

    private void HandleBlockInput()
    {
        bool wantBlock = Input.GetMouseButton(1) && weapons.HasShield;
        isBlocking = wantBlock;
    }

    // Diğer sistemler (PlayerHealth vb.) gelen hasarı buradan geçirir.
    // Bloklanıyorsa WeaponData.blockAmount kadar azaltır ve event tetikler.
    public float MitigateDamage(float incoming)
    {
        if (!isBlocking || !weapons.HasShield || incoming <= 0f) return incoming;

        float block = Mathf.Clamp01(weapons.Shield.blockAmount);
        float blocked = incoming * block;
        EventBus.FirePlayerBlocked(playerID, blocked);
        return incoming - blocked;
    }

    public void SetPlayerID(int id) => playerID = id;
}
