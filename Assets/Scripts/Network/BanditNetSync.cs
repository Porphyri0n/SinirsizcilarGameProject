using System;
using UnityEngine;
using Unity.Netcode;

// Haydut senkronu — pozisyon/rotasyon + AI state stream, spawn/kill RPC.
// Host (owner): kendi transformunu ve AI state'ini serialize eder; ölümde RPC_BanditKilled broadcast eder.
// Diğer client'lar: gelen değerlere lerp eder, RPC ile spawn/kill uygular.
// Ölüm hook'u IDamageable üzerinden alınır (BanditHealth bunu uygular) — somut tipe bağlı değil.
public class BanditNetSync : NetworkBehaviour
{
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private float teleportDistance = 5f;     // Bu mesafeden uzaksa lerp yerine ışınla

    private IDamageable health;
    private readonly NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> netAiState = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool deathSubscribed;
    private bool spawnAnnounced;

    // Haydut AI durumu (Idle/Chase/Attack) ağ üzerinden taşınır; BanditAI yazar/okur.
    public int AiState
    {
        get => netAiState.Value;
        set { if (IsServer) netAiState.Value = value; }
    }

    private void Awake()
    {
        health = GetComponent<IDamageable>();
    }

    private void OnEnable()
    {
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
        if (IsServer)
        {
            netPosition.Value = transform.position;
            netRotation.Value = transform.rotation;
            AnnounceSpawnOnce();
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

    // View hazır olunca spawn'ı bir kez bildir (ShipNetSync ile aynı yaklaşım).
    private void AnnounceSpawnOnce()
    {
        if (spawnAnnounced || NetworkObject == null || !NetworkObject.IsSpawned) return;
        spawnAnnounced = true;
        RPC_BanditSpawnRpc(transform.position, transform.rotation);
    }

    private void HandleHostDeath()
    {
        if (!AuthorityManager.RequireHost("Bandit killed")) return;
        RPC_BanditKilledRpc();
    }

    // ── RPC'ler ─────────────────────────────────────────────────────────

    [Rpc(SendTo.NotOwner)]
    private void RPC_BanditSpawnRpc(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    [Rpc(SendTo.Everyone)]
    private void RPC_BanditKilledRpc()
    {
        // Pool kullanımıyla uyumlu — gerçek destroy yok, deaktif et.
        gameObject.SetActive(false);
    }
}
