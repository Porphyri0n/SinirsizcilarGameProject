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
    [SerializeField] private float lerpSpeed = 6f;
    [SerializeField] private float teleportDistance = 5f;     // Bu mesafeden uzaksa lerp yerine ışınla

    private readonly NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
        if (IsServer)
        {
            netPosition.Value = transform.position;
            netRotation.Value = transform.rotation;
        }

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
            if (IsServer)
            {
                netPosition.Value = transform.position;
                netRotation.Value = transform.rotation;
            }
            AnnounceApproachOnce();
            AnnounceAttackOnce();
            return;
        }

        Vector3 currentPos = netPosition.Value;
        Quaternion currentRot = netRotation.Value;

        if ((transform.position - currentPos).sqrMagnitude > teleportDistance * teleportDistance)
            transform.position = currentPos;
        else
            transform.position = Vector3.Lerp(transform.position, currentPos, lerpSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(transform.rotation, currentRot, lerpSpeed * Time.deltaTime);
    }

    // ── Host tarafı ─────────────────────────────────────────────────────

    // View hazır olunca kervanın yaklaştığını bir kez bildir (ShipNetSync'teki spawn bildirimi gibi).
    private void AnnounceApproachOnce()
    {
        if (approachAnnounced || NetworkObject == null || !NetworkObject.IsSpawned) return;
        approachAnnounced = true;
        RPC_CaravanApproachRpc();
    }

    private void AnnounceAttackOnce()
    {
        if (attackAnnounced || controller == null) return;
        if (controller.State != CaravanState.UnderAttack) return;
        attackAnnounced = true;
        RPC_CaravanAttackedRpc(transform.position);
    }

    private void HandleHostArrived()
    {
        if (!AuthorityManager.RequireHost("Caravan arrived")) return;
        RPC_CaravanArrivedRpc();
    }

    private void HandleHostDestroyed()
    {
        if (!AuthorityManager.RequireHost("Caravan destroyed")) return;
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
