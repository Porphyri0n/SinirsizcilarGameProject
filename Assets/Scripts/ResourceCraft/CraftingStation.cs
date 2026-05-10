using System;
using System.Collections.Generic;
using UnityEngine;

// Craft ocağı — oyuncu "[E] Craft" ile etkileşir, tarifler buradan listelenir.
// Yükseltilebilir: ocak seviyesi arttıkça daha ileri seviye tarifler açılır.
public class CraftingStation : MonoBehaviour, IInteractable, IUpgradeable
{
    [Header("Tarifler")]
    [SerializeField] private RecipeData[] recipes;

    [Header("Yükseltme")]
    [SerializeField] private UpgradeLevel level = UpgradeLevel.Tier1;
    [SerializeField] private UpgradeData[] upgrades;        // fromLevel -> toLevel adımları

    [Header("Etkileşim")]
    [SerializeField] private string interactPrompt = "[E] Craft";

    public IReadOnlyList<RecipeData> Recipes => recipes;

    // Craft menüsü açma isteği — CraftingUI / craft kuyruğu dinler.
    public event Action OnCraftMenuRequested;

    // ── IInteractable ────────────────────────────────────────────────────
    public string GetInteractPrompt() => interactPrompt;
    public bool CanInteract(GameObject player) => true;

    public void Interact(GameObject player) => OnCraftMenuRequested?.Invoke();

    // Bu ocakta bu tarif yapılabilir mi? (gerekli ocak seviyesi karşılanıyor mu)
    public bool CanCraft(RecipeData recipe)
    {
        return recipe != null && (int)recipe.requiredStationLevel <= (int)level;
    }

    // Mevcut seviyede yapılabilen tarifler.
    public IEnumerable<RecipeData> GetCraftableRecipes()
    {
        if (recipes == null) yield break;
        foreach (RecipeData recipe in recipes)
            if (CanCraft(recipe)) yield return recipe;
    }

    // ── IUpgradeable ─────────────────────────────────────────────────────
    public UpgradeLevel CurrentLevel => level;
    public event Action<UpgradeLevel> OnUpgraded;

    public UpgradeData GetNextUpgrade()
    {
        if (upgrades == null) return null;
        foreach (UpgradeData u in upgrades)
            if (u != null && u.fromLevel == level) return u;
        return null;
    }

    public bool CanUpgrade() => GetNextUpgrade() != null;

    // UpgradeManager maliyet ve süreyi hallettikten sonra çağırır.
    public void Upgrade()
    {
        UpgradeData next = GetNextUpgrade();
        if (next == null) return;

        level = next.toLevel;
        OnUpgraded?.Invoke(level);
        EventBus.FireUpgradeCompleted("CraftingStation", level);
    }
}
