using System;
using UnityEngine;

// Sol tık: kılıçla saldırı. Sağ tık basılı: kalkanla blok.
// Silah yokken hiçbir şey yapamaz. Saldırı ATTACK_COOLDOWN ile sınırlı.
[RequireComponent(typeof(WeaponManager))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Saldırı")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackRadius = 0.6f;
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

        // Önce SphereCast (kabaca önümüzde), ilk IDamageable'a hasar
        bool hitSomething = false;
        RaycastHit hit;
        if (Physics.SphereCast(origin, attackRadius, dir, out hit, attackRange, hitMask, QueryTriggerInteraction.Collide))
        {
            GameObject targetGO = hit.collider.gameObject;
            IDamageable target = targetGO.GetComponentInParent<IDamageable>();
            
            if (target != null && target.IsAlive)
            {
                // Network Sync: Farklı birimler için uygun RPC'yi çağır
                if (targetGO.TryGetComponent(out BanditNetSync banditNet) || targetGO.GetComponentInParent<BanditNetSync>())
                {
                    var bNet = banditNet ?? targetGO.GetComponentInParent<BanditNetSync>();
                    bNet.RequestTakeDamageRpc(damage, hit.point);
                }
                else if (targetGO.TryGetComponent(out CombatNetSync playerNet) || targetGO.GetComponentInParent<CombatNetSync>())
                {
                    var pNet = playerNet ?? targetGO.GetComponentInParent<CombatNetSync>();
                    pNet.RequestTakeDamage(damage, hit.point);
                }
                else
                {
                    // Yerel veya ağ dışı birim
                    target.TakeDamage(damage, hit.point);
                }

                hitSomething = true;
                
                // Hit Juice
                ApplyHitJuice(hit.point);
            }
        }

        Vector3 firePos = hitSomething ? hit.point : origin + dir * attackRange;
        EventBus.FirePlayerAttacked(playerID, damage, firePos);
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
