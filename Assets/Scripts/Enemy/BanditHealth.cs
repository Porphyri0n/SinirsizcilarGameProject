using System;
using UnityEngine;

// Haydut can sistemi (IDamageable). Hasar alır, canı bitince ölür.
// Maksimum can BanditData'dan okunur; ölünce EventBus.FireBanditKilled tür ve pozisyonla tetiklenir.
public class BanditHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private BanditData banditData;

    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => banditData != null ? banditData.maxHealth : 0f;
    public bool IsAlive => !isDead && currentHealth > 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;      // mevcut can, maksimum can — efekt/UI için

    private BanditType Type => banditData != null ? banditData.banditType : BanditType.Raider;

    private void OnEnable()
    {
        // ObjectPooler ile tekrar kullanımda durumu sıfırla
        isDead = false;
        currentHealth = MaxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsAlive || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        EventBus.FireBanditKilled(Type, transform.position);
        OnDeath?.Invoke();
    }

    // BanditSpawner haydutu yapılandırırken BanditData'yı (Raider/Brute) buradan verir
    public void Configure(BanditData data)
    {
        banditData = data;
        isDead = false;
        currentHealth = MaxHealth;
    }
}
