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

    /// <summary>
    /// Network senkronu için yan etkisiz can güncellemesi.
    /// </summary>
    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        EventBus.FireCastleDamaged(currentHealth, maxHealth);
        
        if (currentHealth <= 0f && !destroyed)
            HandleDestroyed();
        else if (currentHealth > 0f && destroyed)
            destroyed = false; // Tamir mekanizması varsa diye
    }

    private void Awake()
{
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        EventBus.OnGameRestart += HandleGameRestart;
    }

    private void OnDisable()
    {
        EventBus.OnGameRestart -= HandleGameRestart;
    }

    private void HandleGameRestart()
    {
        destroyed = false;
        currentHealth = maxHealth;
        EventBus.FireCastleDamaged(currentHealth, maxHealth);
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
