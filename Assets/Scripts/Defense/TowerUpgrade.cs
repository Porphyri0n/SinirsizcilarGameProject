using System;
using UnityEngine;

// Kule yukseltme sistemi — IUpgradeable implement eder.
// UpgradeData SO'lar (Tier1->2, Tier2->3) Inspector'dan baglanir; maliyet ve sure
// UpgradeManager tarafindan harcanir, biz sadece seviye bump'ini yapariz.
// Yukselen tier'a gore damage/range carpanlari acikta; CannonTower/ArcherTower
// ates anda bu carpanlari okur (entegrasyon Oturum 3'te).
public class TowerUpgrade : MonoBehaviour, IUpgradeable
{
    [Header("Mevcut Seviye")]
    [SerializeField] private UpgradeLevel level = UpgradeLevel.Tier1;

    [Header("Tier Gecisleri")]
    // Tier1->2 ve Tier2->3 UpgradeData SO'lari (cost: Iron3+Stone3 / Steel3+Gold2)
    [SerializeField] private UpgradeData[] upgrades;

    [Header("Stat Carpanlari (level index'ine gore)")]
    // index: Tier1=0, Tier2=1, Tier3=2
    [SerializeField] private float[] damageMultipliers = { 1f, 1.4f, 1.8f };
    [SerializeField] private float[] rangeMultipliers = { 1f, 1.2f, 1.4f };

    [Header("Event Etiketi")]
    [SerializeField] private string upgradeTargetName = "Tower";    // EventBus.FireUpgradeCompleted icin

    public event Action<UpgradeLevel> OnUpgraded;

    // Dis sistemler (UpgradeManager) carpanlari okuyup tur tabanli filtreleme yapar
    public float DamageMultiplier => MultiplierFor(damageMultipliers);
    public float RangeMultiplier => MultiplierFor(rangeMultipliers);

    // ── IUpgradeable ─────────────────────────────────────────────────────
    public UpgradeLevel CurrentLevel => level;

    public UpgradeData GetNextUpgrade()
    {
        if (upgrades == null) return null;
        foreach (UpgradeData u in upgrades)
            if (u != null && u.fromLevel == level) return u;
        return null;
    }

    public bool CanUpgrade() => GetNextUpgrade() != null;

    // UpgradeManager maliyeti harcayip sureyi sayinca cagirir.
    public void Upgrade()
    {
        UpgradeData next = GetNextUpgrade();
        if (next == null) return;

        level = next.toLevel;
        OnUpgraded?.Invoke(level);
        EventBus.FireUpgradeCompleted(upgradeTargetName, level);
    }

    private float MultiplierFor(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 1f;
        int idx = Mathf.Clamp((int)level, 0, arr.Length - 1);
        return arr[idx];
    }
}
