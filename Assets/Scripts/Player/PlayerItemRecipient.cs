using UnityEngine;
using Unity.Netcode;

/// <summary>
/// CraftingStation'dan gelen başarılı üretim olaylarını dinler ve yerel oyuncuya eşyayı teslim eder.
/// </summary>
public class PlayerItemRecipient : MonoBehaviour
{
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private WeaponData[] weaponCatalog; // Map WeaponType to Data

    private void Awake()
    {
        if (weaponManager == null) weaponManager = GetComponent<WeaponManager>();
    }

    private void OnEnable()
    {
        EventBus.OnCraftCompleted += HandleCraftCompleted;
    }

    private void OnDisable()
    {
        EventBus.OnCraftCompleted -= HandleCraftCompleted;
    }

    private void HandleCraftCompleted(RecipeData recipe)
    {
        // Sadece bu objeyi kontrol eden (yerel) oyuncu için geçerli
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsOwner) return;

        Debug.Log($"[PlayerItemRecipient] Processing craft result: {recipe.recipeName}");

        if (recipe.outputWeapon.HasValue)
        {
            WeaponData data = FindWeaponData(recipe.outputWeapon.Value, recipe.requiredStationLevel);
            if (data != null)
            {
                Debug.Log($"[PlayerItemRecipient] Equipping new weapon: {data.displayName}");
                weaponManager.Equip(data);
            }
            else
            {
                Debug.LogWarning($"[PlayerItemRecipient] No WeaponData found for type {recipe.outputWeapon.Value}");
            }
        }
        else if (recipe.outputPotion != null)
        {
            PotionSystem potionSystem = GetComponent<PotionSystem>();
            if (potionSystem != null)
            {
                Debug.Log($"[PlayerItemRecipient] Automatically consuming potion: {recipe.outputPotion.displayName}");
                potionSystem.UsePotion(recipe.outputPotion);
            }
            else
            {
                Debug.LogWarning("[PlayerItemRecipient] PotionSystem component not found on player!");
            }
        }
    }

    private WeaponData FindWeaponData(WeaponType type, UpgradeLevel tier)
    {
        if (weaponCatalog == null) return null;
        foreach (var w in weaponCatalog)
        {
            if (w.weaponType == type && w.tier == tier) return w;
        }
        return null;
    }
}
