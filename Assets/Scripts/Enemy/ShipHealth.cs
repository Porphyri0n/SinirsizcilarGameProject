using System;
using UnityEngine;

// Düşman gemisinin can sistemi (IDamageable). Hasar alır, canı bitince batar.
// Maksimum can ShipData'dan okunur; batınca EventBus.FireShipDestroyed pozisyonla tetiklenir.
public class ShipHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private ShipData shipData;

    private float currentHealth;
    private bool isSunk;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => shipData != null ? shipData.maxHealth : 0f;
    public bool IsAlive => !isSunk && currentHealth > 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;      // mevcut can, maksimum can — efekt/UI için

    private ShipType Type => shipData != null ? shipData.shipType : ShipType.Light;

    private void OnEnable()
    {
        // ObjectPooler ile tekrar kullanımda durumu sıfırla
        isSunk = false;
        currentHealth = MaxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsAlive || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        if (currentHealth <= 0f)
            Sink();
    }

    private void Sink()
    {
        if (isSunk) return;

        isSunk = true;
        EventBus.FireShipDestroyed(Type, transform.position);
        OnDeath?.Invoke();
    }

    // WaveSpawner / ObjectPooler gemiyi yapılandırırken ShipData'yı buradan verir
    public void Configure(ShipData data)
    {
        shipData = data;
        isSunk = false;
        currentHealth = MaxHealth;
    }
}
