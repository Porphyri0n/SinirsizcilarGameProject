using System;
using UnityEngine;
using Photon.Pun;

// Combat senkronu — kılıç saldırısı, blok, silah değişimi ve hasar alma RPC'leri.
// Her oyuncu prefab'inde bir tane bulunur (PlayerNetSync ile birlikte).
// Sahip (IsMine): EventBus combat olaylarını RPC ile diğer client'lara taşır.
// Diğer client'lar: gelen RPC'yi EventBus'ta tekrar fire eder; UI/ses bu sayede tepki verir.
[RequireComponent(typeof(PhotonView))]
public class CombatNetSync : MonoBehaviourPun
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerCombat playerCombat;

    private int OwnerId => photonView.OwnerActorNr;

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
        photonView.RPC(NetworkKeys.RPC_PLAYER_ATTACK, RpcTarget.Others, dmg, pos);
    }

    private void HandlePlayerBlocked(int pid, float amount)
    {
        if (!IsMineFor(pid)) return;
        photonView.RPC(NetworkKeys.RPC_PLAYER_BLOCK, RpcTarget.Others, amount);
    }

    private void HandleWeaponEquipped(int pid, WeaponType type)
    {
        if (!IsMineFor(pid)) return;
        photonView.RPC(NetworkKeys.RPC_EQUIP_WEAPON, RpcTarget.Others, (int)type);
    }

    // Saldırgan, hedefin CombatNetSync'i üzerinden bu metodu çağırır.
    // Hedefin owner'ında PlayerHealth uygulanır, diğer client'larda görsel/efekt için event fire edilir.
    public void RequestTakeDamage(float amount, Vector3 hitPoint)
    {
        if (amount <= 0f || !PhotonNetwork.InRoom) return;
        photonView.RPC(NetworkKeys.RPC_TAKE_DAMAGE, RpcTarget.All, amount, hitPoint);
    }

    // ── RPC'ler ─────────────────────────────────────────────────────────

    [PunRPC]
    private void RPC_PlayerAttack(float dmg, Vector3 pos)
    {
        EventBus.FirePlayerAttacked(OwnerId, dmg, pos);
    }

    [PunRPC]
    private void RPC_PlayerBlock(float amount)
    {
        EventBus.FirePlayerBlocked(OwnerId, amount);
    }

    [PunRPC]
    private void RPC_EquipWeapon(int typeInt)
    {
        EventBus.FireWeaponEquipped(OwnerId, (WeaponType)typeInt);
    }

    [PunRPC]
    private void RPC_TakeDamage(float amount, Vector3 hitPoint)
    {
        // Blok varsa hasarı azalt (her client'ta aynı kalkan durumunu görmek için)
        float final = playerCombat != null ? playerCombat.MitigateDamage(amount) : amount;

        // Otoriteli can yalnızca owner'da düşer; diğerleri sadece olay/efekt için bilgilenir
        if (photonView.IsMine && playerHealth != null)
            playerHealth.TakeDamage(final, hitPoint);
    }

    // ── Yardımcı ────────────────────────────────────────────────────────

    private bool IsMineFor(int pid) => photonView.IsMine && pid == OwnerId;
}
