using System;
using UnityEngine;
using Photon.Pun;

// Ticari kervan senkronu — pozisyon/rotasyon stream + yaşam döngüsü RPC'leri.
// Host (owner): yaklaşma/varış/saldırı/yok olma anlarını RpcTarget.Others'a broadcast eder.
// Client'lar: gelen RPC'de ilgili EventBus event'ini yeniden fire eder (CaravanData prefab'dan
// geldiği için her client'ta yereldir — SO serialize edilmez, sadece tetik gönderilir).
[RequireComponent(typeof(PhotonView))]
public class CaravanNetSync : MonoBehaviourPun, IPunObservable
{
    [SerializeField] private CaravanController controller;
    [SerializeField] private CaravanMovement movement;
    [SerializeField] private float lerpSpeed = 6f;
    [SerializeField] private float teleportDistance = 5f;     // Bu mesafeden uzaksa lerp yerine ışınla

    private Vector3 netPosition;
    private Quaternion netRotation;
    private bool hostHooked;
    private bool approachAnnounced;
    private bool attackAnnounced;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<CaravanController>();
        if (movement == null) movement = GetComponent<CaravanMovement>();
        netPosition = transform.position;
        netRotation = transform.rotation;
    }

    private void OnEnable()
    {
        netPosition = transform.position;
        netRotation = transform.rotation;

        // Owner değilsek yerel hareket host'un transformuyla çakışmasın diye kapat.
        if (!photonView.IsMine && movement != null)
            movement.enabled = false;

        // Host: varış ve yok olma yerel event'lerini RPC'ye çevir.
        if (AuthorityManager.IsHost && !hostHooked)
        {
            if (movement != null) movement.OnReachedCastle += HandleHostArrived;
            if (controller != null) controller.OnDeath += HandleHostDestroyed;
            hostHooked = true;
        }
    }

    private void OnDisable()
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
        if (photonView.IsMine)
        {
            AnnounceApproachOnce();
            AnnounceAttackOnce();
            return;
        }

        if ((transform.position - netPosition).sqrMagnitude > teleportDistance * teleportDistance)
            transform.position = netPosition;
        else
            transform.position = Vector3.Lerp(transform.position, netPosition, lerpSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(transform.rotation, netRotation, lerpSpeed * Time.deltaTime);
    }

    // ── Host tarafı ─────────────────────────────────────────────────────

    // View hazır olunca kervanın yaklaştığını bir kez bildir (ShipNetSync'teki spawn bildirimi gibi).
    private void AnnounceApproachOnce()
    {
        if (approachAnnounced || photonView.ViewID == 0) return;
        approachAnnounced = true;
        photonView.RPC(NetworkKeys.RPC_CARAVAN_APPROACH, RpcTarget.Others);
    }

    private void AnnounceAttackOnce()
    {
        if (attackAnnounced || controller == null) return;
        if (controller.State != CaravanState.UnderAttack) return;
        attackAnnounced = true;
        photonView.RPC(NetworkKeys.RPC_CARAVAN_ATTACKED, RpcTarget.Others, transform.position);
    }

    private void HandleHostArrived()
    {
        if (!AuthorityManager.RequireHost("Caravan arrived")) return;
        photonView.RPC(NetworkKeys.RPC_CARAVAN_ARRIVED, RpcTarget.Others);
    }

    private void HandleHostDestroyed()
    {
        if (!AuthorityManager.RequireHost("Caravan destroyed")) return;
        photonView.RPC(NetworkKeys.RPC_CARAVAN_DESTROYED, RpcTarget.Others);
    }

    // ── RPC'ler (client tarafı — yerel EventBus'a yeniden yayınla) ────────

    [PunRPC]
    private void RPC_CaravanApproach()
    {
        if (controller != null) EventBus.FireCaravanApproaching(controller.Data);
    }

    [PunRPC]
    private void RPC_CaravanArrived()
    {
        if (controller != null) EventBus.FireCaravanArrived(controller.Data);
    }

    [PunRPC]
    private void RPC_CaravanAttacked(Vector3 pos)
    {
        EventBus.FireCaravanUnderAttack(pos);
    }

    [PunRPC]
    private void RPC_CaravanDestroyed()
    {
        EventBus.FireCaravanDestroyed();
    }

    // ── Stream ──────────────────────────────────────────────────────────

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            netPosition = (Vector3)stream.ReceiveNext();
            netRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
