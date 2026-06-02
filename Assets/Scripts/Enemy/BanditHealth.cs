using System;
using UnityEngine;
using Unity.Netcode;

// Haydut can sistemi (IDamageable). Hasar alır, canı bitince ölür.
// Maksimum can BanditData'dan okunur; ölünce EventBus.FireBanditKilled tür ve pozisyonla tetiklenir.
// NetworkVariable kullanılarak tüm client'larda can senkronize edilir.
public class BanditHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private BanditData banditData;

    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool isDead;

    public float CurrentHealth => netHealth.Value;
    public float MaxHealth => banditData != null ? banditData.maxHealth : 0f;
    public bool IsAlive => !isDead && netHealth.Value > 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;      // mevcut can, maksimum can — efekt/UI için

    private BanditType Type => banditData != null ? banditData.banditType : BanditType.Raider;

    private void OnEnable()
    {
        // ObjectPooler ile tekrar kullanımda durumu sıfırla
        isDead = false;
        if (IsServer)
        {
            netHealth.Value = MaxHealth;
        }
    }

    public override void OnNetworkSpawn()
    {
        netHealth.OnValueChanged += HandleHealthChanged;
        
        // Initial sync for late joiners or re-enabled objects
        if (!IsServer)
        {
            OnHealthChanged?.Invoke(netHealth.Value, MaxHealth);
        }
    }

    public override void OnNetworkDespawn()
    {
        netHealth.OnValueChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float previousValue, float newValue)
    {
        OnHealthChanged?.Invoke(newValue, MaxHealth);
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsServer || !IsAlive || amount <= 0f) return;

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        // Note: OnHealthChanged will be triggered via NetworkVariable callback

        if (netHealth.Value <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        EventBus.FireBanditKilled(Type, transform.position);
        OnDeath?.Invoke();

        // Server-authoritative despawn
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    // BanditSpawner haydutu yapılandırırken BanditData'yı (Raider/Brute) buradan verir
    public void Configure(BanditData data)
    {
        banditData = data;
        isDead = false;
        if (IsServer)
        {
            netHealth.Value = MaxHealth;
        }
    }
}
