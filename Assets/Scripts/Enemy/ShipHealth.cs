using System;
using UnityEngine;
using Unity.Netcode;

// Düşman gemisinin can sistemi (IDamageable). Hasar alır, canı bitince batar.
// Maksimum can ShipData'dan okunur; batınca EventBus.FireShipDestroyed pozisyonla tetiklenir.
// NetworkVariable kullanılarak tüm client'larda can senkronize edilir.
public class ShipHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private ShipData shipData;

    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool isSunk;

    public float CurrentHealth => netHealth.Value;
    public float MaxHealth
    {
        get
        {
            if (shipData == null) return 0f;
            BossShip boss = GetComponent<BossShip>();
            if (boss != null)
            {
                return shipData.maxHealth * Mathf.Max(1f, boss.HealthMultiplier);
            }
            return shipData.maxHealth;
        }
    }
    public bool IsAlive => !isSunk && netHealth.Value > 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;      // mevcut can, maksimum can — efekt/UI için

    private ShipType Type => shipData != null ? shipData.shipType : ShipType.Light;

    private void OnEnable()
    {
        // ObjectPooler ile tekrar kullanımda durumu sıfırla
        isSunk = false;
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

    [Rpc(SendTo.Server)]
    public void RequestTakeDamageRpc(float amount, Vector3 hitPoint)
    {
        TakeDamage(amount, hitPoint);
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsServer || !IsAlive || amount <= 0f) return;

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        // Note: OnHealthChanged will be triggered via NetworkVariable callback

        if (netHealth.Value <= 0f)
            Sink();
    }

    private void Sink()
    {
        if (isSunk) return;

        isSunk = true;
        EventBus.FireShipDestroyed(Type, transform.position);
        OnDeath?.Invoke();

        // Server-authoritative despawn
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    // WaveSpawner / ObjectPooler gemiyi yapılandırırken ShipData'yı buradan verir
    public void Configure(ShipData data)
    {
        shipData = data;
        isSunk = false;
        if (IsServer)
        {
            netHealth.Value = MaxHealth;
        }
    }
}
