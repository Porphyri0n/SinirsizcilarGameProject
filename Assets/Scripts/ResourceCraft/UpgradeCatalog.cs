using System;
using System.Collections.Generic;
using UnityEngine;

// Tüm yükseltme verilerinin merkezi listesi. UpgradeData asset'leri Inspector'dan bağlanır.
// upgradeName hedef adıdır ("Ocak", "El Arabası", "Kule") — GetNext bununla eşleşir.
// Maliyet tablosu (referans):
//   Ocak Tier1→2:       Stone×5 + Iron×3
//   Ocak Tier2→3:       Stone×5 + Steel×3 + Gold×1
//   El Arabası Tier1→2: Wood×3 + Iron×2
//   El Arabası Tier2→3: Iron×3 + Steel×2
[CreateAssetMenu(fileName = "UpgradeCatalog", menuName = "Game/Upgrade Catalog")]
public class UpgradeCatalog : ScriptableObject
{
    [SerializeField] private UpgradeData[] upgrades;

    public IReadOnlyList<UpgradeData> All => upgrades;

    // Bir hedefin (upgradeName) verilen seviyeden sonraki yükseltme adımı.
    public UpgradeData GetNext(string upgradeName, UpgradeLevel fromLevel)
    {
        if (upgrades == null) return null;
        foreach (UpgradeData u in upgrades)
            if (u != null && u.upgradeName == upgradeName && u.fromLevel == fromLevel)
                return u;
        return null;
    }

    // Bir hedefe ait tüm yükseltme adımları (UI / önizleme için).
    public IEnumerable<UpgradeData> ForTarget(string upgradeName)
    {
        if (upgrades == null) yield break;
        foreach (UpgradeData u in upgrades)
            if (u != null && u.upgradeName == upgradeName)
                yield return u;
    }
}
