using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Craft ocağı — oyuncu "[E] Craft" ile etkileşir, tarifler buradan listelenir.
// Yükseltilebilir: ocak seviyesi arttıkça daha ileri seviye tarifler açılır.
public class CraftingStation : NetworkBehaviour, IInteractable, IUpgradeable
{
    [Header("Tarifler")]
    [SerializeField] private RecipeData[] recipes;

    [Header("Yükseltme")]
    private readonly NetworkVariable<int> levelSync = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField] private UpgradeLevel level = UpgradeLevel.Tier1;
    [SerializeField] private UpgradeData[] upgrades;        // fromLevel -> toLevel adımları

    private bool isSyncing;

    public override void OnNetworkSpawn()
    {
        levelSync.OnValueChanged += OnLevelSyncChanged;
        if (IsServer) levelSync.Value = (int)level;
        else level = (UpgradeLevel)levelSync.Value;
    }

    private void OnLevelSyncChanged(int oldVal, int newVal)
    {
        if (isSyncing) return;
        isSyncing = true;
        level = (UpgradeLevel)newVal;
        OnUpgraded?.Invoke(level);
        isSyncing = false;
    }

    [Header("Etkileşim")]
[SerializeField] private string interactPrompt = "[E] Craft";

    [Header("Craft")]
    [SerializeField] private CraftQueueManager queue;

    public CraftQueueManager Queue => queue;

    public IReadOnlyList<RecipeData> Recipes => recipes;

    // Craft menüsü açma isteği — CraftingUI / craft kuyruğu dinler.
    public event Action OnCraftMenuRequested;

    // ── IInteractable ────────────────────────────────────────────────────
    public string GetInteractPrompt() => interactPrompt;
    public bool CanInteract(GameObject player) => true;

    public void Interact(GameObject player)
    {
        Debug.Log($"[CraftingStation] Interacted by {player.name}");
        OnCraftMenuRequested?.Invoke();
        EventBus.FireOpenCraftingMenu(this);
    }

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

    // ── ECONOMY (kaynak kontrolü + harcama) ──────────────────────────────
    // EconomyManager'da tüm malzemeler var mı? (seviye kontrolünden ayrı)
    public bool CanAfford(RecipeData recipe)
    {
        if (recipe == null) return false;
        if (recipe.ingredients == null) return true;

        EconomyManager econ = EconomyManager.Instance;
        if (econ == null) return false;

        foreach (RecipeIngredient ing in recipe.ingredients)
        {
            if (ing == null) continue;
            if (!econ.HasEnough(ing.resourceType, ing.amount)) return false;
        }
        return true;
    }

    // Craft isteği — seviye + kaynak yeterliyse maliyeti düşer, kuyruğa ekler.
    public bool TryCraft(RecipeData recipe)
    {
        if (!CanCraft(recipe)) return false;        // ocak seviyesi yetmiyor
        if (!CanAfford(recipe)) return false;       // kaynak yetersiz → engelle

        if (IsServer)
        {
            SpendIngredients(recipe);
            if (queue != null) queue.Enqueue(recipe);
            return true;
        }
        else
        {
            // Client ise Server'dan talep et
            RequestCraftServerRpc(recipe.recipeName);
            return true; // Talep iletildi varsayıyoruz
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCraftServerRpc(string recipeName)
    {
        RecipeData recipe = null;
        foreach (var r in recipes)
        {
            if (r.recipeName == recipeName) { recipe = r; break; }
        }

        if (recipe != null && CanCraft(recipe) && CanAfford(recipe))
        {
            SpendIngredients(recipe);
            if (queue != null) queue.Enqueue(recipe);
            
            // Client'a başarılı olduğunu bildir (OnCraftStarted event'i zaten CraftNetSync üzerinden yayılır)
        }
    }

    // Tarifteki tüm malzemeleri EconomyManager'dan düş. (CanAfford önce doğrulanmalı)
    private void SpendIngredients(RecipeData recipe)
    {
        if (!IsServer || recipe.ingredients == null) return;

        EconomyManager econ = EconomyManager.Instance;
        if (econ == null) return;

        foreach (RecipeIngredient ing in recipe.ingredients)
        {
            if (ing == null) continue;
            econ.SpendResource(ing.resourceType, ing.amount);
        }
    }

    // Bekleyen + aktif tüm craft'ları iptal eder ve harcanan kaynakları geri verir.
    public void CancelCrafts()
    {
        if (!IsServer || queue == null) return;

        foreach (RecipeData recipe in queue.GetPendingRecipes())
            RefundIngredients(recipe);

        queue.CancelAll();
    }

    // Tarifteki malzemeleri EconomyManager'a geri ekler (iptal iadesi).
    private void RefundIngredients(RecipeData recipe)
    {
        if (!IsServer || recipe == null || recipe.ingredients == null) return;

        EconomyManager econ = EconomyManager.Instance;
        if (econ == null) return;

        foreach (RecipeIngredient ing in recipe.ingredients)
        {
            if (ing == null) continue;
            econ.AddResource(ing.resourceType, ing.amount);
        }
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

        if (IsServer)
        {
            levelSync.Value = (int)next.toLevel;
        }
        else
        {
            // Client ise Server'dan talep et
            RequestUpgradeServerRpc((int)next.toLevel);
        }

        level = next.toLevel;
        OnUpgraded?.Invoke(level);
        EventBus.FireUpgradeCompleted("CraftingStation", level);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUpgradeServerRpc(int newLevel)
    {
        levelSync.Value = newLevel;
    }
}
