using System;
using UnityEngine;

/// <summary>
/// Hasarlı surun tamir etkileşimi (IInteractable).
/// Oyuncu yaklaşınca "[E] Tamir Et" gösterir; EconomyManager'da yeterli kaynak (Stone×2 + Wood×1)
/// varsa kaynakları harcar ve Wall.Repair çağırır — Wall görselini kendisi düzeltir.
/// </summary>
[RequireComponent(typeof(Wall))]
public class WallRepair : MonoBehaviour, IInteractable
{
    [SerializeField] private Wall wall;
    [SerializeField] private string promptText = "[E] Tamir Et";

    private void Awake()
    {
        if (wall == null)
            wall = GetComponent<Wall>();
    }

    public string GetInteractPrompt() => promptText;

    public bool CanInteract(GameObject player)
    {
        return wall != null && wall.NeedsRepair && HasResources();
    }

    public void Interact(GameObject player)
    {
        if (wall == null || !wall.NeedsRepair || !HasResources())
            return;

        SpendResources();
        wall.Repair(wall.MaxHealth);   // Tam tamir; Wall görseli kendi içinde güncellenir
    }

    private bool HasResources()
    {
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null)
            return false;

        foreach (RecipeIngredient ingredient in wall.FullRepairCost)
        {
            if (!economy.HasEnough(ingredient.resourceType, ingredient.amount))
                return false;
        }
        return true;
    }

    private void SpendResources()
    {
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null)
            return;

        foreach (RecipeIngredient ingredient in wall.FullRepairCost)
            economy.SpendResource(ingredient.resourceType, ingredient.amount);
    }
}
