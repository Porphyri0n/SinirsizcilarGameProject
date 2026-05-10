using System;
using UnityEngine;

/// <summary>
/// Tüm düşman gemilerinin temel sınıfı — kuzeyden (denizden) wave olarak gelir.
/// IDamageable: hasar alır, canı bitince batar ve EventBus.FireShipDestroyed tetiklenir.
/// Türeyenler: ShipData ile yapılandırılan normal gemiler ve BossShip.
/// Hareket/saldırı ayrı componentlerdedir (ShipMovement, ShipAttack).
/// </summary>
public abstract class ShipBase : MonoBehaviour, IDamageable
{
    [Header("Gemi Verisi")]
    [SerializeField] protected ShipData shipData;

    protected float currentHealth;
    private bool isSinking;

    // ── IDamageable ──────────────────────────────────────────────────────
    public float CurrentHealth => currentHealth;
    public float MaxHealth => shipData != null ? shipData.maxHealth : 0f;
    public bool IsAlive => !isSinking && currentHealth > 0f;
    public event Action OnDeath;

    /// <summary>Geminin tipi (ShipData'dan). BossShip override edebilir.</summary>
    public virtual ShipType Type => shipData != null ? shipData.shipType : ShipType.Light;

    /// <summary>Can değiştiğinde tetiklenir (mevcut can, maksimum can) — efekt/UI için.</summary>
    public event Action<float, float> OnHealthChanged;

    protected virtual void Awake()
    {
        currentHealth = MaxHealth;
    }

    protected virtual void OnEnable()
    {
        // ObjectPooler ile yeniden kullanımda durumu sıfırla
        isSinking = false;
        currentHealth = MaxHealth;
    }

    public virtual void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsAlive || amount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnDamaged(amount, hitPoint);

        if (currentHealth <= 0f)
            Sink();
    }

    /// <summary>Gemiyi batırır: EventBus.FireShipDestroyed + OnDeath tetiklenir, OnSink çağrılır.</summary>
    protected void Sink()
    {
        if (isSinking)
            return;

        isSinking = true;
        EventBus.FireShipDestroyed(Type, transform.position);
        OnDeath?.Invoke();
        OnSink();
    }

    /// <summary>Hasar alındığında alt sınıfa özel tepki (efekt, ses). Varsayılan: hiçbir şey.</summary>
    protected virtual void OnDamaged(float amount, Vector3 hitPoint) { }

    /// <summary>Batma anında alt sınıfa özel davranış (batma animasyonu, loot, pool'a dönüş). Varsayılan: hiçbir şey.</summary>
    protected virtual void OnSink() { }
}
