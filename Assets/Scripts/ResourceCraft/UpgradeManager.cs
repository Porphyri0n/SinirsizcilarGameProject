using System;
using System.Collections;
using UnityEngine;

// Yükseltme orkestratörü — maliyet kontrol, kaynak harca, süre say, sonunda IUpgradeable.Upgrade().
// Ocak, kule, el arabası yükseltmeleri buradan geçer.
// EventBus.FireUpgradeCompleted(targetName, newLevel) bitişte tetiklenir.
public class UpgradeManager : MonoBehaviour
{
    public bool IsUpgrading { get; private set; }
    public IUpgradeable CurrentTarget { get; private set; }
    public string CurrentTargetName { get; private set; }
    public float Progress { get; private set; }            // 0..1 — UI için

    // UI / efekt dinleyicileri için local event'ler
    public event Action<string, UpgradeData> OnUpgradeStarted;
    public event Action<string, UpgradeLevel> OnUpgradeFinished;

    // Yükseltmeyi başlat. targetName: "CraftingStation", "CannonTower", "Wheelbarrow" vb.
    // false döner: zaten yükseltme var, IUpgradeable null, next upgrade yok ya da maliyet yetersiz.
    public bool TryStartUpgrade(IUpgradeable target, string targetName)
    {
        if (IsUpgrading) return false;
        if (target == null) return false;
        if (!target.CanUpgrade()) return false;

        UpgradeData next = target.GetNextUpgrade();
        if (next == null) return false;

        if (!HasResources(next)) return false;
        if (!SpendResources(next)) return false;

        StartCoroutine(UpgradeRoutine(target, targetName, next));
        return true;
    }

    private bool HasResources(UpgradeData data)
    {
        EconomyManager econ = EconomyManager.Instance;
        if (econ == null) return false;
        if (data.cost == null) return true;

        foreach (RecipeIngredient ing in data.cost)
        {
            if (ing == null) continue;
            if (!econ.HasEnough(ing.resourceType, ing.amount)) return false;
        }
        return true;
    }

    private bool SpendResources(UpgradeData data)
    {
        EconomyManager econ = EconomyManager.Instance;
        if (econ == null) return false;
        if (data.cost == null) return true;

        foreach (RecipeIngredient ing in data.cost)
        {
            if (ing == null) continue;
            // HasResources önce geçtiği için pratikte false dönmez
            if (!econ.SpendResource(ing.resourceType, ing.amount)) return false;
        }
        return true;
    }

    private IEnumerator UpgradeRoutine(IUpgradeable target, string targetName, UpgradeData data)
    {
        IsUpgrading = true;
        CurrentTarget = target;
        CurrentTargetName = targetName;
        Progress = 0f;
        OnUpgradeStarted?.Invoke(targetName, data);

        float duration = Mathf.Max(0.1f, data.upgradeTime);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Progress = Mathf.Clamp01(t / duration);
            yield return null;
        }

        target.Upgrade();       // IUpgradeable kendi seviyesini yükseltir
        EventBus.FireUpgradeCompleted(targetName, target.CurrentLevel);
        OnUpgradeFinished?.Invoke(targetName, target.CurrentLevel);

        IsUpgrading = false;
        CurrentTarget = null;
        CurrentTargetName = null;
        Progress = 0f;
    }
}
