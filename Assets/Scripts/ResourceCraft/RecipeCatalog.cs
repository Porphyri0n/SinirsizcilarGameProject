using System;
using System.Collections.Generic;
using UnityEngine;

// Tüm craft tariflerinin merkezi listesi. RecipeData asset'leri Inspector'dan bağlanır.
// Tarif tablosu (referans):
//   Demir Kılıç  (Tier1): Iron×2 + Wood×1     | Ocak Tier1 | 5 sn
//   Demir Kalkan (Tier1): Iron×3 + Wood×1     | Ocak Tier1 | 6 sn
//   Çelik Kılıç  (Tier2): Steel×2 + Iron×1    | Ocak Tier2 | 8 sn
//   Çelik Kalkan (Tier2): Steel×3 + Iron×1    | Ocak Tier2 | 9 sn
//   Altın Kılıç  (Tier3): Gold×2 + Steel×1    | Ocak Tier3 | 12 sn
//   Kristal Kalkan (Tier3): Crystal×2 + Gold×1| Ocak Tier3 | 14 sn
[CreateAssetMenu(fileName = "RecipeCatalog", menuName = "Game/Recipe Catalog")]
public class RecipeCatalog : ScriptableObject
{
    [SerializeField] private RecipeData[] recipes;

    public IReadOnlyList<RecipeData> All => recipes;

    // Belirli ocak seviyesinde yapılabilen tarifler
    public IEnumerable<RecipeData> ForStationLevel(UpgradeLevel stationLevel)
    {
        if (recipes == null) yield break;
        foreach (RecipeData r in recipes)
            if (r != null && (int)r.requiredStationLevel <= (int)stationLevel)
                yield return r;
    }

    public RecipeData GetByOutputWeapon(WeaponType weapon)
    {
        if (recipes == null) return null;
        foreach (RecipeData r in recipes)
            if (r != null && r.outputWeapon == weapon) return r;
        return null;
    }

    public RecipeData GetByOutputDefense(DefenseType defense)
    {
        if (recipes == null) return null;
        foreach (RecipeData r in recipes)
            if (r != null && r.outputDefense == defense) return r;
        return null;
    }
}
