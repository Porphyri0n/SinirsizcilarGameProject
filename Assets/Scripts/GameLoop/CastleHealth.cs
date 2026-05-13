using System;
using UnityEngine;

// Kale canı (IDamageable). Düşman gemileri / haydutlar hasar verir.
// TakeDamage'de OnCastleDamaged yayınlar; can 0'a inince OnCastleDestroyed + OnDeath fire eder.
// Maks. can GameConstants.CASTLE_MAX_HP'den okunur; oyun bittikten sonra hasar yutmaz.
public class CastleHealth : MonoBehaviour, IDamageable
{
    public static CastleHealth Instance { get; private set; }

    [SerializeField] private float maxHealth = GameConstants.CASTLE_MAX_HP;

    private float currentHealth;
    private bool destroyed;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => !destroyed && currentHealth > 0f;
    public event Action OnDeath;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsAlive || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        EventBus.FireCastleDamaged(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            HandleDestroyed();
    }

    private void HandleDestroyed()
    {
        if (destroyed) return;
        destroyed = true;
        EventBus.FireCastleDestroyed();
        OnDeath?.Invoke();
    }
}
