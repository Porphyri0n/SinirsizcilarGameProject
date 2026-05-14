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

    private WeaponManager weapons;
    private float nextAttackTime;
    private bool isBlocking;

    public bool IsBlocking => isBlocking;
    public bool IsOnAttackCooldown => Time.time < nextAttackTime;

    private void Awake()
    {
        weapons = GetComponent<WeaponManager>();
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
        float damage = sword.damage;
        float cooldown = sword.attackSpeed > 0f ? 1f / sword.attackSpeed : GameConstants.ATTACK_COOLDOWN;
        nextAttackTime = Time.time + Mathf.Max(GameConstants.ATTACK_COOLDOWN, cooldown);

        Vector3 origin = attackOrigin.position;
        Vector3 dir = attackOrigin.forward;

        // Önce SphereCast (kabaca önümüzde), ilk IDamageable'a hasar
        bool hitSomething = false;
        if (Physics.SphereCast(origin, attackRadius, dir, out RaycastHit hit, attackRange, hitMask, QueryTriggerInteraction.Collide))
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(damage, hit.point);
                hitSomething = true;
            }
        }

        Vector3 firePos = hitSomething ? hit.point : origin + dir * attackRange;
        EventBus.FirePlayerAttacked(playerID, damage, firePos);
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
