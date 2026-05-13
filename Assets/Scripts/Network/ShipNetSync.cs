using System;
using UnityEngine;
using Photon.Pun;

// Düşman gemisi senkronu — pozisyon/rotasyon stream + spawn/destroy RPC.
// Host (owner): kendi transformunu serialize eder, ölümde RPC_DestroyShip broadcast eder.
// Diğer client'lar: gelen değere NETWORK_SYNC_RATE aralıklarla lerp eder.
[RequireComponent(typeof(PhotonView))]
public class ShipNetSync : MonoBehaviourPun, IPunObservable
{
    [SerializeField] private ShipHealth shipHealth;
    [SerializeField] private float lerpSpeed = 8f;
    [SerializeField] private float teleportDistance = 5f;     // Bu mesafeden uzaksa lerp yerine ışınla

    private Vector3 netPosition;
    private Quaternion netRotation;
    private bool deathSubscribed;
    private bool spawnAnnounced;

    private void Awake()
    {
        if (shipHealth == null) shipHealth = GetComponent<ShipHealth>();
        netPosition = transform.position;
        netRotation = transform.rotation;
    }

    private void OnEnable()
    {
        // Pool'dan tekrar kullanımda hedef değerleri sıfırla
        netPosition = transform.position;
        netRotation = transform.rotation;
        spawnAnnounced = false;

        if (shipHealth != null && AuthorityManager.IsHost && !deathSubscribed)
        {
            shipHealth.OnDeath += HandleHostDeath;
            deathSubscribed = true;
        }
    }

    private void OnDisable()
    {
        if (deathSubscribed && shipHealth != null)
        {
            shipHealth.OnDeath -= HandleHostDeath;
            deathSubscribed = false;
        }
    }

    private void Update()
    {
        if (photonView.IsMine)
        {
            AnnounceSpawnOnce();
            return;
        }

        if ((transform.position - netPosition).sqrMagnitude > teleportDistance * teleportDistance)
            transform.position = netPosition;
        else
            transform.position = Vector3.Lerp(transform.position, netPosition, lerpSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(transform.rotation, netRotation, lerpSpeed * Time.deltaTime);
    }

    // ── Host tarafı ─────────────────────────────────────────────────────

    // İlk frame'de view'in tam hazır olması için spawn bildirimini Update'e çektim.
    private void AnnounceSpawnOnce()
    {
        if (spawnAnnounced || photonView.ViewID == 0) return;
        spawnAnnounced = true;
        photonView.RPC(NetworkKeys.RPC_SPAWN_SHIP, RpcTarget.Others, transform.position, transform.rotation);
    }

    private void HandleHostDeath()
    {
        if (!AuthorityManager.RequireHost("Ship destroy")) return;
        photonView.RPC(NetworkKeys.RPC_DESTROY_SHIP, RpcTarget.All);
    }

    // ── RPC'ler ─────────────────────────────────────────────────────────

    [PunRPC]
    private void RPC_SpawnShip(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
        netPosition = pos;
        netRotation = rot;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    [PunRPC]
    private void RPC_DestroyShip()
    {
        // Pool kullanıyoruz, gerçek destroy yok — deaktif et, ObjectPooler tekrar kullanır.
        gameObject.SetActive(false);
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
