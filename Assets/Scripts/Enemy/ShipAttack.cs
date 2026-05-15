using System;
using UnityEngine;

// Sahile varan geminin kaleye saldırı davranışı.
// ShipMovement.OnReachedShore'u dinler; sahile varınca attackInterval aralıklarla
// TAG_CASTLE'a sahip objedeki IDamageable'a hasar verir.
[RequireComponent(typeof(ShipMovement))]
public class ShipAttack : MonoBehaviour
{
    [SerializeField] private ShipData shipData;
    [SerializeField] private ShipMovement movement;

    private IDamageable castle;
    private bool isAttacking;
    private float nextAttackTime;

    private float AttackDamage => shipData != null ? shipData.attackDamage : 0f;
    private float AttackInterval => shipData != null ? Mathf.Max(0.1f, shipData.attackInterval) : 1f;

    private void Reset()
    {
        movement = GetComponent<ShipMovement>();
    }

    private void OnEnable()
    {
        // pool'dan tekrar kullanımda durumu sıfırla
        isAttacking = false;
        nextAttackTime = 0f;
        castle = null;

        if (movement == null)
            movement = GetComponent<ShipMovement>();

        if (movement != null)
            movement.OnReachedShore += HandleReachedShore;
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.OnReachedShore -= HandleReachedShore;
    }

    private void HandleReachedShore()
    {
        isAttacking = true;
        nextAttackTime = Time.time + AttackInterval;
        castle = FindCastle();
    }

    private void Update()
    {
        if (!isAttacking) return;
        if (Time.time < nextAttackTime) return;

        // kale yıkıldıysa veya kaybolduysa yeniden ara
        if (castle == null || !castle.IsAlive)
            castle = FindCastle();

        if (castle == null) return;

        castle.TakeDamage(AttackDamage, transform.position);
        nextAttackTime = Time.time + AttackInterval;
    }

    private IDamageable FindCastle()
    {
        GameObject go = GameObject.FindGameObjectWithTag(GameConstants.TAG_CASTLE);
        return go != null ? go.GetComponent<IDamageable>() : null;
    }

    // WaveSpawner / ObjectPooler yapılandırırken ShipData'yı buradan verir
    public void Configure(ShipData data)
    {
        shipData = data;
    }
}
