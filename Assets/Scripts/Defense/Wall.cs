using System;
using UnityEngine;

/// <summary>
/// Kale suru — hasar alır ve tamir edilebilir.
/// Can düştükçe görsel aşama değişir: sağlam → çatlak → yıkık.
/// Tamir maliyeti Stone×2 + Wood×1; gerçek kaynak harcaması WallRepair + EconomyManager üzerinden yapılır.
/// </summary>
public class Wall : MonoBehaviour, IDamageable, IRepairable
{
    [Header("Can")]
    [SerializeField] private float maxHealth = 500f;

    [Header("Hasar Aşamaları (yüksek → düşük can sırasıyla)")]
    [Tooltip("0: sağlam, 1: çatlak, ... sonuncusu: yıkık")]
    [SerializeField] private GameObject[] damageStages;
    [SerializeField] private GameObject destroyedEffect;

    [Header("Tamir Maliyeti")]
    [SerializeField] private RecipeIngredient[] repairCost = new[]
    {
        new RecipeIngredient { resourceType = ResourceType.Stone, amount = 2 },
        new RecipeIngredient { resourceType = ResourceType.Wood, amount = 1 }
    };

    private float currentHealth;

    // ── IDamageable ──────────────────────────────────────────────────────
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0f;
    public event Action OnDeath;

    // ── IRepairable ──────────────────────────────────────────────────────
    public float RepairCost => repairCost.Length > 0 ? repairCost[0].amount : 0f;
    public ResourceType RepairResource => repairCost.Length > 0 ? repairCost[0].resourceType : ResourceType.Stone;
    public bool NeedsRepair => currentHealth < maxHealth;

    /// <summary>Duvarın tam tamir maliyeti (Stone×2 + Wood×1). WallRepair bunu EconomyManager ile harcar.</summary>
    public RecipeIngredient[] FullRepairCost => repairCost;

    /// <summary>Can değiştiğinde tetiklenir (mevcut can, maksimum can) — UI/görsel için.</summary>
    public event Action<float, float> OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
        UpdateDamageVisuals();
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsAlive || amount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateDamageVisuals();

        if (currentHealth <= 0f)
            HandleDestroyed();
    }

    public void Repair(float amount)
    {
        if (amount <= 0f)
            return;

        bool wasDestroyed = !IsAlive;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        if (wasDestroyed && currentHealth > 0f && destroyedEffect != null)
            destroyedEffect.SetActive(false);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateDamageVisuals();
    }

    private void HandleDestroyed()
    {
        if (destroyedEffect != null)
            destroyedEffect.SetActive(true);

        OnDeath?.Invoke();
    }

    /// <summary>Can yüzdesine göre doğru hasar aşamasını aktifleştirir (%100 → sağlam, %0 → yıkık).</summary>
    private void UpdateDamageVisuals()
    {
        if (damageStages == null || damageStages.Length == 0)
            return;

        float pct = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        int lastIndex = damageStages.Length - 1;
        int stage = Mathf.Clamp(Mathf.FloorToInt((1f - pct) * damageStages.Length), 0, lastIndex);

        for (int i = 0; i < damageStages.Length; i++)
        {
            if (damageStages[i] != null)
                damageStages[i].SetActive(i == stage);
        }
    }
}
