using UnityEngine;
using Unity.Netcode;

// Haydut senkronu — AI state senkronizasyonu ve hasar isteklerini yönetir.
// Pozisyon/Rotasyon senkronu prefab üzerindeki NetworkTransform tarafından yönetilir.
// Ölüm ve Despawn işlemleri BanditHealth tarafından yönetilir.
public class BanditNetSync : NetworkBehaviour
{
    private IDamageable health;
    private readonly NetworkVariable<int> netAiState = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

    [Rpc(SendTo.Server)]
    public void RequestTakeDamageRpc(float amount, Vector3 hitPoint)
    {
        if (health != null && health.IsAlive)
        {
            health.TakeDamage(amount, hitPoint);
        }
    }
}
