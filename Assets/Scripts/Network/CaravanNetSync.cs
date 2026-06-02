using System;
using UnityEngine;
using Unity.Netcode;

// Ticari kervan senkronu — pozisyon/rotasyon stream + yaşam döngüsü RPC'leri.
// Host (owner): yaklaşma/varış/saldırı/yok olma anlarını RpcTarget.Others'a broadcast eder.
// Client'lar: gelen RPC'de ilgili EventBus event'ini yeniden fire eder (CaravanData prefab'dan
// geldiği için her client'ta yereldir — SO serialize edilmez, sadece tetik gönderilir).
public class CaravanNetSync : NetworkBehaviour
{
    [SerializeField] private CaravanController controller;
    [SerializeField] private CaravanMovement movement;

    private bool hostHooked;
    private bool approachAnnounced;
    private bool attackAnnounced;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<CaravanController>();
        if (movement == null) movement = GetComponent<CaravanMovement>();
    }

    public override void OnNetworkSpawn()
    {
        // Owner değilsek yerel hareket host'un transformuyla çakışmasın diye kapat.
        if (!IsOwner && movement != null)
            movement.enabled = false;

        // Host: varış ve yok olma yerel event'lerini RPC'ye çevir.
        if (IsServer && !hostHooked)
        {
            if (movement != null) movement.OnReachedCastle += HandleHostArrived;
            if (controller != null) controller.OnDeath += HandleHostDestroyed;
            hostHooked = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (hostHooked)
        {
            if (movement != null) movement.OnReachedCastle -= HandleHostArrived;
            if (controller != null) controller.OnDeath -= HandleHostDestroyed;
            hostHooked = false;
        }
    }

    private void Update()
    {
        if (IsOwner)
        {
            AnnounceApproachOnce();
            AnnounceAttackOnce();
        }
    }

    // ── Host tarafı ─────────────────────────────────────────────────────

    // View hazır olunca kervanın yaklaştığını bir kez bildir (ShipNetSync'teki spawn bildirimi gibi).
    private void AnnounceApproachOnce()
    {
        if (approachAnnounced || NetworkObject == null || !NetworkObject.IsSpawned) return;
        approachAnnounced = true;
        if (controller != null) EventBus.FireCaravanApproaching(controller.Data);
        RPC_CaravanApproachRpc();
    }

    private void AnnounceAttackOnce()
    {
        if (attackAnnounced || controller == null) return;
        if (controller.State != CaravanState.UnderAttack) return;
        attackAnnounced = true;
        EventBus.FireCaravanUnderAttack(transform.position);
        RPC_CaravanAttackedRpc(transform.position);
    }

    private void HandleHostArrived()
    {
        if (!AuthorityManager.RequireHost("Caravan arrived")) return;
        if (controller != null) EventBus.FireCaravanArrived(controller.Data);
        RPC_CaravanArrivedRpc();
    }

    private void HandleHostDestroyed()
    {
        if (!AuthorityManager.RequireHost("Caravan destroyed")) return;
        EventBus.FireCaravanDestroyed();
        RPC_CaravanDestroyedRpc();
    }

    // ── RPC'ler (client tarafı — yerel EventBus'a yeniden yayınla) ────────

    [Rpc(SendTo.NotOwner)]
    private void RPC_CaravanApproachRpc()
    {
        if (controller != null) EventBus.FireCaravanApproaching(controller.Data);
    }

    [Rpc(SendTo.NotOwner)]
    private void RPC_CaravanArrivedRpc()
    {
        if (controller != null) EventBus.FireCaravanArrived(controller.Data);
    }

    [Rpc(SendTo.NotOwner)]
    private void RPC_CaravanAttackedRpc(Vector3 pos)
    {
        EventBus.FireCaravanUnderAttack(pos);
    }

    [Rpc(SendTo.NotOwner)]
    private void RPC_CaravanDestroyedRpc()
    {
        EventBus.FireCaravanDestroyed();
    }
}
