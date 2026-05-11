using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

// Oyuncu senkronu — transform, animasyon ve taşıma durumu.
// Sahip (IsMine): kendi pozisyon/rotasyonunu stream'e yazar, taşıma durumunu player property'sine basar.
// Diğer client'lar: gelen değere NETWORK_SYNC_RATE aralıklarla lerp eder, animator parametrelerini set eder.
[RequireComponent(typeof(PhotonView))]
public class PlayerNetSync : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CarrySystem carrySystem;
    [SerializeField] private float lerpSpeed = 12f;
    [SerializeField] private float teleportDistance = 5f;    // Bu mesafeden uzaksa lerp yerine ışınla

    private static readonly int MovingHash = Animator.StringToHash("IsMoving");
    private static readonly int SprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int CarryingHash = Animator.StringToHash("IsCarrying");

    private Vector3 netPosition;
    private Quaternion netRotation;
    private bool netMoving;
    private bool netSprinting;

    private float nextSendTime;
    private bool lastCarrying;

    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (carrySystem == null) carrySystem = GetComponent<CarrySystem>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        netPosition = transform.position;
        netRotation = transform.rotation;

        // Saniyede NETWORK_SYNC_RATE'in tersi kadar serileştir (0.1 sn -> 10 Hz).
        PhotonNetwork.SerializationRate = Mathf.RoundToInt(1f / GameConstants.NETWORK_SYNC_RATE);
    }

    private void Update()
    {
        if (photonView.IsMine)
            PushCarryState();
        else
            ApplyRemoteTransform();
    }

    // ── Sahip tarafı ────────────────────────────────────────────────────

    private void PushCarryState()
    {
        bool carrying = carrySystem != null && carrySystem.IsCarrying;
        if (carrying == lastCarrying) return;

        lastCarrying = carrying;
        string itemName = carrying ? carrySystem.Carried.ItemName : string.Empty;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetworkKeys.PLAYER_CARRYING, itemName } });
        if (animator != null) animator.SetBool(CarryingHash, carrying);
    }

    // ── Diğer client'lar ────────────────────────────────────────────────

    private void ApplyRemoteTransform()
    {
        if ((transform.position - netPosition).sqrMagnitude > teleportDistance * teleportDistance)
            transform.position = netPosition;
        else
            transform.position = Vector3.Lerp(transform.position, netPosition, lerpSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(transform.rotation, netRotation, lerpSpeed * Time.deltaTime);

        if (animator != null)
        {
            animator.SetBool(MovingHash, netMoving);
            animator.SetBool(SprintingHash, netSprinting);
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != photonView.Owner) return;
        if (!changedProps.TryGetValue(NetworkKeys.PLAYER_CARRYING, out object value)) return;
        if (animator != null) animator.SetBool(CarryingHash, !string.IsNullOrEmpty(value as string));
    }

    // ── Stream ──────────────────────────────────────────────────────────

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(playerController != null && playerController.IsMoving);
            stream.SendNext(playerController != null && playerController.IsSprinting);
        }
        else
        {
            netPosition = (Vector3)stream.ReceiveNext();
            netRotation = (Quaternion)stream.ReceiveNext();
            netMoving = (bool)stream.ReceiveNext();
            netSprinting = (bool)stream.ReceiveNext();
        }
    }
}
