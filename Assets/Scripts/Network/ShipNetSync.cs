using System;
using UnityEngine;
using Unity.Netcode;

// Düşman gemisi senkronu — pozisyon/rotasyon stream + spawn/destroy RPC.
// Host (owner): kendi transformunu serialize eder, ölümde RPC_DestroyShip broadcast eder.
// Diğer client'lar: gelen değere NETWORK_SYNC_RATE aralıklarla lerp eder.
public class ShipNetSync : NetworkBehaviour
{
    [SerializeField] private ShipHealth shipHealth;
    [SerializeField] private float lerpSpeed = 8f;
    [SerializeField] private float teleportDistance = 5f;     // Bu mesafeden uzaksa lerp yerine ışınla

    private readonly NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool deathSubscribed;
    private bool spawnAnnounced;

    private void Awake()
    {
        if (shipHealth == null) shipHealth = GetComponent<ShipHealth>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            netPosition.Value = transform.position;
            netRotation.Value = transform.rotation;
        }
        spawnAnnounced = false;

        if (shipHealth != null && IsServer && !deathSubscribed)
        {
            shipHealth.OnDeath += HandleHostDeath;
            deathSubscribed = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (deathSubscribed && shipHealth != null)
        {
            shipHealth.OnDeath -= HandleHostDeath;
            deathSubscribed = false;
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

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

    private void AnnounceSpawnOnce()
    {
        if (spawnAnnounced || NetworkObject == null || !NetworkObject.IsSpawned) return;
        spawnAnnounced = true;
        RPC_SpawnShipRpc(transform.position, transform.rotation);
    }

    private void HandleHostDeath()
    {
        if (!AuthorityManager.RequireHost("Ship destroy")) return;
        RPC_DestroyShipRpc();
    }

    // ── RPC'ler ─────────────────────────────────────────────────────────

    [Rpc(SendTo.NotOwner)]
    private void RPC_SpawnShipRpc(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    [Rpc(SendTo.Everyone)]
    private void RPC_DestroyShipRpc()
    {
        // Pool kullanıyoruz, gerçek destroy yok — deaktif et, ObjectPooler tekrar kullanır.
        gameObject.SetActive(false);
    }
}
