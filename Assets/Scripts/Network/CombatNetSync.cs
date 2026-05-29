using System;
using UnityEngine;
using Unity.Netcode;

// Combat senkronu — kılıç saldırısı, blok, silah değişimi ve hasar alma RPC'leri.
// Her oyuncu prefab'inde bir tane bulunur (PlayerNetSync ile birlikte).
// Sahip (IsMine): EventBus combat olaylarını RPC ile diğer client'lara taşır.
// Diğer client'lar: gelen RPC'yi EventBus'ta tekrar fire eder; UI/ses bu sayede tepki verir.
public class CombatNetSync : NetworkBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerCombat playerCombat;

    private int OwnerId => (int)OwnerClientId;

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        EventBus.OnPlayerAttacked += HandlePlayerAttacked;
        EventBus.OnPlayerBlocked += HandlePlayerBlocked;
        EventBus.OnWeaponEquipped += HandleWeaponEquipped;
    }

    private void OnDisable()
    {
        EventBus.OnPlayerAttacked -= HandlePlayerAttacked;
        EventBus.OnPlayerBlocked -= HandlePlayerBlocked;
        EventBus.OnWeaponEquipped -= HandleWeaponEquipped;
    }

    // ── Sahip: EventBus -> RPC ──────────────────────────────────────────

    private void HandlePlayerAttacked(int pid, float dmg, Vector3 pos)
    {
        if (!IsMineFor(pid)) return;
        RPC_PlayerAttackRpc(dmg, pos);
    }

    private void HandlePlayerBlocked(int pid, float amount)
    {
        if (!IsMineFor(pid)) return;
        RPC_PlayerBlockRpc(amount);
    }

    private void HandleWeaponEquipped(int pid, WeaponType type)
    {
        if (!IsMineFor(pid)) return;
        RPC_EquipWeaponRpc((int)type);
    }

    // Saldırgan, hedefin CombatNetSync'i üzerinden bu metodu çağırır.
    // Hedefin owner'ında PlayerHealth uygulanır, diğer client'larda görsel/efekt için event fire edilir.
    public void RequestTakeDamage(float amount, Vector3 hitPoint)
    {
        if (amount <= 0f || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsClient) return;
        RPC_TakeDamageRpc(amount, hitPoint);
    }

    // ── RPC'ler ─────────────────────────────────────────────────────────

    [Rpc(SendTo.NotOwner)]
    private void RPC_PlayerAttackRpc(float dmg, Vector3 pos)
    {
        EventBus.FirePlayerAttacked(OwnerId, dmg, pos);
    }

    [Rpc(SendTo.NotOwner)]
    private void RPC_PlayerBlockRpc(float amount)
    {
        EventBus.FirePlayerBlocked(OwnerId, amount);
    }

    [Rpc(SendTo.NotOwner)]
    private void RPC_EquipWeaponRpc(int typeInt)
    {
        EventBus.FireWeaponEquipped(OwnerId, (WeaponType)typeInt);
    }

    [Rpc(SendTo.Everyone)]
    private void RPC_TakeDamageRpc(float amount, Vector3 hitPoint)
    {
        // Blok varsa hasarı azalt (her client'ta aynı kalkan durumunu görmek için)
        float final = playerCombat != null ? playerCombat.MitigateDamage(amount) : amount;

        // Otoriteli can yalnızca owner'da düşer; diğerleri sadece olay/efekt için bilgilenir
        if (IsOwner && playerHealth != null)
            playerHealth.TakeDamage(final, hitPoint);
    }

    // ── Yardımcı ────────────────────────────────────────────────────────
    private bool IsMineFor(int pid) => IsOwner && pid == OwnerId;
}
