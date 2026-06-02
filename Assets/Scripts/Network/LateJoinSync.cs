using System;
using UnityEngine;
using Unity.Netcode;

// GameStateSync faz/dalga/kale HP'sini senkronlar. LateJoinSync onun yanina
// devam eden oyuna katilan oyuncunun kacirdigi kalici state'i ekler:
//   - Kale deposundaki kaynak stoklari (her ResourceType icin)
//   - Game over bayragi (survivedWaves ile)
// Host EventBus event'lerinden okuyup client katildiginda RPC ile gonderir;
// geç katılan client OnNetworkSpawn'da Server'dan talep eder, Server da RPC ile gonderir.
public class LateJoinSync : NetworkBehaviour
{
    private int survivedWaves = -1;
    private bool isGameLost = false;

    [SerializeField] private RecipeCatalog recipeCatalog;

    public void OnEnable()
    {
        EventBus.OnGameLost += HandleGameLost;
    }

    public void OnDisable()
    {
        EventBus.OnGameLost -= HandleGameLost;
    }

    public override void OnNetworkSpawn()
    {
        if (recipeCatalog == null)
        {
            recipeCatalog = Resources.Load<RecipeCatalog>("RecipeCatalog");
        }

        if (IsClient && !IsServer)
        {
            RequestStateServerRpc();
        }
    }

    private void HandleGameLost(int waves)
    {
        if (IsServer)
        {
            isGameLost = true;
            survivedWaves = waves;
        }
    }

    // ── Client -> Server: Bilgi Talebi ───────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestStateServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // 1. Kaynak Stokları
        var values = (ResourceType[])Enum.GetValues(typeof(ResourceType));
        int count = 0;
        for (int i = 0; i < values.Length; i++)
            if (EconomyManager.Instance != null && EconomyManager.Instance.GetStock(values[i]) > 0) count++;

        int[] encodedResources = new int[count * 2];
        int idx = 0;
        for (int i = 0; i < values.Length; i++)
        {
            int stock = EconomyManager.Instance != null ? EconomyManager.Instance.GetStock(values[i]) : 0;
            if (stock <= 0) continue;
            encodedResources[idx++] = (int)values[i];
            encodedResources[idx++] = stock;
        }

        // 2. Kule Seviyeleri
        TowerNetSync[] towers = FindObjectsByType<TowerNetSync>(FindObjectsSortMode.None);
        Array.Sort(towers, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        int[] towerLevels = new int[towers.Length];
        for (int i = 0; i < towers.Length; i++) towerLevels[i] = towers[i].CurrentLevel;

        // 3. Duvar Canları
        Wall[] walls = FindObjectsByType<Wall>(FindObjectsSortMode.None);
        Array.Sort(walls, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        float[] wallHealths = new float[walls.Length];
        for (int i = 0; i < walls.Length; i++) wallHealths[i] = walls[i].CurrentHealth;

        // 4. Crafting Kuyrukları
        CraftingStation[] stations = FindObjectsByType<CraftingStation>(FindObjectsSortMode.None);
        Array.Sort(stations, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        string craftRecipesDelimited = "";
        float[] craftProgress = new float[stations.Length];
        for (int i = 0; i < stations.Length; i++)
        {
            if (stations[i].Queue != null && stations[i].Queue.IsCrafting)
            {
                string rName = stations[i].Queue.CurrentRecipe != null ? stations[i].Queue.CurrentRecipe.recipeName : "";
                craftRecipesDelimited += rName + "|";
                craftProgress[i] = stations[i].Queue.Progress;
            }
            else
            {
                craftRecipesDelimited += "|";
                craftProgress[i] = 0f;
            }
        }

        // 5. Wave İlerlemesi
        int remainingShips = WaveManager.Instance != null ? WaveManager.Instance.RemainingShips : 0;

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        SendStateClientRpc(encodedResources, isGameLost, survivedWaves, towerLevels, wallHealths, craftRecipesDelimited, craftProgress, remainingShips, clientRpcParams);
    }

    // ── Server -> Client: Bilgi Gönderimi ────────────────────────────────

    [ClientRpc]
    private void SendStateClientRpc(
        int[] encodedResources, 
        bool gameLost, 
        int waves, 
        int[] towerLevels, 
        float[] wallHealths, 
        string craftRecipesDelimited, 
        float[] craftProgress,
        int remainingShips,
        ClientRpcParams clientRpcParams = default)
    {
        if (IsServer) return; 

        // 1. Kaynakları uygula
        // ... (Keep existing resource logic)
        for (int i = 0; i + 1 < encodedResources.Length; i += 2)
        {
            ResourceType type = (ResourceType)encodedResources[i];
            int amount = encodedResources[i + 1];
            if (amount > 0)
                EventBus.FireResourceReceived(type, amount);
        }

        // 2. Kule Seviyelerini uygula
        TowerNetSync[] towers = FindObjectsByType<TowerNetSync>(FindObjectsSortMode.None);
        Array.Sort(towers, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        for (int i = 0; i < towers.Length && i < towerLevels.Length; i++)
        {
            var upgrade = towers[i].GetComponent<TowerUpgrade>();
            if (upgrade != null) upgrade.SetLevel((UpgradeLevel)towerLevels[i]);
        }

        // 3. Duvar Canlarını uygula
        Wall[] walls = FindObjectsByType<Wall>(FindObjectsSortMode.None);
        Array.Sort(walls, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        for (int i = 0; i < walls.Length && i < wallHealths.Length; i++)
        {
            walls[i].SetHealth(wallHealths[i]);
        }
        if (CastleWalls.Instance != null) CastleWalls.Instance.UpdateUI();

        // 4. Crafting Kuyruklarını uygula
        string[] craftRecipes = craftRecipesDelimited.Split('|');
        CraftingStation[] stations = FindObjectsByType<CraftingStation>(FindObjectsSortMode.None);
        Array.Sort(stations, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        for (int i = 0; i < stations.Length && i < craftRecipes.Length; i++)
        {
            if (!string.IsNullOrEmpty(craftRecipes[i]) && recipeCatalog != null)
            {
                RecipeData recipe = null;
                foreach (var r in recipeCatalog.All)
                    if (r != null && r.recipeName == craftRecipes[i]) { recipe = r; break; }
                
                if (recipe != null && stations[i].Queue != null)
                {
                    stations[i].Queue.SyncState(recipe, craftProgress[i]);
                }
            }
        }

        // 5. Wave İlerlemesini uygula (UI için event fire et)
        // Not: remainingShips NetworkVariable olduğu için veri zaten oradadır, 
        // ancak geç katılanlarda UI'ın güncellenmesi için bir tetikleyici gerekebilir.
        if (WaveManager.Instance != null && remainingShips > 0)
        {
            // Eğer HUD barı 0 görüyorsa, burada bir event veya doğrudan UI güncellemesi gerekebilir.
            // Ama genellikle WaveManager kendi state'ini güncelleyince UI onu okur (veya event gelir).
        }

        // Game Over durumunu uygula
        if (gameLost)
        {
            EventBus.FireGameLost(waves);
        }
    }
}
