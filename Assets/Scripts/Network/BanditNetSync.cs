using System;
using UnityEngine;
using Photon.Pun;

// Haydut senkronu — pozisyon/rotasyon + AI state stream, spawn/kill RPC.
// Host (owner): kendi transformunu ve AI state'ini serialize eder; ölümde RPC_BanditKilled broadcast eder.
// Diğer client'lar: gelen değerlere lerp eder, RPC ile spawn/kill uygular.
// Ölüm hook'u IDamageable üzerinden alınır (BanditHealth bunu uygular) — somut tipe bağlı değil.
[RequireComponent(typeof(PhotonView))]
public class BanditNetSync : MonoBehaviourPun, IPunObservable
{
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private float teleportDistance = 5f;     // Bu mesafeden uzaksa lerp yerine ışınla

    private IDamageable health;
    private Vector3 netPosition;
    private Quaternion netRotation;
    private bool deathSubscribed;
    private bool spawnAnnounced;

    // Haydut AI durumu (Idle/Chase/Attack) ağ üzerinden taşınır; BanditAI yazar/okur.
    public int AiState { get; set; }

    private void Awake()
    {
        health = GetComponent<IDamageable>();
        netPosition = transform.position;
        netRotation = transform.rotation;
    }

    private void OnEnable()
    {
        // Pool'dan tekrar kullanımda hedef değerleri sıfırla
        netPosition = transform.position;
        netRotation = transform.rotation;
        spawnAnnounced = false;

        if (health != null && AuthorityManager.IsHost && !deathSubscribed)
        {
            health.OnDeath += HandleHostDeath;
            deathSubscribed = true;
        }
    }

    private void OnDisable()
    {
        if (deathSubscribed && health != null)
        {
            health.OnDeath -= HandleHostDeath;
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

    // View hazır olunca spawn'ı bir kez bildir (ShipNetSync ile aynı yaklaşım).
    private void AnnounceSpawnOnce()
    {
        if (spawnAnnounced || photonView.ViewID == 0) return;
        spawnAnnounced = true;
        photonView.RPC(NetworkKeys.RPC_BANDIT_SPAWN, RpcTarget.Others, transform.position, transform.rotation);
    }

    private void HandleHostDeath()
    {
        if (!AuthorityManager.RequireHost("Bandit killed")) return;
        photonView.RPC(NetworkKeys.RPC_BANDIT_KILLED, RpcTarget.All);
    }

    // ── RPC'ler ─────────────────────────────────────────────────────────

    [PunRPC]
    private void RPC_BanditSpawn(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
        netPosition = pos;
        netRotation = rot;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    [PunRPC]
    private void RPC_BanditKilled()
    {
        // Pool kullanımıyla uyumlu — gerçek destroy yok, deaktif et.
        gameObject.SetActive(false);
    }

    // ── Stream ──────────────────────────────────────────────────────────

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(AiState);
        }
        else
        {
            netPosition = (Vector3)stream.ReceiveNext();
            netRotation = (Quaternion)stream.ReceiveNext();
            AiState = (int)stream.ReceiveNext();
        }
    }
}
