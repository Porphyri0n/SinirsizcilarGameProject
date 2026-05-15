using System;
using UnityEngine;
using Photon.Pun;

// Craft senkronu — host'taki craft start/complete olaylarını RPC ile diğer client'lara taşır.
// RecipeData SO ağda gönderilemez; recipeName ile RecipeCatalog'dan lookup yapılır.
// Host EventBus event'i fire eder -> burada RPC'ye çevrilir -> client'larda EventBus tekrar fire edilir.
[RequireComponent(typeof(PhotonView))]
public class CraftNetSync : MonoBehaviourPun
{
    [SerializeField] private RecipeCatalog catalog;

    private void OnEnable()
    {
        EventBus.OnCraftStarted += HandleCraftStarted;
        EventBus.OnCraftCompleted += HandleCraftCompleted;
    }

    private void OnDisable()
    {
        EventBus.OnCraftStarted -= HandleCraftStarted;
        EventBus.OnCraftCompleted -= HandleCraftCompleted;
    }

    // ── Host: EventBus -> RPC ───────────────────────────────────────────

    private void HandleCraftStarted(RecipeData recipe, float duration)
    {
        if (!CanBroadcast() || recipe == null) return;
        photonView.RPC(NetworkKeys.RPC_START_CRAFT, RpcTarget.Others, recipe.recipeName, duration);
    }

    private void HandleCraftCompleted(RecipeData recipe)
    {
        if (!CanBroadcast() || recipe == null) return;
        photonView.RPC(NetworkKeys.RPC_COMPLETE_CRAFT, RpcTarget.Others, recipe.recipeName);
    }

    // ── Client: RPC -> EventBus ─────────────────────────────────────────

    [PunRPC]
    public void RPC_StartCraft(string recipeName, float duration)
    {
        RecipeData recipe = FindRecipe(recipeName);
        if (recipe == null) return;
        EventBus.FireCraftStarted(recipe, duration);
    }

    [PunRPC]
    public void RPC_CompleteCraft(string recipeName)
    {
        RecipeData recipe = FindRecipe(recipeName);
        if (recipe == null) return;
        EventBus.FireCraftCompleted(recipe);
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────

    private RecipeData FindRecipe(string recipeName)
    {
        if (catalog == null || string.IsNullOrEmpty(recipeName)) return null;
        foreach (RecipeData r in catalog.All)
            if (r != null && r.recipeName == recipeName) return r;
        return null;
    }

    private static bool CanBroadcast() => PhotonNetwork.InRoom && AuthorityManager.IsHost;
}
