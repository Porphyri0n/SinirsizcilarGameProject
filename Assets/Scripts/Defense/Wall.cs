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
    [Tooltip("Azalan sırada eşikler. Can yüzdesi eşiğin altına inince sonraki aşamaya geçer (0.5 → %50 çatlak, 0.25 → %25 ağır hasar)")]
    [SerializeField] private float[] stageThresholds = { 0.5f, 0.25f };
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

    private void Start()
    {
        if (CastleWalls.Instance != null)
        {
            CastleWalls.Instance.RegisterWall(this);
        }
    }

    /// <summary>
    /// Network senkronu için yan etkisiz (EventBus tetiklemeyen) can güncellemesi.
    /// </summary>
    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateDamageVisuals();
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
{
        if (!IsAlive || amount <= 0f)
            return;

        // Merkezi yönetim üzerinden hasar ver
        if (CastleWalls.Instance != null)
        {
            CastleWalls.Instance.TakeDamage(this, amount, hitPoint);
        }
        else
        {
            // Manager yoksa doğrudan hasar al (yedek mekanizma)
            ApplyDamageInternal(amount, hitPoint);
        }
    }

    /// <summary>
    /// CastleWalls tarafından çağrılan asıl hasar uygulama metodu.
    /// </summary>
    public void ApplyDamageInternal(float amount, Vector3 hitPoint)
    {
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

        // Merkezi UI güncellemesi
        if (CastleWalls.Instance != null)
        {
            CastleWalls.Instance.UpdateUI();
        }
    }

    private void HandleDestroyed()
    {
        if (destroyedEffect != null)
            destroyedEffect.SetActive(true);

        OnDeath?.Invoke();

        foreach (var stage in damageStages) if (stage != null) stage.SetActive(false);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Efektin görünmesi için kısa bir bekleme sonrası nesneyi sil.
        Destroy(gameObject, 0.5f);
    }

    /// <summary>Can yüzdesine göre doğru hasar aşamasını aktifleştirir (eşikler: %100 sağlam, %50 çatlak, %25 ağır hasar).</summary>
    private void UpdateDamageVisuals()
    {
        if (damageStages == null || damageStages.Length == 0)
            return;

        float pct = maxHealth > 0f ? currentHealth / maxHealth : 0f;

        // Can yüzdesi bir eşiğin altına indikçe bir sonraki hasar aşamasına geç.
        int stage = 0;
        for (int i = 0; i < stageThresholds.Length; i++)
            if (pct <= stageThresholds[i]) stage = i + 1;
        stage = Mathf.Clamp(stage, 0, damageStages.Length - 1);

        for (int i = 0; i < damageStages.Length; i++)
        {
            if (damageStages[i] != null)
                damageStages[i].SetActive(i == stage);
        }
    }
}
